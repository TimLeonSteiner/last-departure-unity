using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class RunwayEveningReview
{
    const string Pending = "Dustline.RunwayEveningReview";
    static RunwayEveningReview() { EditorApplication.update += Tick; }

    [MenuItem("Tools/Dustline/Review evening runway")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) EditorSceneManager.OpenScene("Assets/Boxhead.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || Time.unscaledTime < 5) return;
        SessionState.SetBool(Pending, false);
        try
        {
            var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            var runway = game.transform.Find("Spaceport exterior/Distant runway and service buildings");
            if (runway == null) throw new Exception("Runway extension missing");
            foreach (var collider in runway.GetComponentsInChildren<Collider>())
                if (collider.enabled) throw new Exception("Scenery added a gameplay blocker");
            var hall = game.transform.Find("Spaceport terminal");
            if(Resources.Load<GameObject>("Spaceport/CraftedTerminalSet")!=null)
            {
                if(hall.Find("Spaceport_EmergencyLens")==null)throw new Exception("Blender emergency lenses were not instantiated");
                if(hall.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Crafted terminal added gameplay colliders");
            }
            int overhead = 0;
            foreach (var lamp in hall.GetComponentsInChildren<Light>())
                if (lamp.name == "Rack inspection practical")
                {
                    if (lamp.intensity <= 0 || lamp.shadows != LightShadows.Soft || lamp.range < lamp.transform.localPosition.y + 1 || lamp.transform.forward.y > -.9f)
                        throw new Exception("Emergency practical invalid: color="+lamp.color+", intensity="+lamp.intensity+", range="+lamp.range+", height="+lamp.transform.localPosition.y+", forward="+lamp.transform.forward);
                    overhead++;
                }
            if (overhead != 6) throw new Exception("Expected six overhead practicals");
            SpaceportAssetBuild.Capture(new Vector3(0, 2.5f, 21), new Vector3(0, 3, 140), "Evening-Runway-Doorway");
            SpaceportAssetBuild.Capture(new Vector3(-24, 18, 50), new Vector3(0, 1, 160), "Evening-Runway-Overview");
            SpaceportAssetBuild.Capture(new Vector3(3, 3.6f, -15), new Vector3(0, 4, 12), "Evening-Emergency-Hall");
            SpaceportAssetBuild.Capture(new Vector3(3, 1.65f, -20), new Vector3(10.8f, .4f, 0), "Evening-Orange-Reflections");
            SpaceportAssetBuild.Capture(new Vector3(3, 4.8f, -18), new Vector3(6.5f, 6.905f, -15), "Crafted-Fixture-Unity");
            SpaceportAssetBuild.Capture(new Vector3(-1, 4.8f, -17), new Vector3(-4.06f, 8, -12.18f), "Crafted-Roof-Breach-Unity");
            var pickup=Resources.Load<GameObject>("Spaceport/CaffeineBlisterPickup");
            if(pickup!=null)
            {
                var sample=UnityEngine.Object.Instantiate(pickup,new Vector3(6.5f,1,-15),Quaternion.Euler(0,180,0));
                try { SpaceportAssetBuild.Capture(new Vector3(6.5f,1.48f,-15.55f),new Vector3(6.5f,1,-15),"Caffeine-Blister-Unity"); }
                finally { UnityEngine.Object.DestroyImmediate(sample); }
            }
            File.WriteAllText("Assets/Verification~/RunwayEveningChecks.txt", "PASS Runway extension instantiated\nPASS Decorative colliders disabled\nPASS Six downward aisle lights with soft shadows\nPASS Runtime views rendered\n");
            if (Application.isBatchMode) EditorApplication.Exit(0);
            else EditorApplication.isPaused = true;
        }
        catch (Exception error)
        {
            File.WriteAllText("Assets/Verification~/RunwayEveningChecks.txt", "FAIL " + error);
            Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
