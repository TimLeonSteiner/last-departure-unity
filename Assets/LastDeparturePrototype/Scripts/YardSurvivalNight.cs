using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        [Header("Night visibility")]
        [Range(0, .5f)] public float nightAmbient = DepotNightLighting.Ambient;
        [Range(0, 1)] public float moonlightIntensity = DepotNightLighting.Moonlight;
        public bool flashlightStartsOn = true;
        [Range(10, 180)] public float flashlightIntensity = 125;
        Light flashlight;
        Light flashlightSpill;
        bool flashlightOn;
        Transform[] defensePreviews;
        Renderer[][] previewRenderers;
        int previewType = -1;
        MaterialPropertyBlock previewTintCache;
        MaterialPropertyBlock previewTint => previewTintCache ?? (previewTintCache = new MaterialPropertyBlock());

        void SetUpFlashlight()
        {
            DepotNightLighting.EnsureGlow(transform);
            if (flashlight == null)
            {
                var obj = new GameObject("Player flashlight");
                obj.transform.SetParent(arenaCamera.transform, false);
                obj.transform.localPosition = new Vector3(.12f, -.08f, .04f);
                flashlight = obj.AddComponent<Light>();
                flashlight.type = LightType.Spot; flashlight.range = 34;
                flashlight.cookie = Resources.Load<Texture2D>("TorchFalloff");
                // Let the lens cookie shape the beam instead of multiplying it by
                // a second narrow spotlight falloff that hides most of the light.
                flashlight.spotAngle = 52; flashlight.innerSpotAngle = 44;
                flashlight.color = new Color(1, .94f, .84f);
                flashlight.shadows = LightShadows.Soft;
                flashlight.shadowNearPlane = .05f; flashlight.shadowBias = .02f;
                flashlight.shadowNormalBias = .05f;
                flashlight.renderMode = LightRenderMode.ForcePixel;
                flashlight.cullingMask = ~(1 << 30);
                flashlight.GetUniversalAdditionalLightData().renderingLayers = 1;
                flashlight.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier = 2;
            }
            if (flashlightSpill == null)
            {
                var spillObject = new GameObject("Flashlight peripheral spill");
                spillObject.transform.SetParent(flashlight.transform, false);
                flashlightSpill = spillObject.AddComponent<Light>();
                flashlightSpill.type = LightType.Spot; flashlightSpill.range = 12;
                flashlightSpill.spotAngle = 78; flashlightSpill.innerSpotAngle = 55;
                flashlightSpill.color = flashlight.color;
                flashlightSpill.shadows = LightShadows.None;
                flashlightSpill.shadowNearPlane = flashlight.shadowNearPlane;
                flashlightSpill.shadowBias = flashlight.shadowBias;
                flashlightSpill.shadowNormalBias = flashlight.shadowNormalBias;
                flashlightSpill.renderMode = LightRenderMode.ForcePixel;
                flashlightSpill.cullingMask = flashlight.cullingMask;
                flashlightSpill.GetUniversalAdditionalLightData().renderingLayers = 1;
                flashlightSpill.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier = 0;
            }
            flashlight.intensity = flashlightIntensity;
            flashlightSpill.intensity = flashlightIntensity * .07f;
            flashlightOn = flashlightStartsOn; flashlight.enabled = flashlightOn;
            flashlightSpill.enabled = flashlightOn;
        }
        void UpdateFlashlight()
        {
            if (flashlight == null) return;
            if (!dead && !showHelp && Pressed(Control.Flashlight)) flashlightOn = !flashlightOn;
            flashlight.enabled = flashlightOn && !dead;
            flashlightSpill.enabled = flashlight.enabled;
            flashlight.transform.localRotation = Quaternion.identity;
            if (building && buildRayValid)
                flashlight.transform.rotation = Quaternion.LookRotation(buildPoint + Vector3.up * .5f - flashlight.transform.position);
        }

        void CreateDefensePreviews()
        {
            defensePreviews = new Transform[4]; previewRenderers = new Renderer[4][];
            defensePreviews[0] = DepotModels.Barricade(this, actors, Vector3.up * .6f, false);
            Transform unused;
            defensePreviews[1] = DepotModels.Turret(this, actors, Vector3.zero, out unused);
            if(BarrelTemplate!=null){var preview=Instantiate(BarrelTemplate,actors);preview.enabled=false;preview.SetDetonated(false);explosiveBarrels.Remove(preview);defensePreviews[3]=preview.transform;}
            else defensePreviews[3]=new GameObject("Barrel preview unavailable").transform;
            for (int i = 0; i < defensePreviews.Length; i++)
            {
                var preview = defensePreviews[i]; if(preview==null)continue; preview.name = "Placement preview / " + preview.name;
                previewRenderers[i] = preview.GetComponentsInChildren<Renderer>();
                foreach (var renderer in previewRenderers[i]) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                foreach (var collider in preview.GetComponentsInChildren<Collider>()) collider.enabled = false;
                foreach (var light in preview.GetComponentsInChildren<Light>()) light.enabled = false;
                preview.gameObject.SetActive(false);
            }
            previewType = 0; ghost = defensePreviews[0];
        }
        void UpdateDefensePreview()
        {
            if (defensePreviews == null) return;
            if(selectedDefense==2)selectedDefense=0;
            if (previewType != selectedDefense)
            {
                if (ghost != null) ghost.gameObject.SetActive(false);
                previewType = selectedDefense; ghost = defensePreviews[previewType];
            }
            bool visible = building && !dead && !showHelp && buildRayValid;
            ghost.gameObject.SetActive(visible);
            if (!visible) return;
            validPlacement = CanPlaceDefense(buildPoint, selectedDefense, rotated);
            ghost.position = buildPoint + Vector3.up * (selectedDefense == 0 ? .6f : 0);
            ghost.rotation = Quaternion.Euler(0, selectedDefense == 0 && rotated ? 90 : 0, 0);
            var tint = validPlacement ? new Color(.25f, .8f, .48f) : new Color(.85f, .15f, .1f);
            foreach (var renderer in previewRenderers[previewType])
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    previewTint.Clear();
                    Color original = materials[i] != null && materials[i].HasProperty("_BaseColor") ? materials[i].GetColor("_BaseColor") : Color.gray;
                    previewTint.SetColor("_BaseColor", Color.Lerp(original, tint, .6f));
                    previewTint.SetColor("_Color", Color.Lerp(original, tint, .6f));
                    renderer.SetPropertyBlock(previewTint, i);
                }
            }
        }

