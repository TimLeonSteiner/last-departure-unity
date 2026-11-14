using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class WindowsReleaseBuild
{
    const string Request = "Assets/BoxheadPrototype/WindowsRelease.request";
    const string Pending = "LastDeparture.WindowsRelease.Pending";
    const string OutputKey = "LastDeparture.WindowsRelease.Output";
    const string ReportPath = "Assets/Verification~/WindowsRelease.txt";
    static double nextPoll;

    static WindowsReleaseBuild() { EditorApplication.update += Poll; }

    [MenuItem("Tools/Yard Survival/Build Windows release")]
    public static void RequestBuild()
    {
        Directory.CreateDirectory("Assets/Verification~");
        SessionState.SetBool(Pending, true);
        File.WriteAllText(ReportPath, "Preparing Windows release\n");
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + 1;
        if (File.Exists(Request))
        {
            string request = File.ReadAllText(Request).Trim(); File.Delete(Request);
            SessionState.SetString(OutputKey, request == "hotbar-update" ? "LastDeparture-Windows-HotbarUpdate" : request == "full-roof" ? "LastDeparture-Windows-FullRoof" : request == "roof-update" ? "LastDeparture-Windows-RoofUpdate" : "LastDeparture-Windows");
            RequestBuild();
        }
        if (!SessionState.GetBool(Pending, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return; }
        try
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                File.WriteAllText(ReportPath, "Switching to Windows 64-bit\n");
                if (!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                    throw new InvalidOperationException("Could not activate Windows build support.");
                return;
            }
            SessionState.SetBool(Pending, false);
            string output = SessionState.GetString(OutputKey, "LastDeparture-Windows");
            SessionState.EraseString(OutputKey);
            BuildTo(output);
        }
        catch (Exception ex)
        {
            SessionState.SetBool(Pending, false);
            File.WriteAllText(ReportPath, "FAIL " + ex);
            Debug.LogException(ex);
        }
    }

    public static void Build()
        => BuildTo("LastDeparture-Windows");

    static void BuildTo(string output)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before building.");
        string folder = Path.GetFullPath(Path.Combine("Builds", output));
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory("Assets/Verification~");
        File.WriteAllText(ReportPath, "Building Windows release\n");
        string oldName = PlayerSettings.productName;
        var oldMode = PlayerSettings.fullScreenMode;
        int oldWidth = PlayerSettings.defaultScreenWidth, oldHeight = PlayerSettings.defaultScreenHeight;
        bool oldResizable = PlayerSettings.resizableWindow;
        bool oldNativeResolution = PlayerSettings.defaultIsNativeResolution;
        BuildReport report;
        try
        {
            PlayerSettings.productName = "Last Departure";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Boxhead.unity" },
                locationPathName = Path.Combine(folder, "Last Departure.exe"),
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.None
            });
        }
        finally
        {
            PlayerSettings.productName = oldName;
            PlayerSettings.fullScreenMode = oldMode;
            PlayerSettings.defaultScreenWidth = oldWidth;
            PlayerSettings.defaultScreenHeight = oldHeight;
            PlayerSettings.resizableWindow = oldResizable;
            PlayerSettings.defaultIsNativeResolution = oldNativeResolution;
        }
        if (report == null) throw new Exception("Unity did not produce a build report. See the Editor log for details.");
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Windows build " + report.summary.result + "; errors=" + report.summary.totalErrors);
        File.Copy("Assets/BoxheadPrototype/ThirdParty/AnimatedArsenal/CREDITS.txt", Path.Combine(folder, "weapon-credits.txt"), true);
        File.WriteAllText(ReportPath, "PASS " + DateTime.Now.ToString("s") + "\n" + folder + "\nBytes: " + report.summary.totalSize + "\nWarnings: " + report.summary.totalWarnings + "\nErrors: " + report.summary.totalErrors + "\n");
        foreach (var step in report.steps)
            foreach (var message in step.messages)
                if (message.type == LogType.Error || message.type == LogType.Exception)
                    File.AppendAllText(ReportPath, step.name + ": " + message.content + "\n");
        Debug.Log("Windows release ready: " + folder);
    }
}
