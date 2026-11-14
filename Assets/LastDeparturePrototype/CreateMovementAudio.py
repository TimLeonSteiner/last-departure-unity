"""Generate original concrete boot, gear and sliding foley at 48 kHz."""
from pathlib import Path
import json
import uuid
import wave
import numpy as np

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'Audio' / 'Player_Movement'
SR = 48000
RNG = np.random.default_rng(2727)


def band(noise, low, high):
    f = np.fft.rfftfreq(len(noise), 1 / SR)
    return np.fft.irfft(np.fft.rfft(noise) * (1 - np.exp(-(f / low) ** 4)) / (1 + (f / high) ** 8), len(noise))


def foley(duration, kind, variant=0):
    t = np.arange(round(duration * SR)) / SR
    noise = RNG.normal(0, 1, len(t))
    grit = band(noise, 350, 4200)
    cloth = band(noise, 180, 1700)
    if kind == 'slide':
        envelope = np.minimum(t / .04, 1) * (1 - t / duration) ** .7
        texture = .7 + .16 * np.sin(t * 83) + .1 * np.sin(t * 139)
        signal = (grit * .65 + cloth * .45) * envelope * texture
        signal += band(noise, 60, 360) * np.exp(-t * 32) * .6
    elif kind == 'jump':
        signal = cloth * np.sin(np.pi * t / duration) ** 1.5
        signal += grit * np.exp(-t * 70) * .35
    else:
        weight = 1.4 if kind == 'land' else .8
        attack = np.sin(2 * np.pi * (85 + variant * 5) * t) * np.exp(-t * 42)
        signal = weight * attack + grit * np.exp(-t * 55) * .6
        toe = np.maximum(t - .035, 0)
        signal += cloth * np.exp(-toe * 32) * (t >= .035) * .4
    signal -= np.mean(signal)
    signal *= np.minimum(t / .0015, 1) * np.minimum((duration - t) / .025, 1)
    return signal * .65 / np.max(np.abs(signal))


def main():
    OUT.mkdir(exist_ok=True)
    report, sounds = [], {}
    items = [(f'Boot_Concrete_{i+1:02}', .2, 'step', i) for i in range(4)]
    items += [('Jump_Gear', .22, 'jump', 0), ('Land_Boots', .3, 'land', 0),
              ('Slide_Concrete', .7, 'slide', 0), ('Slide_Stop', .15, 'step', 2)]
    template = (ROOT / 'Audio/Weapon_Impact/M4_Service_Shot_01.wav.meta').read_text()
    for name, duration, kind, variant in items:
        signal = foley(duration, kind, variant)
        sounds[name] = signal
        path = OUT / (name + '.wav')
        with wave.open(str(path), 'wb') as audio:
            audio.setnchannels(1); audio.setsampwidth(2); audio.setframerate(SR)
            audio.writeframes(np.round(signal * 32767).astype('<i2').tobytes())
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            old_guid = template.split('guid: ')[1].splitlines()[0]
            meta.write_text(template.replace(old_guid, uuid.uuid4().hex))
        report.append(dict(file=path.name, seconds=duration, peak=float(np.max(np.abs(signal))), rms=float(np.sqrt(np.mean(signal ** 2)))))
    preview = np.concatenate([part for name in ['Boot_Concrete_01', 'Boot_Concrete_02', 'Jump_Gear', 'Land_Boots', 'Slide_Concrete', 'Slide_Stop']
                              for part in (sounds[name] * .4, np.zeros(round(.22 * SR)))])
    with wave.open(str(OUT / 'Movement_Preview.wav'), 'wb') as audio:
        audio.setnchannels(1); audio.setsampwidth(2); audio.setframerate(SR)
        audio.writeframes(np.round(preview * 32767).astype('<i2').tobytes())
    (OUT / 'Validation.json').write_text(json.dumps(report, indent=2) + '\n')
    print('Generated four boot variations, jump, landing, slide, slide stop and a preview.')


if __name__ == '__main__':
    main()
