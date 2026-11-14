"""Create an original, repeatable stereo rain bed without external samples."""
from pathlib import Path
import math
import random
import struct
import wave

rate, seconds = 22050, 12
count = rate * seconds
channels = []
for channel in range(2):
    rng = random.Random(2707 + channel)
    noise = [rng.uniform(-1, 1) for _ in range(count)]
    # Circular filters keep the noise characteristics continuous at the loop seam.
    signal = []
    for i in range(count):
        smooth = sum(noise[(i - k) % count] for k in range(8)) / 8
        hiss = noise[i] - smooth
        gust = .85 + .1 * math.sin(2 * math.pi * i / count * 3)
        signal.append((smooth * .7 + hiss * .16) * gust)
    channels.append(signal)
peak = max(abs(v) for values in channels for v in values)
gain = .55 / peak
pcm = b''.join(struct.pack('<hh', int(channels[0][i] * gain * 32767), int(channels[1][i] * gain * 32767)) for i in range(count))
output = Path(__file__).parent / 'Audio' / 'Depot_Rain_Procedural.wav'
with wave.open(str(output), 'wb') as wav:
    wav.setnchannels(2)
    wav.setsampwidth(2)
    wav.setframerate(rate)
    wav.writeframes(pcm)
rms = math.sqrt(sum(v * v for values in channels for v in values) / (count * 2)) * gain
print(f'{output.name}: {seconds}s stereo, peak 0.55, RMS {rms:.4f}')
