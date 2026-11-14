"""Balance landing impacts from preserved originals; safe to run repeatedly."""
from pathlib import Path
import json
import shutil
import numpy as np
import soundfile as sf
from scipy.signal import butter, sosfilt

root = Path(__file__).parent
bank = root / 'Audio/ElevenLabs_Zombies'
originals = root / 'Audio/LandingSource~'
originals.mkdir(exist_ok=True)
report = []
for category, target_rms in [('Body_Fall', .16), ('Limb_Land', .12)]:
    for target in sorted(bank.glob(category + '_*.wav')):
        original = originals / target.name
        if not original.exists():
            shutil.copy2(target, original)
        samples, rate = sf.read(original, dtype='float32', always_2d=True)
        samples -= samples.mean(axis=0)
        before_rms = float(np.sqrt(np.mean(samples ** 2)))
        # Keep the recorded impact; reinforce its low body and tame isolated peaks.
        low = sosfilt(butter(2, 180, fs=rate, output='sos'), samples, axis=0)
        samples = samples + low * (.5 if category == 'Body_Fall' else .25)
        samples *= target_rms / max(float(np.sqrt(np.mean(samples ** 2))), .0001)
        samples = .88 * np.tanh(samples / .88)
        fade = min(int(rate * .005), len(samples)//2)
        samples[:fade] *= np.linspace(0, 1, fade)[:, None]
        samples[-fade:] *= np.linspace(1, 0, fade)[:, None]
        assert np.isfinite(samples).all() and np.max(np.abs(samples)) < .9
        sf.write(target, samples, rate, subtype='PCM_16')
        report.append(dict(file=target.name, before_rms=before_rms,
                           after_rms=float(np.sqrt(np.mean(samples ** 2))),
                           peak=float(np.max(np.abs(samples))), seconds=len(samples)/rate))
(originals/'Validation.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
