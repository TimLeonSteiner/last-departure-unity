using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class WeaponAudioSetup
{
    const string Folder = "Assets/LastDeparturePrototype/Audio/ElevenLabs_Weapons/";
    static readonly string[] Names = { "P12", "M4_Service", "Scout", "M60" };
    static readonly string[] Actions = { "P12_Slide", "M4_Charging_Handle", "Scout_Bolt", "M60_Charging_Handle" };
    static WeaponAudioSetup() { EditorApplication.playModeStateChanged += VerifyEditorState; }

    public static void Configure(YardSurvival game)
    {
        game.weaponSounds = new YardSurvival.WeaponSoundBank[Names.Length];
        for (int i = 0; i < Names.Length; i++)
            game.weaponSounds[i] = new YardSurvival.WeaponSoundBank
            {
                weapon = WeaponCatalog.All[i].name,
                shot = ImportImpact(Names[i] + "_Shot_01"),
                shotVariations = new[] { ImportImpact(Names[i] + "_Shot_01"), ImportImpact(Names[i] + "_Shot_02"), ImportImpact(Names[i] + "_Shot_03") },
                automaticShotVariations = ImportAutomatic(Names[i]),
                reload = Import(Names[i] + "_Reload"),
                action = Import(Actions[i])
            };
        game.turretShotSound = ImportImpact("Turret_Shot_01");
        game.footstepSounds = new[] { ImportMovement("Boot_Concrete_01"), ImportMovement("Boot_Concrete_02"), ImportMovement("Boot_Concrete_03"), ImportMovement("Boot_Concrete_04") };
        game.jumpSound = ImportMovement("Jump_Gear"); game.landingSound = ImportMovement("Land_Boots");
        game.slideSound = ImportMovement("Slide_Concrete"); game.slideStopSound = ImportMovement("Slide_Stop");
        AdditionalWeaponImport.ConfigureAudio(game);
        AnimatedArsenalImport.ConfigureAudio(game);
        if(File.Exists("Assets/LastDeparturePrototype/Audio/AnimatedArsenalAudio/Mapping.json")&&!EditorApplication.isPlaying)AnimatedArsenalAudioImport.Setup();
        EditorUtility.SetDirty(game);
    }

    static AudioClip ImportImpact(string name) => Import(name, true);
    static AudioClip ImportMovement(string name) => Import(name, true, true);

    public static AudioClip[] ImportAutomatic(string name)
    {
        string prefix = "Assets/LastDeparturePrototype/Audio/Weapon_Impact/" + name + "_Auto_";
        if (!File.Exists(prefix + "01.wav")) return Array.Empty<AudioClip>();
        var clips = new AudioClip[6];
        for (int i = 0; i < clips.Length; i++) clips[i] = ImportImpact(name + "_Auto_" + (i + 1).ToString("00"));
        return clips;
    }

    static AudioClip Import(string name, bool impact = false, bool movement = false)
    {
        string path = (movement ? "Assets/LastDeparturePrototype/Audio/Player_Movement/" : impact ? "Assets/LastDeparturePrototype/Audio/Weapon_Impact/" : Folder) + name + ".wav";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) throw new Exception("Missing weapon audio: " + path);
        var settings = importer.defaultSampleSettings;
        if(settings.loadType==AudioClipLoadType.DecompressOnLoad&&settings.compressionFormat==(impact?AudioCompressionFormat.PCM:AudioCompressionFormat.Vorbis)&&settings.preloadAudioData&&importer.forceToMono&&!importer.loadInBackground&&Mathf.Abs(settings.quality-.85f)<.001f)return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = impact ? AudioCompressionFormat.PCM : AudioCompressionFormat.Vorbis;
        settings.quality = .85f;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = true;
        importer.loadInBackground = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    [MenuItem("Tools/Yard Survival/Connect weapon sounds")]
    public static void Connect()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode before connecting weapon sounds.");
        AssetDatabase.Refresh();
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/LastDeparture.unity")
        {
            if (scene.isDirty) throw new Exception("Save the current scene before connecting weapon audio.");
            scene = EditorSceneManager.OpenScene("Assets/LastDeparture.unity");
        }
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if (game == null) throw new Exception("LastDeparture game is missing.");
        Configure(game);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    public static void VerifyBatch()
    {
        Connect();
        EditorApplication.playModeStateChanged += VerifyPlayMode;
        EditorApplication.isPlaying = true;
    }

    public static void VerifyInEditor()
    {
        SessionState.SetBool("WeaponAudio.RestorePlay", EditorApplication.isPlaying);
        SessionState.SetBool("WeaponAudio.WaitingEdit", EditorApplication.isPlaying);
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        else ConnectAndPlay();
    }

    static void ConnectAndPlay()
    {
        try
        {
            Connect();
            SessionState.SetBool("WeaponAudio.Pending", true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception ex) { FinishEditorVerification("FAIL: " + ex); }
    }

    static void VerifyEditorState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("WeaponAudio.WaitingEdit", false))
        {
            SessionState.SetBool("WeaponAudio.WaitingEdit", false);
            ConnectAndPlay();
        }
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("WeaponAudio.Pending", false)) return;
        SessionState.SetBool("WeaponAudio.Pending", false);
        EditorApplication.delayCall += () =>
        {
            try
            {
                var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
                string weapons = game.RunSmokeChecks() + "\n" + game.RunAdditionalWeaponChecks() + "\n" + game.RunWeaponAudioChecks();
                string rain = game.RunRainAudioChecks();
                File.WriteAllText("Assets/LastDeparturePrototype/Audio/RainAudioChecks.txt", rain + "\n");
                FinishEditorVerification(weapons + "\n" + rain);
            }
            catch (Exception ex) { FinishEditorVerification("FAIL: " + ex); }
        };
    }

    static void FinishEditorVerification(string report)
    {
        SessionState.SetBool("WeaponAudio.Pending", false);
        SessionState.SetBool("WeaponAudio.WaitingEdit", false);
        File.WriteAllText(Folder + "WeaponAudioChecks.txt", report);
        Debug.Log(report);
        if (!SessionState.GetBool("WeaponAudio.RestorePlay", false)) EditorApplication.isPlaying = false;
    }

    static void VerifyPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.delayCall += () =>
        {
            try
            {
                var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
                string report = game.RunSmokeChecks() + "\n" + game.RunWeaponAudioChecks();
                File.WriteAllText(Folder + "WeaponAudioChecks.txt", report);
                Debug.Log(report);
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        };
    }
}
