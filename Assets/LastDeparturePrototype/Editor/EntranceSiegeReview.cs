using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class EntranceSiegeReview
{
    const string Folder = "Assets/Verification~/EntranceSiege/";
    const string Pending = "EntranceSiegeReview.Pending";
    static double ready;
    static EntranceSiegeReview() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Folder + "Run.request"))
        {
            File.Delete(Folder + "Run.request");
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Pending + "Started", !EditorApplication.isPlaying);
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            ready = EditorApplication.timeSinceStartup + 5;
        }
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < ready) return;
        SessionState.SetBool(Pending, false);
        try
        {
            var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            File.WriteAllText(Folder + "Result.txt", game.RunEntranceSiegeChecks() + "\n" + game.RunBarricadeAudioChecks());
            game.StartWave();
            var update = typeof(YardSurvival).GetMethod("UpdateEnemies", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 20; i++) update.Invoke(game, new object[] { .05f });
            Capture(game);
            ZombieRosterBuild.CaptureLineup();
        }
        catch (Exception exception) { File.WriteAllText(Folder + "Result.txt", "FAIL: " + exception); Debug.LogException(exception); }
        finally { if (SessionState.GetBool(Pending + "Started", false)) EditorApplication.isPlaying = false; }
    }
    static void Capture(YardSurvival game)
    {
        var obj = new GameObject("Siege review camera");
        var camera = obj.AddComponent<Camera>(); camera.enabled = false;
        camera.transform.position = new Vector3(0, 1.65f, 18);
        camera.transform.rotation = Quaternion.LookRotation(new Vector3(0, 1.3f, 31) - camera.transform.position);
        camera.fieldOfView = 75; camera.farClipPlane = 320; camera.cullingMask = ~(1 << 30);
        obj.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(YardSurvival).GetMethod("EnsureDreamEffects", flags).Invoke(game, null);
        var flash = typeof(YardSurvival).GetMethod("ApplyStormFlash", flags);
        var rt = new RenderTexture(1280, 800, 24); rt.Create();
        var texture = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        var old = RenderTexture.active;
        try
        {
            flash.Invoke(game, new object[] { .65f });
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); texture.Apply();
            File.WriteAllBytes(Folder + "Horde-and-barricades.png", texture.EncodeToPNG());
        }
        finally
        {
            flash.Invoke(game, new object[] { 0f }); RenderTexture.active = old;
            rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
