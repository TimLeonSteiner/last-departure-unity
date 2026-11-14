"""Original 1920s-inspired radio jazz arrangement and game sound effects."""
from pathlib import Path
import json
import wave
import numpy as np

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "Audio"
OUT.mkdir(exist_ok=True)
SR = 22050
RNG = np.random.default_rng(2707)
BPM = 112
BEAT = 60 / BPM
BAR = BEAT * 4
LENGTH = int(round(16 * BAR * SR))


def freq(note):
    return 440 * 2 ** ((note - 69) / 12)


def voice(note, duration, kind):
    t = np.arange(int(duration * SR)) / SR
    f = freq(note)
    if kind == "piano":
        result = sum(np.sin(2 * np.pi * f * (k + .0007 * k * k) * t) * np.exp(-t * (3 + k * .9)) / k**1.3 for k in range(1, 9))
        result += RNG.normal(0, .025, len(t)) * np.exp(-t * 90)
    elif kind == "banjo":
        result = sum(np.sin(2 * np.pi * f * k * t) * np.exp(-t * (8 + k)) / k**.8 for k in range(1, 10))
        result += RNG.normal(0, .02, len(t)) * np.exp(-t * 60)
    elif kind == "reed":
        phase = 2 * np.pi * f * t + .018 * np.sin(2 * np.pi * 5.4 * t)
        result = sum(np.sin(k * phase) / k**1.5 for k in [1, 3, 5, 7, 9])
        result *= np.minimum(t / .03, 1) * np.minimum((duration - t) / .06, 1)
        result *= .7 + .3 * np.exp(-t * 5)
    else:
        result = (np.sin(2 * np.pi * f * t) + .32 * np.sin(4 * np.pi * f * t)) * np.exp(-t * 4)
    result *= np.minimum(t / .004, 1) * np.minimum((duration - t) / .02, 1)
    return result


def add(track, start, sound, gain=1):
    # Circular mixing carries room tails across the bar-16/bar-1 loop boundary.
    index = int(round(start * SR))
    indices = (np.arange(len(sound)) + index) % len(track)
    np.add.at(track, indices, sound * gain)


def band(signal, lo, hi):
    bins = np.fft.rfftfreq(len(signal), 1 / SR)
    mask = (1 - np.exp(-(bins / lo) ** 4)) / (1 + (bins / hi) ** 8)
    return np.fft.irfft(np.fft.rfft(signal) * mask, len(signal))


def write(name, signal, peak=.86):
    signal = np.asarray(signal, dtype=np.float64)
    signal *= peak / max(np.max(np.abs(signal)), 1e-6)
    signal = np.clip(signal, -1, 1)
    pcm = np.round(signal * 32767).astype("<i2")
    with wave.open(str(OUT / (name + ".wav")), "wb") as file:
        file.setnchannels(1)
        file.setsampwidth(2)
        file.setframerate(SR)
        file.writeframes(pcm.tobytes())
    return {"file": name + ".wav", "seconds": len(signal) / SR, "peak": float(np.max(np.abs(signal))), "rms": float(np.sqrt(np.mean(signal**2)))}


music = np.zeros(LENGTH)
lead = np.zeros(LENGTH)
# Sixteen-bar original theme in C: stride piano, swung banjo/reed, walking bass.
chords = [(48, [60, 64, 67, 69]), (45, [57, 61, 64, 67]), (50, [62, 65, 69, 72]), (43, [59, 62, 65, 69]),
          (48, [60, 64, 67, 71]), (52, [59, 64, 68, 74]), (45, [60, 64, 67, 69]), (43, [59, 62, 65, 68]),
          (53, [60, 65, 69, 72]), (46, [58, 62, 65, 68]), (48, [60, 64, 67, 69]), (45, [57, 61, 64, 67]),
          (50, [62, 65, 69, 72]), (43, [59, 62, 65, 68]), (48, [60, 64, 67, 69]), (43, [59, 62, 65, 69])]
themes = [[76, 79, 81, 79, 76, 74, 72, 74], [76, 73, 76, 79, 81, 79, 76, 73],
          [77, 81, 84, 81, 79, 77, 74, 72], [74, 77, 79, 80, 79, 77, 74, 71],
          [79, 76, 74, 72, 74, 76, 79, 81], [80, 83, 86, 83, 80, 76, 74, 71],
          [81, 79, 76, 74, 72, 69, 72, 76], [74, 77, 80, 83, 81, 80, 77, 71],
          [81, 84, 86, 84, 81, 79, 77, 76], [82, 80, 77, 74, 77, 80, 82, 80],
          [79, 81, 79, 76, 74, 76, 79, 76], [81, 79, 76, 73, 76, 79, 81, 79],
          [77, 81, 79, 77, 74, 72, 74, 77], [79, 80, 83, 80, 79, 77, 74, 71],
          [76, 79, 84, 81, 79, 76, 74, 72], [71, 74, 77, 80, 79, 77, 74, 71]]
