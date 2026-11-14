using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class EncountersReview
{
    const string Root="Assets/Verification~/Encounters/", Key="EncountersReview.Stage";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static double next;
    static EncountersReview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Root+"Skills.request") && EditorApplication.isPlaying)
        {
            var target=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            if(target==null)return;
            File.Delete(Root+"Skills.request");
            var suppression=typeof(YardSurvival).GetField("suppressRunRecords",BindingFlags.Static|BindingFlags.NonPublic);
            bool saved=(bool)suppression.GetValue(target);suppression.SetValue(target,true);
            try { File.WriteAllText(Root+"SkillChecks.txt",target.RunSkillTreeChecks()); }
            catch(Exception error) { File.WriteAllText(Root+"SkillChecks.txt","FAIL "+error); Debug.LogException(error); }
            finally { typeof(YardSurvival).GetMethod("ShowHome",Flags).Invoke(target,null);suppression.SetValue(target,saved); }
            return;
        }
        if(File.Exists(Root+"Charger.request") && EditorApplication.isPlaying)
        {
            File.Delete(Root+"Charger.request");
            var target=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            try { File.WriteAllText(Root+"ChargerChecks.txt",target.RunChargerChecks()); }
            catch(Exception error) { File.WriteAllText(Root+"ChargerChecks.txt","FAIL "+error); Debug.LogException(error); }
            return;
        }
        if(File.Exists(Root+"Gameplay.request") && EditorApplication.isPlaying)
        {
            File.Delete(Root+"Gameplay.request");
            var target=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            if(target!=null)
            {
                try{File.WriteAllText(Root+"Gameplay.txt",target.RunEncounterGameplayChecks());}
                catch(Exception error){File.WriteAllText(Root+"Gameplay.txt","FAIL "+error);Debug.LogException(error);}
            }
            return;
        }
        if(File.Exists(Root+"Capture.request"))
        {
            File.Delete(Root+"Capture.request");SessionState.SetInt(Key,1);
            EditorApplication.isPaused=false;
            if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;
            next=EditorApplication.timeSinceStartup+5;return;
        }
        int stage=SessionState.GetInt(Key,0);
        if(stage==0||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null)return;
        object Call(string name,params object[] args)=>typeof(YardSurvival).GetMethod(name,Flags).Invoke(game,args);
        void Set(string name,object value)=>typeof(YardSurvival).GetField(name,Flags).SetValue(game,value);
        try
        {
            if(stage==1)
            {
                foreach(var inspection in UnityEngine.Object.FindObjectsByType<MapInspectionCamera>(FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(inspection);
                game.enabled=true;
                Call("ShowHome");Set("homeSelection",1);
                EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            }
            else if(stage==2)ScreenCapture.CaptureScreenshot(Root+"Home.png");
            else if(stage==3)Call("ShowEncounters");
            else if(stage==4)ScreenCapture.CaptureScreenshot(Root+"Locked.png");
            else if(stage==5){Set("encounterReviewUnlocked",true);Call("SelectEncounter",0);}
            else if(stage==6){ScreenCapture.CaptureScreenshot(Root+"Shambler.png");}
            else if(stage==7){Call("SelectEncounter",1);}
            else if(stage==8){ScreenCapture.CaptureScreenshot(Root+"Spitter.png");}
            else if(stage==9)Call("SelectEncounter",2);
            else if(stage==10)ScreenCapture.CaptureScreenshot(Root+"Screamer.png");
            else if(stage==11)Call("SelectEncounter",3);
            else if(stage==12)ScreenCapture.CaptureScreenshot(Root+"ArmoredWorker.png");
            else if(stage==13)Call("SelectEncounter",4);
            else if(stage==14)ScreenCapture.CaptureScreenshot(Root+"Charger.png");
            else if(stage==15)
            {
                int brute = Array.FindIndex(game.zombieModels, prefab => prefab.name == "04-Cargo-Brute");
                if (brute >= 0) Call("SelectEncounter", brute);
            }
            else if(stage==16)
            {
                if (Array.Exists(game.zombieModels, prefab => prefab.name == "04-Cargo-Brute"))
                    ScreenCapture.CaptureScreenshot(Root+"Brute.png");
            }
            else if(stage==17)
            {
                var combatBrute = Resources.Load<GameObject>("MeshyCombat/04-Cargo-Brute");
                if (combatBrute == null || !Array.Exists(game.zombieModels, prefab => prefab == combatBrute))
                    throw new Exception("Wave roster did not load the Meshy Brute prefab.");
                var actor = UnityEngine.Object.Instantiate(combatBrute);
                try
                {
                    var brute = actor.GetComponent<ImportedZombie>();
                    if (brute.ArchetypeHealth != 9 || brute.parts.Length != 6)
                        throw new Exception("Meshy Brute lost its combat regions or archetype.");
                    brute.TickMovement(.1f, .15f, false);
                    brute.TickMovement(.1f, 0, true, .5f);
                    brute.SamplePose(brute.deathClip, brute.deathClip.length * .5f);
                }
                finally { UnityEngine.Object.Destroy(actor); }
                Call("CloseEncounters");Set("encounterReviewUnlocked",false);
                if(typeof(YardSurvival).GetField("specimenArtwork",Flags).GetValue(game)==null)throw new Exception("Specimen artwork missing");
                foreach(string file in new[]{"Home.png","Locked.png","Shambler.png","Spitter.png"})if(!File.Exists(Root+file))throw new Exception("Missing "+file);
                CheckDiscovery(game);
                AssetDatabase.Refresh();
                File.WriteAllText(Root+"Result.txt","PASS Meshy Brute is in the wave roster and samples walk, attack and death poses. Archive captures saved. Behind-camera enemy stays locked; visible enemy unlocks; opening entry marks read; discovery survives return home. Player discovery save restored after checks.");
                SessionState.SetInt(Key,0);return;
            }
            SessionState.SetInt(Key,stage+1);next=EditorApplication.timeSinceStartup+2;
        }
        catch(Exception e)
        {
            Set("encounterReviewUnlocked",false);Call("CloseEncounters");
            SessionState.SetInt(Key,0);File.WriteAllText(Root+"Result.txt","FAIL "+e);Debug.LogException(e);
        }
    }
    static void CheckDiscovery(YardSurvival game)
    {
        var type=typeof(YardSurvival);
        object Call(string name,params object[] args)=>type.GetMethod(name,Flags).Invoke(game,args);
        void Set(string name,object value)=>type.GetField(name,Flags).SetValue(game,value);
        string key="LastDeparture.Encounters.v1."+game.zombieModels[0].name;
        bool had=PlayerPrefs.HasKey(key),hadRead=PlayerPrefs.HasKey(key+".Read");
        int saved=PlayerPrefs.GetInt(key),read=PlayerPrefs.GetInt(key+".Read");
        try
        {
            Call("ShowHome");PlayerPrefs.DeleteKey(key);PlayerPrefs.DeleteKey(key+".Read");
            var camera=(Camera)type.GetField("arenaCamera",Flags).GetValue(game);
            var front=type.GetNestedType("FrontScreen",BindingFlags.NonPublic);
            Vector3 point=Vector3.zero;bool clear=false;
            for(int x=-10;x<=10&&!clear;x+=2)for(int z=-10;z<=10&&!clear;z+=2)
            {
                point=new Vector3(x,0,z);
                if(!(bool)Call("Free",point,.5f,true))continue;
                Vector3 eye=point+Vector3.up-Vector3.forward*3;
                if((float)Call("ShotStopDistance",eye,Vector3.forward,null)>3.1f)clear=true;
            }
            if(!clear)throw new Exception("No clear test sightline found");
            Call("SpawnEnemy",point);camera.transform.position=point+Vector3.up-Vector3.forward*3;
            Set("frontScreen",Enum.Parse(front,"Game"));
            camera.transform.rotation=Quaternion.LookRotation(Vector3.back);Set("nextEncounterScan",0f);Call("TickEncounterDiscovery");
            if((bool)Call("EncounterKnown",0))throw new Exception("Enemy behind camera unlocked");
            camera.transform.rotation=Quaternion.LookRotation(Vector3.forward);Set("nextEncounterScan",0f);Call("TickEncounterDiscovery");
            if(!(bool)Call("EncounterKnown",0) || !(bool)Call("EncounterNew",0))throw new Exception("Visible enemy did not unlock as NEW");
            Call("ShowEncounters");
            if((bool)Call("EncounterNew",0))throw new Exception("Opened entry still marked NEW");
            Call("CloseEncounters");Call("ShowHome");
            if(!(bool)Call("EncounterKnown",0))throw new Exception("Discovery lost on run reset");
        }
        finally
        {
            if(had)PlayerPrefs.SetInt(key,saved);else PlayerPrefs.DeleteKey(key);
            if(hadRead)PlayerPrefs.SetInt(key+".Read",read);else PlayerPrefs.DeleteKey(key+".Read");
            PlayerPrefs.Save();Call("ShowHome");
        }
    }

}
