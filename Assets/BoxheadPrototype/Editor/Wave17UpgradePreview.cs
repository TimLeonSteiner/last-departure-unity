using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;
[InitializeOnLoad]
public static class Wave17UpgradePreview
{
    const string Request="Library/Wave17UpgradePreview.request";
    static Wave17UpgradePreview(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
        if(!EditorApplication.isPlaying){EditorApplication.isPlaying=true;return;}
        if(Time.timeSinceLevelLoad<2)return;
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null)return;
        File.Delete(Request);
        try{File.WriteAllText("Assets/Verification~/UniqueSkillChecks.txt",game.RunUniqueSkillChecks());File.WriteAllText("Assets/Verification~/Wave17UpgradePreview.txt",game.StartWave17UpgradePreview());}
        catch(Exception e){File.WriteAllText("Assets/Verification~/Wave17UpgradePreview.txt","FAIL "+e);Debug.LogException(e);}
    }
}
