using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class SurfaceLightingVerification
{
    // An isolated ceiling patch verifies that darkness and distance affect the shader.
    public static void Run()
    {
        const string output = "Assets/Verification~/SurfaceLighting";
        Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientIntensity = 0;
        RenderSettings.ambientProbe = new SphericalHarmonicsL2();
        RenderSettings.reflectionIntensity = 0;
        RenderSettings.fog = false;
        var shader = Shader.Find("Dustline/Spaceport Cel");
        if (shader == null) throw new Exception("Surface shader missing");
        var material = new Material(shader);
        material.SetColor("_BaseColor", new Color(.4f, .4f, .4f));
        material.SetFloat("_Metallic", 0);
        material.SetFloat("_Smoothness", .15f);
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.transform.position = new Vector3(0, 8, 0);
        ceiling.transform.localScale = new Vector3(12, .2f, 12);
        ceiling.GetComponent<Renderer>().sharedMaterial = material;
        var camera = new GameObject("Verification camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 1, 0);
        camera.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        var light = new GameObject("Verification torch").AddComponent<Light>();
        light.type = LightType.Spot;
        light.transform.rotation = camera.transform.rotation;
        light.spotAngle = 70; light.innerSpotAngle = 40;
        light.range = 25; light.intensity = 12;
        light.shadows = LightShadows.Soft;
        var target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGBHalf);
        target.Create(); camera.targetTexture = target;
        var pixels = new Texture2D(512, 512, TextureFormat.RGBAFloat, false, true);
        float Capture(string name)
        {
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); pixels.Apply();
            File.WriteAllBytes(output + "/" + name + ".png", pixels.EncodeToPNG());
            float sum = 0;
            for (int y = 240; y < 272; y++)
                for (int x = 240; x < 272; x++) sum += pixels.GetPixel(x, y).grayscale;
            return sum / 1024;
        }
        try
        {
            light.enabled = false;
            float dark = Capture("Ceiling-off");
            light.enabled = true; light.transform.position = new Vector3(0, 5, 0);
            float near = Capture("Ceiling-near");
            light.transform.position = new Vector3(0, 1, 0);
            float far = Capture("Ceiling-far");
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                if (message.severity.ToString() == "Error") throw new Exception(message.message);
            string report = "Ceiling luminance: off=" + dark + ", near=" + near + ", far=" + far;
            if (dark > .001f || near < .01f || far >= near * .7f || far <= dark + .001f)
                throw new Exception("Lighting response failed: " + report);
            File.WriteAllText(output + "/Result.txt", "PASS " + report);
        }
        finally
        {
            RenderTexture.active = null; camera.targetTexture = null;
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
