using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class BloodReview
{
    static BloodReview(){EditorApplication.update+=Tick;}
    public static void RunBatch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/LastDeparture.unity");
        SessionState.SetBool("Dustline.BloodReview",true);EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        if(!SessionState.GetBool("Dustline.BloodReview",false)||!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;
        SessionState.SetBool("Dustline.BloodReview",false);Run();
    }
    [MenuItem("Tools/Dustline/Review blood stains")]
    public static void Run()
    {
        if(!EditorApplication.isPlaying){RunBatch();return;}
        const string path="Assets/LastDeparturePrototype/Resources/BloodStain.mat";
        var shader=Shader.Find("Dustline/Blood Stain");
        if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Blood shader failed compilation");
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
        AssetDatabase.SaveAssets();
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if(!EditorApplication.isPlaying||game==null)throw new Exception("Enter Play mode before reviewing blood");
        try
        {
            File.WriteAllText("Assets/Verification~/BloodChecks.txt",game.RunBloodChecks());
            SpaceportAssetBuild.Capture(new Vector3(0,2.8f,1.5f),new Vector3(.2f,0,4.3f),"Blood-Floor");
            SpaceportAssetBuild.Capture(new Vector3(8,1.2f,20.5f),new Vector3(8,.6f,23.8f),"Blood-Wall");
            EditorApplication.isPaused=true;Debug.Log("Blood stain checks passed; preview marks are in the paused level.");
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        catch(Exception error){File.WriteAllText("Assets/Verification~/BloodChecks.txt","FAIL "+error);Debug.LogException(error);if(Application.isBatchMode)EditorApplication.Exit(1);}
    }
}
