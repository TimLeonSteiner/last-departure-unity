using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class ZombieMotionReview
{
    static ZombieMotionReview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!SessionState.GetBool("Dustline.ZombieMotionReview",false)||!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;
        SessionState.SetBool("Dustline.ZombieMotionReview",false);Run();
    }
    [MenuItem("Tools/Dustline/Review zombie motion")]
    public static void Run()
    {
        if(!EditorApplication.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Boxhead.unity");
            SessionState.SetBool("Dustline.ZombieMotionReview",true);EditorApplication.isPaused=false;EditorApplication.isPlaying=true;return;
        }
        try
        {
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            Directory.CreateDirectory("Assets/Verification~");
            if(!YardSurvival.GoreEnabled)
            {
                File.WriteAllText("Assets/Verification~/NonGraphicCombatChecks.txt",game.RunNonGraphicCombatChecks());
                File.WriteAllText("Assets/Verification~/CoreObjectiveChecks.txt",game.RunCoreObjectiveChecks());
                SpaceportAssetBuild.Capture(new Vector3(5,4,-8),new Vector3(0,3,0),"Suspended-Core");
                EditorApplication.isPaused=true;
                Debug.Log("Non-graphic combat and core objective checks passed. Run reset for playtesting.");
                if(Application.isBatchMode)EditorApplication.Exit(0);
                return;
            }
            File.WriteAllText("Assets/Verification~/ZombieMotionChecks.txt",game.RunZombieCombatMotionChecks());
            File.WriteAllText("Assets/Verification~/ZombieInjuryChecks.txt",game.RunZombieInjuryChecks());
            File.WriteAllText("Assets/Verification~/ZombieCrowdChecks.txt",game.RunZombieCrowdChecks());
            game.ReviewZombieMotionPoses(()=>CapturePoses("Zombie-Motion-Poses"));
            game.ReviewZombieInjuryPoses(()=>CapturePoses("Zombie-Injury-Poses"));
            game.ReviewZombieInjuryPoses(()=>CapturePoses("Zombie-Collapse-Stages"),true);
            EditorApplication.isPaused=true;Debug.Log("Zombie motion and combat checks passed. Run reset for playtesting.");
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        catch(Exception error){File.WriteAllText("Assets/Verification~/ZombieMotionChecks.txt","FAIL "+error);Debug.LogException(error);EditorApplication.isPaused=true;if(Application.isBatchMode)EditorApplication.Exit(1);}
    }
    static void CapturePoses(string imageName)
    {
        // Bake the sampled poses for this same-frame inspection camera; skinned
        // renderer GPU updates otherwise wait until the following player frame.
        var root=new GameObject("Pose inspection meshes");
        var light=root.AddComponent<Light>();light.type=LightType.Point;light.range=15;light.intensity=5;
        root.transform.position=new Vector3(0,3,1);
        var meshes=new System.Collections.Generic.List<Mesh>();
        try
        {
            foreach(var zombie in UnityEngine.Object.FindObjectsByType<ImportedZombie>(FindObjectsSortMode.None))
                foreach(var skin in zombie.parts)
                {
                    if(!skin.enabled)continue;
                    var mesh=new Mesh();skin.BakeMesh(mesh,false);meshes.Add(mesh);
                    var obj=new GameObject("Sampled pose");obj.transform.SetParent(root.transform);
                    obj.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);
                    obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                    obj.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
                }
            SpaceportAssetBuild.Capture(new Vector3(0,2,-2),new Vector3(0,1.1f,4),imageName);
        }
        finally{UnityEngine.Object.DestroyImmediate(root);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);}
    }
}
