"""Prepare an ElevenLabs rain download, preserving the assigned Unity asset GUID."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import uuid
import numpy as np
import soundfile as sf


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    dest = root / 'Audio'
    source = args.source.resolve()
    data, rate = sf.read(source, always_2d=True, dtype='float64')
    if len(data) < rate * 20 or not np.isfinite(data).all():
        raise ValueError('Expected at least 20 seconds of finite rain audio')
    # Preserve stereo, remove DC and blend the wrap without a gap or fade to silence.
    data -= np.mean(data, axis=0)
    overlap = int(rate * 1)
    t = np.linspace(0, np.pi / 2, overlap)[:, None]
    seam = data[-overlap:] * np.cos(t) + data[:overlap] * np.sin(t)
    loop = np.concatenate((data[overlap:-overlap], seam))
    rms = float(np.sqrt(np.mean(loop ** 2)))
    peak = float(np.max(np.abs(loop)))
    if rms < .001:
        raise ValueError('Silent rain source')
    # Occasional isolated droplets have much larger peaks than the steady bed.
    # Soft-limit those peaks so they don't force the entire ambience into silence.
    low, high = 0., max(1., .24 / rms)
    for _ in range(30):
        gain = (low + high) / 2
        balanced = .65 * np.tanh(loop * gain / .65)
        if np.sqrt(np.mean(balanced ** 2)) < .12:
            low = gain
        else:
            high = gain
    loop = .65 * np.tanh(loop * high / .65)
    target = dest / 'Depot_Rain.wav'
    backup = dest / 'Depot_Rain_Procedural.wav'
    if target.exists() and not backup.exists():
        shutil.copy2(target, backup)
        template = Path(str(target) + '.meta').read_text()
        guid = template.split('guid: ')[1].splitlines()[0]
        Path(str(backup) + '.meta').write_text(template.replace(guid, uuid.uuid4().hex))
    originals = dest / 'ElevenLabs_Rain'
    originals.mkdir(exist_ok=True)
    shutil.copy2(source, originals / 'Depot_Rain_Source.wav')
    sf.write(target, loop, rate, subtype='PCM_16')
    written, _ = sf.read(target, always_2d=True)
    report = dict(provider='ElevenLabs Sound Effects', source=source.name,
                  source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                  output=target.name, rate=rate, channels=written.shape[1],
                  seconds=len(written) / rate, wrap_crossfade_seconds=1,
                  peak_control='soft limiter, RMS target 0.12, ceiling 0.65',
                  rms=float(np.sqrt(np.mean(written ** 2))),
                  peak=float(np.max(np.abs(written))),
                  wrap_sample_delta=float(np.max(np.abs(written[-1] - written[0]))),
                  default_slider_percent=60, maximum_source_volume=.22)
    (dest / 'RainAudioValidation.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
