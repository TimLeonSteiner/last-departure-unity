using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class LaunchAudioDiagnostics
{
    static double watchUntil;
    static bool previousBackground;
    static readonly System.Collections.Generic.Dictionary<string,float> peaks=new System.Collections.Generic.Dictionary<string,float>();
    const string Request = "Assets/Verification~/LaunchAudio.request";
    static LaunchAudioDiagnostics() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if(watchUntil>0)
        {
            foreach(var source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if(source.clip==null || (!source.name.Contains("Launch")&&!source.name.Contains("title voice")))continue;
                var samples=new float[1024];source.GetOutputData(samples,0);float energy=0;foreach(float value in samples)energy+=value*value;
                float rms=Mathf.Sqrt(energy/samples.Length);string key=source.clip.name;
                if(!peaks.ContainsKey(key)||rms>peaks[key])peaks[key]=rms;
            }
            if(EditorApplication.timeSinceStartup>=watchUntil)
            {
                var output=new StringBuilder();foreach(var peak in peaks)output.AppendLine(peak.Key+" peak output RMS="+peak.Value);
                File.WriteAllText("Assets/Verification~/LaunchAudioOutput.txt",output.ToString());
                Application.runInBackground=previousBackground;watchUntil=0;
            }
        }
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string command=File.ReadAllText(Request).Trim();
        File.Delete(Request);
        if(command=="watch")
        {
            previousBackground=Application.runInBackground;Application.runInBackground=true;
            peaks.Clear();watchUntil=EditorApplication.timeSinceStartup+20;
        }
        if(command=="enable")
        {
            PlayerPrefs.SetInt("Dustline.SoundEnabled",1);
            PlayerPrefs.Save();
            EditorApplication.isPaused=false;
        }
        var report = new StringBuilder();
        report.AppendLine($"Playing={EditorApplication.isPlaying} Paused={EditorApplication.isPaused} EditorMuted={EditorUtility.audioMasterMute} ListenerVolume={AudioListener.volume} ListenerPaused={AudioListener.pause}");
        var game = Object.FindAnyObjectByType<YardSurvival>();
        if(command=="enable" && game!=null && !(bool)typeof(YardSurvival).GetField("soundEnabled",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game))
            game.ToggleSound();
        foreach (string field in new[]{"frontScreen","musicEnabled","soundEnabled","musicVolume","soundVolume","audioUnlocked","titleVoicePlayed","launchStartedAt"})
            report.AppendLine(field+"="+(game == null ? "no game" : typeof(YardSurvival).GetField(field, BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(game)));
        foreach(var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            report.AppendLine($"Listener {listener.name}: enabled={listener.enabled} active={listener.gameObject.activeInHierarchy}");
        foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if(source.name.Contains("Launch") || source.name.Contains("title voice"))
            {
                report.AppendLine($"Source {source.name}: clip={source.clip?.name} state={source.clip?.loadState} playing={source.isPlaying} enabled={source.enabled} active={source.gameObject.activeInHierarchy} mute={source.mute} volume={source.volume} samples={source.timeSamples}");
                var samples=new float[1024];source.GetOutputData(samples,0);float energy=0;foreach(float sample in samples)energy+=sample*sample;
                report.AppendLine("  output RMS="+Mathf.Sqrt(energy/samples.Length));
            }
        foreach(string path in new[]{"Launch/TitleMusic","Announcer/last_departure","Announcer/launch_sting","Announcer/launch_transition"})
        {
            var clip=Resources.Load<AudioClip>(path);
            report.AppendLine($"Resource {path}: {(clip==null?"MISSING":clip.length+" seconds / "+clip.loadState)}");
        }
        File.WriteAllText("Assets/Verification~/LaunchAudioDiagnostics.txt",report.ToString());
    }
}
