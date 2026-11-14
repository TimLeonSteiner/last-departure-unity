using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
#if UNITY_EDITOR
        public string RunFlashlightRenderChecks()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null || !pipeline.supportsAdditionalLightShadows || pipeline.additionalLightsRenderingMode != LightRenderingMode.PerPixel)
                throw new Exception("Flashlight needs per-pixel additional lights and spotlight shadows.");
            SetUpFlashlight();
            if (flashlightSpill == null || flashlightSpill.shadows != LightShadows.None)
                throw new Exception("Peripheral spill must use an unshadowed fill light.");
            const string folder = "Assets/LastDeparturePrototype/FlashlightReview";
            Directory.CreateDirectory(folder);
            var target = new RenderTexture(640, 400, 24); target.Create();
            var image = new Texture2D(640, 400, TextureFormat.RGB24, false);
            var oldTarget = arenaCamera.targetTexture; var oldActive = RenderTexture.active;
            Vector3 oldPosition = arenaCamera.transform.position;
            Quaternion oldRotation = arenaCamera.transform.rotation;
            bool oldOn = flashlightOn, oldBuilding = building;
            var oldClear = arenaCamera.clearFlags; Color oldBackground = arenaCamera.backgroundColor;
            var testRoot = new GameObject("Flashlight render verification");
            Material receiver = null;
            try
            {
                building = false;
                arenaCamera.targetTexture = target;
                Color[] Capture(string name, bool enabled)
                {
                    flashlightOn = enabled; UpdateFlashlight();
                    arenaCamera.Render(); RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 640, 400), 0, 0); image.Apply();
                    File.WriteAllBytes(folder + "/" + name + ".png", image.EncodeToPNG());
                    return image.GetPixels();
                }
                // Capture the actual yard; the beam must visibly affect its lit materials.
                arenaCamera.transform.position = new Vector3(0, 1.65f, 0);
                arenaCamera.transform.rotation = Quaternion.LookRotation(new Vector3(-7, .6f, -7) - arenaCamera.transform.position);
                var surroundingsOff = Capture("Surroundings-off", false);
                var surroundingsOn = Capture("Surroundings-on", true);
                float surroundingsDelta = MeanDelta(surroundingsOn, surroundingsOff, 80, 40, 560, 360);
                if (surroundingsDelta < .003f) throw new Exception("Flashlight does not illuminate the actual surroundings: " + surroundingsDelta);

                // Isolate a receiver and blocker above the yard for a deterministic occlusion check.
                arenaCamera.transform.position = new Vector3(0, 50, 0);
                arenaCamera.transform.rotation = Quaternion.identity;
                arenaCamera.clearFlags = CameraClearFlags.SolidColor; arenaCamera.backgroundColor = Color.black;
                receiver = new Material(wallMaterial); receiver.color = new Color(.4f, .4f, .4f);
                if (receiver.HasProperty("_Metallic")) receiver.SetFloat("_Metallic", 0);
                if (receiver.HasProperty("_Smoothness")) receiver.SetFloat("_Smoothness", .2f);
                Box("Opaque beam receiver", new Vector3(0, 50, 6), new Vector3(8, 6, .2f), receiver, testRoot.transform);
                var noLight = Capture("Receiver-off", false);
                var noBlocker = Capture("Receiver-on", true);
                float litDelta = MeanDelta(noBlocker, noLight, 275, 150, 365, 250);
                var blocker = Box("Opaque beam blocker", new Vector3(0, 50, 3), new Vector3(1, 2, .2f), receiver, testRoot.transform);
                // Keep the blocker out of the image but in the light's shadow pass.
                blocker.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                var blocked = Capture("Receiver-shadow", true);
                float shadowDelta = MeanDelta(noBlocker, blocked, 300, 175, 340, 225);
                if (litDelta < .02f || shadowDelta < litDelta * .25f)
                    throw new Exception("Rendered flashlight illumination/occlusion failed: light=" + litDelta + ", shadow=" + shadowDelta);
                flashlightOn = false; UpdateFlashlight();
                if (flashlight.enabled || flashlightSpill.enabled) throw new Exception("Flashlight toggle leaves a beam on.");
                return "FLASHLIGHT RENDER PASS: actual yard brightness delta=" + surroundingsDelta.ToString("F4")
                    + "; receiver illumination=" + litDelta.ToString("F4") + "; opaque shadow=" + shadowDelta.ToString("F4")
                    + "; per-pixel lighting, shadowed hotspot and peripheral fill and synchronized toggle.";
            }
            finally
            {
                testRoot.SetActive(false); Destroy(testRoot);
                if (receiver != null) Destroy(receiver);
                arenaCamera.transform.position = oldPosition; arenaCamera.transform.rotation = oldRotation;
                arenaCamera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                arenaCamera.clearFlags = oldClear; arenaCamera.backgroundColor = oldBackground;
                flashlightOn = oldOn; building = oldBuilding; UpdateFlashlight();
                target.Release(); Destroy(target); Destroy(image);
            }
        }

        static float MeanDelta(Color[] brighter, Color[] darker, int x0, int y0, int x1, int y1)
        {
            float sum = 0;
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
                sum += brighter[y * 640 + x].grayscale - darker[y * 640 + x].grayscale;
            return sum / ((x1 - x0) * (y1 - y0));
        }
#endif
    }
}