for bar, (root, chord) in enumerate(chords):
    for beat in range(4):
        at = bar * BAR + beat * BEAT
        add(music, at, voice(root + [0, 7, 9, 7][beat], .38, "bass"), .25)
        if beat in [1, 3]:
            for note in chord:
                add(music, at, voice(note, .55, "piano"), .05)
        for subdivision in range(2):
            swung = subdivision * 2 / 3
            tick = at + swung * BEAT
            for note in chord[:3]:
                add(music, tick, voice(note + 12, .21, "banjo"), .012 if subdivision == 0 else .009)
            n = themes[bar][beat * 2 + subdivision]
            add(lead, tick, voice(n, .32 if subdivision == 0 else .18, "reed"), .14)
            t = np.arange(int(.08 * SR)) / SR
            brush = RNG.normal(0, 1, len(t)) * np.exp(-t * 65)
            add(music, tick, band(brush, 2500, 7000), .02)
        t = np.arange(int(.17 * SR)) / SR
        if beat in [0, 2]:
            kick = np.sin(2 * np.pi * (55 * t + .5 * (1 - np.exp(-t * 40)))) * np.exp(-t * 24)
            add(music, at, kick, .08)
        else:
            snare = band(RNG.normal(0, 1, len(t)), 650, 5500) * np.exp(-t * 22)
            add(music, at, snare, .045)
music += lead
music += np.roll(lead, int(.075 * SR)) * .11 + np.roll(lead, int(.14 * SR)) * .07
music = band(np.tanh(music * 1.2), 190, 3900)
music += RNG.normal(0, .0006, LENGTH)
# Gentle seam smoothing at a near-zero crossing; total duration stays exactly 16 bars.
music[:180] *= np.linspace(0, 1, 180)
music[-180:] *= np.linspace(1, 0, 180)
report = [write("Dustline_Radio_Rag", music)]

t = np.arange(int(.22 * SR)) / SR
shot = band(RNG.normal(0, 1, len(t)), 550, 6800) * np.exp(-t * 37)
shot += .7 * np.sin(2 * np.pi * 95 * t) * np.exp(-t * 45)
shot += np.roll(shot, int(.032 * SR)) * .13
report.append(write("Carbine_Shot", shot, .78))
t = np.arange(int(.22 * SR)) / SR
hit = .5 * np.sin(2 * np.pi * (125 * t + 1.4 * (1 - np.exp(-t * 15)))) * np.exp(-t * 18)
hit += band(RNG.normal(0, 1, len(t)), 180, 1600) * np.exp(-t * 27) * .3
report.append(write("Ghoul_Hit", hit, .65))
t = np.arange(int(.9 * SR)) / SR
phase = 2 * np.pi * (63 * t + .7 * np.sin(2 * np.pi * 1.5 * t))
groan = sum(np.sin(phase * k) / k for k in [1, 2, 3, 5, 7])
groan = band(np.tanh(groan * 2), 80, 1700) * np.sin(np.pi * t / .9) ** 2
groan += band(RNG.normal(0, 1, len(t)), 250, 1300) * .06 * np.sin(np.pi * t / .9)
report.append(write("Ghoul_Groan", groan, .66))
t = np.arange(int(.35 * SR)) / SR
impact = band(RNG.normal(0, 1, len(t)), 300, 5200) * np.exp(-t * 24)
impact += .25 * np.sin(2 * np.pi * 260 * t) * np.exp(-t * 12)
report.append(write("Scrap_Impact", impact, .65))
# Soft wave transition: low falling drum body and a muted air tail, no bell.
# Keep the asset name/GUID so existing scene references continue to work.
t = np.arange(int(.95 * SR)) / SR
wave_rng = np.random.default_rng(714)
body = np.sin(2 * np.pi * (62 * t + 3.2 * (1 - np.exp(-t * 9))))
body *= (1 - np.exp(-t * 65)) * np.exp(-t * 7)
air = band(wave_rng.normal(0, 1, len(t)), 100, 850)
air *= (1 - np.exp(-t * 18)) * np.exp(-t * 5)
wave_cue = body * .65 + air * .32
wave_cue *= np.minimum(t / .015, 1) * np.clip((.95 - t) / .18, 0, 1)
report.append(write("Wave_Chime", wave_cue, .42))
death = np.zeros(int(1.2 * SR))
for i, note in enumerate([60, 58, 55, 48]):
    sound = voice(note, .48, "reed")
    start = int(i * .18 * SR)
    death[start:start + len(sound)] += sound * .2
report.append(write("Game_Over", band(death, 180, 2500), .66))
t = np.arange(int(.32 * SR)) / SR
hurt = band(RNG.normal(0, 1, len(t)), 120, 1100) * np.exp(-t * 18)
hurt += .45 * np.sin(2 * np.pi * 75 * t) * np.exp(-t * 12)
report.append(write("Player_Hurt", hurt, .64))
(OUT / "AudioValidation.json").write_text(json.dumps({"sample_rate": SR, "bpm": BPM, "bars": 16, "files": report}, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
