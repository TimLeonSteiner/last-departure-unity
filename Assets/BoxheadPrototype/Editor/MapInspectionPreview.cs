using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class MapInspectionPreview
{
    const string Pending = "MapInspection.Pending";
    const string Request = "Library/MapInspection.request";
    static MapInspectionPreview()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        File.Delete(Request);
        Start();
    }
    [MenuItem("Tools/Map Inspection/Explore without zombies")]
    public static void Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Boxhead.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        var game = Object.FindFirstObjectByType<YardSurvival>();
        if (game == null) { Debug.LogError("Map inspection: YardSurvival missing in scene."); EditorApplication.isPlaying = false; return; }
        game.BeginMapInspection();
        EditorApplication.ExecuteMenuItem("Window/General/Game");
    }
}
