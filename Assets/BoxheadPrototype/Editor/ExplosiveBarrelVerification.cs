using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

public static class ExplosiveBarrelVerification
{
    public static void Run()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode for barrel checks.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosiveBarrelSetup.PrefabPath);
        string reportPath = "Assets/BoxheadPrototype/ExplosiveBarrelChecks.txt";
        try { var result = UnityEngine.Object.FindAnyObjectByType<YardSurvival>().RunBarrelChecks(prefab); File.WriteAllText(reportPath, result); Debug.Log(result); Capture(prefab); }
        catch (Exception e) { File.WriteAllText(reportPath, e.ToString()); throw; }
    }
    [MenuItem("Tools/Yard Survival/Review explosive barrel")]
    public static void Review() => Capture(AssetDatabase.LoadAssetAtPath<GameObject>(ExplosiveBarrelSetup.PrefabPath));
    static void Capture(GameObject prefab)
    {
        var stage = new GameObject("Barrel review"); stage.transform.position = new Vector3(200, 20, 200);
        var barrel = UnityEngine.Object.Instantiate(prefab, stage.transform);
        foreach (var t in barrel.GetComponentsInChildren<Transform>()) t.gameObject.layer = 29;
        var cameraObject = new GameObject("Barrel review camera"); var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false; camera.cullingMask = 1 << 29; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f, .15f, .17f);
        cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        camera.transform.position = barrel.transform.position + new Vector3(1.1f, 1.15f, 1.5f); camera.transform.LookAt(barrel.transform.position + Vector3.up * .45f); camera.fieldOfView = 38;
        var lightObject = new GameObject("Barrel review light"); var light = lightObject.AddComponent<Light>(); light.type = LightType.Point; light.range = 10; light.intensity = 40;
        light.transform.position = barrel.transform.position + new Vector3(1, 2, 2);
        var target = new RenderTexture(800, 900, 24); target.Create(); var texture = new Texture2D(800, 900, TextureFormat.RGB24, false);
        double ready = EditorApplication.timeSinceStartup + .5;
        EditorApplication.update += Render;
        void Render()
        {
        if (EditorApplication.timeSinceStartup < ready) return;
        EditorApplication.update -= Render;
        bool fog = RenderSettings.fog; var previous = RenderTexture.active;
        try
        {
            if (barrel == null) return;
            barrel.GetComponent<ExplosiveBarrel>().SetDetonated(false);
            RenderSettings.fog = false;
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request); UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, 800, 900), 0, 0); texture.Apply();
            File.WriteAllBytes("Assets/BoxheadPrototype/ExplosiveBarrelReview.jpg", texture.EncodeToJPG(92));
            File.WriteAllText("Assets/BoxheadPrototype/ExplosiveBarrelRender.txt", "Barrel=" + barrel.transform.position + " camera=" + camera.transform.position + " bounds=" + barrel.GetComponentInChildren<Renderer>().bounds);
        }
        finally
        {
            RenderSettings.fog = fog; RenderTexture.active = previous; target.Release();
            UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(cameraObject); UnityEngine.Object.Destroy(lightObject); UnityEngine.Object.Destroy(stage);
        }
        }
    }
}
