using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class ZombieAudioSetup
{
    static ZombieAudioSetup() { EditorApplication.playModeStateChanged += VerifyFreshPlayMode; }
    public static void VerifyInEditor()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode before verification.");
        SessionState.SetBool("ZombieAudio.RestoreFastPlay", EditorSettings.enterPlayModeOptionsEnabled);
        SessionState.SetBool("ZombieAudio.PendingCheck", true);
        EditorSettings.enterPlayModeOptionsEnabled = false;
        EditorApplication.isPlaying = true;
    }
    static void VerifyFreshPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("ZombieAudio.PendingCheck", false)) return;
        SessionState.SetBool("ZombieAudio.PendingCheck", false);
        EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool("ZombieAudio.RestoreFastPlay", true);
        EditorApplication.delayCall += () =>
        {
            try
            {
                var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
                string report = game.RunSmokeChecks() + "\n" + game.RunAdaptiveAudioChecks();
                File.WriteAllText(AudioFolder + "AdaptiveAudioChecks.txt", report); Debug.Log(report);
            }
            catch (Exception ex) { File.WriteAllText(AudioFolder + "AdaptiveAudioChecks.txt", "FAIL: " + ex); Debug.LogException(ex); }
        };
    }
    const string AudioFolder = "Assets/LastDeparturePrototype/Audio/";
    public static readonly string[] Categories = { "Zombie_Idle", "Zombie_Attack", "Zombie_Pain", "Zombie_Death", "Flesh_Bullet_Impact", "Arm_Sever", "Leg_Sever", "Head_Sever", "Body_Fall", "Limb_Land", "Blood_Splatter", "Zombie_Shuffle", "Zombie_Crawl", "Zombie_Bite", "Zombie_Gag" };
    public static void Configure(YardSurvival game)
    {
        game.zombieSounds = new YardSurvival.ZombieSoundBank[Categories.Length];
        for (int c = 0; c < Categories.Length; c++)
        {
            var bank = new YardSurvival.ZombieSoundBank { category = Categories[c], clips = new AudioClip[4] };
            for (int v = 0; v < 4; v++) bank.clips[v] = Import("ElevenLabs_Zombies/" + Categories[c] + "_" + (v + 1).ToString("00"), false);
            game.zombieSounds[c] = bank;
        }
        game.openingMusic = Import("ElevenLabs_Music/Opening", true);
        game.combatMusic = Import("ElevenLabs_Music/Combat", true);
        game.pressureMusic = Import("ElevenLabs_Music/Pressure", true);
        game.hordeMusic = Import("ElevenLabs_Music/Horde", true);
        EditorUtility.SetDirty(game);
    }
    static AudioClip Import(string name, bool music)
    {
        string path = AudioFolder + name + ".wav";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) throw new Exception("Missing audio: " + path);
        var settings = importer.defaultSampleSettings;
        if(settings.loadType==(music?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad)&&settings.compressionFormat==AudioCompressionFormat.Vorbis&&Mathf.Abs(settings.quality-.8f)<.001f&&importer.forceToMono==!music&&!importer.loadInBackground)return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .8f;
        importer.defaultSampleSettings = settings; importer.forceToMono = !music; importer.loadInBackground = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    [MenuItem("Tools/Yard Survival/Connect zombie sounds and adaptive music")]
    public static void Connect()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode before connecting assets.");
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene("Assets/LastDeparture.unity");
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if (game == null) throw new Exception("LastDeparture game is missing.");
        Configure(game); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }
    public static void VerifyBatch()
    {
        Connect();
        EditorApplication.playModeStateChanged += VerifyPlayMode;
        EditorApplication.isPlaying = true;
    }
    static void VerifyPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.delayCall += () =>
        {
            try
            {
                var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
                string report = game.RunSmokeChecks() + "\n" + game.RunAdaptiveAudioChecks();
                File.WriteAllText(AudioFolder + "AdaptiveAudioChecks.txt", report);
                Debug.Log(report); EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        };
    }
}
