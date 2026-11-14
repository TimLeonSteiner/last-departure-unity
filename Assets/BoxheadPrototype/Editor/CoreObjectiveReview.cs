using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;
[InitializeOnLoad]
public static class CoreObjectiveReview
{
    static CoreObjectiveReview(){EditorApplication.update+=Tick;}
    [MenuItem("Tools/Dustline/Review core and upgrades")]
    public static void Run()
    {
        if(!EditorApplication.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Boxhead.unity");
            SessionState.SetBool("Dustline.CoreReview",true);EditorApplication.isPlaying=true;return;
        }
        try
        {
            if(ShaderUtil.ShaderHasError(Shader.Find("Dustline/Spaceport Cel")))throw new Exception("Scenery shader failed compilation");
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            Directory.CreateDirectory("Assets/Verification~");
            File.WriteAllText("Assets/Verification~/CoreObjectiveChecks.txt",game.RunCoreObjectiveChecks());
            File.WriteAllText("Assets/Verification~/CorePresentationChecks.txt",game.RunCorePresentationChecks(false));
            SpaceportAssetBuild.Capture(new Vector3(5,4,-8),new Vector3(0,3,0),"Suspended-Core");
            SpaceportAssetBuild.Capture(new Vector3(2.8f,3.2f,-5),new Vector3(0,3,0),"Anatomical-Core");
            SpaceportAssetBuild.Capture(new Vector3(7,2.2f,-18),new Vector3(13,1.4f,-14.8f),"Realistic-Racks");
            game.PreviewCoreUpgrades();EditorApplication.isPaused=false;
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        catch(Exception error){File.WriteAllText("Assets/Verification~/CoreObjectiveChecks.txt","FAIL "+error);Debug.LogException(error);if(Application.isBatchMode)EditorApplication.Exit(1);}
    }
    static void Tick()
    {
        if(!SessionState.GetBool("Dustline.CoreReview",false)||!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;
        SessionState.SetBool("Dustline.CoreReview",false);Run();
    }
}
