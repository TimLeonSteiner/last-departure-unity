"""Verify prepared runtime PCM and manifest; no Unity execution."""
import hashlib,json,re,uuid
import numpy as np
import soundfile as sf
from PrepareAudio import ROOT,ASSETS,RATE

def main():
 m=json.loads((ROOT/'Mapping.json').read_text())
 assert m['status']=='complete' and not m['missing'],m['missing']
 assert len(m['weapons'])==17
 files=set()
 for w in m['weapons']:
  files.update(w['events'].values());files.update(w.get('shot_variations',[]))
  if w['id'] in (5,10):assert 'shot' not in w['events'] and 'reload' not in w['events']
  if w['id']==7:assert 'Crossbow' in w['events']['shot']
  if w['id']==4:assert w['events']['shot']!=w['events']['explosion']
 files.add(m['turret'])
 for group in m['movement'].values():files.update(group)
 report=[]
 for p in sorted(files):
  f=ASSETS/p.removeprefix('Assets/');i=sf.info(f);x,rate=sf.read(f)
  assert i.channels==1 and rate==RATE and i.subtype=='PCM_16',p
  peak=float(np.max(np.abs(x)));rms=float(np.sqrt(np.mean(x*x)))
  assert np.isfinite(x).all() and .003<peak<.851 and rms>.0001,p
  assert abs(x[0])<.001 and abs(x[-1])<.001,p
  report.append(dict(file=p,seconds=len(x)/rate,peak=peak,rms=rms,clipped_samples=int(np.sum(np.abs(x)>=.999))))
 for w in m['weapons']:
  if w['reload_seconds']>0:
   x,r=sf.read(ASSETS/w['events']['reload'].removeprefix('Assets/'))
   assert len(x)/r<=w['reload_seconds']+.01,w['weapon']
 for c in m['burst_checks']:assert c['peak_at_full_effects_gain']<=.821 and c.get('worst_variant_overlap_bound',0)<=.821
 # Independent variation claims must be backed by different source hashes.
 for names in [('HeavyPistol_Shot','HeavyPistol_Shot_Take02','HeavyPistol_Shot_Take03'),('Jump_01','Jump_02','Jump_03'),('Landing_01','Landing_02','Landing_03')]:
  hashes=[hashlib.sha256((ROOT/'Sources'/(n+'.wav')).read_bytes()).hexdigest() for n in names]
  assert len(set(hashes))==3,('Duplicate source variation',names)
 data=dict(status='PASS',weapons=17,runtime_clips=len(files),new_source_takes=len(json.loads((ROOT/'Production.json').read_text())),burst_checks=len(m['burst_checks']),clips=report,
  limits='Numerical package verification only; animation event integration and listening in the game remain with the integrator.')
 (ROOT/'PackageChecks.json').write_text(json.dumps(data,indent=2)+'\n')
 # Prepare PCM importer metadata only for files inside this new package; preserve GUIDs.
 template=(ROOT.parent/'ElevenLabs_Weapons'/'P12_Shot.wav.meta').read_text()
 for f in ROOT.glob('*.wav'):
  meta=f.with_suffix('.wav.meta');t=meta.read_text() if meta.exists() else re.sub(r'^guid: \w+','guid: '+uuid.uuid4().hex,template,flags=re.M)
  for key,val in [('compressionFormat','0'),('preloadAudioData','1'),('forceToMono','1'),('normalize','0'),('loadType','0'),('sampleRateOverride','48000')]:
   t=re.sub(r'(?m)^(\s*'+key+r': )\d+',lambda a:a[1]+val,t)
  meta.write_text(t)
 print(f"PASS: 17 weapons, {len(files)} runtime clips, {len(m['burst_checks'])} burst checks, distinct movement/pistol source takes, PCM48k and reload-duration bounds.")

if __name__=='__main__':main()
