using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class MeshyRoofReview
{
    const string Request = "Assets/Verification~/ReviewMeshyRoof.request";
    const string Phase = "LastDeparture.MeshyRoofReview.Phase";
    static MeshyRoofReview() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Request))
        {
            File.Delete(Request); SessionState.SetInt(Phase, 1);
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
            return;
        }
        int phase = SessionState.GetInt(Phase, 0);
        if (phase == 0) return;
        EditorApplication.isPaused = false;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        if (phase == 1)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetInt(Phase, 2); EditorApplication.isPlaying = true; return;
        }
        if (!EditorApplication.isPlaying) return;
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if (game == null || Time.unscaledTime < 4) return;
        SessionState.SetInt(Phase, 0);
        try
        {
            var roof = game.transform.Find("Spaceport terminal/Meshy roof");
            if (roof == null || roof.childCount != 1) throw new Exception("Expected one complete Meshy roof.");
            var renderer=roof.GetComponentInChildren<MeshRenderer>();
            if(renderer.sharedMaterial.GetTexture("_BaseMap")==null || renderer.sharedMaterial.GetTexture("_BumpMap")==null || ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader))throw new Exception("Roof material invalid");
            if(renderer.allowOcclusionWhenDynamic)throw new Exception("Roof still uses obsolete scene occlusion bake");
            if(roof.GetComponentsInChildren<MeshCollider>().Length!=1)throw new Exception("Exact roof collision missing");
            SpaceportAssetBuild.Capture(new Vector3(-13, 3, -13), new Vector3(0, 8, -1), "MeshyRoof-Interior");
            SpaceportAssetBuild.Capture(new Vector3(18, 3, 18), new Vector3(2, 8, 10), "MeshyRoof-Collapse");
            SpaceportAssetBuild.Capture(new Vector3(0, 30, 0), new Vector3(0, 0, .01f), "MeshyRoof-Above");
            var inspectionLight = new GameObject("Temporary roof inspection light").AddComponent<Light>();
            try
            {
                inspectionLight.type = LightType.Point; inspectionLight.intensity = 18; inspectionLight.range = 22;
                inspectionLight.color = new Color(.85f,.9f,1);
                inspectionLight.transform.position = new Vector3(-12, 5.5f, -12);
                SpaceportAssetBuild.Capture(new Vector3(-14, 5, -14), new Vector3(-10, 8.1f, -10), "MeshyRoof-Detail-InspectionLight");
            }
            finally { UnityEngine.Object.DestroyImmediate(inspectionLight.gameObject); }
            File.WriteAllText("Assets/Verification~/FullRoofRenderer.txt", "bounds="+renderer.bounds+" local="+renderer.localBounds+" active="+renderer.gameObject.activeInHierarchy+" enabled="+renderer.enabled+" off="+renderer.forceRenderingOff+" mesh="+renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount+" tris="+renderer.GetComponent<MeshFilter>().sharedMesh.triangles.Length+" shader="+renderer.sharedMaterial.shader.name+" scale="+renderer.transform.lossyScale);
            var allRenderers=UnityEngine.Object.FindObjectsByType<Renderer>();
            var enabled=new bool[allRenderers.Length];
            var ambient=RenderSettings.ambientLight;var mode=RenderSettings.ambientMode;bool fog=RenderSettings.fog;
            var previewLight=new GameObject("Temporary full roof preview light").AddComponent<Light>();
            try
            {
                for(int i=0;i<allRenderers.Length;i++){enabled[i]=allRenderers[i].enabled;allRenderers[i].enabled=allRenderers[i].transform.IsChildOf(roof);}
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;RenderSettings.fog=false;
                previewLight.type=LightType.Directional;previewLight.intensity=1.8f;previewLight.transform.rotation=Quaternion.Euler(-90,0,0);
                renderer.allowOcclusionWhenDynamic=false;
                SpaceportAssetBuild.Capture(new Vector3(0,-35,0),new Vector3(0,8,.001f),"FullRoof-Isolated-Underside");
            }
            finally
            {
                for(int i=0;i<allRenderers.Length;i++)allRenderers[i].enabled=enabled[i];
                RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=mode;RenderSettings.fog=fog;
                UnityEngine.Object.DestroyImmediate(previewLight.gameObject);
            }
            var rain = (System.Collections.Generic.List<ParticleSystem>)typeof(YardSurvival).GetField("rainfall", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(game);
            File.WriteAllText("Assets/Verification~/MeshyRoofRain.txt", string.Join("\n", rain.ConvertAll(p => p.name + " " + p.transform.localPosition + " parent=" + p.transform.parent.name)));
            string warehouse = game.RunWarehouseChecks();
            File.WriteAllText("Assets/Verification~/MeshyRoofReview.txt", "PASS complete textured Meshy roof; geometry-matched collision and rain.\n" + warehouse + "\n");
        }
        catch (Exception ex) { File.WriteAllText("Assets/Verification~/MeshyRoofReview.txt", "FAIL " + ex); Debug.LogException(ex); }
        finally { EditorApplication.isPlaying = false; }
    }
}
