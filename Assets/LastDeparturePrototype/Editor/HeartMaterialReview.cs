using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class HeartMaterialReview
{
    const string Request="Assets/Verification~/HeartMaterial.request";
    const string Pending="D27.HeartMaterialReview";
    static double ready;
    static HeartMaterialReview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Request))
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}
            File.Delete(Request);
            const string folder="Assets/LastDeparturePrototype/Resources/Spaceport/";
            foreach(string file in new[]{"AnatomicalHeart.fbx","HeartColor.png","HeartNormal.png","HeartMetallicSmoothness.png"})
                AssetDatabase.ImportAsset(folder+file,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            typeof(HorrorArtReview).GetMethod("BuildHeartSurface",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{folder,"Heart"});
            AssetDatabase.SaveAssets();SessionState.SetBool(Pending,true);ready=EditorApplication.timeSinceStartup+5;EditorApplication.isPlaying=true;
        }
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<ready)return;
        SessionState.SetBool(Pending,false);
        try
        {
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            if(game==null)throw new Exception("Game missing");
            var core=UnityEngine.Object.FindAnyObjectByType<SuspendedCore>();
            var renderer=core.transform.Find("Living anatomical heart/Blender anatomical heart").GetComponentInChildren<MeshRenderer>();
            var material=renderer.sharedMaterial;
            if(material.GetTexture("_BaseMap").width<4096||material.GetTexture("_BumpMap")==null)throw new Exception("Heart atlas missing");
            if(material.GetColor("_BaseColor")!=Color.white||material.GetFloat("_BumpScale")>1)throw new Exception("Heart hue or relief override regressed");
            SpaceportAssetBuild.Capture(new Vector3(1.7f,3.3f,-3.5f),new Vector3(0,3,0),"Heart-Material-Front");
            SpaceportAssetBuild.Capture(new Vector3(-3.2f,3.7f,-2.1f),new Vector3(0,3.1f,0),"Heart-Material-Side");
            File.WriteAllText("Assets/Verification~/HeartMaterialChecks.txt",game.RunCorePresentationChecks(false)+"\nPASS 4096px PBR atlases, neutral atlas tint, restrained relief, two Unity material captures.\n");
        }
        catch(Exception e){File.WriteAllText("Assets/Verification~/HeartMaterialChecks.txt","FAIL "+e);Debug.LogException(e);}
        finally{EditorApplication.isPlaying=false;}
    }
}
