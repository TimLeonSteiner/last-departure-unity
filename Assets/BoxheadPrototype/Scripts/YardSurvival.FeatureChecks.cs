#if UNITY_EDITOR
using System;using System.Linq;using System.Text;using UnityEngine;
namespace YardSurvivalPrototype {public sealed partial class YardSurvival {
public string RunFeatureChecks(){var log=new StringBuilder();bool active=enabled;enabled=false;
Action<bool,string> check=(pass,label)=>log.AppendLine((pass?"PASS ":"FAIL ")+label);
Action<string,Action> test=(name,body)=>{try{body();}catch(Exception e){log.AppendLine("FAIL "+name+": "+e);}};
try{
test("LuckyChest",()=>{
PlayFromMenu();remainingToSpawn=0;nextWaveCountdown=-1;player.position=ChestApproach+Vector3.up*.65f;supplies=5;
check(!BuyLuckyChest(player.position,weaponId)&&chestStage==0&&supplies==5,"Chest rejects insufficient scrap without charging");
check(luckyChest!=null&&luckyChest.opening!=null,"Original animated chest installed");
supplies=12;int old=weaponId;ammunition=11;reserveAmmo=31;int count=pickups.Count;
check(BuyLuckyChest(player.position,weaponId)&&supplies==6,"Chest purchase charges exactly six scrap");
check(chestWeapon>=0&&chestWeapon<WeaponCatalog.All.Length&&chestWeapon!=old,"Chest prize is a different imported weapon");
check(!BuyLuckyChest(player.position,weaponId)&&supplies==6,"Second purchase blocked while opening");
UpdateLuckyChest(.65f);check(chestStage==2,"Chest lid opens in .65 seconds");
UpdateLuckyChest(1.8f);check(chestStage==3&&chestReward!=null&&chestGlow.intensity>0,"Prize revealed with glowing model");
Quaternion before=chestReward.localRotation;UpdateLuckyChest(.25f);check(Quaternion.Angle(before,chestReward.localRotation)>5,"Prize visibly rotates");
int prize=chestWeapon;check(ClaimLuckyChest(player.position,false)&&weaponId==prize,"Claim equips selected prize");
check(ammunition==WeaponCatalog.All[prize].capacity&&reserveAmmo==ammunition*3,"Claim grants one magazine and three reserve magazines");
check(pickups.Count==count,"Chest claim creates no ground weapon pickup");
check(!ClaimLuckyChest(player.position,false),"Prize claim cannot repeat");UpdateLuckyChest(.45f);check(chestStage==0,"Chest closes and becomes available");
check(BuyLuckyChest(player.position,weaponId)&&supplies==0,"Second valid purchase charges remaining six scrap");UpdateLuckyChest(.65f);UpdateLuckyChest(1.8f);UpdateLuckyChest(15);UpdateLuckyChest(.45f);check(chestStage==0,"Unclaimed prize expires and chest resets");
});
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
