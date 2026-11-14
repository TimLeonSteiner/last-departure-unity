#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunArsenalChecks()
        {
            bool previous=soundEnabled;soundEnabled=false;
            string folder="Assets/LastDeparturePrototype/ArsenalReview";Directory.CreateDirectory(folder);
            var target=new RenderTexture(800,500,24);target.Create();var image=new Texture2D(800,500,TextureFormat.RGB24,false);
            var old=arenaCamera.targetTexture;var active=RenderTexture.active;
            try
            {
                string result=RunAdditionalWeaponChecks();
                arenaCamera.targetTexture=target;
                for(int id=0;id<WeaponCatalog.All.Length;id++)
                {
                    ResetRun();EquipWeapon(id,WeaponCatalog.All[id].capacity);
                    gun.GetComponentInChildren<AnimatedWeaponRig>().Pose(0,false,1);
                    for(int phase=0;phase<4;phase++)
                    {
                        viewWeapon.Tick(phase==0?0:(phase-.5f)/3,phase>0,0,0);
                        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(arenaCamera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;image.ReadPixels(new Rect(0,0,800,500),0,0);image.Apply();
                        File.WriteAllBytes(folder+"/"+id+"-"+phase+".png",image.EncodeToPNG());
                    }
                }
                float reviewFov=baseFov;
                foreach(var size in new[]{new Vector2Int(960,540),new Vector2Int(960,720),new Vector2Int(1260,540)})
                foreach(float fov in new[]{60f,75f,100f})
                foreach(int id in new[]{2,5,10})
                {
                    var frame=new RenderTexture(size.x,size.y,24);frame.Create();var pixels=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                    ResetRun();EquipWeapon(id,WeaponCatalog.All[id].capacity);gun.GetComponentInChildren<AnimatedWeaponRig>().Pose(0,false,1);viewWeapon.Tick(0,false,0,0);
                    arenaCamera.fieldOfView=fov;arenaCamera.targetTexture=frame;
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(arenaCamera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=frame});
                    RenderTexture.active=frame;pixels.ReadPixels(new Rect(0,0,size.x,size.y),0,0);pixels.Apply();File.WriteAllBytes(folder+"/FOV-"+id+"-"+fov+"-"+size.x+"x"+size.y+".png",pixels.EncodeToPNG());
                    arenaCamera.targetTexture=target;frame.Release();Destroy(frame);Destroy(pixels);
                }
                baseFov=reviewFov;arenaCamera.fieldOfView=baseFov;
                var priorMask=arenaCamera.cullingMask;var priorClear=arenaCamera.clearFlags;var priorBackground=arenaCamera.backgroundColor;
                arenaCamera.cullingMask=0;arenaCamera.clearFlags=CameraClearFlags.SolidColor;arenaCamera.backgroundColor=new Color(.11f,.13f,.15f);
                foreach(int id in new[]{4,5,6,7,8,9,10,13})
                {
                    ResetRun();EquipWeapon(id,WeaponCatalog.All[id].capacity);var rig=gun.GetComponentInChildren<AnimatedWeaponRig>();rig.Pose(0,false,1);viewWeapon.Tick(0,false,0,0);
                    for(int frame=0;frame<=rig.fire.length*rig.fire.frameRate;frame+=10)
                    {
                        rig.fire.SampleAnimation(rig.model,frame/rig.fire.frameRate);
                        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(arenaCamera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});
                        RenderTexture.active=target;image.ReadPixels(new Rect(0,0,800,500),0,0);image.Apply();File.WriteAllBytes(folder+"/Timeline-"+id+"-"+frame+".png",image.EncodeToPNG());
                    }
                }
                arenaCamera.cullingMask=priorMask;arenaCamera.clearFlags=priorClear;arenaCamera.backgroundColor=priorBackground;
                ResetRun();EquipWeapon(10,1);SpawnEnemy(new Vector3(0,0,8));MeleeAttack(Vector3.forward);if(enemies.Count!=1||enemies[0].hp!=3)throw new Exception("Melee hit outside range");
                ResetRun();EquipWeapon(5,1);SpawnEnemy(new Vector3(0,0,1.5f));MeleeAttack(Vector3.forward);if(enemies.Count!=0)throw new Exception("Pitchfork misses in range");
                ResetRun();EquipWeapon(7,1);SpawnEnemy(new Vector3(0,0,5));LaunchArsenalProjectile((enemies[0].position+Vector3.up-arenaCamera.transform.position).normalized,false);for(int i=0;i<100;i++)UpdateArsenalProjectiles(.02f);if(enemies.Count!=0)throw new Exception("Crossbow bolt failed");
                ResetRun();EquipWeapon(4,1);SpawnEnemy(new Vector3(0,0,4));LaunchArsenalProjectile((enemies[0].position+Vector3.up*.3f-arenaCamera.transform.position).normalized,true);for(int i=0;i<160;i++)UpdateArsenalProjectiles(.02f);if(enemies.Count!=0)throw new Exception("Grenade splash failed");
                ResetRun();TryPlaceDefense(new Vector3(0,0,3),1,false);SpawnEnemy(new Vector3(0,0,6));for(int i=0;i<3;i++)UpdateTurrets(.7f);if(enemies.Count!=0)throw new Exception("Imported sentry failed to fire");
                gun.gameObject.SetActive(false);UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(arenaCamera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;image.ReadPixels(new Rect(0,0,800,500),0,0);image.Apply();File.WriteAllBytes(folder+"/Turret.png",image.EncodeToPNG());
                return result+"\nPASS: melee range, crossbow flight, grenade splash and sentry targeting/fire.\n"+RunSmokeChecks()+"\n"+RunFrontEndAndCollisionChecks();
            }
            finally{soundEnabled=previous;arenaCamera.targetTexture=old;RenderTexture.active=active;target.Release();Destroy(target);Destroy(image);ResetRun();}
        }
        string RunFrontEndAndCollisionChecks()
        {
            void Check(bool ok,string label){if(!ok)throw new Exception(label);}
            ShowHome();Check(frontScreen==FrontScreen.Home&&!fighting&&!dead,"Home clean run");
            ShowOptions(FrontScreen.Home);Check(optionsReturn==FrontScreen.Home,"Home options return");
            PlayFromMenu();Check(frontScreen==FrontScreen.Game&&fighting&&wave==1,"Play starts round one");
            PauseFromGame();Check(Time.timeScale==0&&AudioListener.pause,"Pause freezes gameplay/audio");
            ShowOptions(FrontScreen.Pause);Check(optionsReturn==FrontScreen.Pause,"Paused options return");
            ResumeFromMenu();Check(Time.timeScale==1&&!AudioListener.pause,"Resume restores gameplay/audio");
            ResetRun();frontScreen=FrontScreen.Game;ToggleBuilding();Check(building,"Build opens");
            Check(TryPlaceDefense(new Vector3(3,0,0),1,false),"Build turret");
            ToggleBuilding();Check(!building&&!ghost.gameObject.activeSelf,"Same toggle closes preview after building");
            ToggleBuilding();Check(building,"Build opens again");ToggleBuilding();
            Check(!Free(new Vector3(3,0,0),PlayerRadius,true),"Imported turret blocks player");
            Check(Move(new Vector3(-16,0,-9),Vector3.right*25,PlayerRadius,true).x < -13,"Large movement cannot cross freight");
            Vector3 along=Move(new Vector3(-13.5f,0,-9),new Vector3(2,0,1.5f),PlayerRadius,true);
            Check(along.x < -13 && along.z > -9,"Diagonal movement slides along freight");
            Check(Free(new Vector3(0,0,16),PlayerRadius,true),"Loading door remains open");
            RegisterObstacles();Check(!Free(ArenaPosition(new Vector3(-10,0,-9)),PlayerRadius,false),"Collision survives list rebuild");
            float original=baseFov;
            foreach(float fov in new[]{60f,75f,100f})
            {
                baseFov=fov;arenaCamera.fieldOfView=fov;EquipWeapon(10,1);viewWeapon.Tick(0,false,0,0);
                Check(Mathf.Approximately(viewmodelCamera.fieldOfView,70),"Viewmodel FOV independent");
                arenaCamera.fieldOfView=Mathf.Min(42,fov*.55f);RestoreFrontEndEffects();Check(Mathf.Approximately(arenaCamera.fieldOfView,fov),"FOV restores after ADS");
            }
            baseFov=original;ResetRun();SpawnEnemy(new Vector3(0,0,2));BeginDeathSequence();
            for(int i=0;i<80;i++)TickDeath(.05f);
            Check(arenaCamera.transform.position.y<=.23f&&deathVolume.weight>.99f&&AudioListener.volume<.3f,"Death drop and grading/audio");
            PlayFromMenu();Check(!dead&&deathVolume.weight==0&&AudioListener.volume==1&&arenaCamera.transform.position.y>1.5f,"Restart restores death states");
            ShowHome();Check(!dead&&wave==0&&enemies.Count==0,"Main menu clears run");
            return "PASS: home/play, pause/options/resume, death/restart/menu, build toggle after placement, turret/freight collisions, diagonal wall slide, large movement steps, open loading door, FOV 60/75/100 and ADS restoration.";
        }
        public void CaptureFrontEndReview(){StartCoroutine(CaptureFrontEndFrames());}
        System.Collections.IEnumerator CaptureFrontEndFrames()
        {
            string folder="Assets/LastDeparturePrototype/ArsenalReview/";
            ShowHome();yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(folder+"Menu.png");
            yield return null;ShowOptions(FrontScreen.Home);yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(folder+"Options.png");
            yield return null;PlayFromMenu();PauseFromGame();yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(folder+"Pause.png");
            yield return null;ResumeFromMenu();ResetRun();SpawnEnemy(new Vector3(0,0,1.3f));BeginDeathSequence();for(int i=0;i<60;i++)TickDeath(.05f);
            yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(folder+"Death.png");
            yield return null;ShowHome();UnityEditor.EditorApplication.isPlaying=false;
        }
    }
}
#endif


