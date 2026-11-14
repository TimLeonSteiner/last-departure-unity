using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class OriginalPickupReview
{
    const string Root="Assets/Verification~/";
    static OriginalPickupReview(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(!File.Exists(Root+"OriginalPickupsReview.request")||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(!EditorApplication.isPlaying){EditorApplication.isPlaying=true;return;}
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null)return;
        File.Delete(Root+"OriginalPickupsReview.request");
        GameObject pack=null,ammo=null,lightObject=null,cameraObject=null;
        try
        {
            string report=game.RunOriginalPickupChecks();
            Vector3 origin=new Vector3(1000,1000,1000);
            cameraObject=new GameObject("Original pickup review camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.10f,.11f);camera.nearClipPlane=.01f;camera.farClipPlane=10;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            lightObject=new GameObject("Original pickup review light");var light=lightObject.AddComponent<Light>();light.type=LightType.Point;light.range=8;light.intensity=10;light.transform.position=origin+new Vector3(-1,2,-1);
            pack=CaffeinePickupVisual.Create(null);pack.transform.position=origin;
            camera.transform.position=origin+new Vector3(.18f,.38f,-.36f);camera.transform.LookAt(origin);camera.fieldOfView=40;
            Capture(camera,"Original-Caffeine-Unity");
            UnityEngine.Object.DestroyImmediate(pack);pack=null;
            ammo=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("OriginalPickups/AmmoBox"));ammo.transform.position=origin;
            camera.transform.position=origin+new Vector3(.65f,.65f,-.85f);camera.transform.LookAt(origin+Vector3.up*.18f);camera.fieldOfView=40;
            Capture(camera,"Original-Ammo-Unity");
            ammo.transform.position=new Vector3(-3,0,-10);
            pack=CaffeinePickupVisual.Create(null);pack.transform.position=new Vector3(-2.4f,.16f,-10);pack.transform.rotation=Quaternion.Euler(15,0,0);
            light.transform.position=new Vector3(-2.5f,1.2f,-11.2f);light.intensity=3;
            SpaceportAssetBuild.Capture(new Vector3(-2.5f,1.05f,-11.2f),new Vector3(-2.7f,.18f,-10),"Original-Pickups-InGame");
            File.WriteAllText(Root+"OriginalPickupsReview.txt",report+"PASS Studio and in-game renders captured.\n");
        }
        catch(Exception e){File.WriteAllText(Root+"OriginalPickupsReview.txt","FAIL "+e);Debug.LogException(e);}
        finally{foreach(var obj in new[]{pack,ammo,lightObject,cameraObject})if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);}
    }
    static void Capture(Camera camera,string name)
    {
        var rt=new RenderTexture(1200,900,24);rt.Create();var tex=new Texture2D(1200,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();File.WriteAllBytes(Root+name+".png",tex.EncodeToPNG());}
        finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
    }
}
