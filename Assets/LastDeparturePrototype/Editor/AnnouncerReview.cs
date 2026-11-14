using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class AnnouncerReview
{
    const string Request="Assets/Verification~/RunAnnouncerChecks.request";
    static AnnouncerReview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        File.Delete(Request);
        Run();
    }
    [MenuItem("Tools/Dustline/Review announcer audio")]
    public static void Run()
    {
        foreach(string audio in Directory.GetFiles("Assets/LastDeparturePrototype/Resources/Announcer","*.wav"))
            AssetDatabase.ImportAsset(audio.Replace('\\','/'),ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        foreach(string audio in Directory.GetFiles("Assets/LastDeparturePrototype/Resources/PlayerVoice","*.mp3"))
            AssetDatabase.ImportAsset(audio.Replace('\\','/'),ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        if(!EditorApplication.isPlaying)
        {
            SessionState.SetBool("Dustline.AnnouncerReview",true);
            EditorApplication.playModeStateChanged-=OnPlay;
            EditorApplication.playModeStateChanged+=OnPlay;
            EditorApplication.isPlaying=true;
            return;
        }
        try
        {
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            if(game==null)throw new Exception("Open the LastDeparture game scene before checking announcer playback.");
            File.WriteAllText("Assets/Verification~/AnnouncerChecks.txt",game.RunAnnouncerChecks());
            try { File.WriteAllText("Assets/Verification~/DrowsinessChecks.txt",game.RunDrowsinessChecks()); }
            catch(Exception error) { File.WriteAllText("Assets/Verification~/DrowsinessChecks.txt","FAIL "+error);Debug.LogException(error); }
        }
        catch(Exception error){File.WriteAllText("Assets/Verification~/AnnouncerChecks.txt","FAIL "+error);Debug.LogException(error);}
    }
    [InitializeOnLoadMethod]
    static void Register(){EditorApplication.playModeStateChanged-=OnPlay;EditorApplication.playModeStateChanged+=OnPlay;}
    static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("Dustline.AnnouncerReview",false))return;
        SessionState.SetBool("Dustline.AnnouncerReview",false);
        EditorApplication.delayCall+=Run;
    }
}
