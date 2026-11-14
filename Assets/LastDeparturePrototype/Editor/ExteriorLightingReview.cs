using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class ExteriorLightingReview
{
    const string Request = "Assets/Verification~/ExteriorLighting/Capture.request";
    const string Output = "Assets/Verification~/ExteriorLighting";
    const string Pending = "ExteriorLightingReview.Pending";
    static double readyAt;
    static ExteriorLightingReview() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Request))
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}
            File.Delete(Request);

            AssetDatabase.SaveAssets();
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Pending + ".StartedPlay", !EditorApplication.isPlaying);
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            readyAt = EditorApplication.timeSinceStartup + 4;
        }
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < readyAt) return;
        SessionState.SetBool(Pending, false);
        try { Capture(); }
        catch (Exception ex) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Result.txt", "FAIL " + ex); Debug.LogException(ex); }
        finally { if (SessionState.GetBool(Pending + ".StartedPlay", false)) EditorApplication.isPlaying = false; }
    }
    static void Capture()
    {
        Directory.CreateDirectory(Output);
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if (game == null) throw new Exception("Open LastDeparture scene first.");
        var root = game.transform.Find("Spaceport exterior");
        if (root == null) throw new Exception("Spaceport exterior missing.");
        var set = StormLandmarks.Build(root);
        ExteriorDecay.Build(root);
        if (root.GetComponentsInChildren<ExteriorFixture>().Length != 8) throw new Exception("Expected eight synchronized yard fixtures.");
        if (set.GetComponentsInChildren<Collider>().Length != 0) throw new Exception("Decorations must not add active collision geometry.");
        int renderers = set.GetComponentsInChildren<MeshRenderer>().Length;
        
        var budget=root.GetComponent<ExteriorLightBudget>();
        if(budget==null)throw new Exception("Exterior lighting budget missing");
        int hangarLights=0,apronLights=0,markers=0;
        foreach(var lamp in root.GetComponentsInChildren<Light>()){
            if(lamp.name=="Hangar door practical")hangarLights++;
            if(lamp.name=="Apron work light")apronLights++;
            if(lamp.name=="Runway marker spill")markers++;
        }
        if(hangarLights!=24||apronLights!=8||markers==0)throw new Exception("Missing physical fixture lights");
        foreach(var fixture in root.GetComponentsInChildren<ExteriorFixture>()){
            fixture.ApplyStrength(1);float full=fixture.Lamp.intensity;
            fixture.ApplyStrength(.08f);
            var block=new MaterialPropertyBlock();fixture.GetComponent<Renderer>().GetPropertyBlock(block);
            if(Mathf.Abs(fixture.Lamp.intensity-full*.08f)>.001f||Mathf.Abs(block.GetFloat("_Glow")-.144f)>.001f)throw new Exception("Flicker lens and light are not synchronized");
            fixture.ApplyStrength(1);
        }
        var cameraObject = new GameObject("Landmark review camera");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.fieldOfView = 75; camera.nearClipPlane = .05f; camera.farClipPlane = 320;
        camera.clearFlags = CameraClearFlags.Skybox; camera.cullingMask = ~(1 << 30);
        cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var flash = typeof(YardSurvival).GetMethod("ApplyStormFlash", flags);
        typeof(YardSurvival).GetMethod("EnsureDreamEffects", flags).Invoke(game, null);
        var target = new RenderTexture(1280, 800, 24); target.Create();
        var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        var oldActive = RenderTexture.active;
        try
        {
            string[] names = { "North-Approach", "East-Approach", "South-Approach", "West-Approach", "Near-Hangar", "Runway-Wear" };
            Vector3[] targets = { new Vector3(5, 2, 38), new Vector3(38, 2, -5), new Vector3(-5, 2, -38), new Vector3(-38, 2, 5), new Vector3(-39, 7.5f, 78), new Vector3(0, 0, 62) };
            for (int i = 0; i < names.Length; i++)
            {
                camera.transform.position = i<4 ? Quaternion.Euler(0, i * 90, 0) * new Vector3(0, 2.3f, 24) : i==4 ? new Vector3(-17,5,64) : new Vector3(3,3,45);
                camera.transform.rotation = Quaternion.LookRotation(targets[i] - camera.transform.position);
                budget.RefreshFor(camera.transform.position);
                if(budget.ActiveShadowCount>8)throw new Exception("Shadow budget exceeded");
                foreach (bool lit in new[] { false, true })
                {
                    flash.Invoke(game, new object[] { lit ? .7f : 0f });
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Output + "/" + names[i] + (lit ? "-Lightning" : "-Night") + ".png", pixels.EncodeToPNG());
                }
            }
            File.WriteAllText(Output + "/Result.txt", "PASS: 24 hangar practicals, 8 aligned apron lamps, runway spill, synchronized lens/light flicker, maximum 8 exterior shadow lights; " + renderers + " batched/detail renderers. Twelve night/lightning approach, hangar and runway captures.");
        }
        finally
        {
            flash.Invoke(game, new object[] { 0f });
            RenderTexture.active = oldActive; target.Release();
            UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
