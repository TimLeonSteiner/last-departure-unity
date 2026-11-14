using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class SkyEyeReview
{
    const string Request="Assets/Verification~/SkyEye/Capture.request";
    const string Pending="SkyEyeReview.Pending";
    static double ready;
    static SkyEyeReview(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Request))
        {
            if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
            File.Delete(Request);
            var path="Assets/LastDeparturePrototype/Resources/Spaceport/WatcherEye.png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;
            importer.maxTextureSize=2048;importer.SaveAndReimport();
            var sky=Resources.Load<Material>("Spaceport/SpaceportSky");
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(texture==null)throw new Exception("Watcher texture import missing");
            sky.SetTexture("_WatcherEye",texture);
            sky.SetFloat("_WatcherSize",.52f);EditorUtility.SetDirty(sky);AssetDatabase.SaveAssets();
            SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
            ready=EditorApplication.timeSinceStartup+8;
        }
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<ready)return;
        SessionState.SetBool(Pending,false);
        try
        {
            var shader=Shader.Find("Dustline/Ashfall Sky");
            if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Sky shader compilation failed");
            if(RenderSettings.skybox==null||RenderSettings.skybox.GetTexture("_WatcherEye")==null)throw new Exception("Live sky lost its eye texture");
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            if(game==null||game.transform.Find("Spaceport terminal")==null)throw new Exception("Terminal missing");
            string checks=game.RunWarehouseChecks();
            Shader.SetGlobalFloat("_WatcherReviewEnabled",1);
            Shader.SetGlobalFloat("_WatcherReviewTime",2);
            SpaceportAssetBuild.Capture(new Vector3(0,1.65f,11),new Vector3(0,25,25),"SkyEye/Hall-Open");
            SpaceportAssetBuild.Capture(new Vector3(0,1.65f,6),new Vector3(0,25,20),"SkyEye/Hall-Framed");
            SpaceportAssetBuild.Capture(new Vector3(-9,1.65f,12),new Vector3(-9,25,26),"SkyEye/Hall-Side");
            SpaceportAssetBuild.Capture(new Vector3(0,1.65f,28),new Vector3(0,25,42),"SkyEye/Outside");
            Shader.SetGlobalFloat("_WatcherReviewTime",10.6f);
            SpaceportAssetBuild.Capture(new Vector3(0,1.65f,11),new Vector3(0,25,25),"SkyEye/Hall-Blink");
            Shader.SetGlobalFloat("_WatcherReviewTime",7);
            SpaceportAssetBuild.Capture(new Vector3(0,1.65f,11),new Vector3(0,25,25),"SkyEye/Hall-Gaze");
            File.WriteAllText("Assets/Verification~/SkyEye/Result.txt","PASS: sky shader compiled; texture bound; six live scene captures.\n"+checks);
        }
        catch(Exception ex){File.WriteAllText("Assets/Verification~/SkyEye/Result.txt","FAIL "+ex);Debug.LogException(ex);}
        finally{Shader.SetGlobalFloat("_WatcherReviewEnabled",0);EditorApplication.isPlaying=false;}
    }
}
