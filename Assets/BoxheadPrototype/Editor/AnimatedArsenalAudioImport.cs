using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;
public static class AnimatedArsenalAudioImport
{
    [Serializable] class Manifest { public string status;public Entry[] weapons;public string turret;public Movement movement; }
    [Serializable] class Entry {public int id;public string weapon;public Events events;public string[] shot_variations;public bool reload_includes_mechanical_close;}
    [Serializable] class Events {public string shot,reload,action,swing,stab,impact,flesh_impact,explosion,motor_start,motor_stop;}
    [Serializable] class Movement {public string[] Jump,Landing;}
    public static void Setup()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/BoxheadPrototype/Audio/AnimatedArsenalAudio/Mapping.json"));
        if(manifest.status!="complete"||manifest.weapons.Length!=17)throw new Exception("Arsenal audio production is not complete yet");
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();var banks=new YardSurvival.WeaponSoundBank[17];
        foreach(var entry in manifest.weapons)
        {
            if(entry.id<0||entry.id>=17||entry.weapon!=WeaponCatalog.All[entry.id].name)throw new Exception("Audio manifest/catalog mismatch");
            var events=entry.events;bool melee=WeaponCatalog.IsMelee(entry.id);
            banks[entry.id]=new YardSurvival.WeaponSoundBank{weapon=entry.weapon,shot=Import(melee?events.stab:events.shot),reload=melee?null:Import(events.reload),action=Import(melee?events.stab:events.action),shotVariations=melee?new[]{Import(events.stab),Import(events.swing)}:(entry.shot_variations??Array.Empty<string>()).Select(Import).ToArray(),automaticShotVariations=Array.Empty<AudioClip>()};
            banks[entry.id].reloadIncludesAction=entry.reload_includes_mechanical_close;
            if(entry.id==1){game.minigunStart=Import(events.motor_start);game.minigunStop=Import(events.motor_stop);}
            if(entry.id==4)game.grenadeExplosion=Import(events.explosion);
            if(entry.id==5)game.pitchforkImpact=Import(events.impact);
            if(entry.id==10)game.knifeImpact=Import(events.impact);
            if(entry.id==7){game.crossbowImpact=Import(events.impact);game.crossbowFleshImpact=Import(events.flesh_impact);}
        }
        game.weaponSounds=banks;game.turretShotSound=Import(manifest.turret);
        game.jumpVariations=manifest.movement.Jump.Select(Import).ToArray();game.landingVariations=manifest.movement.Landing.Select(Import).ToArray();game.jumpSound=game.jumpVariations[0];game.landingSound=game.landingVariations[0];
        EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);AssetDatabase.SaveAssets();
        File.WriteAllText("Assets/BoxheadPrototype/AnimatedArsenalAudioImport.txt","PASS: completed manifest connected to 17 banks, separate motor/impact/explosion and jump/landing events; existing warehouse reflections and music retained.");
    }
    static AudioClip Import(string path)
    {
        if(string.IsNullOrEmpty(path)||!path.StartsWith("Assets/BoxheadPrototype/Audio/"))throw new Exception("Invalid audio path");
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=AssetImporter.GetAtPath(path) as AudioImporter;if(importer==null)throw new Exception("Missing "+path);
        var settings=importer.defaultSampleSettings;
        if(settings.loadType!=AudioClipLoadType.DecompressOnLoad||settings.compressionFormat!=AudioCompressionFormat.PCM||!importer.forceToMono||settings.sampleRateSetting!=AudioSampleRateSetting.OverrideSampleRate||settings.sampleRateOverride!=48000||!settings.preloadAudioData||importer.loadInBackground){settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;settings.sampleRateSetting=AudioSampleRateSetting.OverrideSampleRate;settings.sampleRateOverride=48000;settings.preloadAudioData=true;importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.loadInBackground=false;importer.SaveAndReimport();}
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
