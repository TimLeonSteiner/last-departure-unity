using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class PerformanceReview
{
    static PerformanceReview(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/PerformanceReview.request"))return;
        string label=File.ReadAllText("Library/PerformanceReview.request").Trim();File.Delete("Library/PerformanceReview.request");
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        game.PrepareInventoryHudReview(false);
        game.enabled=false;
        foreach(var freeCamera in UnityEngine.Object.FindObjectsByType<MapInspectionCamera>(FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(freeCamera);
        typeof(YardSurvival).GetMethod("RestoreLaunchRendering",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(game,null);
        var camera=Camera.main;camera.transform.position=new Vector3(0,2.6f,-6.5f);camera.transform.LookAt(new Vector3(0,2.1f,0));
        if(label=="lod1")foreach(var core in UnityEngine.Object.FindObjectsByType<SuspendedCore>(FindObjectsSortMode.None))foreach(var lod in core.GetComponentsInChildren<LODGroup>())lod.ForceLOD(1);
        if(label=="reference")foreach(var core in UnityEngine.Object.FindObjectsByType<SuspendedCore>(FindObjectsSortMode.None))foreach(var lod in core.GetComponentsInChildren<LODGroup>())lod.ForceLOD(-1);
        camera.gameObject.AddComponent<PerformanceSample>().label=label;
    }
}
public sealed class PerformanceSample:MonoBehaviour
{
    public string label;
    readonly List<float> frames=new List<float>();
    float started;bool background;long allocated;double draws,triangles;
    void Start(){started=Time.realtimeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;allocated=GC.GetTotalMemory(false);}
    void LateUpdate()
    {
        float elapsed=Time.realtimeSinceStartup-started;if(elapsed<4)return;
        frames.Add(Time.unscaledDeltaTime*1000);draws+=UnityStats.drawCalls;triangles+=UnityStats.triangles;
        if(elapsed<12)return;
        frames.Sort();var renderers=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
            .Where(f=>f.GetComponent<Renderer>()!=null&&f.GetComponent<Renderer>().isVisible&&f.sharedMesh!=null)
            .Select(f=>new {name=f.transform.parent.name+"/"+f.name,vertices=f.sharedMesh.vertexCount}).OrderByDescending(x=>x.vertices).Take(20);
        string report=$"{label}: {frames.Count} frames; mean {frames.Average():F2} ms; p95 {frames[(int)((frames.Count-1)*.95f)]:F2} ms; draw calls {draws/frames.Count:F0}; triangles {triangles/frames.Count:F0}; managed memory delta {GC.GetTotalMemory(false)-allocated}; GPU {SystemInfo.graphicsDeviceName}; resolution {Screen.width}x{Screen.height}\n";
        report+=string.Join("\n",renderers.Select(r=>r.name+": "+r.vertices+" vertices"));
        File.WriteAllText("Assets/Verification~/Performance-"+label+".txt",report);ScreenCapture.CaptureScreenshot("Assets/Verification~/Performance-"+label+".png");Application.runInBackground=background;Destroy(this);
    }
}
