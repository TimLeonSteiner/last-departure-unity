#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunGameplayPolishChecks(string action)
        {
            if(action=="hotbar")
            {
                string result=RunInventoryChecks()+"\n"+RunPeerInventoryChecks();
                PrepareInventoryHudReview(true);RestoreLaunchRendering();arenaCamera.enabled=true;
                var savedPosition=player.position;int savedSupplies=supplies;supplies=1000;player.position=new Vector3(10,.65f,0);
                bool extendedReach=false,extendedArea=false;
                for(int x=-15;x<=15;x++)for(int z=-15;z<=15;z++)
                {
                    var point=new Vector3(x,0,z);
                    if(!CanPlaceDefense(point,1,false))continue;
                    extendedReach|=Vector2.Distance(new Vector2(10,0),new Vector2(x,z))>8;
                    extendedArea|=Mathf.Abs(x)>14 || Mathf.Abs(z)>14;
                }
                bool tooFar=DefensePlacementIssue(new Vector3(25,0,0),1,false)?.Contains("range 14 m")==true;
                player.position=savedPosition;supplies=savedSupplies;
                if(!extendedReach||!extendedArea||!tooFar)throw new Exception("Expanded placement check: reach="+extendedReach+", area="+extendedArea+", limit="+tooFar);
                result+="\nPASS Legal placements beyond old reach and area; 14m limit enforced.\n";
                SelectDefense(0);enabled=true;
                ScreenCapture.CaptureScreenshot("Assets/Verification~/GameplayPolish/Hotbar.png");
                return result;
            }
            if (action == "crowd")
            {
                // This legacy pursuit fixture expects player targeting, without the core objective.
                bool server=dedicatedServer;
                try { dedicatedServer=true;return RunZombieCrowdChecks(); }
                finally { dedicatedServer=server;ResetRun(); }
            }
            var report = new StringBuilder();
            void Check(bool pass, string text) { if (!pass) throw new Exception(text); report.AppendLine("PASS " + text); }
            if (action == "grid")
            {
                var saved = new List<Enemy>(enemies); enemies.Clear();
                var random = new System.Random(42);
                try
                {
                    for (int i = 0; i < 400; i++) enemies.Add(new Enemy { netId = i, position = new Vector3((float)random.NextDouble()*40-20,0,(float)random.NextDouble()*40-20) });
                    foreach (var enemy in enemies)
                    {
                        crowdGridActive = false;
                        Vector3 full = SteerZombieCrowd(enemy, Vector3.forward, 1.4f);
                        RebuildCrowdGrid();
                        Vector3 local = SteerZombieCrowd(enemy, Vector3.forward, 1.4f);
                        Check((full-local).sqrMagnitude < .000001f, "Neighbour steering matches full scan for enemy " + enemy.netId);
                        crowdGridActive=false;
                        Vector3 fullMove=MoveZombieInCrowd(enemy,new Vector3(.12f,0,.08f));
                        RebuildCrowdGrid();
                        Check((fullMove-MoveZombieInCrowd(enemy,new Vector3(.12f,0,.08f))).sqrMagnitude<.000001f,"Collision movement matches full scan for enemy "+enemy.netId);
                    }
                    var timer = new System.Diagnostics.Stopwatch();
                    for (int mode = 0; mode < 2; mode++)
                    {
                        timer.Restart();
                        for (int frame=0;frame<100;frame++)
                        {
                            if (mode==1) RebuildCrowdGrid(); else crowdGridActive=false;
                            foreach(var enemy in enemies) SteerZombieCrowd(enemy,Vector3.forward,1.4f);
                        }
                        timer.Stop();report.AppendLine((mode==0?"Full scan":"Spatial grid")+": "+timer.Elapsed.TotalMilliseconds.ToString("F2")+" ms / 100 iterations of 400 enemies");
                    }
                    Check(!WeaponCatalog.CanAim(3) && !WeaponCatalog.CanAim(6) && !WeaponCatalog.CanAim(9), "Shotguns and heavy rifle reject aim");
                    foreach(var callout in monsterCallouts)
                    {
                        var seen = new HashSet<int>();callout.Rotation.Clear();
                        for(int i=1;i<callout.Lines.Length;i++)seen.Add(NextMonsterLine(callout));
                        Check(seen.Count==callout.Lines.Length-1,"All alternate lines play before a repeat: "+callout.ClipPrefix);
                    }
                    return report.ToString();
                }
                finally { crowdGridActive=false;enemies.Clear();enemies.AddRange(saved); }
            }
            PrepareInventoryHudReview(false);RestoreLaunchRendering();enabled=false;
            arenaCamera.enabled=true;
            if (action == "spotting")
            {
                foreach(var old in enemies) if(old.body!=null) DestroyImmediate(old.body.gameObject);
                enemies.Clear();ResetMonsterVoice();
                arenaCamera.transform.position=new Vector3(0,1.7f,-12);
                arenaCamera.transform.LookAt(new Vector3(0,1.15f,-8));
                Enemy Actor(string modelName, int id)
                {
                    var prefab=Array.Find(zombieModels,p=>p!=null&&p.name==modelName);
                    var root=new GameObject("Spotting check "+modelName).transform;root.SetParent(actors,false);root.position=new Vector3(0,0,-8);
                    var model=Instantiate(prefab,root);
                    var enemy=new Enemy{netId=id,body=root,position=root.position,zombie=model.GetComponent<ImportedZombie>()};enemies.Add(enemy);return enemy;
                }
                var armor=Actor("ArmoredWorker",901);
                Check(!TryScreamerSpottedWarning(),"Armored Worker cannot trigger Screamer incoming");
                enemies.Clear();DestroyImmediate(armor.body.gameObject);
                var screamer=Actor("Screamer",902);
                arenaCamera.transform.Rotate(0,180,0);
                Check(!TryScreamerSpottedWarning(),"Offscreen Screamer does not trigger spotting warning");
                arenaCamera.transform.LookAt(new Vector3(0,1.15f,-8));
                Check(TryScreamerSpottedWarning()&&monsterSubtitle=="Screamer incoming.","Visible living Screamer gets its own spotting recording");
                Check(float.IsPositiveInfinity(screamer.screamReactionAt)&&!monsterCallouts[1].Introduced,"Spotting does not unlock or consume the post-scream reaction");
                Check(!TryScreamerSpottedWarning(),"Spotting warning does not repeat");
                ResetMonsterVoice();screamer.hp=0;
                Check(!TryScreamerSpottedWarning(),"Dead Screamer does not trigger spotting warning");
                ResetMonsterVoice();screamer.hp=3;screamer.screamReactionAt=Time.time-1;
                Check(!TryScreamerSpottedWarning(),"Already-heard scream uses reaction instead of late incoming warning");
                ResetRun();enabled=false;
                return report.ToString();
            }
            if (action == "weapon")
            {
                EquipWeapon(9,20,-1);
                viewWeapon.ReviewPose(0,false,0,1,1);
                arenaCamera.fieldOfView=baseFov*.77f;
                CapturePolish("HeavyRifle");
                var rig=viewWeapon.GetComponentInChildren<AnimatedWeaponRig>();
                report.AppendLine("Heavy rifle aim="+rig.aimPosition+" rotation="+rig.aimEuler+" rear="+rig.rearSight+" front="+rig.frontSight);
            }
            else if(action == "brute")
            {
                if(viewWeapon!=null)viewWeapon.gameObject.SetActive(false);
                var root=new GameObject("Brute death review");
                var model=Instantiate(Resources.Load<GameObject>("MeshyCombat/04-Cargo-Brute"),root.transform);
                root.transform.position=new Vector3(0,0,6);
                var zombie=model.GetComponent<ImportedZombie>();zombie.Tick(.4f,true);
                var bones=model.GetComponentsInChildren<Transform>();var lengths=new Dictionary<Transform,Vector3>();
                foreach(var bone in bones)lengths[bone]=bone.localPosition;
                zombie.Die(Vector3.forward,root.transform.position+Vector3.up,2);
                Check(zombie.RagdollBodyCount>=10,"Brute death creates articulated physics");
                var previous=Physics.simulationMode;
                try { Physics.simulationMode=SimulationMode.Script; for(int i=0;i<120;i++){Physics.Simulate(1f/60);zombie.SyncRagdollPose();} }
                finally { Physics.simulationMode=previous; }
                foreach(var bone in bones)if(bone.name.Contains("Arm")||bone.name.Contains("Hand"))
                    Check((bone.localPosition-lengths[bone]).sqrMagnitude<.000001f,"Death preserves bone length: "+bone.name);
                arenaCamera.transform.position=new Vector3(3,2,3);arenaCamera.transform.LookAt(root.transform.position+Vector3.up*.5f);
                CapturePolish("BruteDeath");
            }
            else if(action == "voices")
            {
                foreach(var callout in monsterCallouts)
                    for(int i=0;i<callout.Lines.Length;i++)
                    {
                        string suffix=i==0?"_first":i==1?"_incoming":i==2?"_return":"_alt"+i;
                        var clip=Resources.Load<AudioClip>("PlayerVoice/Encounters/"+callout.ClipPrefix+suffix);
                        Check(clip!=null&&clip.length>.5f,"Recorded dialogue: "+callout.ClipPrefix+suffix);
                    }
                foreach(string file in AnnouncerFiles)for(int i=1;i<=2;i++)Check(Resources.Load<AudioClip>("Announcer/"+file+"_alt"+i)!=null,"Narrator variation: "+file+"_alt"+i);
                var enemy=new Enemy { position=arenaCamera.transform.position };
                Check(float.IsPositiveInfinity(enemy.screamReactionAt),"Unheard Screamer cannot trigger a reaction");
                PlayScreamerCall(enemy);
                Check(enemy.screamReactionAt>=Time.time+screamerCallClip.length,"Reaction waits until scream finishes");
            }
            else if(action == "wall")
            {
                if(viewWeapon!=null)viewWeapon.gameObject.SetActive(false);
                arenaCamera.transform.position=new Vector3(5.3f,2.7f,20);
                arenaCamera.transform.LookAt(new Vector3(5.3f,3.4f,23.6f));
                CapturePolish("WallWithoutDisplay");
            }
            else if(action == "keyboard")
            {
                enabled=true;wave=ChargerIntroWave;fighting=true;remainingToSpawn=0;
                ShowWaveLesson();
                ScreenCapture.CaptureScreenshot("Assets/Verification~/GameplayPolish/ChargerKeyboard.png");
            }
            return report.ToString();
        }
        void CapturePolish(string name)
        {
            var rt=new RenderTexture(1200,750,24);rt.Create();var previous=RenderTexture.active;
            var pixels=new Texture2D(1200,750,TextureFormat.RGB24,false);
            try { RenderPipeline.SubmitRenderRequest(arenaCamera,new RenderPipeline.StandardRequest{destination=rt});RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1200,750),0,0);pixels.Apply();File.WriteAllBytes("Assets/Verification~/GameplayPolish/"+name+".png",pixels.EncodeToPNG()); }
            finally { RenderTexture.active=previous;rt.Release();DestroyImmediate(rt);DestroyImmediate(pixels); }
        }
    }
}
#endif
