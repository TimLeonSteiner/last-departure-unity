using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;
[InitializeOnLoad]
public static class DifficultyReview
{
    const string Request="Assets/Verification~/Difficulty.request";
    static DifficultyReview(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if(game==null)return;
        File.Delete(Request);
        try{File.WriteAllText("Assets/Verification~/DifficultyChecks.txt",game.RunDifficultyChecks());}
        catch(Exception error){File.WriteAllText("Assets/Verification~/DifficultyChecks.txt","FAIL "+error);}
    }
}
