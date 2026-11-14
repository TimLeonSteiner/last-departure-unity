#if UNITY_EDITOR
using System;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunWarehouseChecks()
        {
            var fullRoof = FullRoofLayout.Load();
            if(fullRoof!=null)
            {
                if(fullRoofCollider==null)throw new Exception("Full roof collider missing");
                int solidHits=0;
                for(int x=-20;x<=20;x+=4)for(int z=-20;z<=20;z+=4)
                    if(ShotStopDistance(new Vector3(x,5,z),Vector3.up)<4)solidHits++;
                if(solidHits<50)throw new Exception("Roof does not block upward shots");
                foreach(var patch in fullRoof.rainPatches)
                {
                    for(int ix=0;ix<=4;ix++)for(int iz=0;iz<=4;iz++)
                    {
                        var p=new Vector3(Mathf.Lerp(patch.xMin,patch.xMax,ix/4f)*ArenaScale,5,Mathf.Lerp(patch.yMin,patch.yMax,iz/4f)*ArenaScale);
                        if(ShotStopDistance(p,Vector3.up)<5)throw new Exception("Roof rain patch obstructed at "+p);
                        p.y=12;
                        if(fullRoofCollider.Raycast(new Ray(p,Vector3.down),out var hit,7))throw new Exception("Rain strikes roof at "+p);
                    }
                }
                if(rainMaterial!=null && rainfall.Count!=8+fullRoof.rainPatches.Length*2)throw new Exception("Full-roof rain emitters missing");
            }
            else
            {
            if (transform.Find("Warehouse yard/Warehouse hall/Weathered roof") == null) throw new Exception("Warehouse roof missing");
            if (Mathf.Abs(ShotStopDistance(new Vector3(0, 1.65f, 0), Vector3.up) - 6.325f) > .01f) throw new Exception("Ceiling does not stop bullets");
            foreach(var leak in DepotModels.RoofLeaks)
                if(ShotStopDistance(new Vector3(leak.x*ArenaScale,7.9f,leak.y*ArenaScale),Vector3.up)<1)
                    throw new Exception("Roof leak still blocks bullets");
            int indoorEmitters=0;
            foreach (var emitter in rainfall)
            {
                var position = emitter.transform.localPosition;
                if (Mathf.Abs(position.x) >= 19 || Mathf.Abs(position.z) >= 19)continue;
                bool underOpening=false;
                foreach(var leak in DepotModels.RoofLeaks)
                    if(Vector2.Distance(new Vector2(position.x,position.z),leak)<.01f)underOpening=true;
                if(!underOpening)throw new Exception("Indoor rain outside a roof opening");
                indoorEmitters++;
            }
            if(rainMaterial!=null&&indoorEmitters!=DepotModels.RoofLeaks.Length*2)throw new Exception("Roof rain or floor splashes missing");
            }
            for (int i = 0; i < 8; i++)
            {
                ValidateWeaponClip(warehouseTails[i]); ValidateWeaponClip(warehouseReflections[i]);
                if (warehouseTails[i].channels != 2) throw new Exception("Tail not stereo");
            }
            int oldWeapon = weaponId; var oldCameraPosition = arenaCamera.transform.position;
            float oldVolume = soundVolume; bool oldEnabled = soundEnabled, oldUnlocked = audioUnlocked;
            try
            {
                soundEnabled = audioUnlocked = true;
                weaponId = 0; arenaCamera.transform.position = new Vector3(0, 1.65f, 0);
                PlayWarehouseShot();
                int active = 0;
                foreach (float end in warehouseVoiceEnd) if (end > Time.unscaledTime) active++;
                if (active < 2) throw new Exception("Shot lacks reflection/tail voices");
                soundVolume = 0; ApplyWarehouseVolume();
                foreach (var voice in warehouseVoices) if (voice.volume != 0) throw new Exception("Hall ignores volume");
                StopWarehouseAudio();
                foreach (float end in warehouseVoiceEnd) if (end != 0) throw new Exception("Hall remains scheduled after stop");
            }
            finally { weaponId = oldWeapon; arenaCamera.transform.position = oldCameraPosition; soundVolume = oldVolume; soundEnabled = oldEnabled; audioUnlocked = oldUnlocked; ApplyWarehouseVolume(); }
            return "WAREHOUSE PASS: intact ceiling collision, open roof breaches, localized indoor rain and splashes, eight stereo tails and reflections loaded, bounded spatial voices, volume and scheduled-voice stop.";
        }
    }
}
#endif
