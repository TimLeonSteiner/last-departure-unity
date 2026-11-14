using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class DepotNightSetup
{
    const string Request = "Assets/BoxheadPrototype/NightLighting.request";
    const string Review = "Assets/BoxheadPrototype/NightLightingReview";
    static DepotNightSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Apply(); }
        catch (Exception ex) { Directory.CreateDirectory(Review); File.WriteAllText(Review + "/Result.txt", "FAIL " + ex); Debug.LogException(ex); }
    }
    [MenuItem("Tools/Yard Survival/Apply entrance-lit night")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Boxhead.unity") throw new InvalidOperationException("Open Boxhead first.");
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        var hall = game.transform.Find("Warehouse yard/Warehouse hall");
        if (hall == null) throw new InvalidOperationException("Warehouse hall missing.");
        Directory.CreateDirectory(Review);
        Capture("Before", false);
        game.nightAmbient = DepotNightLighting.Ambient;
        game.moonlightIntensity = DepotNightLighting.Moonlight;
        const string resourceFolder = "Assets/BoxheadPrototype/Resources";
        if (!AssetDatabase.IsValidFolder(resourceFolder)) AssetDatabase.CreateFolder("Assets/BoxheadPrototype", "Resources");
        string materialPath = resourceFolder + "/NightEntranceLED.mat";
        var led = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (led == null)
        {
            led = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(led, materialPath);
        }
        led.SetColor("_BaseColor", new Color(.68f, .9f, .8f));
        led.EnableKeyword("_EMISSION");
        led.SetColor("_EmissionColor", new Color(.68f, .9f, .8f));
        EditorUtility.SetDirty(led); AssetDatabase.SaveAssetIfDirty(led);
        DepotNightLighting.Atmosphere(game);
        DepotNightLighting.Warehouse(game, hall);
        if (game.wastelandSky != null)
        {
            game.wastelandSky.SetColor("_Top", new Color(.0003f, .0005f, .0009f));
            game.wastelandSky.SetColor("_Horizon", new Color(.001f, .0015f, .0025f));
            EditorUtility.SetDirty(game.wastelandSky);
            AssetDatabase.SaveAssetIfDirty(game.wastelandSky);
            RenderSettings.skybox = game.wastelandSky;
        }
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Capture("Entrance", false);
        Capture("Flashlight", true);
        Capture("DarkAisle", false, 44);
        int ceiling = hall.GetComponentsInChildren<Light>().Count(l => l.name == "Warehouse working light" && l.enabled);
        int entrances = hall.GetComponentsInChildren<Light>().Count(l => l.name == "Entrance LED pool" && l.enabled);
        if (ceiling != 0 || entrances != 4) throw new InvalidOperationException("Unexpected lighting counts.");
        File.WriteAllText(Review + "/Result.txt", DateTime.Now.ToString("s") + "\nPASS: saved Boxhead night; 0 ceiling wash lights, 4 shadowed entrance LED pools. Ambient=" + game.nightAmbient + ", moon=" + game.moonlightIntensity + ".\nPlayer-height 75-degree views rendered with flashlight off/on; inspect PNGs.\n");
    }
    static void Capture(string name, bool flashlight, float yaw = 0)
    {
        var obj = new GameObject("Night review camera");
        var camera = obj.AddComponent<Camera>(); camera.enabled = false;
        camera.transform.position = new Vector3(0, 1.65f, 0);
        camera.transform.rotation = Quaternion.Euler(4, yaw, 0);
        camera.fieldOfView = 75; camera.nearClipPlane = .045f; camera.farClipPlane = 240;
        camera.clearFlags = CameraClearFlags.Skybox; camera.cullingMask = ~(1 << 30);
        obj.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        if (flashlight)
        {
            var lightObj = new GameObject("Night review flashlight"); lightObj.transform.SetParent(obj.transform, false);
            lightObj.transform.localPosition = new Vector3(.12f, -.08f, .04f);
            var lamp = lightObj.AddComponent<Light>(); lamp.type = LightType.Spot; lamp.range = 24; lamp.intensity = 55;
            lamp.spotAngle = 48; lamp.innerSpotAngle = 22; lamp.color = new Color(1, .94f, .84f); lamp.shadows = LightShadows.Soft;
        }
        var target = new RenderTexture(1280, 800, 24); var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            target.Create();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply();
            File.WriteAllBytes(Review + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; target.Release();
            UnityEngine.Object.DestroyImmediate(obj); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
