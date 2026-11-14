using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class AnimatedArsenalAudioVerification
{
    const string Pending = "ArsenalAudioVerification.Pending";
    const string Restore = "ArsenalAudioVerification.RestorePlay";
    static double verifyAt, loadDeadline;
    static AnimatedArsenalAudioVerification()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Poll;
    }
    public static void Begin()
    {
        SessionState.SetBool(Restore, EditorApplication.isPlaying);
        SessionState.SetBool(Pending, true);loadDeadline=EditorApplication.timeSinceStartup+15;
        if (EditorApplication.isPlaying) verifyAt = EditorApplication.timeSinceStartup + 1;
        else EditorApplication.isPlaying = true;
    }
    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
            verifyAt = EditorApplication.timeSinceStartup + 1;
    }
    static void Poll()
    {
        if (verifyAt == 0 || EditorApplication.timeSinceStartup < verifyAt || !EditorApplication.isPlaying) return;
        verifyAt = 0;
        try
        {
            var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            if (game == null) throw new Exception("LastDeparture runtime missing");
            var allClips=game.weaponSounds.Where(b=>b!=null).SelectMany(b=>new[]{b.shot,b.reload,b.action}.Concat(b.shotVariations??Array.Empty<AudioClip>())).Concat(new[]{game.turretShotSound,game.minigunStart,game.minigunStop,game.grenadeExplosion,game.pitchforkImpact,game.knifeImpact,game.crossbowImpact,game.crossbowFleshImpact}).Concat(game.jumpVariations).Concat(game.landingVariations).Where(c=>c!=null).Distinct().ToArray();
            foreach(var clip in allClips)if(clip.loadState==AudioDataLoadState.Unloaded&&!clip.LoadAudioData())throw new Exception("LoadAudioData rejected "+clip.name+" state="+clip.loadState+" channels="+clip.channels+" frequency="+clip.frequency);
            if(allClips.Any(c=>c.loadState==AudioDataLoadState.Loading)){if(loadDeadline==0)loadDeadline=EditorApplication.timeSinceStartup+15;if(EditorApplication.timeSinceStartup<loadDeadline){verifyAt=EditorApplication.timeSinceStartup+.2;return;}throw new Exception("Audio load timeout: "+string.Join("; ",allClips.Where(c=>c.loadState!=AudioDataLoadState.Loaded).Select(c=>c.name+" "+c.loadState+" channels="+c.channels+" frequency="+c.frequency)));}
            string report = game.RunWeaponAudioChecks();
            int decoded = 0;
            Action<AudioClip> validate = clip =>
            {
                if (clip == null || !AssetDatabase.GetAssetPath(clip).StartsWith("Assets/LastDeparturePrototype/Audio/AnimatedArsenalAudio/"))
                    throw new Exception("Missing clip or old audio assignment: " + (clip == null ? "null" : clip.name));
                // WebGL serializes imported audio at 44100 Hz; source WAVs are separately checked at 48000 Hz.
                bool validRate = clip.frequency == 48000 || (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL && clip.frequency == 44100);
                if (clip.loadState != AudioDataLoadState.Loaded || clip.channels != 1 || !validRate)
                    throw new Exception("Invalid loaded mono audio: " + clip.name + " state=" + clip.loadState + " channels=" + clip.channels + " rate=" + clip.frequency);
                var pcm = new float[clip.samples];
                if (!clip.GetData(pcm, 0) || !pcm.Any(v => Mathf.Abs(v) > .001f)) throw new Exception("Silent/unreadable clip: " + clip.name);
                if (pcm.Any(v => float.IsNaN(v) || float.IsInfinity(v) || Mathf.Abs(v) >= .999f)) throw new Exception("Invalid/clipped PCM: " + clip.name);
                decoded++;
            };
            for (int i = 0; i < 17; i++)
            {
                if (!WeaponCatalog.IsAvailable(i)) continue;
                var bank = game.weaponSounds[i];
                validate(bank.shot); validate(bank.action);
                if (!WeaponCatalog.IsMelee(i)) validate(bank.reload);
                foreach (var clip in bank.shotVariations) validate(clip);
                bool closes = i == 1 || i == 4 || i == 6 || i == 7;
                if (bank.reloadIncludesAction != closes) throw new Exception("Duplicate reload action policy: " + bank.weapon);
            }
            foreach (var clip in new[] { game.turretShotSound, game.minigunStart, game.minigunStop, game.grenadeExplosion, game.pitchforkImpact, game.knifeImpact, game.crossbowImpact, game.crossbowFleshImpact }) validate(clip);
            if (game.jumpVariations.Length != 3 || game.landingVariations.Length != 3) throw new Exception("Movement variation coverage");
            foreach (var clip in game.jumpVariations.Concat(game.landingVariations)) validate(clip);
            report += "\nPLAY MODE AUDIO PASS: all 17 banks use AnimatedArsenalAudio; " + decoded + " clip references decoded as non-silent mono samples at 48kHz (or WebGL's 44.1kHz import rate), special events and movement variations loaded, reload closing actions not duplicated.";
            Finish(report);
        }
        catch (Exception ex) { Finish("FAIL: " + ex); }
    }
    static void Finish(string report)
    {
        SessionState.SetBool(Pending, false);
        File.WriteAllText("Assets/LastDeparturePrototype/Audio/AnimatedArsenalAudio/UnityRuntimeChecks.txt", report + "\n");
        Debug.Log(report);
        if (!SessionState.GetBool(Restore, false)) EditorApplication.isPlaying = false;
    }
}
