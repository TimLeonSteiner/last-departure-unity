#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunToolChecks()
        {
            var log=new StringBuilder();int count=0;bool active=enabled;enabled=false;
            void Check(bool pass,string label){if(!pass)throw new Exception(label);count++;log.AppendLine("PASS "+label);}
            try
            {
                ResetRun();frontScreen=FrontScreen.Game;supplies=20;
                Check(bindings[(int)Control.Build].primary==0 && bindings[(int)Control.Hologram].primaryDefault==(int)Key.Tab && bindings[(int)Control.Barrel].primaryDefault==(int)Key.Digit3,"Tool defaults: barrel 3 in hammer mode, Tab opens menus");
                SelectDefense(3);Check(building&&!buildMenuVisible&&selectedDefense==3,"Barrel selection does not open build menu");
                ToggleHammerSlot();Check(!building&&!buildMenuVisible,"Hammer key exits barrel mode consistently");
                ToggleHammerSlot();Check(building&&selectedDefense==3,"Hammer remembers selected piece");
                SelectDefense(-1);Check(selectedDefense==3,"Invalid selection preserves piece");
                SelectDefense(1);Check(building&&!buildMenuVisible,"Defense hotkey keeps menu closed");
                Check(BarrelTemplate!=null,"Scene has an explosive barrel template");
                var point=new Vector3(0,0,3);int before=supplies;
                Check(TryPlaceDefense(point,3,false),"Place explosive barrel at clear location");
                Check(supplies==before-2&&placedBarrels.Count==1,"Placement charges exactly two scrap");
                var barrel=placedBarrels.Values.First();string key=barrel.SceneKey;
                Check(key.StartsWith("placed:")&&!Free(point,.3f,true),"Placed barrel has stable network identity and blocks movement");
                Check(!TryPlaceDefense(point,3,false)&&supplies==before-2,"Overlap rejected without spending scrap");
                Check(!TryPlaceDefense(new Vector3(0,0,16),3,false),"Out-of-range barrel rejected");
                var states=placedBarrels.Select(p=>new PlacedBarrelState{id=p.Key,position=p.Value.transform.position}).ToArray();
                foreach(var b in placedBarrels.Values){explosiveBarrels.Remove(b);DestroyImmediate(b.gameObject);}placedBarrels.Clear();
                ApplyPlacedBarrels(states);ApplyPlacedBarrels(states);
                Check(placedBarrels.Count==1&&placedBarrels.Values.First().SceneKey==key,"Snapshot reconstructs barrel once with same identity");
                ApplySpentBarrels(new[]{key});Check(placedBarrels.Values.First().Detonated,"Explosion state reaches reconstructed barrel");
                ResetRun();Check(placedBarrels.Count==0,"Restart removes player-placed barrels");
                foreach(var prefab in playerSkinModels)
                {
                    var avatar=Instantiate(prefab).GetComponent<HumanoidPlayerAvatar>();
                    try
                    {
                        avatar.Tick(.1f,2,0,false);
                        for(int id=0;id<animatedWeaponModels.Length;id++)
                        {
                            avatar.SetWeapon(ModelFor(id),id,15);
                            Check(avatar.VisibleWeaponId==id,"Partner equips "+WeaponCatalog.All[id].name+" on "+prefab.name);
                        }
                        Check(avatar.rig.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterials.Any(m=>m!=null&&m.name.ToLowerInvariant().Contains("smg"))).All(r=>!r.enabled),"Authored SMG hidden on "+prefab.name);
                    }
                    finally{DestroyImmediate(avatar.gameObject);}
                }
                log.AppendLine(count+" checks passed.");
            }
            catch(Exception e){log.AppendLine("FAIL "+e);}
            finally{ResetRun();ReleaseMouse();enabled=active;}
            return log.ToString();
        }
    }
}
#endif
