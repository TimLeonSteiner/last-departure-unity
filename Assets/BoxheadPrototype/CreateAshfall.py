"""Original seamless action/horror score and mechanical rifle reload effects."""
from pathlib import Path
import json
import wave
import numpy as np

OUT = Path(__file__).resolve().parent / 'Audio'
SR, BPM = 22050, 128
BEAT = 60 / BPM
SIZE = round(32 * 4 * BEAT * SR)
rng = np.random.default_rng(2709)

def hz(n): return 440 * 2 ** ((n-69)/12)
def band(a, low, high):
    f = np.fft.rfftfreq(len(a), 1/SR)
    return np.fft.irfft(np.fft.rfft(a) * (1-np.exp(-(f/low)**4))/(1+(f/high)**8), len(a))
def add(target, time, sound, gain=1):
    np.add.at(target, (np.arange(len(sound))+round(time*SR)) % len(target), sound*gain)
def synth(note, length, kind):
    t = np.arange(round(length*SR))/SR
    f = hz(note)
    if kind == 'bass':
        a = np.sin(2*np.pi*f*t) + .32*np.sin(4*np.pi*f*t) + .15*np.sin(6*np.pi*f*t)
        a = np.tanh(a*1.8)*np.exp(-t*8)
    elif kind == 'strings':
        a = sum(np.sin(2*np.pi*(f*(k+.002*np.sin(t*2.3))+ .35)*t)/k**1.3 for k in range(1,8))
        a += .4*np.sin(2*np.pi*(f*.997)*t)
        a *= np.minimum(t/.45,1)*np.minimum((length-t)/.5,1)
    else:
        a = (np.sin(2*np.pi*f*t)+.3*np.sin(2*np.pi*f*2.003*t)+.12*np.sin(2*np.pi*f*4.13*t))*np.exp(-t*2.5)
    return a*np.minimum(t/.006,1)*np.minimum((length-t)/.035,1)

music = np.zeros(SIZE)
roots = [38,38,34,34,41,41,37,37]
for bar in range(32):
    root = roots[bar % 8]
    at = bar*BEAT*4
    for note in [root+12,root+19,root+27]:
        add(music, at, synth(note, BEAT*4.5, 'strings'), .045)
    # D minor ostinato, ominous semitone turns, low industrial percussion.
    for step in range(8):
        time = at+step*BEAT/2
        add(music,time,synth(root+[0,0,12,7,0,12,7,1][step],.23,'bass'),.15)
        t=np.arange(round(.055*SR))/SR
        hat=band(rng.normal(size=len(t)),4500,8500)*np.exp(-t*70)
        add(music,time,hat,.022 if step%2 else .037)
    for beat in range(4):
        t=np.arange(round(.42*SR))/SR
        drum=np.sin(2*np.pi*(45*t+.9*(1-np.exp(-t*28))))*np.exp(-t*13)
        drum+=band(rng.normal(size=len(t)),90,1800)*np.exp(-t*42)*.3
        add(music,at+beat*BEAT,drum,.23 if beat in [0,2] else .11)
        if beat in [1,3]:
            snare=band(rng.normal(size=len(t)),800,6500)*np.exp(-t*19)
            snare+=np.sin(2*np.pi*170*t)*np.exp(-t*25)*.25
            add(music,at+beat*BEAT,snare,.15)
    if bar%2 == 0:
        for step,note in enumerate([74,77,73,69]):
            bell=synth(note,1.4,'bell')
            add(music,at+step*BEAT,bell,.055)
            for delay,gain in [(.23,.018),(.47,.012),(.71,.007)]: add(music,at+step*BEAT+delay,bell,gain)
    if bar%8 in [6,7]:
        t=np.arange(round(BEAT*4*SR))/SR
        tension=band(rng.normal(size=len(t)),1200,5200)*np.linspace(0,.018,len(t))
        add(music,at,tension)
# Wind and detuned harmonic drone carry through the loop. Circular delay avoids a dead seam.
t=np.arange(SIZE)/SR
music+=band(rng.normal(size=SIZE),100,750)*.009
music+=.019*np.sin(2*np.pi*hz(26)*t)*(1+.25*np.sin(2*np.pi*t/(32*4*BEAT)))
music=band(np.tanh(music*1.4),38,7800)
music[:100]*=np.linspace(0,1,100); music[-100:]*=np.linspace(1,0,100)

def write(name,a,peak):
    a=a*peak/max(np.max(np.abs(a)),1e-6)
    with wave.open(str(OUT/(name+'.wav')),'wb') as f:
        f.setnchannels(1); f.setsampwidth(2); f.setframerate(SR); f.writeframes(np.round(a*32767).astype('<i2').tobytes())
    return {'file':name+'.wav','seconds':len(a)/SR,'rms':float(np.sqrt(np.mean(a*a))), 'peak':float(np.max(np.abs(a)))}

report=[write('Ashfall_Action_Horror',music,.8)]
swap=np.zeros(round(1.7*SR))
for at,duration,gain in [(0,.1,.7),(.24,.17,.3),(.78,.22,.18),(1.36,.12,.75)]:
    t=np.arange(round(duration*SR))/SR
    sound=band(rng.normal(size=len(t)),700,7000)*np.exp(-t*35)
    sound+=.2*np.sin(2*np.pi*430*t)*np.exp(-t*30)
    add(swap,at,sound,gain)
report.append(write('Magazine_Swap',swap,.68))
bolt=np.zeros(round(.32*SR))
for at in [0,.14]:
    t=np.arange(round(.12*SR))/SR
    a=band(rng.normal(size=len(t)),400,6200)*np.exp(-t*32)+np.sin(2*np.pi*290*t)*np.exp(-t*40)*.3
    add(bolt,at,a)
report.append(write('Charging_Handle',bolt,.68))
(OUT/'AshfallValidation.json').write_text(json.dumps({'bpm':BPM,'bars':32,'files':report},indent=2))
print(json.dumps(report,indent=2))
