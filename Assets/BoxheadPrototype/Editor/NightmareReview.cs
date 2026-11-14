using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class NightmareReview
{
    static int captureStage;
    static double captureAt;
    static string pendingReport;
    static bool visualOnly;
    static NightmareReview() { EditorApplication.update += Tick; }
    [MenuItem("Tools/Dustline/Review nightmare gameplay")]
    public static void Run()
    {
        AssetDatabase.ImportAsset("Assets/BoxheadPrototype/Resources/NightmareSilhouette.shader", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        if (!EditorApplication.isPlaying && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Boxhead.unity")
            EditorSceneManager.OpenScene("Assets/Boxhead.unity");
        SessionState.SetBool("Dustline.NightmareReview", true);
        SessionState.SetBool("Dustline.NightmareVisualOnly", visualOnly);
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (captureStage > 0)
        {
            if (EditorApplication.timeSinceStartup < captureAt) return;
            FinishCapture(); return;
        }
        const string request = "Assets/Verification~/RunNightmareChecks.request";
        if (File.Exists(request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        { visualOnly = File.ReadAllText(request).Trim() == "visual"; File.Delete(request); Run(); }
        if (!SessionState.GetBool("Dustline.NightmareReview", false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || Time.unscaledTime < 2) return;
        SessionState.SetBool("Dustline.NightmareReview", false);
        visualOnly = SessionState.GetBool("Dustline.NightmareVisualOnly", false);
        try
        {
            var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            if (game == null) throw new Exception("Game scene did not load");
            string report = "PASS Nightmare shader compiled and awake/drowsy previews rendered\n";
            if (!visualOnly)
            {
                report = game.RunDrowsinessChecks();
                File.WriteAllText("Assets/Verification~/DrowsinessChecks.txt",report);
                report += game.RunCoreObjectiveChecks();
                report += game.RunAnnouncerChecks();
            }
            pendingReport = report;
            game.PreviewNightmare(false); captureStage = 1; captureAt = EditorApplication.timeSinceStartup + 1;
        }
        catch (Exception error)
        {
            File.WriteAllText("Assets/Verification~/NightmareChecks.txt", "FAIL " + error);
            Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
    static void FinishCapture()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        try
        {
            SpaceportAssetBuild.Capture(new Vector3(0, 1.7f, -10), new Vector3(0, 1.1f, -3), captureStage == 1 ? "Nightmare-Drowsy" : "Nightmare-Awake");
            if (captureStage == 1)
            { game.PreviewNightmare(true); captureStage = 2; captureAt = EditorApplication.timeSinceStartup + 1; return; }
            captureStage = 0;
            if (ShaderUtil.ShaderHasError(Resources.Load<Shader>("NightmareSilhouette"))) throw new Exception("Nightmare shader compilation failed");
            File.WriteAllText(visualOnly ? "Assets/Verification~/NightmareVisualChecks.txt" : "Assets/Verification~/NightmareChecks.txt", pendingReport);
            game.EndNightmarePreview();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            captureStage = 0; if (game != null) game.EndNightmarePreview();
            File.WriteAllText("Assets/Verification~/NightmareChecks.txt", "FAIL " + error); Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
