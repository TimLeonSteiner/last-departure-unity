"""Prepare downloaded soundtrack cues with a wrap crossfade and consistent headroom."""
import json
from pathlib import Path
import numpy as np
import soundfile as sf

output = Path(__file__).parent / 'Audio' / 'ElevenLabs_Music'
output.mkdir(exist_ok=True)
downloads = Path.home() / 'Downloads'
patterns = {'Opening': 'Abandoned_Terminal_*.mp3', 'Combat': 'Stormbound_Terminal_2026-*.mp3',
            'Pressure': 'Stormbound_Terminal_-_Rising_Pressure_*.mp3',
            'Horde': 'Stormbound_Terminal_-_Overrun_*.mp3'}
report = []
for stage, pattern in patterns.items():
    candidates = list(downloads.glob(pattern))
    if not candidates:
        raise RuntimeError(f'Missing downloaded music cue: {stage}')
    source = max(candidates, key=lambda p: p.stat().st_mtime)
    data, rate = sf.read(source, always_2d=True, dtype='float32')
    if len(data) < rate * 30 or not np.isfinite(data).all():
        raise RuntimeError(f'Invalid music: {source.name}')
    # Crossfade the final 1.5 seconds into the beginning. The new wrap joins
    # the same neighboring samples, avoiding abrupt waveform discontinuities.
    overlap = int(rate * 1.5)
    blend = np.linspace(0, 1, overlap, dtype=np.float32)[:, None]
    seam = data[-overlap:] * (1 - blend) + data[:overlap] * blend
    loop = np.concatenate((data[overlap:-overlap], seam))
    rms = float(np.sqrt(np.mean(loop ** 2)))
    if rms < .0001:
        raise RuntimeError(f'Silent music cue: {stage}')
    gain = min(.12 / rms, .7 / float(np.max(np.abs(loop))))
    loop *= gain
    sf.write(output / f'{stage}.wav', loop, rate, subtype='PCM_16')
    report.append({'stage': stage, 'source': source.name, 'rate': rate,
                   'seconds': len(loop) / rate, 'rms': float(np.sqrt(np.mean(loop ** 2))),
                   'peak': float(np.max(np.abs(loop)))})
(output / 'catalog.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
