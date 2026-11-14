#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunRecoilChecks()
        {
            var report=new StringBuilder();Action<bool,string> check=(ok,label)=>report.AppendLine((ok?"PASS ":"FAIL ")+label);
            PlayFromMenu();enabled=false;var unique=new HashSet<float>();
            for(int id=0;id<WeaponCatalog.All.Length;id++)
            {
                EquipWeapon(id,WeaponCatalog.All[id].capacity);aiming=false;
                int before=ammunition;float basePitch=pitch,baseYaw=yaw;Fire(arenaCamera.transform.forward);
                float hip=cameraKick;unique.Add(hip);
                check(hip>0&&weaponKick>0&&ammunition==before-(WeaponCatalog.IsMelee(id)?0:1),WeaponCatalog.All[id].name+": shot applies its profile and correct ammo cost");
                check(pitch==basePitch&&yaw==baseYaw,"Recoil preserves controllable base aim for weapon "+id);
                ResetRecoil();aiming=true;ApplyWeaponRecoil();if(!WeaponCatalog.IsMelee(id))check(cameraKick<hip,"ADS reduces recoil for weapon "+id);
                ResetRecoil();aiming=false;for(int shot=0;shot<200;shot++)ApplyWeaponRecoil();
                check(cameraKick<=recoilProfiles[id].limit&&Mathf.Abs(recoilSide)<=3&&weaponKick<=recoilProfiles[id].weapon*1.7f+.001f,"Sustained recoil stays bounded for weapon "+id);
                Vector3 direction=Quaternion.Euler(-cameraKick,recoilSide,0)*Vector3.forward;
                check(Vector3.Dot(Vector3.forward,direction)>.9f,"Co-op shot direction stays within server validation for weapon "+id);
                TickRecoil(3);check(cameraKick==0&&recoilSide==0&&weaponKick==0,"Recoil recovers fully for weapon "+id);
            }
            check(unique.Count==WeaponCatalog.All.Length,"All 17 weapons have distinct first-shot upward recoil");
            EquipWeapon(8,60);aiming=false;ApplyWeaponRecoil();float first=recoilSide;recoilSide=0;ApplyWeaponRecoil();check(first>0&&recoilSide<0,"Dual SMGs alternate lateral recoil");
            EquipWeapon(3,6);ApplyWeaponRecoil();EquipWeapon(2,15);check(cameraKick==0&&weaponKick==0&&recoilSide==0&&recoilBurst==0,"Switching weapons clears previous recoil");
            dead=true;Fire(Vector3.forward);check(cameraKick==0,"Dead player cannot gain firing recoil");dead=false;
            reloading=true;Fire(Vector3.forward);check(cameraKick==0,"Reloading prevents firing recoil");reloading=false;
            ApplyWeaponRecoil();health=50;RestoreCoopPlayer();check(cameraKick==0&&recoilSide==0,"Revive clears recoil");
            ResetRun();check(cameraKick==0&&recoilSide==0&&recoilBurst==0,"New run clears recoil");
            return report.ToString();
        }
    }
}
#endif
