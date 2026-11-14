using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class InventoryReview
{
    const string Root = "Assets/BoxheadPrototype/";
    static int stage;
    static double at;
    static YardSurvival game;
    static InventoryReview() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !EditorApplication.isPlaying) return;
        try
        {
            if (File.Exists(Root + "InventoryChecks.request"))
            {
                File.Delete(Root + "InventoryChecks.request");
                game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
                File.WriteAllText(Root + "InventoryChecks.txt", game.RunInventoryChecks()+"\n"+game.RunPeerInventoryChecks());
            }
            if (File.Exists(Root + "InventoryReview.request") && stage == 0)
            {
                File.Delete(Root + "InventoryReview.request");
                game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
                game.PrepareInventoryHudReview(false);
                Directory.CreateDirectory(Root + "InventoryReview");
                stage = 1; at = EditorApplication.timeSinceStartup + 2;
            }
            if (stage == 0) return;
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            if (EditorApplication.timeSinceStartup < at) return;
            if (stage == 1) ScreenCapture.CaptureScreenshot(Root + "InventoryReview/FourWeapons.png");
            if (stage == 2) game.PrepareInventoryHudReview(true);
            if (stage == 3) ScreenCapture.CaptureScreenshot(Root + "InventoryReview/AllWeapons.png");
            if (stage == 4) { game.ResetRun(); stage = 0; return; }
            stage++; at = EditorApplication.timeSinceStartup + 2;
        }
        catch (Exception ex) { stage = 0; File.WriteAllText(Root + "InventoryChecks.txt", "FAIL " + ex); Debug.LogException(ex); }
    }
}
