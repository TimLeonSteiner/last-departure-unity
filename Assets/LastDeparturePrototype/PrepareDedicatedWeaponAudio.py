"""Prepare weapon-specific ElevenLabs recordings, preserving asset GUIDs."""
import hashlib
import argparse
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import uuid
import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parent
SOURCES = ROOT / 'Audio' / 'ElevenLabs_Weapons'
IMPACT = ROOT / 'Audio' / 'Weapon_Impact'
DOWNLOADS = Path.home() / 'Downloads'
RATE = 48000
RECORDINGS = [
    ('Scout_Shot', 'Scout_sniper_shot*', .7),
    ('Scout_Reload', 'Scout_sniper_reload*', 2.1),
    ('Scout_Bolt', 'Scout_sniper_bolt*', .4),
    ('M60_Shot', 'M60_machine_gun_shot*', .45),
    ('M60_Reload', 'M60_belt_reload*', 3.1),
    ('M60_Charging_Handle', 'M60_charging_handle*', .4),
    ('AK47_Shot', 'AK47_rifle_shot*', .45),
    ('AK47_Reload', 'AK47_magazine_reload*', 2.15),
    ('AK47_Action', 'AK47_bolt_action*', .4),
    ('Shotgun_Shot', 'Shotgun_pump_shot*', .7),
    ('Shotgun_Reload', 'Shotgun_shell_reload*', 3.4),
    ('Shotgun_Action', 'Shotgun_pump_action*', .4),
    ('Suomi_KP_Shot', 'Suomi_SMG_shot*', .45),
    ('Suomi_KP_Reload', 'Suomi_drum_reload*', 2.3),
    ('Suomi_KP_Action', 'Suomi_bolt_action*', .4),
    ('Grease_Gun_Shot', 'Grease_gun_shot*', .45),
    ('Grease_Gun_Reload', 'Grease_gun_reload*', 1.9),
    ('Grease_Gun_Action', 'Grease_gun_bolt*', .4),
]
SHOT_TIMINGS = {'Scout': (.6, .8), 'M60': (.23, .105), 'AK47': (.26, .17),
                'Shotgun': (.62, .85), 'Suomi_KP': (.18, .09), 'Grease_Gun': (.23, .185)}


def guid(path):
    return re.search(r'^guid: (\w+)', Path(str(path) + '.meta').read_text(), re.M)[1]


def write(path, signal, pcm=False):
    assert len(signal) > 0 and np.isfinite(signal).all() and np.max(np.abs(signal)) < .95
    sf.write(path, signal, RATE, subtype='PCM_16')
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        template = (SOURCES / 'M4_Service_Shot.wav.meta').read_text()
        template = re.sub(r'^guid: \w+', 'guid: ' + uuid.uuid4().hex, template, flags=re.M)
        if pcm:
            template = template.replace('compressionFormat: 1', 'compressionFormat: 0')
        template = template.replace('normalize: 1', 'normalize: 0')
        meta.write_text(template, encoding='utf-8')
    return dict(file=path.relative_to(ROOT).as_posix(), seconds=len(signal) / RATE,
                peak=float(np.max(np.abs(signal))), rms=float(np.sqrt(np.mean(signal ** 2))))


def read(source):
    audio, rate = sf.read(source, always_2d=True)
    assert np.isfinite(audio).all()
    signal = audio.mean(axis=1)
    signal = np.interp(np.arange(round(len(signal) * RATE / rate)) * rate / RATE,
                       np.arange(len(signal)), signal)
    signal -= np.mean(signal)
    peak = np.max(np.abs(signal))
    assert peak > .005, 'Silent recording: ' + str(source)
    active = np.flatnonzero(np.abs(signal) > max(.001, peak * .015))
    return signal[max(0, active[0] - 144):min(len(signal), active[-1] + 1440)]


def fit(signal, limit):
    if len(signal) / RATE <= limit:
        return signal
    tempo = len(signal) / RATE / limit
    filters = []
    while tempo > 2:
        filters.append('atempo=2')
        tempo /= 2
    filters.append(f'atempo={tempo:.8f}')
    with tempfile.TemporaryDirectory(prefix='dedicated-weapon-') as temp:
        raw, fitted = Path(temp) / 'raw.wav', Path(temp) / 'fitted.wav'
        sf.write(raw, signal, RATE, subtype='PCM_16')
        subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                        '-i', str(raw), '-af', ','.join(filters), str(fitted)], check=True)
        result, _ = sf.read(fitted)
        return result[:round(limit * RATE)]


