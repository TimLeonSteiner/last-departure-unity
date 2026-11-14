using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class HeartPlatformReview
{
    const string Root="Assets/Verification~/",Key="HeartPlatformReview.Stage";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static double next;
    static HeartPlatformReview(){EditorApplication.update+=Tick;}
    static void Check(bool value,string label){if(!value)throw new Exception(label);File.AppendAllText(Root+"HeartPlatformChecks.txt","PASS "+label+"\n");}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Root+"HeartPlatformReview.request"))
        {File.Delete(Root+"HeartPlatformReview.request");File.WriteAllText(Root+"HeartPlatformChecks.txt","");SessionState.SetInt(Key,1);next=EditorApplication.timeSinceStartup+7;if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;}
        if(SessionState.GetInt(Key,0)==0||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;
        try
        {
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null)return;
            object Call(string name,params object[] args)=>typeof(YardSurvival).GetMethod(name,Flags).Invoke(game,args);
            Call("RestoreLaunchRendering");Call("PlayFromMenu");game.enabled=false;
            var core=(SuspendedCore)typeof(YardSurvival).GetField("coreVisual",Flags).GetValue(game);
            Check(core.HasMeshyPlatform,"New Meshy platform replaces legacy assembly");
            Check(core.transform.Find("Heart maintenance bay")==null,"Legacy railings and service lamps removed");
            var model=core.transform.Find("Meshy biomechanical heart platform");
            Check(model.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3<=65000,"65k triangle budget");
            Check(!ShaderUtil.ShaderHasError(model.GetComponent<Renderer>().sharedMaterial.shader),"Platform shader compiles");
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
            foreach(var band in new[]{new Vector2(1.8f,2.2f),new Vector2(.7f,1f)})
            {
                var b=new Bounds();bool first=true;
                foreach(var v in model.GetComponent<MeshFilter>().sharedMesh.vertices)if(v.x>2.1f&&v.y>band.x&&v.y<band.y){if(first){b=new Bounds(v,Vector3.zero);first=false;}else b.Encapsulate(v);}
                File.AppendAllText(Root+"HeartPlatformChecks.txt","HARDWARE "+band+" "+b+"\n");
            }
            var scale=model.localScale;core.SamplePulse(.17f/1.05f);float beat=model.GetComponent<Renderer>().sharedMaterial.GetFloat("_Pulse");
            core.SamplePulse(.75f/1.05f);Check(beat>.9f&&model.GetComponent<Renderer>().sharedMaterial.GetFloat("_Pulse")<.01f&&model.localScale==scale,"Heartbeat changes tissue shader without scaling rigid frame");
            player.position=new Vector3(-10,.65f,-8);
            SpaceportAssetBuild.Capture(new Vector3(7,5,-9),new Vector3(0,2,0),"HeartPlatform-Overview");
            SpaceportAssetBuild.Capture(new Vector3(0,1.8f,-6.5f),new Vector3(0,2.2f,0),"HeartPlatform-Player");
            var pumpBounds=new Bounds();bool firstPump=true;
            foreach(var v in model.GetComponent<MeshFilter>().sharedMesh.vertices)if(v.x>1.8f&&v.x<2.9f&&v.y>.7f&&v.y<1f){if(firstPump){pumpBounds=new Bounds(v,Vector3.zero);firstPump=false;}else pumpBounds.Encapsulate(v);}
            File.AppendAllText(Root+"HeartPlatformChecks.txt","PUMP "+pumpBounds+"\n");
            // Read actual low surfaces to compare the smooth walking proxy with the asset.
            var probe=model.gameObject.AddComponent<MeshCollider>();
            for(float r=2;r<4.3f;r+=.3f)if(probe.Raycast(new Ray(new Vector3(0,.7f,-r),Vector3.down),out var hit,1))File.AppendAllText(Root+"HeartPlatformChecks.txt","SURFACE "+r+" = "+hit.point.y+"\n");
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
            File.AppendAllText(Root+"HeartPlatformChecks.txt","DONE\n");
        }
        catch(Exception e){File.AppendAllText(Root+"HeartPlatformChecks.txt","FAIL "+e+"\n");Debug.LogException(e);}
        finally{SessionState.SetInt(Key,0);EditorApplication.isPlaying=false;}
    }
}
