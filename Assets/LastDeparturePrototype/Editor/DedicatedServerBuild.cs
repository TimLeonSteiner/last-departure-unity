using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class DedicatedServerBuild
{
    const string CheckKey = "Dustline.DedicatedChecks";
    static DedicatedServerBuild() { EditorApplication.update += PollChecks; }

    public static void CheckBatch()
    {
        EditorSceneManager.OpenScene("Assets/LastDeparture.unity");
        SessionState.SetBool(CheckKey,true);
        EditorApplication.isPlaying = true;
    }

    static void PollChecks()
    {
        if (!SessionState.GetBool(CheckKey,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if (game == null || Time.timeSinceLevelLoad < .5f) return;
        SessionState.SetBool(CheckKey,false);
        try
        {
            string result = game.RunDedicatedChecks();
            File.WriteAllText("Assets/Verification~/DedicatedChecks.txt",result);
            Debug.Log(result); EditorApplication.Exit(0);
        }
        catch(Exception ex)
        {
            File.WriteAllText("Assets/Verification~/DedicatedChecks.txt","FAIL " + ex);
            Debug.LogException(ex); EditorApplication.Exit(1);
        }
    }

    public static void BuildLinux()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneLinux64))
            throw new InvalidOperationException("Install Linux Dedicated Server Build Support for this Unity version.");
        Build(BuildTarget.StandaloneLinux64,Path.Combine(Path.GetTempPath(),"Dustline-PlayFlowServer","DustlineServer.x86_64"),true);
    }

    public static void BuildWindowsQA() => Build(BuildTarget.StandaloneWindows64,Path.Combine(Path.GetTempPath(),"Dustline-DedicatedQA","DustlineQA.exe"),false);

    static void Build(BuildTarget target,string path,bool server)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] {"Assets/LastDeparture.unity"}, target = target,
            subtarget = (int)(server ? StandaloneBuildSubtarget.Server : StandaloneBuildSubtarget.Player),
            locationPathName = path, options = server ? BuildOptions.None : BuildOptions.Development
        });
        if (result.summary.result != BuildResult.Succeeded) throw new Exception("Dedicated build failed: " + result.summary.result);
        File.WriteAllText("Assets/Verification~/DedicatedBuild.txt", "PASS " + Path.GetFullPath(path) + "\n");
    }
}
