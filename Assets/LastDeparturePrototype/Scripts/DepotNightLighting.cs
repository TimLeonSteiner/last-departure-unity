using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YardSurvivalPrototype
{
    // Shared by generated environments and the saved scene; never changes gameplay geometry.
    public static class DepotNightLighting
    {
        public const float Ambient = .0006f;
        public const float Moonlight = .025f;
        public static readonly Color Fog = new Color(.0005f, .0008f, .0012f);

        public static void Atmosphere(YardSurvival game)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(game.nightAmbient * .72f, game.nightAmbient * .85f, game.nightAmbient);
            // Keep old sky probes and glossy puddles from lighting an otherwise unlit room.
            RenderSettings.ambientIntensity = 0;
            RenderSettings.ambientProbe = new SphericalHarmonicsL2();
            RenderSettings.reflectionIntensity = 0;
            RenderSettings.fog = true;
            RenderSettings.fogColor = Fog;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = .018f;
            Light moon = null;
            foreach (var lamp in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (lamp.type == LightType.Directional && lamp.name != "Viewmodel fill" && lamp.name != "Lightning reveal")
                {
                    if (moon != null) { lamp.intensity = 0; continue; }
                    moon = lamp;
                    lamp.color = new Color(.55f, .67f, .88f);
                    lamp.intensity = game.moonlightIntensity;
                    lamp.shadows = LightShadows.Soft;
                }
            if (moon == null)
            {
                var obj = new GameObject("Clouded moonlight"); obj.transform.SetParent(game.transform, false);
                moon = obj.AddComponent<Light>(); moon.type = LightType.Directional;
                moon.color = new Color(.55f, .67f, .88f);
                moon.intensity = game.moonlightIntensity; moon.shadows = LightShadows.Soft;
            }
            moon.enabled = true;
            moon.transform.rotation = Quaternion.Euler(32, -38, 0);
            moon.cullingMask = ~(1 << 30);
            moon.shadowBias = .025f; moon.shadowNormalBias = .15f;
            // Let the brighter lightning become URP's main directional light during a strike.
            RenderSettings.sun = null;
        }

        public static void EnsureGlow(Transform parent)
        {
            var existing = parent.Find("Entrance optical glow");
            var obj = existing != null ? existing.gameObject : new GameObject("Entrance optical glow"); obj.transform.SetParent(parent, false);
            var volume = obj.GetComponent<Volume>() ?? obj.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 5;
            volume.sharedProfile = Resources.Load<VolumeProfile>("NightOptics");
        }

        public static void Warehouse(YardSurvival game, Transform hall)
        {
            if (hall == null) return;
            EnsureGlow(game.transform);
            foreach (var lamp in hall.GetComponentsInChildren<Light>(true))
            {
                if (lamp.name == "Warehouse working light") { lamp.enabled = false; lamp.intensity = 0; }
                if (lamp.name == "Bay sign wash") { lamp.intensity = .12f; lamp.range = 1.6f; }
            }
            for (int side = 0; side < 4; side++)
            {
                var wall = hall.Find("Window wall " + side);
                if (wall == null) continue;
                var fixture = wall.Find("Entrance emergency LED");
                if (fixture == null)
                {
                    fixture = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                    fixture.name = "Entrance emergency LED";
                    fixture.SetParent(wall, false);
                    fixture.GetComponent<Collider>().enabled = false;
                }
                var ledMaterial = Resources.Load<Material>("NightEntranceLED");
                fixture.GetComponent<Renderer>().sharedMaterial = ledMaterial != null ? ledMaterial : game.accentMaterial;
                fixture.localPosition = new Vector3(0, 4.05f, 16.8f);
                fixture.localScale = new Vector3(2.6f, .075f, .12f);
                var lampTransform = wall.Find("Entrance LED pool");
                if (lampTransform == null)
                {
                    lampTransform = new GameObject("Entrance LED pool").transform;
                    lampTransform.SetParent(wall, false);
                    lampTransform.gameObject.AddComponent<Light>();
                }
                lampTransform.localPosition = new Vector3(0, 3.95f, 16.45f);
                lampTransform.localRotation = Quaternion.LookRotation(new Vector3(0, -3.95f, -.8f));
                var light = lampTransform.GetComponent<Light>();
                light.enabled = true; light.type = LightType.Spot;
                light.color = new Color(.65f, .86f, .74f);
                light.intensity = 14; light.range = 6;
                light.spotAngle = 55; light.innerSpotAngle = 32;
                light.cullingMask = ~(1 << 30);
                light.shadows = LightShadows.Soft;
                light.shadowBias = .025f; light.shadowNormalBias = .15f;
                light.shadowNearPlane = .1f;
                var outerFixture = wall.Find("Entrance exterior LED");
                if (outerFixture == null) { outerFixture = GameObject.CreatePrimitive(PrimitiveType.Cube).transform; outerFixture.name="Entrance exterior LED";outerFixture.SetParent(wall,false);outerFixture.GetComponent<Collider>().enabled=false; }
                outerFixture.localPosition=new Vector3(0,4.05f,18.04f);outerFixture.localScale=new Vector3(2.6f,.075f,.12f);
                outerFixture.GetComponent<Renderer>().sharedMaterial=fixture.GetComponent<Renderer>().sharedMaterial;
                var outerLamp=wall.Find("Entrance approach pool");
                if(outerLamp==null){outerLamp=new GameObject("Entrance approach pool").transform;outerLamp.SetParent(wall,false);outerLamp.gameObject.AddComponent<Light>();}
                outerLamp.localPosition=new Vector3(0,3.95f,18.07f);outerLamp.localRotation=Quaternion.LookRotation(new Vector3(0,-3.95f,1.2f));
                var approach=outerLamp.GetComponent<Light>();approach.type=LightType.Spot;approach.color=light.color;approach.intensity=8;approach.range=5;
                approach.spotAngle=55;approach.innerSpotAngle=32;approach.shadows=LightShadows.Soft;approach.shadowNearPlane=.1f;approach.shadowBias=.025f;approach.shadowNormalBias=.15f;approach.cullingMask=light.cullingMask;

            }
        }
    }
}
