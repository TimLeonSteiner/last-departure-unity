using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YardSurvivalPrototype;

public static class WarehouseSetup
{
    [MenuItem("Tools/Yard Survival/Open warehouse game and set startup scene")]
    public static void Open()
    {
        const string gamePath = "Assets/LastDeparture.unity";
        const string reportPath = "Assets/LastDeparturePrototype/WarehouseChecks.txt";
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene edits before opening the warehouse.");
            var scene = EditorSceneManager.OpenScene(gamePath);
            var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            if (game == null) throw new InvalidOperationException("Missing game component.");
            var yard = game.transform.Find("Warehouse yard");
            if (yard != null) UnityEngine.Object.DestroyImmediate(yard.gameObject);
            game.BuildEnvironment();
            game.ConfigureCamera();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            // This is the playable entry point; keep tutorial assets available separately.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(gamePath, true) };
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(gamePath);
            Selection.activeGameObject = game.gameObject;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0, 2, 7), Quaternion.Euler(8, 0, 0), 8);
            AssetDatabase.SaveAssets();
            File.WriteAllText(reportPath, "PASS: active and saved scene " + scene.path + "; build/start scene set; warehouse roof, window openings, four freight footprints and loading lanes created. Runtime/audio testing pending.\n");
        }
        catch (Exception error) { File.WriteAllText(reportPath, "FAIL: " + error.Message + "\n"); Debug.LogException(error); }
    }
}
