"""Verify the saved LastDeparture scene references against the completed audio manifest."""
import json,re
from pathlib import Path
from PrepareAudio import ROOT,ASSETS

def guid(path):
 f=ASSETS/path.removeprefix('Assets/')
 return re.search(r'^guid: (\w+)',Path(str(f)+'.meta').read_text(),re.M)[1]

def main():
 manifest=json.loads((ROOT/'Mapping.json').read_text())
 scene=(ASSETS/'LastDeparture.unity').read_text(encoding='utf-8')
 section=re.search(r'^  weaponSounds:\n(.*?)^  turretShotSound:',scene,re.M|re.S)[1]
 banks=re.findall(r'^  - weapon: (.*?)\n(.*?)(?=^  - weapon:|\Z)',section,re.M|re.S)
 assert len(banks)==17
 checks=[]
 for entry,(name,bank) in zip(manifest['weapons'],banks):
  assert name==entry['weapon'],(name,entry['weapon'])
  melee=entry['id'] in (5,10)
  closes=re.search(r'^    reloadIncludesAction: ([01])$',bank,re.M)
  assert closes and bool(int(closes[1]))==entry['reload_includes_mechanical_close'],name
  for key in ('shot','reload','action'):
   actual=re.search(r'^    '+key+r': \{([^}]+)\}',bank,re.M)[1]
   if melee and key=='reload':assert 'fileID: 0' in actual
   else:
    path=entry['events']['stab'] if melee else entry['events'][key]
    assert guid(path) in actual,(name,key,path,actual)
  expected=[entry['events']['stab'],entry['events']['swing']] if melee else entry.get('shot_variations',[])
  variants=re.search(r'^    shotVariations:(.*?)^    automaticShotVariations:',bank,re.M|re.S)[1]
  assert re.findall(r'guid: (\w+)',variants)==[guid(p) for p in expected],name
  checks.append(dict(id=entry['id'],weapon=name,shot_variations=len(expected),saved_assignment='PASS'))
 by_id={e['id']:e['events'] for e in manifest['weapons']}
 special={'turretShotSound':manifest['turret'],'minigunStart':by_id[1]['motor_start'],'minigunStop':by_id[1]['motor_stop'],'grenadeExplosion':by_id[4]['explosion'],'pitchforkImpact':by_id[5]['impact'],'knifeImpact':by_id[10]['impact'],'crossbowImpact':by_id[7]['impact'],'crossbowFleshImpact':by_id[7]['flesh_impact'],'jumpSound':manifest['movement']['Jump'][0],'landingSound':manifest['movement']['Landing'][0]}
 for field,path in special.items():
  value=re.search(r'^  '+field+r': \{([^}]+)\}',scene,re.M)[1]
  assert guid(path) in value,(field,path,value)
 for field,event in [('jumpVariations','Jump'),('landingVariations','Landing')]:
  value=re.search(r'^  '+field+r':\n((?:  - .*\n)+)',scene,re.M)[1]
  assert re.findall(r'guid: (\w+)',value)==[guid(p) for p in manifest['movement'][event]],field
 report=dict(status='PASS',scene='Assets/LastDeparture.unity',weapon_banks=checks,special_events=len(special),movement_variations=6)
 (ROOT/'SceneConnectionChecks.json').write_text(json.dumps(report,indent=2)+'\n')
 print('SCENE AUDIO PASS: all 17 saved banks match the manifest, shot variations match, 10 special events and 6 movement variations assigned.')

if __name__=='__main__':main()
