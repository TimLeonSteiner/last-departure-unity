using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class LaunchAndBruteReview
{
    const string Request = "Assets/Verification~/LaunchAndBrute.request";
    const string Key = "LaunchAndBrute.Stage";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static double after;
    static LaunchAndBruteReview() { EditorApplication.update += Tick; }
    static object Get(YardSurvival g, string field) => typeof(YardSurvival).GetField(field, Flags).GetValue(g);
    static void Set(YardSurvival g, string field, object value) => typeof(YardSurvival).GetField(field, Flags).SetValue(g, value);
    static void Call(YardSurvival g, string method, params object[] args) => typeof(YardSurvival).GetMethod(method, Flags).Invoke(g, args);
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Request))
        {
            File.Delete(Request); SessionState.SetInt(Key, 1); EditorApplication.isPlaying = true; return;
        }
        int stage = SessionState.GetInt(Key, 0);
        if (stage == 0 || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < after) return;
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if (game == null || GameAudioManager.Instance == null) return;
        try
        {
            if (stage == 1)
            {
                // Simulate the ready clip reaching its black final frame.
                Set(game, "launchStartedAt", Time.unscaledTime - 1f);
                Set(game, "readyVideoEnded", true);
                Call(game, "TickLaunchScreen");
                foreach (string name in new[] { "launchVideo", "readyVideo" })
                    if (((VideoPlayer)Get(game, name))?.isPlaying == true) throw new Exception(name + " still playing during exit");
                Set(game, "launchExitAt", Time.unscaledTime - 1f);
                Call(game, "TickLaunchScreen");
                if (Get(game, "frontScreen").ToString() != "Home") throw new Exception("Did not reach main menu");
                foreach (string name in new[] { "launchSting", "launchTransition", "launchMusic", "titleVoice" })
                    if (((AudioSource)Get(game, name))?.isPlaying == true) throw new Exception(name + " continues in menu");
                int brute = Array.FindIndex(game.zombieModels, p => p != null && p.name == "04-Cargo-Brute");
                if (brute < 0) throw new Exception("Brute not in roster");
                Call(game, "BuildEncounterPreview", brute);
                SessionState.SetInt(Key, 2); after = EditorApplication.timeSinceStartup + 3; return;
            }
            var target = (RenderTexture)Get(game, "encounterPreviewTarget");
            if (target == null) throw new Exception("Brute render missing");
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target; image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply();
                File.WriteAllBytes(stage == 2 ? "Assets/BoxheadPrototype/Resources/Encounters/04-Cargo-Brute.png" : "Assets/Verification~/ShamblerPreview.png", image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
            if (stage == 2)
            {
                Call(game, "BuildEncounterPreview", 0);
                SessionState.SetInt(Key, 3); after = EditorApplication.timeSinceStartup + 2; return;
            }
            Call(game, "ReleaseEncounterPreview");
            File.WriteAllText("Assets/Verification~/LaunchAndBrute.txt", "PASS: ready completion reaches Home; both videos stop during exit; intro audio stops; Brute portrait rendered from current game model.\n");
            SessionState.SetInt(Key, 0); EditorApplication.isPlaying = false; AssetDatabase.Refresh();
        }
        catch (Exception ex)
        {
            File.WriteAllText("Assets/Verification~/LaunchAndBrute.txt", "FAIL " + ex);
            SessionState.SetInt(Key, 0); EditorApplication.isPlaying = false; Debug.LogException(ex);
        }
    }
}
