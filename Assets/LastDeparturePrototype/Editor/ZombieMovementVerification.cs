using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class ZombieMovementVerification
{
    static ZombieMovementVerification() { EditorApplication.playModeStateChanged += OnPlay; }
    [MenuItem("Tools/Yard Survival/Verify zombie movement")]
    public static void Verify()
    {
        if (EditorApplication.isPlaying) Run();
        else { SessionState.SetBool("ZombieMovement.Pending", true); EditorApplication.isPlaying = true; }
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("ZombieMovement.Pending", false)) return;
        SessionState.SetBool("ZombieMovement.Pending", false);
        EditorApplication.delayCall += () => { Run(); EditorApplication.isPlaying = false; };
    }
    static void Run()
    {
        string result;
        try { result = UnityEngine.Object.FindAnyObjectByType<YardSurvival>().RunZombieMovementChecks(); }
        catch (Exception ex) { result = "FAIL: " + ex; Debug.LogException(ex); }
        File.WriteAllText("Assets/LastDeparturePrototype/ZombieMovementChecks.txt", result);
        Debug.Log(result);
    }
}
