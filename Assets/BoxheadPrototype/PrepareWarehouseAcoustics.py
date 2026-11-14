"""Bake wet-only warehouse layers from the project's existing weapon recordings."""
from pathlib import Path
import json
import wave
import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve, resample_poly

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'Resources' / 'WarehouseAudio'
RATE = 24000

def read(path):
    with wave.open(str(path), 'rb') as wav:
        assert wav.getsampwidth() == 2
        rate = wav.getframerate()
        data = np.frombuffer(wav.readframes(wav.getnframes()), '<i2').reshape(-1, wav.getnchannels()).mean(axis=1) / 32768
    from math import gcd
    factor = gcd(rate, RATE)
    return resample_poly(data, RATE // factor, rate // factor)

def write(path, data):
    assert np.isfinite(data).all() and abs(data).max() < 1
    with wave.open(str(path), 'wb') as wav:
        wav.setnchannels(1 if data.ndim == 1 else data.shape[1])
        wav.setsampwidth(2); wav.setframerate(RATE)
        wav.writeframes((data * 32767).astype('<i2').tobytes())

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    names = ['P12', 'M4_Service', 'Scout', 'M60', 'AK47', 'Shotgun', 'Suomi_KP', 'Grease_Gun']
    report = []
    preview = np.zeros((RATE * 14, 2))
    for i, name in enumerate(names):
        matches = list((ROOT / 'Audio').rglob(name + '_Shot_01.wav'))
        if not matches:
            matches = list((ROOT / 'Audio' / 'AdditionalWeapons').rglob(name + '*Shot*.wav'))
        if not matches: raise FileNotFoundError(name)
        source = read(matches[0])
        # No direct transient in the wet layer; filter and diffuse its first 90 ms.
        excitation = source[:int(.09 * RATE)].copy()
        excitation *= np.linspace(1, 0, len(excitation))
        filtered = sosfilt(butter(2, [160, 4200], fs=RATE, btype='bandpass', output='sos'), excitation)
        early = fftconvolve(filtered, np.ones(21) / 21)
        early *= .6 / max(abs(early).max(), 1e-6)
        write(OUT / f'{i}_Reflection.wav', early)
        rng = np.random.default_rng(2700 + i)
        length = int(1.65 * RATE)
        time = np.arange(length) / RATE
        impulse = rng.standard_normal((length, 2)) * np.exp(-time[:, None] * 4.4)
        impulse *= np.minimum(time[:, None] / .028, 1)
        impulse = sosfilt(butter(2, 3100, fs=RATE, output='sos'), impulse, axis=0)
        wet = np.stack([fftconvolve(filtered, impulse[:, channel]) for channel in range(2)], axis=1)
        wet *= .55 / max(abs(wet).max(), 1e-6)
        wet[-1200:] *= np.linspace(1, 0, 1200)[:, None]
        write(OUT / f'{i}_Tail.wav', wet)
        report.append({'weapon': name, 'source': str(matches[0].relative_to(ROOT)), 'tail_seconds': len(wet) / RATE, 'peak': float(abs(wet).max())})
        if i < 4:
            start = i * RATE * 3
            dry = np.repeat(source[:, None], 2, axis=1) * .45
            preview[start:start+len(dry)] += dry
            onset = start + int(.055 * RATE)
            preview[onset:onset+len(wet)] += wet * .22
    write(OUT / 'WarehousePreview.wav', preview)
    (OUT / 'Validation.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))

if __name__ == '__main__': main()
