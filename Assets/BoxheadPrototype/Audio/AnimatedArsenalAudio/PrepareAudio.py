"""Prepare only this audio package; never edits Unity code, scenes or inboxes."""
import argparse, hashlib, json, shutil, subprocess, tempfile
from pathlib import Path
import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parent
ASSETS = ROOT.parents[2]
RATE = 48000

def prepare(entry):
    source = Path(entry['download'])
    archived = ROOT / 'Sources' / (entry['name'] + '.wav')
    archived.parent.mkdir(exist_ok=True)
    if source.exists(): shutil.copy2(source, archived)
    audio, rate = sf.read(archived, always_2d=True)
    x = audio.mean(axis=1)
    x = np.interp(np.arange(round(len(x)*RATE/rate))*rate/RATE, np.arange(len(x)), x)
    x -= x.mean()
    peak = float(np.max(np.abs(x)))
    assert peak > .003 and np.isfinite(x).all(), entry['name']
    active = np.flatnonzero(np.abs(x) > max(.0004, peak*.01))
    x = x[max(0, active[0]-96):min(len(x),active[-1]+960)]
    limit = entry['limit']
    if len(x) > round(limit*RATE):
        if entry.get('transient', False):
            x = x[:round(limit*RATE)]
        else:
            tempo = len(x)/RATE/limit
            filters = []
            while tempo > 2: filters.append('atempo=2'); tempo /= 2
            filters.append(f'atempo={tempo:.8f}')
            with tempfile.TemporaryDirectory(dir=ROOT) as tmp:
                inp, out = Path(tmp)/'in.wav', Path(tmp)/'out.wav'
                sf.write(inp,x,RATE)
                subprocess.run(['ffmpeg','-v','error','-y','-i',str(inp),'-af',','.join(filters),str(out)],check=True)
                x, _ = sf.read(out)
            x = x[:round(limit*RATE)]
    attack, tail = min(16,len(x)//4), min(480,len(x)//4)
    x[:attack] *= np.linspace(0,1,attack)
    x[-tail:] *= np.linspace(1,0,tail)
    target = entry.get('peak',.72)
    x *= target / max(float(np.max(np.abs(x))),1e-9)
    output = ROOT/(entry['name']+'.wav')
    sf.write(output,x,RATE,subtype='PCM_16')
    return dict(name=entry['name'], file=output.relative_to(ASSETS).as_posix(),
                seconds=len(x)/RATE, peak=float(np.max(np.abs(x))), rms=float(np.sqrt(np.mean(x*x))),
                source_file=archived.relative_to(ASSETS).as_posix(), download_path=str(source),
                sha256=hashlib.sha256(archived.read_bytes()).hexdigest(), prompt=entry['prompt'],
                provider='ElevenLabs Sound Effects', source_type='new generation',
                processing='mono 48k PCM16, DC removal, silence trim, boundary fades; no added reverb',
                temporal_processing='truncate transient tail' if entry.get('transient') else 'atempo only if needed')

def main():
    manifest=json.loads((ROOT/'Production.json').read_text(encoding='utf-8-sig'))
    reports=[]
    for e in manifest:
        if Path(e.get('download','missing')).is_file() or (ROOT/'Sources'/(e['name']+'.wav')).is_file():
            reports.append(prepare(e))
    (ROOT/'Validation.json').write_text(json.dumps(reports,indent=2)+'\n')
    print(f'Prepared {len(reports)}/{len(manifest)} recordings; all finite, non-silent PCM48k with bounded peaks.')

if __name__=='__main__': main()
