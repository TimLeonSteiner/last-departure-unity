using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class AudioSingletonReview
{
    const string Request = "Assets/Verification~/AudioSingleton.request";
    const string Stage = "AudioSingletonReview.Pending";
    static AudioSingletonReview() { EditorApplication.update += Tick; }
    [MenuItem("Tools/Yard Survival/Verify audio singleton")]
    public static void Run()
    {
        Directory.CreateDirectory("Assets/Verification~");
        File.WriteAllText(Request, "run");
    }
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Request))
        {
            File.Delete(Request);
            SessionState.SetBool(Stage, true);
            EditorApplication.isPlaying = true;
            return;
        }
        if (!SessionState.GetBool(Stage, false) || !EditorApplication.isPlaying || GameAudioManager.Instance == null) return;
        SessionState.SetBool(Stage, false);
        GameObject duplicate = null;
        try
        {
            var manager = GameAudioManager.Instance;
            var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            Require(game != null, "Game initialized");
            Require(GameAudioManager.GetOrCreate(game.transform) == manager, "Shared instance reused");
            foreach (string name in new[] { "radio", "musicB", "effects" })
            {
                var source = (AudioSource)typeof(YardSurvival).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
                Require(source != null && source.transform.parent == manager.transform, name + " belongs to manager");
            }
            duplicate = new GameObject("Duplicate audio manager test");
            var rejected = duplicate.AddComponent<GameAudioManager>();
            Require(GameAudioManager.Instance == manager && !rejected.enabled, "Duplicate rejected");
            Require(duplicate.GetComponentsInChildren<AudioSource>().Length == 0, "Duplicate creates no audio sources");
            UnityEngine.Object.DestroyImmediate(duplicate);
            Require(GameAudioManager.Instance == manager, "Duplicate destruction preserves instance");
            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            Require(GameAudioManager.Instance == null, "Destruction clears instance");
            var replacement = GameAudioManager.GetOrCreate(game.transform);
            Require(replacement != null && replacement != manager && replacement.Music != null, "Manager can be recreated");
            File.WriteAllText("Assets/Verification~/AudioSingleton.txt", "PASS " + DateTime.Now.ToString("s") + "\nLive game sources wired; shared instance reused; duplicate rejected without sources; duplicate cleanup preserves instance; owner destruction clears instance; recreation succeeds.\n");
        }
        catch (Exception ex) { File.WriteAllText("Assets/Verification~/AudioSingleton.txt", "FAIL " + ex); Debug.LogException(ex); }
        finally { if (duplicate != null) UnityEngine.Object.DestroyImmediate(duplicate); EditorApplication.isPlaying = false; }
    }
    static void Require(bool result, string message) { if (!result) throw new Exception(message); }
}
