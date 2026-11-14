"""Build punchier one-shots from the preserved weapon recordings, plus burst QA."""
from pathlib import Path
import hashlib
import json
import uuid
import wave
import argparse
import numpy as np

ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / 'Audio' / 'ElevenLabs_Weapons'
OUT = ROOT / 'Audio' / 'Weapon_Impact'
SR = 48000
PROFILES = [
    ('P12', .34, 110, .30, .30),
    ('M4_Service', .25, 115, .38, .14),
    ('Scout', .6, 95, .46, .8),
    ('M60', .23, 100, .48, .105),
    ('Turret', .28, 110, .34, .6),
]


def read(path):
    with wave.open(str(path), 'rb') as wav:
        assert wav.getsampwidth() == 2, path
        rate = wav.getframerate()
        signal = np.frombuffer(wav.readframes(wav.getnframes()), dtype='<i2')
        signal = signal.reshape(-1, wav.getnchannels()).mean(axis=1) / 32768
    return np.interp(np.arange(round(len(signal) * SR / rate)) * rate / SR,
                     np.arange(len(signal)), signal)


def band(signal, low, high):
    frequencies = np.fft.rfftfreq(len(signal), 1 / SR)
    response = (1 - np.exp(-(frequencies / low) ** 4)) / (1 + (frequencies / high) ** 8)
    return np.fft.irfft(np.fft.rfft(signal) * response, len(signal))


def write(name, signal):
    assert np.all(np.isfinite(signal)) and np.max(np.abs(signal)) < .96
    path = OUT / (name + '.wav')
    with wave.open(str(path), 'wb') as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(SR)
        wav.writeframes(np.round(signal * 32767).astype('<i2').tobytes())
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        template = (SOURCE / 'M4_Service_Shot.wav.meta').read_text()
        old_guid = template.split('guid: ')[1].splitlines()[0]
        # Uncompressed PCM preserves the brief attack; no importer renormalization.
        template = template.replace(old_guid, uuid.uuid4().hex)
        template = template.replace('compressionFormat: 1', 'compressionFormat: 0')
        template = template.replace('normalize: 1', 'normalize: 0')
        meta.write_text(template)
    return dict(file=path.name, seconds=len(signal) / SR,
                peak=float(np.max(np.abs(signal))), rms=float(np.sqrt(np.mean(signal ** 2))))


