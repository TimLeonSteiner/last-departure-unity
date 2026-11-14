using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;
[InitializeOnLoad]
public static class CombatAudioReview
{
    const string Root="Assets/Verification~/";
    static AudioSource voice;
    static int stage;
    static double until;
    static float peak;
    static string report;
    static bool background;
    static CombatAudioReview(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(stage>0){
            var data=new float[1024];voice.GetOutputData(data,0);foreach(var v in data)peak=Mathf.Max(peak,Mathf.Abs(v));
            if(EditorApplication.timeSinceStartup<until)return;
            report+=(peak>0.0001f?"PASS ":"FAIL ")+voice.clip.name+" output peak="+peak+"\n";
            if(stage==1){StartClip(Resources.Load<AudioClip>("Announcer/last_departure"));stage=2;return;}
            UnityEngine.Object.DestroyImmediate(voice.gameObject);Application.runInBackground=background;stage=0;
            File.WriteAllText(Root+"CombatAudioReview.txt",report);return;
        }
        if(!File.Exists(Root+"CombatAudioReview.request")||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        File.Delete(Root+"CombatAudioReview.request");
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null)return;
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        if(!(bool)typeof(YardSurvival).GetField("soundEnabled",flags).GetValue(game))game.ToggleSound();
        typeof(YardSurvival).GetMethod("SaveAudioSettings",flags).Invoke(game,null);
        game.UnlockAudio();
        report="Effects enabled and persisted; existing effects volume preserved.\n";
        background=Application.runInBackground;Application.runInBackground=true;
        voice=new GameObject("Combat audio output review").AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;voice.ignoreListenerPause=true;
        voice.volume=.3f;voice.loop=true;
        var clip=(AudioClip)typeof(YardSurvival).GetMethod("WeaponClip",flags).Invoke(game,new object[]{0});
        StartClip(clip);stage=1;
    }
    static void StartClip(AudioClip clip){voice.Stop();clip.LoadAudioData();voice.clip=clip;peak=0;until=EditorApplication.timeSinceStartup+3;voice.Play();}
}
