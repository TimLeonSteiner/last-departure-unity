using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class GraphicsPresetVerification
{
    static GraphicsPresetVerification() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!SessionState.GetBool("GraphicsPresetVerification.Pending",false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if (game == null) return;
        SessionState.SetBool("GraphicsPresetVerification.Pending",false);
        StartVerification();
    }
    public static void VerifyBatch()
    {
        EditorSceneManager.OpenScene("Assets/Boxhead.unity");
        SessionState.SetBool("GraphicsPresetVerification.Pending",true);
        EditorApplication.isPlaying = true;
    }
    static void StartVerification()
    {
        Debug.Log("Starting graphics preset verification");
        UnityEngine.Object.FindAnyObjectByType<YardSurvival>().StartCoroutine(Verify());
    }
    static IEnumerator Verify()
    {
        yield return null;
        try
        {
            var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            var apply = typeof(YardSurvival).GetMethod("ApplyGraphicsPreset", BindingFlags.Instance | BindingFlags.NonPublic);
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline == null) throw new Exception("Missing runtime URP pipeline");
            var sceneCamera = (Camera)typeof(YardSurvival).GetField("arenaCamera",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
            if(sceneCamera.enabled) throw new Exception("Hidden scene is rendering during intro");
            var ready = (UnityEngine.Video.VideoPlayer)typeof(YardSurvival).GetField("readyVideo",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
            if(ready != null && (ready.isPlaying || ready.isPrepared)) throw new Exception("Transition decoder started before Ready");
            typeof(YardSurvival).GetMethod("StopLaunchSounds",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
            if(!sceneCamera.enabled) throw new Exception("Intro exit did not restore scene rendering");
            var camera = sceneCamera.GetUniversalAdditionalCameraData();
            apply.Invoke(game, new object[]{0,false});
            if(!Mathf.Approximately(pipeline.renderScale,YardSurvival.GraphicsScale(0,Screen.width,Screen.height)) || pipeline.shadowDistance != 0 || camera.renderPostProcessing || camera.renderShadows)
                throw new Exception("Low preset failed");
            apply.Invoke(game, new object[]{2,false});
            if(!Mathf.Approximately(pipeline.renderScale,YardSurvival.GraphicsScale(2,Screen.width,Screen.height)) || !camera.renderPostProcessing || !camera.renderShadows)
                throw new Exception("High preset did not restore rendering");
            apply.Invoke(game, new object[]{1,false});
            if(!Mathf.Approximately(pipeline.renderScale,YardSurvival.GraphicsScale(1,Screen.width,Screen.height))) throw new Exception("Medium preset failed");
            apply.Invoke(game, new object[]{0,false});
            foreach(var rain in game.GetComponentsInChildren<ParticleSystem>())
                if(rain.name == "Slanted rainfall" && rain.collision.enabled) throw new Exception("Low rain collision still enabled");
            var front = typeof(YardSurvival).GetField("frontScreen",BindingFlags.Instance|BindingFlags.NonPublic);
            front.SetValue(game, Enum.Parse(front.FieldType,"Options"));
            File.WriteAllText("Assets/Verification~/GraphicsPresets.txt","PASS: intro suspends/restores scene rendering; transition decoder deferred; Low/High/Medium switching, resolution, shadows, post-processing, rain collisions.\n"+game.RunRainAudioChecks());
        }
        catch(Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); yield break; }
        yield return null;
        ScreenCapture.CaptureScreenshot("Assets/Verification~/GraphicsOptions.png");
        yield return new WaitForSecondsRealtime(2);
        EditorApplication.Exit(0);
    }
}
