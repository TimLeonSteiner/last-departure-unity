using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using YardSurvivalPrototype;
[InitializeOnLoad]
public static class BuildMenuImageReview
{
    const string Key="BuildMenuImageReview.Stage";static double next;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static BuildMenuImageReview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists("Assets/Verification~/BuildMenuImageReview.request"))
        {
            File.Delete("Assets/Verification~/BuildMenuImageReview.request");
            if(!EditorApplication.isPlaying)EditorSceneManager.OpenScene("Assets/Boxhead.unity");SessionState.SetInt(Key,1);Application.runInBackground=true;if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;return;
        }
        int stage=SessionState.GetInt(Key,0);if(stage==0||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();
        if(EditorApplication.timeSinceStartup<next)return;
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null||Time.unscaledTime<4)return;
        try
        {
            if(stage==1)
            {
                game.PrepareInventoryHudReview(false);
                var available=(int[])typeof(YardSurvival).GetField("AvailableDefenses",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
                if(available.Length!=3||available[0]!=0||available[1]!=1||available[2]!=3)throw new Exception("Build selection must contain barricade, turret and barrel");
                int scrap=(int)typeof(YardSurvival).GetField("supplies",Flags).GetValue(game);
                if(game.TryPlaceDefense(Vector3.zero,2,false)||(int)typeof(YardSurvival).GetField("supplies",Flags).GetValue(game)!=scrap)throw new Exception("Removed trap must be rejected without charging scrap");
                typeof(YardSurvival).GetMethod("RestoreLaunchRendering",Flags).Invoke(game,null);
                typeof(YardSurvival).GetMethod("StopLaunchSounds",Flags).Invoke(game,null);
                typeof(YardSurvival).GetField("supplies",Flags).SetValue(game,24);
                typeof(YardSurvival).GetMethod("OpenHologram",Flags).Invoke(game,new object[]{1});
                EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Show();
                SessionState.SetInt(Key,2);next=EditorApplication.timeSinceStartup+4;
            }
            else if(stage==2)
            {
                var puddles=UnityEngine.Object.FindAnyObjectByType<PuddleReflections>();
                if(puddles!=null)
                {
                    var captureAt=typeof(PuddleReflections).GetField("nextCapture",Flags);
                    var late=typeof(PuddleReflections).GetMethod("LateUpdate",Flags);
                    captureAt.SetValue(puddles,0f);late.Invoke(puddles,null);
                    if((float)captureAt.GetValue(puddles)!=0f)throw new Exception("Paused inventory still refreshes the planar reflection");
                    float scale=Time.timeScale;
                    try{Time.timeScale=1;late.Invoke(puddles,null);if((float)captureAt.GetValue(puddles)<=0)throw new Exception("Reflection does not resume with gameplay");}
                    finally{Time.timeScale=scale;late.Invoke(puddles,null);}
                }
                int images=0;foreach(var image in UnityEngine.Object.FindObjectsByType<RawImage>())if(image.name.StartsWith("Build model image ")){if(image.texture==null)throw new Exception("Missing image: "+image.name);images++;}
                if(images!=3)throw new Exception("Expected three build model images, found "+images);
                ScreenCapture.CaptureScreenshot("Assets/Verification~/BuildMenu-ModelImages.png");
                SessionState.SetInt(Key,3);next=EditorApplication.timeSinceStartup+3;
            }
            else
            {
                if(!File.Exists("Assets/Verification~/BuildMenu-ModelImages.png"))throw new Exception("Menu screenshot not saved");
                File.WriteAllText("Assets/Verification~/BuildMenuImageReview.txt","PASS: all three actual-model thumbnails displayed in the live build menu. Left the menu open for review.");SessionState.SetInt(Key,0);
            }
        }
        catch(Exception e){File.WriteAllText("Assets/Verification~/BuildMenuImageReview.txt","FAIL "+e);SessionState.SetInt(Key,0);Debug.LogException(e);}
    }
}
