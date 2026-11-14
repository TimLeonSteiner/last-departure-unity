using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;

public static class RainAudioVerification
{
    public static void VerifyBatch()
    {
        EditorSceneManager.OpenScene("Assets/Boxhead.unity");
        EditorApplication.playModeStateChanged += Verify;
        EditorApplication.isPlaying = true;
    }

    static void Verify(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.delayCall += () =>
        {
            try
            {
                var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
                string report = game.RunRainAudioChecks();
                File.WriteAllText("Assets/BoxheadPrototype/Audio/RainAudioChecks.txt", report + "\n");
                Debug.Log(report);
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        };
    }
}
