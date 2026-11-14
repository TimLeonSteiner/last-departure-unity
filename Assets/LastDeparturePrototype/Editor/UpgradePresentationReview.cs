using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class UpgradePresentationReview
{
    static double captureAt;
    static int stage;
    static double readyAt = EditorApplication.timeSinceStartup + 3;
    static UpgradePresentationReview() { EditorApplication.update += Tick; }
    [MenuItem("Tools/Dustline/Review upgrade cards")]
    public static void Run()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        stage = 0;
        readyAt = EditorApplication.timeSinceStartup + 3;
        foreach (string name in new[] { "Damage", "Vitality", "Reload", "Core", "Supplies", "Paper", "AnatomicalCards", "VascularBackdrop" })
        {
            string path = "Assets/LastDeparturePrototype/Resources/UpgradeCards/" + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new Exception("Missing artwork: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        if (Resources.Load<Font>("UpgradeCards/Anton-Regular") == null) throw new Exception("Missing heading font");
        if (EditorSceneManager.GetActiveScene().path != "Assets/LastDeparture.unity") throw new Exception("Open LastDeparture scene before running this review.");
        SessionState.SetBool("Dustline.UpgradePresentationReview", true);
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists("Assets/Verification~/PreviewUpgradeCards"))
        {
            File.Delete("Assets/Verification~/PreviewUpgradeCards");
            SessionState.SetBool("Dustline.UpgradePreviewOnly", true);
            Run();
        }
        if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists("Assets/Verification~/RunUpgradeReview")) { File.Delete("Assets/Verification~/RunUpgradeReview"); Run(); }
        if (!SessionState.GetBool("Dustline.UpgradePresentationReview", false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < readyAt) return;
        try
        {
            var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            if (game == null) return;
            // Focus changes during an editor capture can open the game's pause menu.
            if (stage > 0)
            {
                game.enabled = true;
                var screen = typeof(YardSurvival).GetField("frontScreen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                screen.SetValue(game, Enum.Parse(screen.FieldType, "Game"));
                typeof(YardSurvival).GetField("upgradePending", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(game, true);
                EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Repaint();
                EditorApplication.QueuePlayerLoopUpdate();
                Time.timeScale = 1;
            }
            if (stage == 0)
            {
                if (!SessionState.GetBool("Dustline.UpgradePreviewOnly", false))
                    File.WriteAllText("Assets/Verification~/UpgradePresentationChecks.txt", game.RunCoreObjectiveChecks() + game.RunUpgradeInputChecks());
                SessionState.SetBool("Dustline.UpgradePreviewOnly", false);
                stage = -1; readyAt = EditorApplication.timeSinceStartup + 1;
            }
            else if (stage == -1)
            {
                game.PreviewCoreUpgrades();
                stage = 1; captureAt = EditorApplication.timeSinceStartup + 3;
            }
            else if (EditorApplication.timeSinceStartup >= captureAt)
            {
                if (stage == 1)
                {
                    ScreenCapture.CaptureScreenshot("Assets/Verification~/Upgrade-Dossier-Primary.png");
                    stage = 2; captureAt = EditorApplication.timeSinceStartup + 2;
                }
                else if (stage == 2)
                {
                    var offers = (int[])typeof(YardSurvival).GetField("coreOffers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(game);
                    offers[0] = 2; offers[1] = 4; offers[2] = 3;
                    stage = 3; captureAt = EditorApplication.timeSinceStartup + 2;
                }
                else if (stage == 3)
                {
                    ScreenCapture.CaptureScreenshot("Assets/Verification~/Upgrade-Dossier-Other.png");
                    stage = 4; captureAt = EditorApplication.timeSinceStartup + 3;
                }
                else { SessionState.SetBool("Dustline.UpgradePresentationReview", false); if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.isPlaying = false; }
            }
        }
        catch (Exception e)
        {
            File.WriteAllText("Assets/Verification~/UpgradePresentationChecks.txt", "FAIL " + e);
            Debug.LogException(e); SessionState.SetBool("Dustline.UpgradePresentationReview", false); if (Application.isBatchMode) EditorApplication.Exit(1); else EditorApplication.isPlaying = false;
        }
    }
}




