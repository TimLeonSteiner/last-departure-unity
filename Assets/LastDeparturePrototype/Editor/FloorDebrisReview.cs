using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class FloorDebrisReview
{
    const string Key="FloorDebrisReview.Stage";static double next;
    static FloorDebrisReview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists("Assets/Verification~/FloorDebrisReview.request"))
        {File.Delete("Assets/Verification~/FloorDebrisReview.request");SessionState.SetInt(Key,1);if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;return;}
        int stage=SessionState.GetInt(Key,0);if(stage==0)return;
        if(stage==1){if(EditorApplication.isPlayingOrWillChangePlaymode)return;SessionState.SetInt(Key,2);Application.runInBackground=true;EditorApplication.isPlaying=true;return;}
        if(!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null||Time.unscaledTime<5||EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(stage==2)
            {
                game.BeginMapInspection();var camera=UnityEngine.Object.FindAnyObjectByType<MapInspectionCamera>().GetComponent<Camera>();
                camera.transform.position=new Vector3(7.1f,1.25f,18.4f);camera.transform.LookAt(new Vector3(7.6f,.05f,20.25f));camera.fieldOfView=60;
                SessionState.SetInt(Key,3);next=EditorApplication.timeSinceStartup+3;return;
            }
            var debris=UnityEngine.Object.FindAnyObjectByType<DetailedFloorDebris>();
            if(debris==null||debris.PropCount!=108)throw new Exception("Detailed props missing");
            if(debris.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Decorative trash blocks movement");
            var renderers=debris.GetComponentsInChildren<MeshRenderer>();if(renderers.Length!=7)throw new Exception("Expected seven material batches");
            int triangles=0;foreach(var filter in debris.GetComponentsInChildren<MeshFilter>())triangles+=filter.sharedMesh.triangles.Length/3;
            foreach(var renderer in renderers)if(ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader))throw new Exception("Invalid debris shader");
            var cameraMain=UnityEngine.Object.FindAnyObjectByType<MapInspectionCamera>().GetComponent<Camera>();
            var rt=new RenderTexture(1440,900,24);var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{rt.Create();RenderPipeline.SubmitRenderRequest(cameraMain,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();File.WriteAllBytes("Assets/Verification~/FloorDebris-Unity.png",pixels.EncodeToPNG());}
            finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
            File.WriteAllText("Assets/Verification~/FloorDebrisReview.txt","PASS: 108 recognizable props, seven material batches, "+triangles+" triangles, no movement colliders. Live Unity close-up captured.");SessionState.SetInt(Key,0);
        }
        catch(Exception e){File.WriteAllText("Assets/Verification~/FloorDebrisReview.txt","FAIL "+e);SessionState.SetInt(Key,0);Debug.LogException(e);}
    }
}
