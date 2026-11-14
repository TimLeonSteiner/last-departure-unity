using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class WaveProgressionReview
{
    const string Pending = "Dustline.WaveProgressionReview";
    static WaveProgressionReview() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Boxhead.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || Time.timeSinceLevelLoad < 1) return;
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if (game == null) return;
        SessionState.SetBool(Pending, false);
        try
        {
            string result = game.RunProgressionChecks() + game.RunDedicatedChecks();
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) CaptureLayout(game);
            File.WriteAllText("Assets/Verification~/WaveProgressionChecks.txt", result);
            Debug.Log(result);
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText("Assets/Verification~/WaveProgressionChecks.txt", "FAIL " + ex);
            Debug.LogException(ex); EditorApplication.Exit(1);
        }
    }
    static void CaptureLayout(YardSurvival game)
    {
        // Inspection view only: remove the roof and raise ambient light to expose floor layout.
        var roof = game.transform.Find("Warehouse yard/Warehouse hall/Continuous steel roof").gameObject;
        roof.SetActive(false);
        var camera = new GameObject("Layout inspection camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 55, -15);
        camera.transform.LookAt(Vector3.zero);
        camera.orthographic = true; camera.orthographicSize = 30;
        camera.cullingMask = ~(1 << 30);
        RenderSettings.fog = false;
        RenderSettings.ambientLight = Color.white * .65f;
        var lamp = new GameObject("Layout inspection light").AddComponent<Light>();
        lamp.type = LightType.Directional; lamp.intensity = 1.2f;
        lamp.transform.rotation = Quaternion.Euler(60, -30, 0);
        var target = new RenderTexture(1200, 1000, 24); target.Create();
        var image = new Texture2D(1200, 1000, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1200, 1000), 0, 0); image.Apply();
            File.WriteAllBytes("Assets/Verification~/ExpandedHall-Layout.png", image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image); }
    }
}
