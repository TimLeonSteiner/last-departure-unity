using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;
[InitializeOnLoad]
public static class DifficultyScreenReview
{
    const string Request="Library/DifficultyScreenReview.request";
    static DifficultyScreenReview(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
        if(!EditorApplication.isPlaying){EditorApplication.isPlaying=true;return;}
        if(Time.timeSinceLevelLoad<2)return;
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null)return;
        File.Delete(Request);
        try{File.WriteAllText("Assets/Verification~/DifficultyScreenChecks.txt",game.RunDifficultyScreenChecks());}
        catch(Exception error){File.WriteAllText("Assets/Verification~/DifficultyScreenChecks.txt","FAIL "+error);Debug.LogException(error);}
    }
}
