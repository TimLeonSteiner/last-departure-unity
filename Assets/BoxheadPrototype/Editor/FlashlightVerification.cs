using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class FlashlightVerification
{
    const string Report = "Assets/BoxheadPrototype/FlashlightChecks.txt";
    static FlashlightVerification() { EditorApplication.playModeStateChanged += OnPlayState; }

    [MenuItem("Tools/Yard Survival/Verify flashlight environment lighting")]
    public static void Verify()
    {
        SessionState.SetBool("Flashlight.RestorePlay", EditorApplication.isPlaying);
        if (EditorApplication.isPlaying) RunCheck();
        else StartCheck();
    }
    static void StartCheck()
    {
        AssetDatabase.Refresh();
        SessionState.SetBool("Flashlight.Pending", true);
        EditorApplication.isPlaying = true;
    }
    static void OnPlayState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("Flashlight.Pending", false)) return;
        SessionState.SetBool("Flashlight.Pending", false);
        EditorApplication.delayCall += RunCheck;
    }
    static void RunCheck()
    {
            try
            {
                var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
                string result = game.RunFlashlightRenderChecks();
                File.WriteAllText(Report, result); Debug.Log(result);
            }
            catch (Exception ex) { File.WriteAllText(Report, "FAIL: " + ex); Debug.LogException(ex); }
            if (!SessionState.GetBool("Flashlight.RestorePlay", false)) EditorApplication.isPlaying = false;
    }
}
