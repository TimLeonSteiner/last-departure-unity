using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class HeartGameplayReview
{
    const string Key="D27.HeartGameplayReview";
    const string Folder="Assets/Verification~/";
    static double next;
    static HeartGameplayReview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling)return;
        int stage=SessionState.GetInt(Key,0);
        if(SessionState.GetBool("Dustline.UpgradePresentationReview",false)||SessionState.GetBool("Dustline.NightmareReview",false))
        {
            if(stage>0){SessionState.SetInt(Key,0);File.WriteAllText(Folder+"HeartGameplay.request","capture");}
            return;
        }
        if(stage==0&&File.Exists(Folder+"HeartGameplay.request"))
        {
            File.Delete(Folder+"HeartGameplay.request");
            SessionState.SetInt(Key,1);
            EditorApplication.isPlaying=true;
            return;
        }
        if(stage==0||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Repaint();
        try
        {
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null)return;
            if(stage==1)
            {
                Application.runInBackground=true;
                EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
                File.WriteAllText(Folder+"HeartGameplayChecks.txt",game.RunCorePresentationChecks(false));
                File.WriteAllText(Folder+"HeartBayCollisionChecks.txt",game.RunBayCollisionChecks());
                game.PrepareHeartGameplayReview(0);next=EditorApplication.timeSinceStartup+2;SessionState.SetInt(Key,2);
            }
            else if(stage==2&&EditorApplication.timeSinceStartup>next)
            {
                ScreenCapture.CaptureScreenshot(Folder+"HeartGameplay-Front.png");next=EditorApplication.timeSinceStartup+3;SessionState.SetInt(Key,3);
            }
            else if(stage==3&&EditorApplication.timeSinceStartup>next)
            {
                game.PrepareHeartGameplayReview(1);next=EditorApplication.timeSinceStartup+2;SessionState.SetInt(Key,4);
            }
            else if(stage==4&&EditorApplication.timeSinceStartup>next)
            {
                ScreenCapture.CaptureScreenshot(Folder+"HeartGameplay-Side.png");next=EditorApplication.timeSinceStartup+3;SessionState.SetInt(Key,5);
            }
            else if(stage==5&&EditorApplication.timeSinceStartup>next)
            {
                if(!File.Exists(Folder+"HeartGameplay-Front.png")||!File.Exists(Folder+"HeartGameplay-Side.png"))throw new Exception("Gameplay capture missing");
                game.EndHeartGameplayReview();SessionState.SetInt(Key,0);Debug.Log("GAMEPLAY HEART REVIEW PASS");
            }
        }
        catch(Exception e){SessionState.SetInt(Key,0);Time.timeScale=1;Debug.LogException(e);}
    }
}
