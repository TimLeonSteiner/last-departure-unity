using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;
[InitializeOnLoad]
public static class AnimatedArsenalVerification
{
    static AnimatedArsenalVerification(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Begin(){SessionState.SetBool("Arsenal.Verify",true);var type=typeof(Editor).Assembly.GetType("UnityEditor.GameView");if(type!=null)EditorWindow.GetWindow(type).Show();if(EditorApplication.isPlaying)Run();else EditorApplication.isPlaying=true;}
    static void Changed(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("Arsenal.Verify",false))EditorApplication.delayCall+=Run;}
    static void Run(){SessionState.SetBool("Arsenal.Verify",false);try{var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();File.WriteAllText("Assets/BoxheadPrototype/AnimatedArsenalChecks.txt",game.RunArsenalChecks());RealisticZombieImport.Review();game.CaptureFrontEndReview();}catch(Exception e){File.WriteAllText("Assets/BoxheadPrototype/AnimatedArsenalChecks.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.isPlaying=false;}}
}