def fade(signal):
    result = signal.copy()
    attack, tail = min(24, len(result) // 4), min(576, len(result) // 4)
    result[:attack] *= np.linspace(0, 1, attack)
    result[-tail:] *= np.linspace(1, 0, tail)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--profiles', nargs='+', choices=list(SHOT_TIMINGS), default=list(SHOT_TIMINGS))
    parser.add_argument('--connect-scene', action='store_true', help='Explicitly update Scout/M60 scene references')
    args = parser.parse_args()
    selected = args.profiles
    report = []
    originals = SOURCES / 'Dedicated_Originals'
    originals.mkdir(exist_ok=True)
    for name, pattern, limit in RECORDINGS:
        if not any(name.startswith(profile + '_') for profile in selected):
            continue
        # Browser filenames are truncated after about twenty prompt characters.
        candidates = list(DOWNLOADS.glob(pattern.rstrip('*')[:19] + '*.wav'))
        candidates = [p for p in candidates if '#1-' in p.name]
        archived = originals / (name + '_Source.wav')
        if candidates:
            source = max(candidates, key=lambda p: p.stat().st_mtime)
            shutil.copy2(source, archived)
        elif archived.exists():
            source = archived
        else:
            raise FileNotFoundError('Missing ElevenLabs recording: ' + pattern)
        signal = read(source)
        if name.endswith('_Shot'):
            signal = signal[:round(limit * RATE)]
        else:
            signal = fit(signal, limit)
        signal = fade(signal)
        signal *= (.82 if name.endswith('_Shot') else .75) / np.max(np.abs(signal))
        info = write(SOURCES / (name + '.wav'), signal)
        info.update(source=source.name, source_sha256=hashlib.sha256(source.read_bytes()).hexdigest())
        report.append(info)
        if name.endswith('_Shot'):
            profile = name[:-5]
            duration, interval = SHOT_TIMINGS[profile]
            output = IMPACT if profile in ('Scout', 'M60') else ROOT / 'Audio' / 'AdditionalWeapons'
            shots = []
            for variant, speed in enumerate((1., .99, 1.01), 1):
                t = np.arange(round(duration * RATE)) / RATE
                shot = np.interp(np.arange(len(t)) * speed, np.arange(len(signal)), signal, right=0)
                # Keep the natural muzzle attack, shorten the tail for repeated shots.
                long_report = profile in ('Scout', 'Shotgun')
                shot *= np.exp(-np.maximum(t - (.10 if long_report else .045), 0) * (7 if long_report else 24))
                shots.append(fade(shot))
            burst = np.zeros(round((7 * interval + duration + .1) * RATE))
            for bullet in range(8):
                at = round(bullet * interval * RATE)
                burst[at:at + len(shots[bullet % 3])] += shots[bullet % 3]
            # Full effects volume (.7) and per-shot gain (.85), matching gameplay.
            playback_peak = float(np.max(np.abs(burst)) * .7 * .85)
            headroom = min(1., .85 / max(playback_peak, 1e-6))
            for variant, shot in enumerate(shots, 1):
                report.append(write(output / f'{profile}_Shot_{variant:02}.wav', shot * headroom, True))
            preview = burst * headroom * .7 * .85
            assert np.max(np.abs(preview)) <= .85001
            report.append(write(output / (profile + '_Preview.wav'), preview, True))
        elif name.startswith(('AK47_', 'Shotgun_', 'Suomi_KP_', 'Grease_Gun_')):
            report.append(write(ROOT / 'Audio' / 'AdditionalWeapons' / (name + '.wav'), signal, True))

    # Replace only the two renamed weapon banks; preserve all other scene content.
    scene = ROOT.parent / 'LastDeparture.unity'
    text = scene.read_text(encoding='utf-8')
    for old, label, profile, action in [
        ('M4 / MARKSMAN', 'SCOUT / SNIPER', 'Scout', 'Scout_Bolt'),
        ('M4 / HEAVY', 'M60 / MACHINE GUN', 'M60', 'M60_Charging_Handle'),
    ]:
        if not args.connect_scene or profile not in selected:
            continue
        bank = '  - weapon: ' + label + '\n'
        for event, path in [('shot', IMPACT / (profile + '_Shot_01.wav')),
                            ('reload', SOURCES / (profile + '_Reload.wav')),
                            ('action', SOURCES / (action + '.wav'))]:
            bank += f'    {event}: {{fileID: 8300000, guid: {guid(path)}, type: 3}}\n'
        bank += '    shotVariations:\n'
        for variant in range(1, 4):
            bank += f'    - {{fileID: 8300000, guid: {guid(IMPACT / f"{profile}_Shot_{variant:02}.wav")}, type: 3}}\n'
        pattern = r'^  - weapon: (?:' + re.escape(old) + '|' + re.escape(label) + r')\n.*?(?=^  - weapon:|^  turretShotSound:)'
        text, count = re.subn(pattern, lambda match: bank, text, flags=re.M | re.S)
        assert count == 1, 'Expected one scene bank: ' + label
    if args.connect_scene and any(profile in selected for profile in ('Scout', 'M60')):
        scene.write_text(text, encoding='utf-8')
    validation = SOURCES / 'DedicatedWeaponValidation.json'
    previous = json.loads(validation.read_text()) if validation.exists() else []
    for entry in previous:
        entry['file'] = entry['file'].replace('\\', '/')
    updated_files = {entry['file'] for entry in report}
    report = [entry for entry in previous if entry['file'] not in updated_files] + report
    validation.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    additional = [dict(entry) for entry in report if entry['file'].startswith('Audio/AdditionalWeapons/')]
    for entry in additional:
        profile = next(p for p in SHOT_TIMINGS if Path(entry['file']).name.startswith(p + '_'))
        event = 'Shot' if '_Shot_' in entry['file'] or '_Preview.' in entry['file'] else Path(entry['file']).stem[len(profile) + 1:]
        entry['source'] = f'Audio/ElevenLabs_Weapons/{profile}_{event}.wav'
        entry['source_sha256'] = hashlib.sha256((ROOT / entry['source']).read_bytes()).hexdigest()
    (ROOT / 'Audio' / 'AdditionalWeapons' / 'Sources.json').write_text(json.dumps(additional, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