def main(profile_filter=None):
    OUT.mkdir(exist_ok=True)
    report = []
    previews = []
    for profile, duration, body_frequency, weight, interval in PROFILES:
        if profile_filter and profile != profile_filter:
            continue
        source_path = SOURCE / (profile + '_Shot.wav')
        raw = read(source_path)
        # Remove any leading silence, retaining the natural initial crack.
        active = np.flatnonzero(np.abs(raw) > np.max(np.abs(raw)) * .025)
        raw = raw[max(0, active[0] - int(.001 * SR)):]
        shots = []
        for variant in range(3):
            if profile == 'M4_Service':
                # Independently generated takes retain their own attack and body.
                fresh = ROOT / 'Audio' / 'ElevenLabs_M4_Impact' / f'M4_Service_Shot_{variant + 1:02}.wav'
                if fresh.exists():
                    source_path = fresh
                    raw = read(fresh)
                    active = np.flatnonzero(np.abs(raw) > np.max(np.abs(raw)) * .025)
                    raw = raw[max(0, active[0] - int(.001 * SR)):]
            rng = np.random.default_rng(27100 + PROFILES.index((profile, duration, body_frequency, weight, interval)) * 10 + variant)
            t = np.arange(round(duration * SR)) / SR
            rate = 1 if profile == 'M4_Service' else (1, .987, 1.013)[variant]
            recorded = np.interp(np.arange(len(t)) * rate, np.arange(len(raw)), raw, right=0)
            recorded = band(recorded, 45, 10500)
            if profile == 'P12':
                # M1911 direction: a deep, dry .45 report built from the recording.
                # Preserve the attack instead of replacing it with synthetic noise.
                pistol_rate = (.94, .928, .952)[variant]
                natural = np.interp(np.arange(len(t)) * pistol_rate, np.arange(len(raw)), raw, right=0)
                natural = band(natural, 55, 10500)
                body = band(natural, 90, 650)
                presence = band(natural, 1500, 8500)
                signal = natural * .85 + body * 1.25 + presence * .22
                # Gentle saturation gives the low-mid report weight without flattening the crack.
                signal = np.tanh(signal * 1.35)
                signal *= np.exp(-np.maximum(t - .095, 0) * 15)
                reflection = band(natural, 300, 3400)
                for seconds, gain in [(.032, .12), (.057, .055)]:
                    delay = round(seconds * SR)
                    signal[delay:] += reflection[:-delay] * gain
                # A brief real mechanical detail from the pistol's existing slide recording.
                slide = read(SOURCE / 'P12_Slide.wav')
                active_slide = np.flatnonzero(np.abs(slide) > np.max(np.abs(slide)) * .06)
                if len(active_slide):
                    detail = slide[active_slide[0]:active_slide[0] + round(.035 * SR)].copy()
                    if len(detail):
                        detail *= np.hanning(len(detail)) * .07
                        delay = round(.045 * SR)
                        signal[delay:delay + len(detail)] += detail
                signal -= np.mean(signal)
                signal *= np.minimum(t / .00015, 1) * np.minimum((duration - t) / .018, 1)
                signal *= .9 / max(np.max(np.abs(signal)), 1e-6)
                shots.append(signal)
                info = write(f'{profile}_Shot_{variant + 1:02}', signal)
                info['source_sha256'] = hashlib.sha256(source_path.read_bytes()).hexdigest()
                info['processing'] = 'M1911-style dry .45 report; deeper recorded body, preserved crack, subtle slide detail; no synthetic noise layers'
                report.append(info)
                continue
            if profile in ('M4_Service', 'Scout', 'M60'):
                # Preserve the imported rifle transient. Added noise, bass and
                # repeated saturation turn rapid fire into a dense distorted wash.
                tail_start = .1 if profile == 'Scout' else .045
                decay = 7 if profile == 'Scout' else 24 if profile == 'M60' else 20
                signal = recorded * np.exp(-np.maximum(t - tail_start, 0) * decay)
                signal -= np.mean(signal)
                signal *= np.minimum(t / .00015, 1) * np.minimum((duration - t) / .012, 1)
                level = .9 if profile == 'M4_Service' else .8
                signal *= (level / max(np.max(np.abs(signal)), 1e-6) if profile == 'M4_Service'
                           else min(1, level / max(np.max(np.abs(signal)), 1e-6)))
                shots.append(signal)
                info = write(f'{profile}_Shot_{variant + 1:02}', signal)
                info['source_sha256'] = hashlib.sha256(source_path.read_bytes()).hexdigest()
                info['processing'] = 'natural rifle transient; short tail; no synthetic layers or saturation'
                report.append(info)
                continue
            # Soft compression brings the report forward without boosting its tail.
            recorded = np.tanh(recorded * 2.3) * .64
            recorded *= np.exp(-np.maximum(t - .065, 0) * 13)
            noise = rng.normal(0, 1, len(t))
            blast = band(noise, 85, 850) * np.exp(-t * 35)
            thump = np.sin(2 * np.pi * (body_frequency * t + .5 * (1 - np.exp(-t * 55)))) * np.exp(-t * 48)
            crack = band(noise, 2200, 10500) * np.exp(-t * 260) * .09
            signal = recorded + weight * (blast * 1.2 + thump * .45) + crack
            # Compact, low-level yard reflection, with no long automatic-fire wash.
            delay = round(.034 * SR)
            signal[delay:] += recorded[:-delay] * .10
            signal -= np.mean(signal)
            signal = np.tanh(signal * (3 if profile == 'M4_Heavy' else 1.5))
            signal *= np.minimum(t / .00015, 1) * np.minimum((duration - t) / .018, 1)
            signal *= .88 / max(np.max(np.abs(signal)), 1e-6)
            shots.append(signal)
            info = write(f'{profile}_Shot_{variant + 1:02}', signal)
            info['source_sha256'] = hashlib.sha256(source_path.read_bytes()).hexdigest()
            report.append(info)
        burst = np.zeros(round((7 * interval + duration + .12) * SR))
        for bullet in range(8):
            at = round(bullet * interval * SR)
            burst[at:at + len(shots[bullet % 3])] += shots[bullet % 3]
        gain = .42 if profile == 'Turret' else .85
        playback_peak = float(np.max(np.abs(burst * .7 * gain)))
        assert playback_peak < .9, (profile, playback_peak)
        # Preview reflects the actual effects bus and player-shot gain.
        preview = burst * .7 * gain
        report.append(dict(**write(profile + '_Preview', preview), playback_peak=playback_peak))
        previews.extend([shots[0] * .7 * gain, np.zeros(round(.4 * SR))])
    if not profile_filter:
        write('All_Weapons_Preview', np.concatenate(previews))
    report_name = f'{profile_filter}_Validation.json' if profile_filter else 'Validation.json'
    (OUT / report_name).write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--profile', choices=[p[0] for p in PROFILES])
    main(parser.parse_args().profile)
