"""Import the new ElevenLabs SFX v3 performances without altering their pitch."""
import hashlib
import json
import uuid
from pathlib import Path
import numpy as np
import soundfile as sf

root = Path(__file__).parent
downloads = Path.home() / 'Downloads'
output = root / 'Resources' / 'HordeVoices'
archive = root / 'Audio' / 'HordeSource~'
archive.mkdir(parents=True, exist_ok=True)
groups = {
    'Zombie_Idle': ('One_terrifying_organ_#*.wav', 'Deep ruined-throat growl, hungry moan and rattling breath'),
    'Zombie_Gag': ('One_horrifying_wet_c_#*.wav', 'Wet choking, glottal rattle, bubbling throat and breathy hiss'),
    'Zombie_Attack': ('A_single_terrifying__#*.wav', 'Broken higher-register infected hunting cry and feral snarl'),
}
template = (root / 'Audio/ElevenLabs_Zombies/Zombie_Idle_01.wav.meta').read_text()
report = []
for category, (pattern, direction) in groups.items():
    sources = sorted(downloads.glob(pattern))
    if len(sources) != 4:
        raise RuntimeError(f'Expected four new recordings for {category}, found {len(sources)}: {pattern}')
    folder = output / category
    folder.mkdir(parents=True, exist_ok=True)
    for index, source in enumerate(sources, 1):
        data, rate = sf.read(source, dtype='float32', always_2d=True)
        mono = data.mean(axis=1)
        mono -= mono.mean()
        active = np.flatnonzero(np.abs(mono) > .002)
        if len(active) == 0 or not np.isfinite(mono).all():
            raise RuntimeError(f'Silent or invalid recording: {source}')
        mono = mono[max(0, active[0]-int(.04*rate)):min(len(mono), active[-1]+int(.12*rate))]
        rms = float(np.sqrt(np.mean(mono*mono)))
        mono *= min(.13/max(rms,.0001), .82/max(float(np.max(np.abs(mono))),.0001))
        fade = min(int(.008*rate), len(mono)//2)
        mono[:fade] *= np.linspace(0,1,fade)
        mono[-fade:] *= np.linspace(1,0,fade)
        target = folder / f'{category}_New_{index:02d}.wav'
        sf.write(target, mono, rate, subtype='PCM_16')
        meta = Path(str(target)+'.meta')
        if not meta.exists():
            import re
            meta.write_text(re.sub(r'guid: [a-f0-9]+', 'guid: '+uuid.uuid4().hex, template))
        (archive/source.name).write_bytes(source.read_bytes())
        report.append(dict(category=category, file=str(target.relative_to(root)), source=source.name,
                           service='ElevenLabs', model='Sound Effects v3', direction=direction,
                           seconds=len(mono)/rate, rate=rate, peak=float(np.max(np.abs(mono))),
                           rms=float(np.sqrt(np.mean(mono*mono))),
                           sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
(archive/'Validation.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
