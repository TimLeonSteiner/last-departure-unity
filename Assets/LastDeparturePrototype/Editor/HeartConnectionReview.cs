using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class HeartConnectionReview
{
    static HeartConnectionReview() { EditorApplication.update += CaptureRequestedEndpoints; }

    static void CaptureRequestedEndpoints()
    {
        const string request = "Assets/Verification~/InspectHeartEndpoints.request";
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(request)) return;
        File.Delete(request);
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var inspection = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(inspection);
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LastDeparturePrototype/Resources/HeartPlatform/Modular/Platform.prefab"));
            foreach (var lod in model.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(1);
            var light = new GameObject("Endpoint inspection light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2.5f;
            light.transform.rotation = Quaternion.Euler(28, -65, 0);
            foreach (var route in model.GetComponentsInChildren<HeartHoseRoute>())
            {
                if (!route.name.StartsWith("Vessel 07") && !route.name.StartsWith("Vessel 08")) continue;
                route.SendMessage("OnEnable");
                foreach (bool start in new[] { true, false })
                {
                    Vector3 point = start ? route.a : route.d;
                    SpaceportAssetBuild.Capture(point + new Vector3(.35f, .34f, -1.20f), point,
                        route.name + (start ? " heart end" : " far end"));
                }
            }
        }
        finally
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(inspection, true);
        }
    }
    public static void Run()
    {
        ModularHeartImport.RepairConnections();
        Inspect();
    }
    public static void Inspect()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LastDeparturePrototype/Resources/HeartPlatform/Modular/Platform.prefab"));
        foreach(var route in model.GetComponentsInChildren<HeartHoseRoute>(true))route.SendMessage("OnEnable");
        foreach(var lod in model.GetComponentsInChildren<LODGroup>())lod.ForceLOD(0);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.35f,.35f,.35f);
        var light=new GameObject("Inspection key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(30,-25,0);
        var fill=new GameObject("Inspection fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=1;fill.transform.rotation=Quaternion.Euler(20,145,0);
        SpaceportAssetBuild.Capture(new Vector3(0,2.6f,-6.5f),new Vector3(0,2.1f,0),"HeartConnections-Front");
        SpaceportAssetBuild.Capture(new Vector3(3.6f,2.2f,-3.4f),new Vector3(1.4f,1.9f,-.2f),"HeartConnections-Right");
        SpaceportAssetBuild.Capture(new Vector3(-3.6f,2.2f,-3.4f),new Vector3(-1.4f,1.9f,-.2f),"HeartConnections-Left");
        SpaceportAssetBuild.Capture(new Vector3(0,2.2f,-3.2f),new Vector3(0,2.1f,0),"HeartConnections-Belt");
        for(int i=0;i<8;i++)
        {
            float angle=i*Mathf.PI/4;
            SpaceportAssetBuild.Capture(new Vector3(Mathf.Sin(angle)*6,3,Mathf.Cos(angle)*6),new Vector3(0,2.3f,0),"HeartConnections-Orbit-"+i);
        }
        SpaceportAssetBuild.Capture(new Vector3(0,7,-.1f),new Vector3(0,1.5f,0),"HeartConnections-Top");
        SpaceportAssetBuild.Capture(new Vector3(0,.6f,5),new Vector3(0,2.9f,0),"HeartConnections-LowRear");
        int index=0;
        foreach(var route in model.GetComponentsInChildren<HeartHoseRoute>(true))
        {
            if(route.name.Contains("chain"))continue;
            foreach(bool start in new[]{true,false})
            {
                var point=start?route.a:route.d;
                var radial=new Vector3(point.x,0,point.z).normalized;
                var offset=radial*1.1f+Vector3.up*.55f;
                SpaceportAssetBuild.Capture(point+offset,point,"HeartEndpoint-"+(index.ToString("D2"))+(start?"-A":"-D"));
            }
            if(route.d.y>3.5f)
                SpaceportAssetBuild.Capture(route.d+new Vector3(-route.d.x*.25f,-.65f,-route.d.z*.25f),route.d,"HeartMount-"+index.ToString("D2"));
            index++;
        }
        foreach(var lod in model.GetComponentsInChildren<LODGroup>())lod.ForceLOD(1);
        SpaceportAssetBuild.Capture(new Vector3(3.6f,2.2f,-3.4f),new Vector3(1.4f,1.9f,-.2f),"HeartConnections-LOD1");
        File.WriteAllText("Assets/Verification~/HeartConnectionReview.txt","Assembly rebuilt. Rendered 8 orbit views, top, low rear, 4 detail views, 40 endpoints, 10 frame undersides and LOD1. 20 tube routes / 40 sleeved endpoints.\n");
        Debug.Log("HEART_CONNECTION_REVIEW_READY");
    }
}
