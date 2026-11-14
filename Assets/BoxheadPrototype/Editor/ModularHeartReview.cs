using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class ModularHeartReview
{
    const string Root="Assets/Verification~/",Key="ModularHeartReview.Stage";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static double next;
    static ModularHeartReview(){EditorApplication.update+=Tick;}
    static void Check(bool value,string label){if(!value)throw new Exception(label);File.AppendAllText(Root+"ModularHeartChecks.txt","PASS "+label+"\n");}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Root+"ModularHeartReview.request"))
        {File.Delete(Root+"ModularHeartReview.request");File.WriteAllText(Root+"ModularHeartChecks.txt","");SessionState.SetInt(Key,1);next=EditorApplication.timeSinceStartup+7;if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;}
        if(SessionState.GetInt(Key,0)==0||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
        try
        {
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null)return;
            object Call(string name,params object[] args)=>typeof(YardSurvival).GetMethod(name,Flags).Invoke(game,args);
            Call("RestoreLaunchRendering");Call("PlayFromMenu");game.enabled=false;
            var core=(SuspendedCore)typeof(YardSurvival).GetField("coreVisual",Flags).GetValue(game);
            Check(core.HasModularPlatform,"Modular Meshy platform is active");
            Check(core.transform.Find("Heart maintenance bay")==null,"Legacy railings and service lamps removed");
            var model=core.transform.Find("Modular biomechanical heart platform");
            Check(model!=null,"Modular assembly exists");
            var heart=model.Find("Heart");var heartRenderer=heart.GetComponentInChildren<Renderer>(true);
            Check(heart.GetComponentInChildren<MeshFilter>(true).sharedMesh.triangles.Length/3>1000000,"Original detailed heart mesh preserved");
            int vessels=0;foreach(var route in model.GetComponentsInChildren<HeartHoseRoute>(true))if(route.name.StartsWith("Vessel "))vessels++;
            Check(vessels==12,"Twelve individual vessel connections");
            int ramps=0,decks=0;foreach(Transform child in model){if(child.name.StartsWith("Perimeter ramp"))ramps++;if(child.name.StartsWith("Deck sector"))decks++;}
            Check(ramps==8&&decks==4,"Continuous ring uses eight ramps and four deck sections");
            Check(!ShaderUtil.ShaderHasError(heartRenderer.sharedMaterial.shader),"Modular shader compiles");
            for(int i=0;i<16;i++)
            {
                var p=new Vector3(Mathf.Cos(i*Mathf.PI/8),0,Mathf.Sin(i*Mathf.PI/8))*4.6f;
                Check((bool)Call("Free",p,.28f,true),"Open ramp approach "+i);
            }
            var player=(Transform)typeof(YardSurvival).GetField("player",Flags).GetValue(game);
            player.position=new Vector3(0,.65f,-3);Call("UpdatePlayerMovement",Vector2.zero,false,false,.016f,false);
            Check(Mathf.Abs(player.position.y-(.65f+core.PlatformFloor(player.position)))<.001f,"Player stands on raised deck");
            Call("UpdatePlayerMovement",Vector2.zero,true,false,.1f,false);Check(player.position.y>1,"Jump rises from platform");
            for(int i=0;i<120;i++)Call("UpdatePlayerMovement",Vector2.zero,false,false,.016f,false);
            Check(Mathf.Abs(player.position.y-(.65f+core.PlatformFloor(player.position)))<.001f,"Jump lands back on deck");
            var scale=model.localScale;core.SamplePulse(.17f/1.05f);float beat=heartRenderer.sharedMaterial.GetFloat("_Pulse");
            core.SamplePulse(.75f/1.05f);Check(beat>.9f&&heartRenderer.sharedMaterial.GetFloat("_Pulse")<.01f&&model.localScale==scale,"Heartbeat affects tissue while frame stays rigid");
            Check(!ShaderUtil.ShaderHasError(Shader.Find("Dustline/Spaceport Cel")),"Hall floor shader compiles");
            player.position=new Vector3(-10,.65f,-8);
            var service=core.GetComponentInChildren<HeartServiceDetails>();
            Check(service!=null,"Heart maintenance details exist");
            Check(service.GetComponentsInChildren<Light>().Length==3,"Two warm frame lamps and one pulse spill");
            foreach(var collider in service.GetComponentsInChildren<Collider>())Check(!collider.enabled,"Service dressing leaves movement unobstructed");
            core.SamplePulse(.17f/1.05f);float spillPeak=service.PulseIntensity;
            core.SamplePulse(.75f/1.05f);Check(spillPeak>service.PulseIntensity+.9f,"Red spill follows the heartbeat");
            foreach(var r in service.GetComponentsInChildren<Renderer>())Check(!ShaderUtil.ShaderHasError(r.sharedMaterial.shader),"Service shader valid: "+r.name);
            service.Steam.Simulate(2.2f,true,true,true);
            Check(service.Steam.particleCount>0&&service.Steam.main.maxParticles==24,"Intermittent steam emits within particle budget");
            SpaceportAssetBuild.Capture(new Vector3(4.5f,2.8f,-5.5f),new Vector3(.5f,1.7f,0),"HeartService-Details");

            SpaceportAssetBuild.Capture(new Vector3(10,7,-13),new Vector3(0,.4f,0),"HeartHall-Floor");
            SpaceportAssetBuild.Capture(new Vector3(7,5,-9),new Vector3(0,2,0),"ModularHeart-Overview");
            SpaceportAssetBuild.Capture(new Vector3(0,1.8f,-6.5f),new Vector3(0,2.2f,0),"ModularHeart-Player");
            SpaceportAssetBuild.Capture(new Vector3(2.5f,3.5f,-4),new Vector3(0,2.6f,0),"ModularHeart-Connections");
            foreach(var p in new[]{new Vector3(0,0,-3.8f),new Vector3(0,0,3.8f),new Vector3(-3.8f,0,0),new Vector3(3.8f,0,0)})
                Check((bool)Call("Free",p,.28f,true)&&core.PlatformFloor(p)>0,"Walkable ramp surface "+p);
            player.position=new Vector3(0,.65f,-5);
            Check(game.TryPlaceDefense(new Vector3(0,0,-2.5f),1,false),"Turret can be placed on flat platform deck");
            var turrets=(System.Collections.IList)typeof(YardSurvival).GetField("turrets",Flags).GetValue(game);
            var turret=turrets[turrets.Count-1];var body=(Transform)turret.GetType().GetField("body").GetValue(turret);
            Check(Mathf.Abs(body.position.y-core.PlatformFloor(body.position))<.001f,"Defense stands on platform surface");
            foreach(var start in new[]{new Vector3(0,0,-6),new Vector3(0,0,6),new Vector3(-6,0,0),new Vector3(6,0,0)})
            {
                Call("ResetRun");player=(Transform)typeof(YardSurvival).GetField("player",Flags).GetValue(game);player.position=new Vector3(-18,.65f,-18);
                Call("SpawnEnemy",start);
                for(int i=0;i<400;i++){if(i%6==0)Call("RefreshPaths");Call("UpdateEnemies",.05f);}
                Check((int)typeof(YardSurvival).GetField("coreHealth",Flags).GetValue(game)<500,"Enemy reaches and attacks heart from "+start);
            }
            File.AppendAllText(Root+"ModularHeartChecks.txt","DONE\n");
        }
        catch(Exception e){File.AppendAllText(Root+"ModularHeartChecks.txt","FAIL "+e+"\n");Debug.LogException(e);}
        finally{SessionState.SetInt(Key,0);EditorApplication.isPlaying=false;}
    }
}