#if UNITY_EDITOR
        public void CaptureNightReview()
        {
            const string folder = "Assets/LastDeparturePrototype/NightReview";
            System.IO.Directory.CreateDirectory(folder);
            var target = new RenderTexture(1100, 700, 24); target.Create();
            var image = new Texture2D(1100, 700, TextureFormat.RGB24, false);
            var previousTarget = arenaCamera.targetTexture; var previousActive = RenderTexture.active;
            try
            {
                ResetRun(); gun.gameObject.SetActive(false);
                arenaCamera.targetTexture = target;
                buildRayValid = true; buildPoint = new Vector3(0, 0, 3); building = true;
                foreach (int i in AvailableDefenses)
                {
                    selectedDefense = i; UpdateDefensePreview(); UpdateFlashlight();
                    arenaCamera.Render(); RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1100, 700), 0, 0); image.Apply();
                    System.IO.File.WriteAllBytes(folder + "/Placement-" + i + ".png", image.EncodeToPNG());
                }
                building = false; UpdateDefensePreview();
                SpawnEnemy(new Vector3(0, 0, 4));
                enemies[enemies.Count - 1].body.rotation = Quaternion.Euler(0, 180, 0);
                arenaCamera.transform.rotation = Quaternion.LookRotation(new Vector3(0, 1.1f, 4) - arenaCamera.transform.position);
                UpdateFlashlight();
                arenaCamera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1100, 700), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes(folder + "/Zombie-flashlight.png", image.EncodeToPNG());
            }
            finally
            {
                arenaCamera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                target.Release(); Destroy(target); Destroy(image); ResetRun();
            }
        }

        public string RunNightAndPreviewChecks()
        {
            Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); };
            ResetRun();
            check(flashlight != null && flashlight.type == LightType.Spot && flashlight.transform.parent == arenaCamera.transform, "Flashlight follows FPS camera");
            check(flashlight.enabled && flashlight.range >= 20 && flashlight.shadows != LightShadows.None, "Flashlight begins on with shadows");
            flashlightOn = false; UpdateFlashlight(); check(!flashlight.enabled, "Flashlight switches off");
            flashlightOn = true; UpdateFlashlight(); check(flashlight.enabled, "Flashlight switches back on");
            check(RenderSettings.ambientLight.maxColorComponent < .2f, "Outdoor ambient is dark");
            check(barricadeModel != null && turretModel != null && trapModel != null, "Three reusable defense prefabs assigned");
            buildRayValid = true; buildPoint = new Vector3(0, 0, 3); building = true;
            foreach (int type in AvailableDefenses)
            {
                selectedDefense = type; UpdateDefensePreview();
                check(ghost == defensePreviews[type] && ghost.gameObject.activeSelf && previewRenderers[type].Length > 3, "Preview uses actual model: " + type);
                foreach (var collider in ghost.GetComponentsInChildren<Collider>()) check(!collider.enabled, "Preview collider disabled");
                check(validPlacement, "Valid preview position: " + type);
            }
            supplies = 0; UpdateDefensePreview(); check(!validPlacement, "Unaffordable placement is invalid");
            building = false; UpdateDefensePreview(); check(!ghost.gameObject.activeSelf, "Combat hides preview");
            ResetRun();
            return "NIGHT/PREVIEW PASS: dark ambient, shadowed camera flashlight on/off, reusable defense prefabs, three actual-model previews, valid/invalid placement and cleanup.";
        }
#endif
    }
}
