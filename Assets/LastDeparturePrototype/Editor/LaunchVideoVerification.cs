using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class LaunchVideoVerification
{
    const string Request="Assets/Verification~/LaunchVideo.request";
    static LaunchVideoVerification(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Request))
        {
            if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
            File.Delete(Request);SessionState.SetBool("LaunchVideoReview",true);EditorApplication.isPlaying=true;
        }
        if(!EditorApplication.isPlaying||!SessionState.GetBool("LaunchVideoReview",false))return;
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if(game==null)return;
        SessionState.SetBool("LaunchVideoReview",false);game.StartCoroutine(Verify(game));
    }
    static IEnumerator Verify(YardSurvival game)
    {
        yield return new WaitForSecondsRealtime(5);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var video=(VideoPlayer)typeof(YardSurvival).GetField("launchVideo",flags).GetValue(game);
        var camera=(Camera)typeof(YardSurvival).GetField("launchPresentationCamera",flags).GetValue(game);
        var world=(Camera)typeof(YardSurvival).GetField("arenaCamera",flags).GetValue(game);
        string report="";
        long firstFrame=video!=null?video.frame:-1;
        ScreenCapture.CaptureScreenshot("Assets/Verification~/Launch-Fixed.png");
        yield return new WaitForSecondsRealtime(1);
        try
        {
            if(camera==null||!camera.enabled||camera.cullingMask!=0||world.enabled)throw new Exception("Intro presentation/world camera state incorrect");
            if(video==null || !video.isPlaying||video.texture==null||firstFrame<0||video.frame==firstFrame)throw new Exception("Video missing or frames did not advance");
            ScreenCapture.CaptureScreenshot("Assets/Verification~/Launch-Fixed-Later.png");
            report="PASS: intro display camera active, world rendering suspended; "+(video==null?"poster mode":"video frames advance "+firstFrame+" -> "+video.frame)+". ";
        }
        catch(Exception ex){report="FAIL: "+ex;}
        yield return new WaitForSecondsRealtime(1);
        typeof(YardSurvival).GetMethod("StopLaunchSounds",flags).Invoke(game,null);
        report+=world.enabled&&!camera.enabled?"Gameplay camera restored and presentation camera disabled.":"FAIL: camera restoration";
        File.WriteAllText("Assets/Verification~/LaunchVideoResult.txt",report);
        EditorApplication.isPlaying=false;
    }
}
