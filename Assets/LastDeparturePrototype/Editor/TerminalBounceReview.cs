using System;
using System.IO;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class TerminalBounceReview
{
    const string Folder = "Assets/Verification~/TerminalBounce";
    static double hallReadyAt;
    static TerminalBounceReview() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists(Folder+"/Hall.request"))
        {
            File.Delete(Folder+"/Hall.request");
            SessionState.SetBool("TerminalBounce.HallPending",true);
            SessionState.SetBool("TerminalBounce.StartedPlay",!EditorApplication.isPlaying);
            if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;
            hallReadyAt=EditorApplication.timeSinceStartup+6;
        }
        if(SessionState.GetBool("TerminalBounce.HallPending",false) && EditorApplication.isPlaying &&
            !EditorApplication.isCompiling && !EditorApplication.isUpdating && EditorApplication.timeSinceStartup>hallReadyAt)
        {
            SessionState.SetBool("TerminalBounce.HallPending",false);
            try { CaptureHall(); }
            catch(Exception e){File.WriteAllText(Folder+"/Hall-result.txt","FAIL "+e);UnityEngine.Debug.LogException(e);}
            finally {if(SessionState.GetBool("TerminalBounce.StartedPlay",false))EditorApplication.isPlaying=false;}
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Folder+"/Review.request")) return;
        File.Delete(Folder+"/Review.request");
        try { Run(); }
        catch (Exception e) { File.WriteAllText(Folder+"/Result.txt", "FAIL " + e); UnityEngine.Debug.LogException(e); }
    }
    [MenuItem("Tools/Dustline/Lighting/Toggle terminal bounce preview")]
    static void Toggle() { TerminalBounceFeature.PreviewEnabled = !TerminalBounceFeature.PreviewEnabled; }

    static void CaptureHall()
    {
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if(game==null || game.transform.Find("Spaceport terminal")==null)throw new Exception("Terminal not loaded");
        var obj=new GameObject("Terminal GI comparison camera");var camera=obj.AddComponent<Camera>();
        camera.enabled=false;camera.cullingMask=~(1<<30);camera.clearFlags=CameraClearFlags.Skybox;
        camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.farClipPlane=100;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        var torch=new GameObject("Comparison torch").AddComponent<Light>();torch.transform.SetParent(obj.transform,false);
        torch.type=LightType.Spot;torch.intensity=55;torch.range=24;torch.spotAngle=48;torch.innerSpotAngle=22;
        torch.color=new Color(1,.94f,.84f);torch.shadows=LightShadows.Soft;torch.cullingMask=~(1<<30);
        var target=new RenderTexture(1280,800,24);target.Create();camera.targetTexture=target;
        var pixels=new Texture2D(1280,800,TextureFormat.RGB24,false);
        var old=RenderTexture.active;bool oldPreview=TerminalBounceFeature.PreviewEnabled;
        try
        {
            Vector3[] positions={new Vector3(0,1.65f,10),new Vector3(3,1.65f,13)};
            Vector3[] targets={new Vector3(0,7.9f,14),new Vector3(6,1,17)};
            for(int v=0;v<positions.Length;v++)
            {
                camera.transform.position=positions[v];camera.transform.LookAt(targets[v]);
                foreach(bool enabled in new[]{false,true})
                {
                    TerminalBounceFeature.PreviewEnabled=enabled;
                    camera.Render();camera.Render();RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,1280,800),0,0);pixels.Apply();
                    File.WriteAllBytes(Folder+"/Hall-"+v+(enabled?"-bounce":"-direct")+".png",pixels.EncodeToPNG());
                }
            }
            File.WriteAllText(Folder+"/Hall-result.txt","PASS: actual terminal ceiling and floor/wall views captured with identical lights and camera for each on/off pair.");
        }
        finally
        {
            TerminalBounceFeature.PreviewEnabled=oldPreview;RenderTexture.active=old;
            camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(obj);
        }
    }

    [MenuItem("Tools/Dustline/Lighting/Verify terminal bounce prototype")]
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/SourceFiles/Settings/PC_Renderer.asset");
        var shader = Shader.Find("Hidden/Dustline/TerminalBounce");
        if (data == null || shader == null) throw new Exception("Prototype renderer/shader missing");
        TerminalBounceFeature feature = null;
        foreach (var item in data.rendererFeatures) if (item is TerminalBounceFeature found) feature = found;
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<TerminalBounceFeature>();
            feature.name = "Terminal single-bounce prototype";
            AssetDatabase.AddObjectToAsset(feature, data); data.rendererFeatures.Add(feature);
        }
        feature.bounceShader = shader; feature.SetActive(true); feature.Create();
        data.SetDirty(); EditorUtility.SetDirty(feature); EditorUtility.SetDirty(data);
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        RenderTexture target = null; Texture2D pixels = null;
        var materials = new System.Collections.Generic.List<Material>();
        bool success = false;
        try
        {
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0; RenderSettings.ambientProbe = new SphericalHarmonicsL2();
            RenderSettings.reflectionIntensity = 0; RenderSettings.fog = false; RenderSettings.skybox = null;
            void Panel(string name, Vector3 position, Vector3 scale, Color color)
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.layer = 29;
                obj.transform.position = position; obj.transform.localScale = scale;
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); materials.Add(mat);
                mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", 0);
                obj.GetComponent<Renderer>().sharedMaterial = mat;
            }
            Panel("Red bounce floor",new Vector3(0,0,0),new Vector3(12,.2f,12),new Color(.65f,.025f,.012f));
            Panel("Unlit receiver wall",new Vector3(0,3,4),new Vector3(12,6,.2f),new Color(.6f,.6f,.6f));
            Panel("Ceiling",new Vector3(0,6,0),new Vector3(12,.2f,12),new Color(.4f,.4f,.4f));
            var camera = new GameObject("Bounce review camera").AddComponent<Camera>();
            camera.enabled=false; camera.cullingMask=1<<29; camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=Color.black; camera.nearClipPlane=.1f; camera.farClipPlane=30;
            camera.transform.position=new Vector3(0,3,-5);
            camera.transform.LookAt(new Vector3(0,1.8f,2)); camera.fieldOfView=70;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var lamp = new GameObject("Downward flashlight").AddComponent<Light>();
            lamp.type=LightType.Spot; lamp.range=15; lamp.intensity=35;
            lamp.spotAngle=65; lamp.innerSpotAngle=40; lamp.shadows=LightShadows.Soft;
            lamp.cullingMask=1<<29; lamp.transform.position=new Vector3(0,3,1);
            lamp.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
            target=new RenderTexture(960,600,24,RenderTextureFormat.ARGBHalf); target.Create(); camera.targetTexture=target;
            pixels=new Texture2D(960,600,TextureFormat.RGB24,false,true);
            Color[] Capture(string name, bool bounce)
            {
                TerminalBounceFeature.PreviewEnabled=bounce; camera.Render(); camera.Render();
                RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,960,600),0,0); pixels.Apply();
                File.WriteAllBytes(Folder+"/"+name+".png",pixels.EncodeToPNG()); return pixels.GetPixels();
            }
            var off=Capture("01-direct-only",false); var on=Capture("02-bounce-on",true);
            double delta=0, red=0, green=0; int changed=0;
            for(int i=0;i<off.Length;i++)
            {
                float d=on[i].grayscale-off[i].grayscale; delta+=d;
                if(d>.002f)changed++;
                red+=on[i].r-off[i].r;green+=on[i].g-off[i].g;
            }
            lamp.enabled=false;
            var darkOff=Capture("03-dark-off",false);var darkOn=Capture("04-dark-on",true);
            double darkDelta=0;for(int i=0;i<off.Length;i++)darkDelta+=Math.Abs(darkOn[i].grayscale-darkOff[i].grayscale);
            lamp.enabled=true;
            double Measure(bool enabled)
            {
                TerminalBounceFeature.PreviewEnabled=enabled;
                for(int i=0;i<3;i++)camera.Render();
                var watch=Stopwatch.StartNew();
                for(int i=0;i<10;i++)
                {
                    camera.Render();
                    // Readback fences GPU completion; submission alone hides expensive shaders.
                    RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,960,600),0,0);pixels.Apply();
                }
                watch.Stop();
                return watch.Elapsed.TotalMilliseconds/10;
            }
            double directMs=Measure(false),bounceMs=Measure(true);
            foreach(var message in ShaderUtil.GetShaderMessages(shader))
                if(message.severity.ToString()=="Error")throw new Exception(message.message);
            string report="Changed pixels="+changed+"; mean added luminance="+delta/off.Length+
                "; red/green added="+red+"/"+green+"; dark delta="+darkDelta/off.Length+
                "\n960x600 synchronous render + GPU readback average (10 samples, includes readback overhead; not gameplay FPS): direct="+directMs+"ms; bounce="+bounceMs+"ms.";
            if(changed<100 || delta<=0 || red<=green || darkDelta/off.Length>.001)
                throw new Exception("Bounce response check failed. "+report);
            File.WriteAllText(Folder+"/Result.txt","PASS "+report);
            success=true;
        }
        finally
        {
            TerminalBounceFeature.PreviewEnabled=true;
            feature.SetActive(success); data.SetDirty(); EditorUtility.SetDirty(feature);
            RenderTexture.active=null;
            if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
            if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);
            foreach(var mat in materials)UnityEngine.Object.DestroyImmediate(mat);
            SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene,true);
            AssetDatabase.SaveAssets();
        }
    }
}
