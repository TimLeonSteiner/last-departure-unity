"""Original M4 automatic-fire transients and burst previews, generated locally."""
from pathlib import Path
import json
import wave
import numpy as np

ROOT = Path(__file__).parent
OUT = ROOT / 'Audio' / 'M4_Automatic'
OUT.mkdir(exist_ok=True)
SR = 44100
report = []

def save(name, signal):
    pcm = np.round(signal * 32767).astype('<i2')
    with wave.open(str(OUT / (name + '.wav')), 'wb') as wav:
        wav.setnchannels(1); wav.setsampwidth(2); wav.setframerate(SR)
        wav.writeframes(pcm.tobytes())
    report.append({'file': name + '.wav', 'seconds': len(signal) / SR,
                   'peak': float(np.max(np.abs(signal))),
                   'rms': float(np.sqrt(np.mean(signal ** 2)))})

for tier, interval, weight in [('Service', .14, 1.0), ('Heavy', .105, 1.22)]:
    shots = []
    for variant in range(3):
        rng = np.random.default_rng(270700 + variant + (100 if tier == 'Heavy' else 0))
        t = np.arange(int(.26 * SR)) / SR
        noise = rng.normal(0, 1, len(t))
        bins = np.fft.rfftfreq(len(t), 1 / SR)
        spectrum = np.fft.rfft(noise)
        crack = np.fft.irfft(spectrum * (1 - np.exp(-(bins / 1400) ** 4)) / (1 + (bins / 9500) ** 8), len(t))
        body = np.fft.irfft(spectrum * (1 - np.exp(-(bins / 110) ** 4)) / (1 + (bins / 2300) ** 8), len(t))
        signal = crack * np.exp(-t * 170) * 1.3 + body * np.exp(-t * 52) * weight
        # A short chesty muzzle report and a delayed metallic cycling sound.
        signal += .32 * weight * np.sin(2 * np.pi * (145 * t + 2.8 * (1 - np.exp(-t * 60)))) * np.exp(-t * 75)
        cycle = np.maximum(t - .038, 0)
        mechanical = sum(np.sin(2 * np.pi * f * cycle) * np.exp(-cycle * decay)
                         for f, decay in [(1300, 150), (2250, 200), (3800, 260)])
        signal += mechanical * (t >= .038) * .035
        echo = np.zeros_like(signal)
        for delay, gain in [(.025, .15), (.061, .08), (.097, .035)]:
            n = round(delay * SR); echo[n:] += signal[:-n] * gain
        signal += echo
        signal *= np.minimum(t / .00025, 1) * np.minimum((t[-1] - t) / .025, 1)
        signal -= np.mean(signal)
        signal *= .78 / np.max(np.abs(signal))
        # End the residual DC correction smoothly, so each tail can overlap safely.
        signal *= np.minimum(t / .00025, 1) * np.minimum((t[-1] - t) / .01, 1)
        shots.append(signal)
        save(f'M4_{tier}_Auto_{variant + 1:02}', signal)
    burst = np.zeros(round((9 * interval + .35) * SR))
    for bullet in range(10):
        start = round(bullet * interval * SR)
        burst[start:start + len(shots[bullet % 3])] += shots[bullet % 3]
    # Preview uses the same one-shot overlap as ten real bullets, without normalization.
    assert np.max(np.abs(burst)) < 1, 'Automatic burst clips'
    save(f'M4_{tier}_Burst_Preview', burst)

(OUT / 'Validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
