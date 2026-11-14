using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class TerminalWearReview
{
    const string Request = "Assets/Verification~/TerminalWear/Capture.request";
    const string Output = "Assets/Verification~/TerminalWear";
    const string Pending = "TerminalWearReview.Pending";
    static double readyAt;
    static TerminalWearReview() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Request))
        {
            File.Delete(Request);
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
        var root = game.transform.Find("Spaceport terminal");
        if (root == null) throw new Exception("Spaceport exterior missing.");
        var previous = root.Find("Terminal wear and wayfinding");
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var exterior = game.transform.Find("Spaceport exterior");
        var oldSigns = exterior.Find("Four storm outlooks");
        if (oldSigns != null) UnityEngine.Object.DestroyImmediate(oldSigns.gameObject);
        StormLandmarks.Build(exterior);
        var set = TerminalWear.Build(root);
        int labels=0; foreach(Transform child in set.transform) if(child.name.StartsWith("Painted stencil /")) labels++;
        if(labels!=12) throw new Exception("Expected twelve painted stencils.");
        if (set.GetComponentsInChildren<Collider>().Length != 0) throw new Exception("Decorations must not add active collision geometry.");
        int renderers = set.GetComponentsInChildren<MeshRenderer>().Length;
        if (renderers > 70) throw new Exception("Landmark material batching exceeded budget: " + renderers);
        var cameraObject = new GameObject("Landmark review camera");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.fieldOfView = 75; camera.nearClipPlane = .05f; camera.farClipPlane = 320;
        camera.clearFlags = CameraClearFlags.Skybox; camera.cullingMask = ~(1 << 30);
        cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var flash = typeof(YardSurvival).GetMethod("ApplyStormFlash", flags);
        typeof(YardSurvival).GetMethod("EnsureDreamEffects", flags).Invoke(game, null);
        var target = new RenderTexture(1920, 1200, 24); target.Create();
        var pixels = new Texture2D(1920, 1200, TextureFormat.RGB24, false);
        var oldActive = RenderTexture.active;
        try
        {
            string[] names = { "Launch", "Service", "Cargo", "Transit" };
            Vector3[] targets = { new Vector3(0, 5.8f, 23), new Vector3(23, 5.8f, 0), new Vector3(0, 5.8f, -23), new Vector3(-23, 5.8f, 0) };
            for (int i = 0; i < 4; i++)
            {
                camera.transform.position = Quaternion.Euler(0, i * 90, 0) * new Vector3(0, 2.5f, 11);
                camera.transform.rotation = Quaternion.LookRotation(targets[i] - camera.transform.position);
                foreach (bool lit in new[] { false, true })
                {
                    flash.Invoke(game, new object[] { lit ? .7f : 0f });
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1920, 1200), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Output + "/" + names[i] + (lit ? "-Lightning" : "-Night") + ".png", pixels.EncodeToPNG());
                }
                flash.Invoke(game,new object[]{0f});
                camera.transform.position=Quaternion.Euler(0,i*90,0)*new Vector3(.8f,5.1f,16.3f);
                camera.transform.rotation=Quaternion.LookRotation(targets[i]-camera.transform.position);
                camera.fieldOfView=65;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1920,1200),0,0);pixels.Apply();
                File.WriteAllBytes(Output+"/"+names[i]+"-Detail.png",pixels.EncodeToPNG());
                camera.fieldOfView=75;
            }
            File.WriteAllText(Output + "/Result.txt", "PASS: four bay signs, twelve labels, no colliders, " + renderers + " batched/detail renderers. Eight ceiling/sign night/lightning captures.");
        }
        finally
        {
            flash.Invoke(game, new object[] { 0f });
            RenderTexture.active = oldActive; target.Release();
            UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}

