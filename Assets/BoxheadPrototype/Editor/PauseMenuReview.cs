using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class PauseMenuReview
{
    const string Key="D27.PauseMenuReview";
    static string Output=>SessionState.GetBool(Key+".Home",false)?"Assets/Verification~/PauseMenu/ActualHome.png":"Assets/Verification~/PauseMenu/ActualPause.png";
    static double next;
    static PauseMenuReview(){EditorApplication.update+=Tick;}
    public static void CaptureBatch()
    {
        SessionState.SetBool(Key+".Batch",true);
        SessionState.SetBool(Key+".Home",false);
        EditorSceneManager.OpenScene("Assets/Boxhead.unity");
        if(File.Exists(Output))File.Delete(Output);
        SessionState.SetInt(Key,1);
        EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        int stage=SessionState.GetInt(Key,0);
        if(stage==0&&File.Exists("Assets/Verification~/PauseMenu/Capture.request")&&!EditorApplication.isCompiling)
        {
            SessionState.SetBool(Key+".Home",File.ReadAllText("Assets/Verification~/PauseMenu/Capture.request").Trim()=="home");
            File.Delete("Assets/Verification~/PauseMenu/Capture.request");
            if(File.Exists(Output))File.Delete(Output);
            SessionState.SetBool(Key+".Batch",false);
            SessionState.SetInt(Key,1);
            if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;
            return;
        }
        if(stage==0||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(stage==1)
            {
                var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
                if(game==null)return;
                const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
                if(File.Exists("Assets/Verification~/PauseMenu/HeartChecks.request"))
                {
                    File.Delete("Assets/Verification~/PauseMenu/HeartChecks.request");
                    string report=game.RunCorePresentationChecks(false);
                    var core=UnityEngine.Object.FindAnyObjectByType<SuspendedCore>();
                    if(core==null||core.transform.Find("Riveted overhead suspension drum")==null)throw new Exception("Suspension crown missing");
                    File.WriteAllText("Assets/Verification~/PauseMenu/HeartChecks.txt",report+"PASS Runtime suspension crown exists\n");
                }
                if(SessionState.GetBool(Key+".Home",false))typeof(YardSurvival).GetMethod("ShowHome",flags).Invoke(game,null);
                else
                {
                    typeof(YardSurvival).GetMethod("PlayFromMenu",flags).Invoke(game,null);
                    typeof(YardSurvival).GetMethod("PauseFromGame",flags).Invoke(game,null);
                }
                next=EditorApplication.timeSinceStartup+3;
                SessionState.SetInt(Key,2);
            }
            else if(stage==2&&EditorApplication.timeSinceStartup>next)
            {
                ScreenCapture.CaptureScreenshot(Output);
                next=EditorApplication.timeSinceStartup+20;
                SessionState.SetInt(Key,3);
            }
            else if(stage==3)
            {
                if(File.Exists(Output))
                {
                    SessionState.SetInt(Key,0);
                    Debug.Log("PAUSE SCREEN CAPTURE PASS: "+Output);
                    if(SessionState.GetBool(Key+".Batch",false))EditorApplication.Exit(0);
                }
                else if(EditorApplication.timeSinceStartup>next)throw new Exception("Pause screenshot timed out");
            }
        }
        catch(Exception e)
        {
            SessionState.SetInt(Key,0);Debug.LogException(e);
            if(SessionState.GetBool(Key+".Batch",false))EditorApplication.Exit(1);
        }
    }
}

