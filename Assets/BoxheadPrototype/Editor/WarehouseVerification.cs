using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class WarehouseVerification
{
    static WarehouseVerification() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void Verify()
    {
        if (EditorApplication.isPlaying)
        { SessionState.SetBool("Warehouse.Restart", true); EditorApplication.isPlaying = false; }
        else Prepare();
    }
    static void Prepare()
    {
        AssetDatabase.Refresh();
        foreach (string file in Directory.GetFiles("Assets/BoxheadPrototype/Resources/WarehouseAudio", "*.wav"))
        {
            var importer = AssetImporter.GetAtPath(file.Replace('\\', '/')) as AudioImporter;
            if (importer == null) continue;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !file.Contains("Tail") && !file.Contains("Preview");
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }
        SessionState.SetBool("Warehouse.Pending", true); EditorApplication.isPlaying = true;
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("Warehouse.Restart", false))
        { SessionState.SetBool("Warehouse.Restart", false); EditorApplication.delayCall += Prepare; }
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("Warehouse.Pending", false)) return;
        SessionState.SetBool("Warehouse.Pending", false);
        EditorApplication.delayCall += Run;
    }
    static void Run()
    {
        string result;
        try
        {
            var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            result = game.RunWarehouseChecks() + "\n" + game.RunSmokeChecks() + "\n" + game.RunWeaponAudioChecks() + "\n" + game.RunRainAudioChecks();
            var camera = Camera.main;
            var position = camera.transform.position; var rotation = camera.transform.rotation;
            var target = new RenderTexture(1280, 720, 24); target.Create();
            var previous = RenderTexture.active;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                for (int view = 0; view < 2; view++)
                {
                    camera.transform.SetPositionAndRotation(view == 0 ? new Vector3(0, 1.65f, -5) : new Vector3(4, 1.65f, 13), Quaternion.Euler(view == 0 ? -10 : 0, view == 0 ? 20 : 0, 0));
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                    File.WriteAllBytes("Assets/BoxheadPrototype/HandReview/warehouse-" + view + ".png", image.EncodeToPNG());
                }
            }
            finally { camera.transform.SetPositionAndRotation(position, rotation); RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image); }
        }
        catch (Exception ex) { result = "FAIL: " + ex; Debug.LogException(ex); }
        File.WriteAllText("Assets/BoxheadPrototype/WarehouseRuntimeChecks.txt", result);
        Debug.Log(result);
    }
}
