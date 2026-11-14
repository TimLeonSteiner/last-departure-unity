"""Build a reviewable manifest, reusable PCM files and burst checks in this folder only."""
import hashlib, json
from pathlib import Path
import numpy as np
import soundfile as sf
from scipy.signal import butter, sosfilt
from PrepareAudio import ROOT, ASSETS, RATE, prepare

OLD=ROOT.parent/'ElevenLabs_Weapons'
REUSE={
 'LMG_Shot':'M60_Shot', 'LMG_Reload':'M60_Reload', 'LMG_Action':'M60_Charging_Handle',
 'P9_Shot':'P12_Shot','P9_Reload':'P12_Reload','P9_Action':'P12_Slide',
 'Beretta_Reload':'P12_Reload','Beretta_Action':'P12_Slide',
 'Boomstick_Shot':'Shotgun_Shot','Boomstick_Reload':'Shotgun_Reload','Boomstick_Action':'Shotgun_Action',
 'Sniper_Shot':'Scout_Shot','Sniper_Reload':'Scout_Reload','Sniper_Action':'Scout_Bolt',
 'DualSMG_Shot':'Suomi_KP_Shot','DualSMG_Action':'Suomi_KP_Action',
 'HeavyRifle_Action':'M4_Charging_Handle','FAL_Action':'M4_Charging_Handle',
 'Talon_Action':'P12_Slide','HeavyPistol_Action':'P12_Slide',
 'Persuader_Action':'Grease_Gun_Action', 'Turret_Shot':'Turret_Shot',
}
NAMES=['LMG / BELT FED','MINIGUN','P9 / PISTOL','BOOMSTICK','GRENADE LAUNCHER','PITCHFORK','DUAL SAWNOFFS','CROSSBOW','DUAL SMGS','HEAVY RIFLE','KNIFE','TALON','BERETTA','HEAVY PISTOL','PERSUADER','FAL / BATTLE RIFLE','BOLT / SNIPER']
PREFIX=['LMG','Minigun','P9','Boomstick','Grenade','Pitchfork','Sawnoff','Crossbow','DualSMG','HeavyRifle','Knife','Talon','Beretta','HeavyPistol','Persuader','FAL','Sniper']
INTERVAL=[.105,.06,.29,1.05,1,.75,.55,.8,.075,.35,.5,.4,.25,.5,.085,.23,1.1]
RELOAD=[5.1,2.3,1.65,4.2,2.8,0,2.8,2.6,2.1,3.4,0,2.7,2.7,2.7,2.8,2.7,1.85]
AUTO={0,1,8,14,15}

def path(name):return 'Assets/'+(ROOT/(name+'.wav')).relative_to(ASSETS).as_posix()
def write(name,x):sf.write(ROOT/(name+'.wav'),x,RATE,subtype='PCM_16')

def firearm_body(x,prefix):
 # A short parallel bass channel retains the source transient and mechanisms.
 # The restrained pressure accent is original procedural sound design, not a new take.
 if prefix=='Crossbow':return x
 automatic=prefix in ('LMG','Minigun','DualSMG','Persuader','Turret')
 shotgun=prefix in ('Boomstick','Sawnoff')
 duration=.038 if automatic else .105 if shotgun else .075 if prefix=='HeavyPistol' else .06
 frequency=67 if shotgun or prefix=='HeavyPistol' else 84 if automatic else 76
 x=x/max(float(np.max(np.abs(x))),1e-9)
 low=sosfilt(butter(2,220,fs=RATE,output='sos'),x)
 low*=min(3,.16/max(float(np.max(np.abs(low))),1e-9))
 t=np.arange(len(x))/RATE
 accent=np.sin(2*np.pi*frequency*t)*np.exp(-t/duration)*np.minimum(t/.002,1)
 y=x+low+accent*(.22 if shotgun else .15 if prefix=='HeavyPistol' else .11)
 y=np.tanh(y*1.25)
 y*=.8/max(float(np.max(np.abs(y))),1e-9)
 y[-min(480,len(y)):] *= np.linspace(1,0,min(480,len(y)))
 return y

