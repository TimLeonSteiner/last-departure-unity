#if UNITY_EDITOR
using System;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunOriginalPickupChecks()
        {
            void Check(bool ok,string text){if(!ok)throw new Exception(text);}
            bool savedSuppression=suppressRunRecords;suppressRunRecords=true;
            try
            {
                PlayFromMenu();remainingToSpawn=0;lightningCountdown=1000;
                Check(Resources.Load<GameObject>("OriginalPickups/Caffeine")!=null,"Caffeine prefab missing");
                Check(Resources.Load<GameObject>("OriginalPickups/AmmoBox")!=null,"Ammo prefab missing");
                SpawnCaffeineDrop(player.position);
                Check(caffeineDrops.Count==1&&caffeineDrops[0].body.GetComponent<LODGroup>()!=null,"Caffeine uses optimized model");
                Check(caffeineDrops[0].body.GetComponentsInChildren<Collider>().Length==0,"Caffeine must not block movement");
                UpdateCaffeineDrops(.01f);Check(sleepState.Caffeine==1&&caffeineDrops.Count==0,"Caffeine collection grants one dose");
                sleepState.Reset(.8f);sleepState.Collect(1);
                Check(ConsumeCaffeine()&&Mathf.Approximately(sleepState.Amount,.8f),"Model swap preserves caffeine fade");
                sleepState.Tick(3,38);Check(sleepState.Amount==0,"Caffeine clears after fade");
                Vector3 point=player.position;point.y=0;int before=supplyBoxes.Count;
                reserveAmmo=0;CreateSupplyBox(point,false);
                Check(supplyBoxes.Count==before+1,"Ammo spawns at walkable point");
                var box=supplyBoxes[supplyBoxes.Count-1];
                Check(box.body.GetComponentInChildren<LODGroup>()!=null,"Ammo uses optimized model");
                Check(box.body.GetComponentsInChildren<Collider>().Length==0,"Ammo must not block movement");
                UpdateSupplyBoxes();Check(reserveAmmo==MagazineSize*2&&supplyBoxes.Count==before,"Ammo grants two magazines once");
                reserveAmmo=MagazineSize*5;CreateSupplyBox(point,false);UpdateSupplyBoxes();
                Check(supplyBoxes.Count==before+1,"Full reserve leaves ammo available");
                return "PASS Original prefab loading, LODs, no blocking colliders, caffeine collection and fade, ammo refill and full-reserve preservation.\n";
            }
            finally{ShowHome();suppressRunRecords=savedSuppression;}
        }
    }
}
#endif
