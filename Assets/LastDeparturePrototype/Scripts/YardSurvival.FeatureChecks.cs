#if UNITY_EDITOR
using System;using System.Linq;using System.Text;using UnityEngine;
namespace YardSurvivalPrototype {public sealed partial class YardSurvival {
public string RunFeatureChecks(){var log=new StringBuilder();bool active=enabled;enabled=false;
Action<bool,string> check=(pass,label)=>log.AppendLine((pass?"PASS ":"FAIL ")+label);
Action<string,Action> test=(name,body)=>{try{body();}catch(Exception e){log.AppendLine("FAIL "+name+": "+e);}};
try{
foreach(int id in new[]{13,16})test("Penetration "+id,()=>{
PlayFromMenu();remainingToSpawn=0;nextWaveCountdown=-1;foreach(var e in enemies.ToArray()){Destroy(e.body.gameObject);}enemies.Clear();EquipWeapon(id,WeaponCatalog.All[id].capacity);
for(int i=0;i<3;i++)SpawnEnemy(new Vector3(0,0,3+i*2));var targets=enemies.ToArray();var hp=targets.Select(e=>e.hp).ToArray();
float y=targets[0].zombie.RegionCenter(5).y;arenaCamera.transform.position=new Vector3(0,y,0);arenaCamera.transform.rotation=Quaternion.identity;
check(ShotStopDistance(arenaCamera.transform.position,Vector3.forward)>8,"Penetration fixture has clear existing warehouse lane "+id);
FireWeaponProjectiles(Vector3.forward);check(targets.Select((e,i)=>e.hp<hp[i]||!enemies.Contains(e)||e.zombie.LostArms>0||e.zombie.LostLegs>0).All(v=>v),"Shot damages all three actual imported zombies in line "+id);
PlayFromMenu();remainingToSpawn=0;nextWaveCountdown=-1;EquipWeapon(id,WeaponCatalog.All[id].capacity);supplies=20;
check(TryPlaceDefense(new Vector3(0,0,2),0,false),"Real barricade placed for penetration blocking "+id);
var cover=barricades.Last();ApplyHammerHit(cover.netId,player.position);ApplyHammerHit(cover.netId,player.position);
SpawnEnemy(new Vector3(0,0,4));var shielded=enemies.Last();int healthBefore=shielded.hp;
arenaCamera.transform.position=new Vector3(0,1,0);arenaCamera.transform.rotation=Quaternion.identity;
FireWeaponProjectiles(Vector3.forward);check(enemies.Contains(shielded)&&shielded.hp==healthBefore&&shielded.zombie.LostArms==0&&shielded.zombie.LostLegs==0,"Penetrating shot stops at actual constructed barricade "+id);
});
}catch(Exception e){log.AppendLine("FAIL top level "+e);}finally{ResetRun();ReleaseMouse();enabled=active;}
return log+"COMPLETE new-feature integration "+DateTime.Now.ToString("s")+"\n";
}
}}
#endif
