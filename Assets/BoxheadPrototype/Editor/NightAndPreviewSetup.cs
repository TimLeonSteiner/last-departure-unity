using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class NightAndPreviewSetup
{
    const string Folder = "Assets/BoxheadPrototype/Prefabs";
    const string ReportPath = "Assets/BoxheadPrototype/NightAndPreviewChecks.txt";
    static NightAndPreviewSetup() { EditorApplication.playModeStateChanged += VerifyEditorState; }

    public static void VerifyInEditor()
    {
        SessionState.SetBool("Night.RestorePlay", EditorApplication.isPlaying);
        SessionState.SetBool("Night.WaitEdit", EditorApplication.isPlaying);
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        else ApplyAndPlay();
    }
    static void ApplyAndPlay()
    {
        try { Apply(); SessionState.SetBool("Night.Pending", true); EditorApplication.isPlaying = true; }
        catch (Exception ex) { File.WriteAllText(ReportPath, "FAIL: " + ex); Debug.LogException(ex); }
    }
    static void VerifyEditorState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("Night.WaitEdit", false))
        { SessionState.SetBool("Night.WaitEdit", false); ApplyAndPlay(); }
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("Night.Pending", false)) return;
        SessionState.SetBool("Night.Pending", false);
        EditorApplication.delayCall += () =>
        {
            try
            {
                var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
                string result = game.RunNightAndPreviewChecks();
                game.CaptureNightReview();
                try { result += "\n" + game.RunSmokeChecks(); }
                catch (Exception smoke) { result += "\nBROADER SMOKE CHECK: " + smoke.Message; }
                 File.WriteAllText(ReportPath, result); Debug.Log(result);
            }
            catch (Exception ex) { File.WriteAllText(ReportPath, "FAIL: " + ex); Debug.LogException(ex); }
            if (!SessionState.GetBool("Night.RestorePlay", false)) EditorApplication.isPlaying = false;
        };
    }

    [MenuItem("Tools/Yard Survival/Apply night and defense assets")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Boxhead.unity")
        {
            if (scene.isDirty) throw new Exception("Save the current scene first.");
            scene = EditorSceneManager.OpenScene("Assets/Boxhead.unity");
        }
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if (game == null) throw new Exception("Survival component missing.");
        ConfigureDefenseAssets(game);
        game.nightAmbient = DepotNightLighting.Ambient; game.moonlightIntensity = DepotNightLighting.Moonlight; game.flashlightStartsOn = true;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/BoxheadPrototype/Textures/CorpseSkin.png");
        if (texture == null) throw new Exception("Corpse skin texture missing.");
        foreach (string path in Directory.GetFiles("Assets/BoxheadPrototype/Materials", "Zombie_*.mat"))
        {
            if (!path.Contains("Skin") && !path.Contains("Face")) continue;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            material.SetTexture("_BaseMap", texture); material.mainTextureScale = Vector2.one;
            material.color = new Color(.72f, .76f, .67f); EditorUtility.SetDirty(material);
        }
        DepotNightLighting.Atmosphere(game);
        DepotNightLighting.Warehouse(game, game.transform.Find("Warehouse yard/Warehouse hall"));
        EditorUtility.SetDirty(game); EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }

    public static void ConfigureDefenseAssets(YardSurvival game)
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/BoxheadPrototype", "Prefabs");
        GameObject oldWall = game.barricadeModel, oldTurret = game.turretModel, oldTrap = game.trapModel;
        var parent = new GameObject("Defense asset authoring").transform;
        try
        {
            game.barricadeModel = game.turretModel = game.trapModel = null;
            var wall = DepotModels.Barricade(game, parent, Vector3.zero, false);
            Transform head; var turret = DepotModels.Turret(game, parent, Vector3.zero, out head);
            var trap = DepotModels.SlowTrap(game, parent, Vector3.zero);
            var wallAsset = PrefabUtility.SaveAsPrefabAsset(wall.gameObject, Folder + "/SteelBarricade.prefab");
            var turretAsset = PrefabUtility.SaveAsPrefabAsset(turret.gameObject, Folder + "/TripodSentry.prefab");
            var trapAsset = PrefabUtility.SaveAsPrefabAsset(trap.gameObject, Folder + "/InductionTrap.prefab");
            // Preserve any separately authored user replacement.
            game.barricadeModel = oldWall != null && !AssetDatabase.GetAssetPath(oldWall).StartsWith(Folder + "/") ? oldWall : wallAsset;
            game.turretModel = oldTurret != null && !AssetDatabase.GetAssetPath(oldTurret).StartsWith(Folder + "/") ? oldTurret : turretAsset;
            game.trapModel = oldTrap != null && !AssetDatabase.GetAssetPath(oldTrap).StartsWith(Folder + "/") ? oldTrap : trapAsset;
        }
        catch { game.barricadeModel = oldWall; game.turretModel = oldTurret; game.trapModel = oldTrap; throw; }
        finally { UnityEngine.Object.DestroyImmediate(parent.gameObject); }
    }

    public static void VerifyBatch()
    {
        Apply(); EditorApplication.playModeStateChanged += EnteredPlay;
        EditorApplication.isPlaying = true;
    }
    static void EnteredPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.delayCall += () =>
        {
            try
            {
                var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
                string result = game.RunNightAndPreviewChecks();
                game.CaptureNightReview();
                try { result += "\n" + game.RunSmokeChecks(); }
                catch (Exception smoke) { result += "\nBROADER SMOKE CHECK: " + smoke.Message; }
                 File.WriteAllText(ReportPath, result); Debug.Log(result); EditorApplication.Exit(0);
            }
            catch (Exception ex) { File.WriteAllText(ReportPath, "FAIL: " + ex); Debug.LogException(ex); EditorApplication.Exit(1); }
        };
    }
}
