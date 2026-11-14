"""Prepare explicitly downloaded ElevenLabs weapon WAVs for the Unity scene."""
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import uuid
import wave

import numpy as np

ROOT = Path(__file__).resolve().parent
DEST = ROOT / 'Audio' / 'ElevenLabs_Weapons'
DOWNLOADS = Path.home() / 'Downloads'
SOUNDS = [
    ('P12_Shot', 'P12_sidearm_exactly__#1-*.wav', .65),
    ('M4_Service_Shot', 'M4_service_rifle_exa_#1-*.wav', .55),
    ('M4_Marksman_Shot', 'M4_marksman_rifle_ex_#1-*.wav', .7),
    ('M4_Heavy_Shot', 'M4_heavy_rifle_exact_#1-*.wav', .55),
    ('P12_Reload', 'P12_pistol_magazine__#1-*.wav', 1.1),
    ('M4_Service_Reload', 'M4_service_magazine__#1-*.wav', 1.6),
    ('M4_Marksman_Reload', 'M4_marksman_magazine_#1-*.wav', 1.8),
    ('M4_Heavy_Reload', 'M4_heavy_magazine_re_#1-*.wav', 2.3),
    ('P12_Slide', 'P12_pistol_slide_act_#1-*.wav', .4),
    ('M4_Charging_Handle', 'M4_rifle_charging_ha_#1-*.wav', .4),
    ('Turret_Shot', 'Automatic_sentry_tur_#1-*.wav', .55),
]


def read_wav(path):
    with wave.open(str(path), 'rb') as audio:
        assert audio.getsampwidth() == 2, path
        rate = audio.getframerate()
        samples = np.frombuffer(audio.readframes(audio.getnframes()), dtype='<i2')
        return rate, samples.reshape(-1, audio.getnchannels()).mean(axis=1) / 32768.


def write_wav(path, rate, samples):
    with wave.open(str(path), 'wb') as audio:
        audio.setnchannels(1)
        audio.setsampwidth(2)
        audio.setframerate(rate)
        audio.writeframes((samples * 32767).astype('<i2').tobytes())


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    report = []
    for name, pattern, limit in SOUNDS:
        candidates = sorted(DOWNLOADS.glob(pattern))
        if not candidates:
            raise FileNotFoundError(pattern)
        source = candidates[0]
        rate, samples = read_wav(source)
        peak = float(np.max(np.abs(samples)))
        assert peak > .005, f'Silent source: {source}'
        active = np.flatnonzero(np.abs(samples) > max(.002, peak * .018))
        start = max(0, int(active[0]) - int(rate * .003))
        end = min(len(samples), int(active[-1]) + int(rate * .03))
        samples = samples[start:end].copy()
        if not name.endswith('_Shot') and len(samples) / rate > limit:
            # Preserve the complete magazine sequence while fitting the animation.
            tempo = len(samples) / rate / limit
            filters = []
            while tempo > 2:
                filters.append('atempo=2')
                tempo /= 2
            filters.append(f'atempo={tempo:.6f}')
            with tempfile.TemporaryDirectory(prefix='weapon-audio-') as temp:
                raw, fitted = Path(temp) / 'raw.wav', Path(temp) / 'fitted.wav'
                write_wav(raw, rate, samples)
                subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                                '-i', str(raw), '-af', ','.join(filters),
                                str(fitted)], check=True)
                rate, samples = read_wav(fitted)
        samples = samples[:int(rate * limit)]
        fade = min(int(rate * .012), len(samples) // 4)
        samples[-fade:] *= np.linspace(1, 0, fade)
        samples[:min(48, len(samples))] *= np.linspace(0, 1, min(48, len(samples)))
        samples *= .85 / np.max(np.abs(samples))
        target = DEST / (name + '.wav')
        write_wav(target, rate, samples)
        meta = Path(str(target) + '.meta')
        if not meta.exists():
            template = (ROOT / 'Audio' / 'Carbine_Shot.wav.meta').read_text()
            old_guid = template.split('guid: ')[1].splitlines()[0]
            template = template.replace(old_guid, uuid.uuid4().hex)
            template = template.replace('forceToMono: 0', 'forceToMono: 1')
            template = template.replace('quality: 0.7', 'quality: 0.85')
            meta.write_text(template)
        report.append(dict(name=name, source=source.name,
                           source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                           duration=round(len(samples) / rate, 4), rate=rate,
                           peak=round(float(np.max(np.abs(samples))), 4),
                           rms=round(float(np.sqrt(np.mean(samples ** 2))), 5),
                           leading_trim_seconds=round(start / rate, 4)))
    (DEST / 'WeaponAudioValidation.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
