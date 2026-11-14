using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class SolidHallWallsReview
{
    const string Key="SolidHallWallsReview.Stage";
    static double next;
    static SolidHallWallsReview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists("Assets/Verification~/SolidHallWallsReview.request"))
        {
            File.Delete("Assets/Verification~/SolidHallWallsReview.request");SessionState.SetInt(Key,1);
            if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;return;
        }
        int stage=SessionState.GetInt(Key,0);if(stage==0)return;
        if(stage==1){if(EditorApplication.isPlayingOrWillChangePlaymode)return;SessionState.SetInt(Key,2);Application.runInBackground=true;EditorApplication.isPlaying=true;return;}
        if(!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null||Time.unscaledTime<5||EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(stage==2)
            {
                game.BeginMapInspection();
                var camera=UnityEngine.Object.FindAnyObjectByType<MapInspectionCamera>().GetComponent<Camera>();
                camera.transform.position=new Vector3(11,2.7f,16);camera.transform.LookAt(new Vector3(0,4,23.8f));
                SessionState.SetInt(Key,3);next=EditorApplication.timeSinceStartup+4;return;
            }
            var terminal=game.transform.Find("Spaceport terminal");var walls=terminal.Find("Hall wall panels");
            if(walls==null||walls.childCount!=4)throw new Exception("Four walls missing");
            if(Array.FindAll(walls.GetComponentsInChildren<Light>(),light=>light.name=="Caged red wall light").Length!=24)throw new Exception("Wall lamps missing");
            var colliders=walls.GetComponentsInChildren<BoxCollider>();int samples=0;
            for(int side=0;side<4;side++)
            {
                var rotation=Quaternion.Euler(0,side*90,0);
                for(float x=-23.25f;x<=23.25f;x+=.5f)for(float y=.2f;y<8;y+=.4f)
                {
                    bool hit=false;var ray=new Ray(rotation*new Vector3(x,y,21),rotation*Vector3.forward);
                    foreach(var collider in colliders)if(collider.Raycast(ray,out var result,5))hit=true;
                    bool shouldHit=Mathf.Abs(x)>=3.5f||y>=4.4f;
                    if(hit!=shouldHit)throw new Exception("Wall coverage or doorway mismatch: "+side+" "+x+" "+y);samples++;
                }
            }
            foreach(var renderer in terminal.GetComponentsInChildren<Renderer>())
                if(renderer.name.StartsWith("Painted stencil")||renderer.name.StartsWith("Engraved plate / D27"))throw new Exception("Doorway sign remains");
            var core=UnityEngine.Object.FindAnyObjectByType<SuspendedCore>();var hoses=core.GetComponentsInChildren<HeartHoseRoute>();
            if(hoses.Length==0)throw new Exception("No modular heart hoses found");
            var block=new MaterialPropertyBlock();
            core.SamplePulse(.162f);
            foreach(var hose in hoses)
            {
                typeof(HeartHoseRoute).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hose,null);
                var renderer=hose.GetComponentInChildren<Renderer>();renderer.GetPropertyBlock(block);
                if(block.GetVector("_TubePulse").x<.9f)throw new Exception("Hose pulse was not delivered");
                if(ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader))throw new Exception("Hose shader has errors");
            }
            var shader=Shader.Find("Dustline/Modular Heart");if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Invalid hose shader");
            Capture(new Vector3(11,2.7f,16),new Vector3(0,4,23.8f),"SolidHallWalls-Unity");
            Capture(new Vector3(18,3,16),new Vector3(22,4.3f,23.8f),"SolidHallWalls-Detail");
            File.WriteAllText("Assets/Verification~/SolidHallWallsReview.txt","PASS: "+samples+" wall/open-door ray checks; 24 practical lamps; doorway signs absent; pulse delivered to "+hoses.Length+" hose routes; shader valid. Live Unity screenshots captured.");
            SessionState.SetInt(Key,0);
        }
        catch(Exception e){SessionState.SetInt(Key,0);File.WriteAllText("Assets/Verification~/SolidHallWallsReview.txt","FAIL "+e);Debug.LogException(e);}
    }
    static void Capture(Vector3 position,Vector3 target,string name)
    {
        var camera=new GameObject("Temporary wall review camera").AddComponent<Camera>();camera.transform.position=position;camera.transform.LookAt(target);
        camera.fieldOfView=72;camera.farClipPlane=300;camera.useOcclusionCulling=false;camera.cullingMask=~(1<<30);camera.clearFlags=CameraClearFlags.Skybox;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        var rt=new RenderTexture(1440,900,24);var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try{rt.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();File.WriteAllBytes("Assets/Verification~/"+name+".png",pixels.EncodeToPNG());}
        finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(camera.gameObject);}
    }
}
