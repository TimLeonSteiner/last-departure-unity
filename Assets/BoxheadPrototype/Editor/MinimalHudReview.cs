using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class MinimalHudReview
{
    const string Root = "Assets/Verification~/";
    static int stage;
    static double at;
    static MinimalHudReview() { EditorApplication.update += Tick; }
    static void Set(YardSurvival game, string name, object value) => typeof(YardSurvival).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, value);
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Root + "RunMinimalHudReview"))
        {
            File.Delete(Root + "RunMinimalHudReview");
            SessionState.SetBool("MinimalHudReview", true);
            stage = 0; at = EditorApplication.timeSinceStartup + 3;
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;
        }
        if (!SessionState.GetBool("MinimalHudReview", false) || !EditorApplication.isPlaying) return;
        try
        {
            var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            if (game == null) return;
            if (stage > 0)
            {
                var field = typeof(YardSurvival).GetField("frontScreen", BindingFlags.Instance | BindingFlags.NonPublic);
                field.SetValue(game, Enum.Parse(field.FieldType, "Game"));
                Time.timeScale = 0;
                EditorApplication.QueuePlayerLoopUpdate();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
            if (EditorApplication.timeSinceStartup < at) return;
            if (stage == 0)
            {
                File.WriteAllText(Root + "MinimalHudChecks.txt", game.RunInventoryChecks());
                game.PrepareInventoryHudReview(false);
                PrepareHudState(game,12,9,28,.68f);
            }
            else if (stage == 1) ScreenCapture.CaptureScreenshot(Root + "MinimalHud-Gameplay.png");
            else if (stage == 2)
            {
                game.PrepareInventoryHudReview(true);
                PrepareHudState(game,0,0,120,.9f);
                Set(game, "health", 24);
                Set(game, "reloading", true);
            }
            else if (stage == 3) ScreenCapture.CaptureScreenshot(Root + "MinimalHud-FullInventory.png");
            else
            {
                Time.timeScale = 1;
                SessionState.SetBool("MinimalHudReview", false);
                EditorApplication.isPlaying = false;
                return;
            }
            stage++; at = EditorApplication.timeSinceStartup + 3;
        }
        catch (Exception e)
        {
            File.WriteAllText(Root + "MinimalHudChecks.txt", "FAIL " + e);
            SessionState.SetBool("MinimalHudReview", false);
            Time.timeScale = 1;
            Debug.LogException(e);
        }
    }
    static void PrepareHudState(YardSurvival game,int weapon,int ammo,int reserve,float drowsiness)
    {
        typeof(YardSurvival).GetMethod("EquipWeapon",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{weapon,ammo,reserve});
        Set(game,"coreHealth",420);Set(game,"wave",4);
        var sleep=(DrowsinessState)typeof(YardSurvival).GetField("sleepState",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
        sleep.Reset(drowsiness);sleep.Collect(2);
    }
}
