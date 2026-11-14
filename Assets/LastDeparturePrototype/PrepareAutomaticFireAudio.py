"""Extract shot-synchronous M4 cycles from the dedicated ElevenLabs burst."""
from pathlib import Path
import hashlib
import json
import re
import shutil
import uuid
import numpy as np
import soundfile as sf
from scipy.signal import find_peaks

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'Audio' / 'Weapon_Impact'
ORIGINALS = ROOT / 'Audio' / 'ElevenLabs_M4_Impact'
RATE = 48000
INTERVAL = .14


def write(path, signal):
    assert np.isfinite(signal).all() and np.max(np.abs(signal)) < .95
    sf.write(path, signal, RATE, subtype='PCM_16')
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        template = (OUT / 'M4_Service_Shot_01.wav.meta').read_text()
        template = re.sub(r'^guid: \w+', 'guid: ' + uuid.uuid4().hex, template, flags=re.M)
        meta.write_text(template, encoding='utf-8')


def main():
    source = ORIGINALS / 'M4_Automatic_Source.wav'
    if not source.exists():
        candidates = list((Path.home() / 'Downloads').glob('M4A1_automatic_fire._#1-*.wav'))
        shutil.copy2(max(candidates, key=lambda p: p.stat().st_mtime), source)
    raw, sr = sf.read(source, always_2d=True)
    assert sr == RATE
    raw = raw.mean(axis=1)
    envelope = np.sqrt(np.mean(raw[:len(raw)//48*48].reshape(-1, 48)**2, axis=1))
    peaks, _ = find_peaks(envelope, distance=85, prominence=envelope.max()*.12,
                          height=envelope.max()*.18)
    strong = [p for p in peaks if p > 50 and envelope[p] > envelope.max()*.5]
    assert len(strong) >= 6, 'Not enough distinct attacks in the generated burst'
    shots, report = [], []
    for index, peak in enumerate(strong[:6], 1):
        # Find the quiet point before this attack and before the next discharge.
        def onset(p):
            left = max(0, int(p)-35)
            quiet = np.flatnonzero(envelope[left:p] < envelope[p]*.2)
            return max(0, (left + int(quiet[-1]) if len(quiet) else left)-1)*48
        start = onset(peak)
        following = [p for p in peaks if p > peak]
        stop = onset(following[0]) if following else start + round(INTERVAL*RATE)
        stop = min(stop, start + round(INTERVAL*RATE))
        shot = raw[start:stop].copy()
        assert len(shot) > .05*RATE
        shot -= np.mean(shot)
        shot[:12] *= np.linspace(0, 1, 12)
        shot[-288:] *= np.linspace(1, 0, 288)
        shot *= .87 / np.max(np.abs(shot))
        path = OUT / f'M4_Service_Auto_{index:02}.wav'
        write(path, shot)
        shots.append(shot)
        report.append(dict(file=path.name, sourceStart=start/RATE, seconds=len(shot)/RATE,
                           peak=float(np.max(np.abs(shot)))))
    first, _ = sf.read(OUT / 'M4_Service_Shot_01.wav')
    preview = np.zeros(round((29*INTERVAL + .3)*RATE))
    for bullet in range(30):
        shot = first if bullet == 0 else shots[bullet % 6]
        at = round(bullet*INTERVAL*RATE)
        preview[at:at+len(shot)] += shot*.7*.85
    assert np.max(np.abs(preview)) < .85
    write(OUT / 'M4_Automatic_Preview.wav', preview)
    report.append(dict(source=source.name, provider='ElevenLabs', generated=True,
                       source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                       burstShots=30, playbackPeak=float(np.max(np.abs(preview)))))
    (OUT / 'M4_Automatic_Validation.json').write_text(json.dumps(report, indent=2)+'\n')
    # Update only this bank, leaving every other scene property intact.
    scene = ROOT.parent / 'LastDeparture.unity'
    text = scene.read_text(encoding='utf-8')
    pattern = r'(^  - weapon: M4 / SERVICE\n.*?)(?=^  - weapon:|^  turretShotSound:)'
    match = re.search(pattern, text, re.M | re.S)
    assert match is not None
    bank = re.sub(r'    automaticShotVariations:\n(?:    - .*\n)*', '', match[1])
    bank += '    automaticShotVariations:\n'
    for index in range(1, 7):
        meta = (OUT / f'M4_Service_Auto_{index:02}.wav.meta').read_text()
        guid = re.search(r'^guid: (\w+)', meta, re.M)[1]
        bank += f'    - {{fileID: 8300000, guid: {guid}, type: 3}}\n'
    text = text[:match.start()] + bank + text[match.end():]
    scene.write_text(text, encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
