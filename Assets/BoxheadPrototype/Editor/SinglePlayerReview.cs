using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class SinglePlayerReview
{
    const string Pending = "LastDeparture.SinglePlayerReview";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static SinglePlayerReview() { EditorApplication.update += Poll; }

    public static void RunBatch()
    {
        EditorSceneManager.OpenScene("Assets/Boxhead.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    static void Poll()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if (game == null || Time.timeSinceLevelLoad < 2) return;
        SessionState.SetBool(Pending, false);
        var report = new StringBuilder();
        object Read(string name) => typeof(YardSurvival).GetField(name, Flags).GetValue(game);
        object Call(string name, params object[] args) => typeof(YardSurvival).GetMethod(name, Flags).Invoke(game, args);
        void Check(bool pass, string label)
        {
            report.AppendLine((pass ? "PASS " : "FAIL ") + label);
            if (!pass) throw new Exception(label);
        }
        try
        {
            game.enabled = false;
            Call("ShowHome");
            Check(Read("frontScreen").ToString() == "Home", "Main menu opens");
            Call("PlayFromMenu");
            Check(Read("frontScreen").ToString() == "Game" && (int)Read("wave") == 1, "Play starts solo wave one");
            int ammo = (int)Read("ammunition");
            Call("Fire", Vector3.up);
            Check((int)Read("ammunition") == ammo - 1, "Solo firing spends ammunition");
            Call("BeginReload");
            Call("UpdateReload", 30f);
            Check(!(bool)Read("reloading") && (int)Read("ammunition") == ammo, "Solo reload completes");
            Call("PauseFromGame");
            Check(Time.timeScale == 0 && Read("frontScreen").ToString() == "Pause", "Pause freezes solo simulation");
            Call("ResumeFromMenu");
            Check(Time.timeScale == 1 && Read("frontScreen").ToString() == "Game", "Resume restores gameplay");
            Call("PlayFromMenu");
            Check((int)Read("wave") == 1 && (int)Read("ammunition") == ammo, "Restart resets wave and ammunition");
            foreach (string method in new[] { "HostCoop", "JoinCoop", "StartDedicatedServer", "ConnectDedicatedServer" })
            {
                bool rejected = false;
                try { Call(method, method == "ConnectDedicatedServer" ? new object[] { "ws://127.0.0.1:7777", "" } : Array.Empty<object>()); }
                catch (TargetInvocationException ex) when (ex.InnerException is NotSupportedException) { rejected = true; }
                Check(rejected, method + " rejects legacy network requests");
            }
            Check(Read("coop") == null && UnityEngine.Object.FindAnyObjectByType<NetworkManager>() == null, "No network manager created");
            Call("ShowHome");
            Check(Read("frontScreen").ToString() == "Home" && Time.timeScale == 1, "Return to menu resets pause state");
            File.WriteAllText("Assets/Verification~/SinglePlayer/Results.txt", report.ToString());
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            report.AppendLine("FAIL " + (ex.InnerException ?? ex));
            File.WriteAllText("Assets/Verification~/SinglePlayer/Results.txt", report.ToString());
            EditorApplication.Exit(1);
        }
    }
}
