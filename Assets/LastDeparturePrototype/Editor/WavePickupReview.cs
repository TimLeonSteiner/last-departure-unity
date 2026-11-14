using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class WavePickupReview
{
    const string Pending = "Dustline.WavePickupReview";
    static WavePickupReview() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        const string request = "Assets/Verification~/RunWavePickupChecks.request";
        if (File.Exists(request))
        {
            File.Delete(request); SessionState.SetBool(Pending,true);
            if (!EditorApplication.isPlaying) { EditorApplication.isPlaying = true; return; }
        }
        if (!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || Time.timeSinceLevelLoad < 2) return;
        SessionState.SetBool(Pending,false);
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if (game == null) return;
        bool wasEnabled = game.enabled;
        try
        {
            File.WriteAllText("Assets/Verification~/WavePickupChecks.txt",game.RunProgressionChecks());
        }
        catch (Exception error)
        {
            File.WriteAllText("Assets/Verification~/WavePickupChecks.txt","FAIL " + error);
            Debug.LogException(error);
        }
        finally { game.enabled = wasEnabled; EditorApplication.isPlaying = false; }
    }
}
