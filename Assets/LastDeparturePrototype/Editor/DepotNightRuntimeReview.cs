using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class DepotNightRuntimeReview
{
    const string Request = "Assets/LastDeparturePrototype/NightRuntime.request";
    const string Review = "Assets/LastDeparturePrototype/NightLightingReview";
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static double at;
    static int stage;
    static YardSurvival game;
    static DepotNightRuntimeReview()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("NightRuntime.Pending", false))
            {
                SessionState.SetBool("NightRuntime.Background", Application.runInBackground);
                Application.runInBackground = true;
                stage = 1; at = EditorApplication.timeSinceStartup + 2;
            }
        };
    }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete(Request);
            SessionState.SetBool("NightRuntime.Pending", true);
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;
            return;
        }
        if (stage == 0 || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup < at) return;
        try
        {
            if (stage == 1)
            {
                game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
                typeof(YardSurvival).GetMethod("PlayFromMenu", Fields).Invoke(game, null);
                typeof(YardSurvival).GetField("remainingToSpawn", Fields).SetValue(game, 0);
                stage = 2; at = EditorApplication.timeSinceStartup + 1;
                return;
            }
            game.enabled = false;
            var camera = (Camera)typeof(YardSurvival).GetField("arenaCamera", Fields).GetValue(game);
            foreach (bool on in new[] { false, true })
            {
                typeof(YardSurvival).GetField("flashlightOn", Fields).SetValue(game, on);
                typeof(YardSurvival).GetMethod("UpdateFlashlight", Fields).Invoke(game, null);
                Capture(camera, on ? "Runtime-Flashlight" : "Runtime-NoFlashlight");
            }
            File.WriteAllText(Review + "/Runtime.txt", DateTime.Now.ToString("s") + "\nCOMPLETE fresh Play: player-camera images with flashlight off/on; ambient=" + game.nightAmbient + ", moon=" + game.moonlightIntensity + ", reflections=" + RenderSettings.reflectionIntensity + ", frame=" + Time.frameCount + ".\n");
        }
        catch (Exception ex) { Directory.CreateDirectory(Review); File.WriteAllText(Review + "/Runtime.txt", "FAIL " + ex); Debug.LogException(ex); }
        stage = 0; SessionState.SetBool("NightRuntime.Pending", false);
        Application.runInBackground = SessionState.GetBool("NightRuntime.Background", false);
        EditorApplication.isPlaying = false;
    }
    static void Capture(Camera camera, string name)
    {
        Directory.CreateDirectory(Review);
        var target = new RenderTexture(1280, 800, 24); var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            target.Create();
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply();
            File.WriteAllBytes(Review + "/" + name + ".png", image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image); }
    }
}
