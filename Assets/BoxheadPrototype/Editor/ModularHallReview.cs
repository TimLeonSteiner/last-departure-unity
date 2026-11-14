using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class ModularHallReview
{
    const string Request = "Assets/Verification~/ReviewModularHall.request";
    const string Phase = "LastDeparture.ModularHallReview.Phase";
    static ModularHallReview() { EditorApplication.update += Poll; }
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
            if (roof == null || roof.childCount != 136 || roof.GetComponentsInChildren<LODGroup>().Length != 136) throw new Exception("Expected 64 separate Meshy ceiling cassettes and 72 riveted trusses, each with game detail levels.");
            var renderer=roof.GetComponentInChildren<MeshRenderer>();
            if(renderer.sharedMaterial.GetTexture("_BaseMap")==null || renderer.sharedMaterial.GetTexture("_BumpMap")==null || ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader))throw new Exception("Roof material invalid");
            if(renderer.allowOcclusionWhenDynamic)throw new Exception("Roof still uses obsolete scene occlusion bake");
            if(roof.GetComponentsInChildren<MeshCollider>().Length!=1)throw new Exception("Exact roof collision missing");
            var roofCollider=roof.GetComponent<MeshCollider>();int covered=0,total=0;
            for(float x=-23.5f;x<24;x+=.5f)for(float z=-23.5f;z<24;z+=.5f)
            {total++;if(roofCollider.Raycast(new Ray(new Vector3(x,12,z),Vector3.down),out var hit,8))covered++;}
            if(covered<total*.98f)throw new Exception("Ceiling coverage below 98% with the three enlarged rain openings: "+covered+"/"+total);
            var walls=game.transform.Find("Spaceport terminal/Hall wall panels");
            if(walls==null||walls.childCount!=4)throw new Exception("Four hall walls missing");
            for(int side=0;side<4;side++)
            {
                var turn=Quaternion.Euler(0,side*90,0);bool wallHit=false;
                foreach(var c in walls.GetComponentsInChildren<BoxCollider>())if(c.Raycast(new Ray(turn*new Vector3(12,2,20),turn*Vector3.forward),out var hit,6))wallHit=true;
                if(!wallHit)throw new Exception("Wall panel missing on side "+side);
                foreach(var c in walls.GetComponentsInChildren<BoxCollider>())if(c.Raycast(new Ray(turn*new Vector3(0,1.65f,20),turn*Vector3.forward),out var hit,6))throw new Exception("Door blocked on side "+side);
            }
            File.WriteAllText("Assets/Verification~/ModularHallCoverage.txt","PASS ceiling "+covered+"/"+total+" samples covered; all four walls present, all four doorways clear.");
            Capture(new Vector3(-13, 3, -13), new Vector3(0, 8, -1), "ModularHall-Interior");
            Capture(new Vector3(18, 3, 18), new Vector3(2, 8, 10), "ModularHall-Collapse");
            Capture(new Vector3(0, 30, 0), new Vector3(0, 0, .01f), "ModularHall-Above");
            var inspectionLight = new GameObject("Temporary roof inspection light").AddComponent<Light>();
            try
            {
                inspectionLight.type = LightType.Point; inspectionLight.intensity = 18; inspectionLight.range = 22;
                inspectionLight.color = new Color(.85f,.9f,1);
                inspectionLight.transform.position = new Vector3(-12, 5.5f, -12);
                Capture(new Vector3(-14, 5, -14), new Vector3(-10, 8.1f, -10), "ModularHall-Detail-InspectionLight");
                inspectionLight.transform.position=new Vector3(0,4,12);inspectionLight.range=55;inspectionLight.intensity=12;
                Capture(new Vector3(-16,2,-10),new Vector3(8,4.5f,23),"ModularHall-Walls-Inspection");
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
                Capture(new Vector3(0,-35,0),new Vector3(0,8,.001f),"ModularHall-Isolated-Underside");
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
            File.WriteAllText("Assets/Verification~/ModularHallReview.txt", "PASS modular Meshy ceiling, four enclosed walls, matching collision and rain.\n" + warehouse + "\n");
        }
        catch (Exception ex) { File.WriteAllText("Assets/Verification~/ModularHallReview.txt", "FAIL " + ex); Debug.LogException(ex); }
        finally { EditorApplication.isPlaying = false; }
    }
    static void Capture(Vector3 position,Vector3 target,string name,bool skyOnly=false)
    {
        var camera=new GameObject("Roof and sky inspection").AddComponent<Camera>();
        camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=80;camera.farClipPlane=800;
        camera.useOcclusionCulling=false;camera.cullingMask=skyOnly?0:~(1<<30);camera.clearFlags=CameraClearFlags.Skybox;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.FastApproximateAntialiasing;
        var rt=new RenderTexture(1440,900,24);var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try{rt.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();File.WriteAllBytes("Assets/Verification~/"+name+".png",pixels.EncodeToPNG());}
        finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(camera.gameObject);}
    }
}
