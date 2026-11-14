using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class MapStateProbe
{
    static MapStateProbe(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/MapStateProbe.request"))return;
        var action=File.ReadAllText("Library/MapStateProbe.request").Trim();
        File.Delete("Library/MapStateProbe.request");
        if(action=="details"&&EditorApplication.isPlaying)
        {
            var capture=typeof(SolidHallWallsReview).GetMethod("Capture",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            capture.Invoke(null,new object[]{new Vector3(2.8f,2.5f,17.5f),new Vector3(0,1.3f,23.5f),"Entrance-NailedBoards"});
            capture.Invoke(null,new object[]{new Vector3(6.1f,2.05f,19.6f),new Vector3(5,1.72f,21.1f),"ServiceTerminal-Detail"});
            capture.Invoke(null,new object[]{new Vector3(18,1.15f,7.5f),new Vector3(13.5f,.4f,12.2f),"ServerRacks-NoSlabs"});
        }
        if(action=="boards"&&EditorApplication.isPlaying)
        {
            var capture=typeof(SolidHallWallsReview).GetMethod("Capture",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var lamp=new GameObject("Temporary board inspection light").AddComponent<Light>();lamp.type=LightType.Point;lamp.intensity=12;lamp.range=12;
            try
            {
                lamp.transform.position=new Vector3(0,2.6f,20.5f);
                capture.Invoke(null,new object[]{new Vector3(1.7f,1.6f,21.3f),new Vector3(-.3f,1.2f,23.45f),"Entrance-BlenderBoards-Close"});
                capture.Invoke(null,new object[]{new Vector3(2.8f,2.5f,17.5f),new Vector3(0,2.2f,23.5f),"Entrance-BlenderBoards"});
                lamp.transform.position=new Vector3(0,2.5f,26);
                capture.Invoke(null,new object[]{new Vector3(-2,1.7f,26),new Vector3(0,2.2f,23.45f),"Entrance-BlenderBoards-Outside"});
            }
            finally { Object.DestroyImmediate(lamp.gameObject); }
            var panels=Object.FindObjectsByType<YardSurvivalPrototype.EntranceBoards>(FindObjectsSortMode.None);
            int checkedPanels=0;
            foreach(var panel in panels)
            {
                if(!YardSurvivalPrototype.EntranceBoards.IsDoorway(panel.transform.position))continue;
                int Count(){int n=0;foreach(Transform child in panel.transform)if(child.name.StartsWith("Damage ")&&child.gameObject.activeSelf)n++;return n;}
                if(Count()!=5)throw new System.Exception("Detailed entrance boards missing");
                panel.SetHealth(60);if(Count()!=3)throw new System.Exception("Damage did not remove two boards");
                panel.SetHealth(100);if(Count()!=5)throw new System.Exception("Repair did not restore boards");checkedPanels++;
            }
            if(checkedPanels!=8)throw new System.Exception("Expected eight entrance panels: "+checkedPanels);
            File.WriteAllText("Assets/Verification~/EntranceBlenderBoards.txt","PASS: all eight panels across four entrances use five Blender damage groups; damage removes two groups at 60 health and repair restores all five. Front, close and outside views captured with a temporary inspection light, removed afterwards.");
        }
        if(action=="inventory"&&EditorApplication.isPlaying)
        {
            foreach(var freeCamera in Object.FindObjectsByType<YardSurvivalPrototype.MapInspectionCamera>(FindObjectsSortMode.None))Object.DestroyImmediate(freeCamera);
            var game=Object.FindAnyObjectByType<YardSurvivalPrototype.YardSurvival>();
            game.enabled=true;game.PrepareInventoryHudReview(true);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(YardSurvivalPrototype.YardSurvival).GetMethod("RestoreLaunchRendering",flags).Invoke(game,null);
            typeof(YardSurvivalPrototype.YardSurvival).GetMethod("OpenHologram",flags).Invoke(game,new object[]{0});
        }
        if(action=="restore"&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            for(int i=SceneManager.sceneCount-1;i>=0;i--)
            {
                var temporary=SceneManager.GetSceneAt(i);
                if(!temporary.isLoaded||!string.IsNullOrEmpty(temporary.path))continue;
                var roots=temporary.GetRootGameObjects();
                if(roots.Length==1&&roots[0].name=="Build image studio")EditorSceneManager.CloseScene(temporary,true);
            }
            File.WriteAllText("Library/MapInspection.request","start");
        }
        var report=new StringBuilder("playing="+EditorApplication.isPlaying+"\n");
        for(int i=0;i<SceneManager.sceneCount;i++)
        {
            var scene=SceneManager.GetSceneAt(i);report.AppendLine("SCENE "+scene.path+" loaded="+scene.isLoaded);
            if(scene.isLoaded)foreach(var root in scene.GetRootGameObjects())report.AppendLine("ROOT "+root.name+" active="+root.activeInHierarchy+" renderers="+root.GetComponentsInChildren<Renderer>(true).Length);
        }
        foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            report.AppendLine("CAMERA "+camera.name+" active="+camera.isActiveAndEnabled+" pos="+camera.transform.position+" mask="+camera.cullingMask);
        File.WriteAllText("Assets/Verification~/MapStateProbe.txt",report.ToString());
        if(EditorApplication.isPlaying)ScreenCapture.CaptureScreenshot("Assets/Verification~/MapStateProbe.png");
    }
}