def main():
 sources=json.loads((ROOT/'Validation.json').read_text())
 for name,old in REUSE.items():
  entry=dict(name=name,download=str(OLD/(old+'.wav')),prompt='Existing ElevenLabs recording reused by mechanism and cartridge class: '+old,limit=RELOAD[PREFIX.index(name.split('_')[0])] if name.endswith('Reload') else .7,peak=.72)
  if entry['limit']==0:entry['limit']=.7
  if name.endswith('Shot'):entry.update(transient=True,limit=.25 if name.split('_')[0] in ['LMG','DualSMG','Persuader'] else .7,peak=.78)
  r=prepare(entry);r.update(source_type='existing recording reused',original_project_file=path(old).replace('AnimatedArsenalAudio','ElevenLabs_Weapons'))
  sources=[s for s in sources if s['name']!=name]+[r]
 mapping=[]; checks=[]; previews=[]; missing=[]
 for id,(weapon,prefix,interval,reload) in enumerate(zip(NAMES,PREFIX,INTERVAL,RELOAD)):
  melee=id in (5,10)
  events={'swing':prefix+'_Swing','stab':prefix+'_Stab','impact':prefix+'_Impact'} if melee else {'shot':prefix+'_Shot','reload':prefix+'_Reload','action':prefix+'_Action'}
  if id==1:events.update(action='Minigun_FeedLatch',motor_start='Minigun_MotorStart',motor_stop='Minigun_MotorStop')
  if id==4:events.update(shot='Grenade_Launch',explosion='Grenade_Explosion',action='Grenade_Breech')
  if id==7:events.update(reload='Crossbow_Cock',action='Crossbow_Cock',impact='Crossbow_Impact',flesh_impact='Knife_Impact')
  if id==6:events['action']='Sawnoff_Break'
  events={k:path(v) for k,v in events.items()}
  for event,p in events.items():
   if not (ASSETS/Path(p).relative_to('Assets')).is_file():missing.append(f'{weapon}: {event} -> {p}')
  record=dict(id=id,weapon=weapon,interval=interval,reload_seconds=reload,events=events,
    trigger_notes='Play swing or stab at attack animation, impact only on confirmed hit; no reload.' if melee else 'Play one shot per actual discharge. Mechanical and impact events remain separate. No long automatic loops.')
  record['reload_includes_mechanical_close']=id in (1,4,6,7)
  record['action_note']='Full reload already contains its closing/loading mechanism; standalone action is for separate handling events, do not play twice.' if id in (1,4,6,7) else 'Standalone charging/slide/bolt action, scheduled separately from magazine or belt handling.'
  if id==1:record['motor_note']='Start once when firing begins; stop once when firing ends, reload starts, magazine empties or weapon switches. Do not play motor start on every shot.'
  if id==14:record['model_assessment']='Wood furniture, steel receiver, slender straight box magazine and pistol-sized cartridge mesh: vintage SMG family, not shotgun. .45 character is an artistic inference, not verified manufacturer/caliber.'
  if id==9:record['model_assessment']='Steel rifle receiver, detachable straight box magazine and long rifle cartridge mesh: full-power rifle family. Exact real-world model/caliber is not asserted.'
  shotname='Grenade_Launch' if id==4 else prefix+'_Shot'
  if (ROOT/(shotname+'.wav')).exists():
   x,_=sf.read(ROOT/('P9_Shot.wav' if id==12 else shotname+'.wav')); x=x*.78/max(float(np.max(np.abs(x))),1e-9) if id==12 else firearm_body(x,prefix); variants=[]
   for n,speed in enumerate([1,.992,1.008],1):
    take=ROOT/f'{prefix}_Shot_Take{n:02}.wav'
    if n>1 and take.exists() and id!=12:
     raw,_=sf.read(take);v=firearm_body(raw,prefix)
     v=np.pad(v,(0,max(0,len(x)-len(v))))[:len(x)]
    else:v=np.interp(np.arange(len(x))*speed,np.arange(len(x)),x,right=0)
    if id in AUTO:
     t=np.arange(len(v))/RATE;v*=np.exp(-np.maximum(t-.035,0)*30)
    tail=min(480,len(v));v[-tail:]*=np.linspace(1,0,tail)
    variants.append(v)
   burst=np.zeros(round((11*interval+len(x)/RATE+.1)*RATE))
   overlap_bound=np.zeros_like(burst)
   envelope=np.max(np.abs(np.stack(variants)),axis=0)
   for n in range(12):
    at=round(n*interval*RATE);burst[at:at+len(x)]+=variants[n%3]
    overlap_bound[at:at+len(x)]+=envelope
   # Check full-scale weapon mixing, not only default effects volume.
   gain=min(1,.82/max(float(np.max(overlap_bound))*.85,1e-9))
   for n,v in enumerate(variants,1):write(f'{prefix}_Shot_{n:02}',v*gain)
   record['shot_variations']=[path(f'{prefix}_Shot_{n:02}') for n in range(1,4)]
   record['events']['shot']=record['shot_variations'][0]
   record['variation_provenance']='Revised Beretta: existing P12/P9 9mm gunshot, subtle rate variations; rejected generated takes archived.' if id==12 else 'Independent ElevenLabs takes where Shot_Take files exist; other variants are subtle rate variants of one source take.'
   record['sound_design']='Natural string and bolt sound; no firearm bass layer.' if id==7 else 'Source transient plus short parallel bass/pressure accent and tanh saturation, peak-normalized with burst headroom. Runtime reverb remains separate.'
   peak=float(np.max(np.abs(burst*gain*.85)));assert peak<.821
   checks.append(dict(weapon=weapon,shots=12,interval=interval,peak_at_full_effects_gain=peak,worst_variant_overlap_bound=float(np.max(overlap_bound))*gain*.85,pass_headroom=True))
   preview=burst*gain*.85;previews.extend([preview,np.zeros(RATE//3)])
  mapping.append(record)
 jumps={event:[path(f'{event}_{n:02}') for n in (1,2,3)] for event in ('Jump','Landing')}
 for event,files in jumps.items():
  for p in files:
   if not(ASSETS/Path(p).relative_to('Assets')).exists():missing.append(p)
 turret,_=sf.read(ROOT/'Turret_Shot.wav');turret=firearm_body(turret,'Turret')
 write('Turret_Shot_01',turret)
 checks.append(dict(weapon='Turret',shots=12,interval=.6,peak_at_full_effects_gain=float(np.max(np.abs(turret)))*.42,pass_headroom=True))
 (ROOT/'Mapping.json').write_text(json.dumps(dict(status='complete' if not missing else 'in production',integration_status=json.loads((ROOT/'Mapping.json').read_text()).get('integration_status','Prepared assets') if (ROOT/'Mapping.json').exists() else 'Prepared assets',weapons=mapping,turret=path('Turret_Shot_01'),movement=jumps,movement_triggers=dict(jump='existing jumpSound on takeoff, gain .24',landing='existing landingSound at ground transition, gain .38'),missing=missing,sources=sources,burst_checks=checks),indent=2)+'\n')
 if previews:write('Weapons_Preview',np.concatenate(previews))
 focus=[]
 for name in ['Beretta_Shot_01','Beretta_Shot_02','Beretta_Shot_03','HeavyPistol_Shot_01','HeavyPistol_Shot_02','HeavyPistol_Shot_03','Boomstick_Shot_01','Sawnoff_Shot_01']:
  f=ROOT/(name+'.wav')
  if f.exists():
   x,_=sf.read(f);focus.extend([x*.85,np.zeros(RATE//4)])
 if focus:write('Focus_Preview',np.concatenate(focus))
 movement=[]
 for n in (1,2,3):
  for event,gain in [('Jump',.24),('Landing',.38)]:
   f=ROOT/f'{event}_{n:02}.wav'
   if f.exists():
    x,_=sf.read(f);movement.extend([x*gain,np.zeros(RATE//3)])
 if movement:write('Movement_Preview',np.concatenate(movement))
 print(f'{len(mapping)} weapon mappings; {len(checks)} burst checks pass; {len(missing)} events pending.')

if __name__=='__main__':main()
