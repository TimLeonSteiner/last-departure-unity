using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;
[InitializeOnLoad]
public static class QuietUiReview
{
    const string Root="Assets/Verification~/QuietUi/", Key="QuietUiReview.Stage";
    static double next;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static YardSurvival game;
    static object Call(string name, params object[] args)=>typeof(YardSurvival).GetMethod(name,Flags).Invoke(game,args);
    static void Set(string name,object value)=>typeof(YardSurvival).GetField(name,Flags).SetValue(game,value);
    static object Read(string name)=>typeof(YardSurvival).GetField(name,Flags).GetValue(game);
    static QuietUiReview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Root+"Capture.request")){
            File.Delete(Root+"Capture.request");SessionState.SetInt(Key,1);next=EditorApplication.timeSinceStartup+6;
            if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;
        }
        int stage=SessionState.GetInt(Key,0);
        if(stage==0||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if(EditorApplication.timeSinceStartup<next)return;
        try {
            game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null)return;
            if(stage==1){
                game.PrepareInventoryHudReview(false);Call("RestoreLaunchRendering");Call("StopLaunchSounds");
                Set("supplies",24);Set("skillPoints",1);Set("remainingToSpawn",1);Set("nextWaveCountdown",-1f);
                Call("ResetPreparationTutorial");Call("SelectDefense",1);Set("bannerTimer",0f);Set("bannerDetail",null);
                EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Show();
            }
            else if(stage==2)ScreenCapture.CaptureScreenshot(Root+"BuildCards.png");
            else if(stage==3)Call("OpenHologram",1);
            else if(stage==4)ScreenCapture.CaptureScreenshot(Root+"BuildTab.png");
            else if(stage==5){
                Call("CloseHologram",false);Set("building",false);Set("wave",2);Set("skillRewardWave",1);
                int scrap=(int)Read("supplies"),points=(int)Read("skillPoints");Call("CompleteWave");Call("ResetPreparationTutorial");
                string expected="+"+((int)Read("skillPoints")-points)+" skill point   ·   +"+((int)Read("supplies")-scrap)+" scrap";
                if((string)Read("bannerDetail")!=expected)throw new Exception("Incorrect reward text");
                Set("bannerTimer",30f);Set("nextWaveCountdown",-1f);Set("remainingToSpawn",1);Call("CaptureMouse");
            }
            else if(stage==6)ScreenCapture.CaptureScreenshot(Root+"WaveComplete.png");
            else {
                Call("ShowBanner","Review complete");
                if(Read("bannerDetail")!=null)throw new Exception("Reward detail leaked into generic notification");
                File.WriteAllText(Root+"Result.txt","PASS: actual reward amounts, generic notification reset, build and wave UI captured in Unity.");
                SessionState.SetInt(Key,0);EditorApplication.isPlaying=false;return;
            }
            SessionState.SetInt(Key,stage+1);next=EditorApplication.timeSinceStartup+2;
        }catch(Exception e){File.WriteAllText(Root+"Result.txt","FAIL "+e);SessionState.SetInt(Key,0);Debug.LogException(e);}
    }
}
