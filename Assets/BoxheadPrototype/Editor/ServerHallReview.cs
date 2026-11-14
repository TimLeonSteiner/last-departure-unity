using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class ServerHallReview
{
    const string Key="ServerHallReview.Stage";static double next;
    static ServerHallReview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists("Assets/Verification~/ServerHallReview.request"))
        {File.Delete("Assets/Verification~/ServerHallReview.request");SessionState.SetInt(Key,1);if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;return;}
        int stage=SessionState.GetInt(Key,0);if(stage==0)return;
        if(stage==1){if(EditorApplication.isPlayingOrWillChangePlaymode)return;SessionState.SetInt(Key,2);Application.runInBackground=true;EditorApplication.isPlaying=true;return;}
        if(!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null||Time.unscaledTime<5||EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(stage>=4)
            {
                var camera=UnityEngine.Object.FindAnyObjectByType<MapInspectionCamera>().GetComponent<Camera>();
                string[] names={"ServerHall-RainOpening","ServerHall-WetFloor","ServerHall-OpenEntrance"};
                Capture(camera.transform.position,camera.transform.position+camera.transform.forward*10,names[stage-4]);
                if(stage==4){camera.transform.position=new Vector3(-12,2.8f,-5.5f);camera.transform.LookAt(new Vector3(-15.22f,.1f,-9.13f));}
                else if(stage==5){camera.transform.position=new Vector3(9,2.6f,17);camera.transform.LookAt(new Vector3(0,3,23.8f));}
                if(stage<6){SessionState.SetInt(Key,stage+1);next=EditorApplication.timeSinceStartup+3;}
                else{File.AppendAllText("Assets/Verification~/ServerHallReview.txt","\nAll four live views captured after camera settling.");SessionState.SetInt(Key,0);}
                return;
            }
            if(stage==2)
            {
                game.BeginMapInspection();var camera=UnityEngine.Object.FindAnyObjectByType<MapInspectionCamera>().GetComponent<Camera>();
                camera.transform.position=new Vector3(5,2.4f,16);camera.transform.LookAt(new Vector3(13,2.5f,22));
                SessionState.SetInt(Key,3);next=EditorApplication.timeSinceStartup+3;return;
            }
            var terminal=game.transform.Find("Spaceport terminal");
            if(terminal.Find("Open entrances without gate posts")==null)throw new Exception("Entrance removal missing");
            if(terminal.GetComponentsInChildren<BoxCollider>().Count(c=>c.name=="Server cabinet collision")!=24)throw new Exception("24 perimeter cabinets missing");
            if(terminal.GetComponentsInChildren<Light>().Count(l=>l.name=="Cool server inspection light")!=8)throw new Exception("Eight server service lights missing");
            foreach(var c in game.GetComponentsInChildren<BoxCollider>())if(c.name=="Loading door upright"&&c.enabled)throw new Exception("Invisible old doorway collider remains");
            var roof=terminal.Find("Meshy roof");var collider=roof.GetComponent<MeshCollider>();var layout=FullRoofLayout.Load();
            if(layout.rainPatches.Length!=3)throw new Exception("Three rain patches missing");
            int covered=0,total=0;for(float x=-23.5f;x<24;x+=.5f)for(float z=-23.5f;z<24;z+=.5f){total++;if(collider.Raycast(new Ray(new Vector3(x,12,z),Vector3.down),out var h,8))covered++;}
            if(covered<total*.98f)throw new Exception("Too much ceiling missing: "+covered+"/"+total);
            foreach(var patch in layout.rainPatches)
            {
                var center=patch.center*YardSurvival.ArenaScale;
                for(int x=0;x<=4;x++)for(int z=0;z<=4;z++)
                {
                    var p=new Vector3(Mathf.Lerp(patch.xMin,patch.xMax,x/4f)*YardSurvival.ArenaScale,12,Mathf.Lerp(patch.yMin,patch.yMax,z/4f)*YardSurvival.ArenaScale);
                    if(collider.Raycast(new Ray(p,Vector3.down),out var hit,7))throw new Exception("Rain blocked by ceiling");
                }
                bool drops=false,splash=false;
                foreach(var ps in game.GetComponentsInChildren<ParticleSystem>())
                {
                    var p=ps.transform.position;if(Vector2.Distance(new Vector2(p.x,p.z),center)>.15f)continue;
                    if(ps.name=="Slanted rainfall"){drops=ps.emission.rateOverTime.constant>0;if(!ps.isPlaying)ps.Play();}
                    if(ps.name=="Ground splashes")splash=true;
                }
                if(!drops||!splash)throw new Exception("Rain or splashes absent at "+center);
            }
            if(ShaderUtil.ShaderHasError(Shader.Find("Dustline/Spaceport Cel")))throw new Exception("Floor shader invalid");
            if(terminal.Find("Server floor dirt and tide marks")==null||ShaderUtil.ShaderHasError(Shader.Find("Dustline/Server Floor Grime")))throw new Exception("Floor grime missing or invalid");
            Capture(new Vector3(5,2.4f,16),new Vector3(13,2.5f,22),"ServerHall-Cabinets");
            var live=UnityEngine.Object.FindAnyObjectByType<MapInspectionCamera>().GetComponent<Camera>();live.transform.position=new Vector3(-10,3,-5);live.transform.LookAt(new Vector3(-15.22f,6.4f,-9.13f));
            File.WriteAllText("Assets/Verification~/ServerHallReview.txt","PASS: 24 cabinets and their collision, eight cool lights, removed entrance post collision, three open rain shafts and matching emitters/splashes. Roof coverage "+covered+"/"+total+". Capturing live views.");SessionState.SetInt(Key,4);next=EditorApplication.timeSinceStartup+3;
        }
        catch(Exception e){File.WriteAllText("Assets/Verification~/ServerHallReview.txt","FAIL "+e);SessionState.SetInt(Key,0);Debug.LogException(e);}
    }
    static void Capture(Vector3 position,Vector3 target,string name)
    {
        var camera=new GameObject("Temporary server hall camera").AddComponent<Camera>();camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=75;camera.farClipPlane=300;camera.cullingMask=~(1<<30);camera.useOcclusionCulling=false;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        var rt=new RenderTexture(1440,900,24);var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try{rt.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();File.WriteAllBytes("Assets/Verification~/"+name+".png",pixels.EncodeToPNG());}
        finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(camera.gameObject);}
    }
}
