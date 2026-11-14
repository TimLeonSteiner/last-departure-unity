using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class RetiredContentReview
{
    const string Request = "Library/RetiredContentReview.request";
    const string Result = "Assets/Verification~/RetiredContentReview.txt";
    static RetiredContentReview() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlaying) { EditorApplication.isPlaying = true; return; }
        if (Time.timeSinceLevelLoad < 2) return;
        File.Delete(Request);
        try
        {
            var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            if (game == null) throw new Exception("No game in active scene");
            var dependencies = AssetDatabase.GetDependencies("Assets/LastDeparture.unity", true);
            if (dependencies.Any(p => p.Contains("LuckyChest") || p.Contains("BlenderKitSlots") || p.Contains("DustlineSlotMachine")))
                throw new Exception("Retired assets remain in scene dependencies");
            var report = "PASS: active scene has no chest or gambling asset dependencies\n";
            report += game.RunProgressionChecks();
            report += game.RunInventoryChecks();
            if (report.Contains("FAIL")) throw new Exception(report);
            File.WriteAllText(Result, report + "\nCOMPLETE " + DateTime.Now.ToString("s"));
        }
        catch (Exception ex) { File.WriteAllText(Result, "FAIL " + ex); Debug.LogException(ex); }
        finally { EditorApplication.isPlaying = false; }
    }
}
