using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        [Header("Storm atmosphere")]
        public Material rainMaterial;
        public AudioClip rainAmbience;
        [Range(0, 2)] public float rainIntensity = 1;
        AudioSource stormAudio;
        Material wetSurface;
        readonly List<Mesh> stormMeshes = new List<Mesh>();
        readonly List<ParticleSystem> rainfall = new List<ParticleSystem>();
        readonly List<float> rainRates = new List<float>();
        float appliedRainIntensity = -1;
        bool spaceportEveningWeather;

        void SetUpWeather()
        {
            var storm = new GameObject("Rain / soaked depot").transform;
            storm.SetParent(transform, false);
            DepotNightLighting.Atmosphere(this);
            DepotNightLighting.Warehouse(this, transform.Find("Warehouse yard/Warehouse hall"));

            wetSurface = new Material(Resources.Load<Shader>("PuddleWater")) { name = "Shallow rain water" };
            DressRuins(storm);
            storm.localScale = new Vector3(ArenaScale, 1, ArenaScale);
            // Outdoor rain plus localized leaks through the damaged roof.
            RainStrip(storm, new Vector3(0, 0, 22), new Vector2(48, 6), 480);
            RainStrip(storm, new Vector3(0, 0, -22), new Vector2(48, 6), 480);
            RainStrip(storm, new Vector3(-22, 0, 0), new Vector2(6, 38), 360);
            RainStrip(storm, new Vector3(22, 0, 0), new Vector2(6, 38), 360);
            var leakRandom=new System.Random(2709);
            var fullRoof = FullRoofLayout.Load();
            if(fullRoof!=null)
            {
                foreach(var patch in fullRoof.rainPatches)
                {
                    var c=patch.center;
                    RainStrip(storm,new Vector3(c.x,0,c.y),patch.size,Mathf.Max(35,patch.width*patch.height*35),8.5f);
                    Puddle(storm,new Vector3(c.x,.032f,c.y),Mathf.Min(2.5f,Mathf.Max(1.6f,Mathf.Max(patch.width,patch.height)*.85f)),leakRandom);
                }
            }
            else foreach(var leak in DepotModels.RoofLeaks)
            {
                bool collapsed=DepotModels.IsCollapsedRoofTile(leak.x,leak.y);
                RainStrip(storm,new Vector3(leak.x,0,leak.y),collapsed?new Vector2(5.6f,5.6f):new Vector2(3.2f,3.2f),collapsed?75:95,7.85f);
                Puddle(storm,new Vector3(leak.x,.032f,leak.y),2.5f,leakRandom);
                Puddle(storm,new Vector3(leak.x+.9f,.031f,leak.y-.7f),1.7f,leakRandom);
            }
            storm.gameObject.AddComponent<PuddleReflections>().Initialize(wetSurface);
            stormAudio = storm.gameObject.AddComponent<AudioSource>();
            stormAudio.clip = rainAmbience; stormAudio.loop = true;
            stormAudio.playOnAwake = false; stormAudio.spatialBlend = 0;
            ApplyRainVolume();
        }

        void RainStrip(Transform parent, Vector3 position, Vector2 size, float rate,float ceiling=6.5f)
        {
            if (rainMaterial == null) return;
            for (int layer = 0; layer < 2; layer++)
            {
                bool drops = layer == 0;
                var obj = new GameObject(drops ? "Slanted rainfall" : "Ground splashes");
                obj.transform.SetParent(parent, false);
                obj.transform.localPosition = position + Vector3.up * (drops ? ceiling : .045f);
                var particles = obj.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.loop = true; main.prewarm = true; main.duration = 2;
                main.startLifetime = drops ? ceiling/16f+.025f : .22f;
                main.startSpeed = 0;
                main.startSize = drops ? new ParticleSystem.MinMaxCurve(.01f, .02f) : new ParticleSystem.MinMaxCurve(.06f, .16f);
                bool roofLeak=ceiling>8;
                main.startColor = new Color(.72f, .78f, .82f, drops ? (roofLeak?.48f:.28f) : (roofLeak?.30f:.16f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.maxParticles = drops ? 1700 : 160;
                main.cullingMode = ParticleSystemCullingMode.Automatic;
                var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(size.x, .005f, size.y);
                var emission = particles.emission; emission.rateOverTime = rate * (drops ? 1 : .12f) * rainIntensity;
                if (drops)
                {
                    var velocity = particles.velocityOverLifetime; velocity.enabled = true;
                    velocity.space = ParticleSystemSimulationSpace.World;
                    velocity.x = -.35f; velocity.y = -16; velocity.z = .15f;
                    var collision = particles.collision; collision.enabled = true;
                    collision.type = ParticleSystemCollisionType.World;
                    collision.mode = ParticleSystemCollisionMode.Collision3D;
                    collision.quality = ParticleSystemCollisionQuality.Medium;
                    collision.collidesWith = 1; collision.enableDynamicColliders = false;
                    collision.lifetimeLoss = 1; collision.radiusScale = .5f;
                }
                else
                {
                    var fade = particles.colorOverLifetime; fade.enabled = true;
                    var gradient = new Gradient();
                    gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                        new[] { new GradientAlphaKey(.7f, 0), new GradientAlphaKey(0, 1) });
                    fade.color = gradient;
                    var growth = particles.sizeOverLifetime; growth.enabled = true;
                    growth.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .3f, 1, 1));
                }
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = Resources.Load<Material>("NightRain") ?? rainMaterial;
                renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
                renderer.renderMode = drops ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.HorizontalBillboard;
                renderer.lengthScale = 3; renderer.velocityScale = .009f;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                rainfall.Add(particles); rainRates.Add(rate * (drops ? 1 : .12f)); particles.Play();
            }
        }

        void DressRuins(Transform parent)
        {
            var random = new System.Random(2707);
            for (int i = 0; i < 65; i++)
            {
                float x = (float)random.NextDouble() * 32 - 16, z = (float)random.NextDouble() * 32 - 16;
                // No puddles or litter through the four occupied building footprints.
                if (Mathf.Abs(x) > 6.6f && Mathf.Abs(x) < 13.4f && Mathf.Abs(z) > 6.6f && Mathf.Abs(z) < 11.4f) continue;
                if (i % 3 == 0) Puddle(parent, new Vector3(x, .014f, z), .45f + (float)random.NextDouble() * 1.4f, random);
                else
                {
                    // Preserve the established puddle layout; detailed trash is placed by the hall.
                    random.NextDouble();random.NextDouble();random.Next(360);
                }
            }
            foreach (int x in new[] { -10, 10 }) foreach (int z in new[] { -9, 9 })
            {
                // Vertical wreckage stays inside the already blocked module bounds.
                for (int i = 0; i < 10; i++)
                {
                    var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.name = "Buckled roof sheet"; obj.transform.SetParent(parent, false);
                    obj.transform.localPosition = new Vector3(x - 2.5f + (float)random.NextDouble() * 5, 2.7f, z - 1.5f + (float)random.NextDouble() * 3);
                    obj.transform.localScale = new Vector3(.5f, .05f, .8f);
                    obj.transform.localRotation = Quaternion.Euler(random.Next(-25, 25), random.Next(360), random.Next(-15, 15));
                    obj.GetComponent<Collider>().enabled = false; obj.GetComponent<Renderer>().sharedMaterial = woodMaterial;
                }
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 12; i++)
                    {
                        var stain = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        stain.name = "Rust runoff / facade grime"; stain.transform.SetParent(parent, false);
                        float length = .3f + (float)random.NextDouble() * 1.2f;
                        stain.transform.localPosition = new Vector3(x - 2.8f + i * .5f, 2.3f - length / 2, z + side * 2.086f);
                        stain.transform.localScale = new Vector3(.035f + (float)random.NextDouble() * .14f, length, .008f);
                        stain.GetComponent<Collider>().enabled = false; stain.GetComponent<Renderer>().sharedMaterial = woodMaterial;
                    }
            }
        }

        void Puddle(Transform parent, Vector3 position, float radius, System.Random random)
        {
            const int segments = 96;
            var vertices = new Vector3[segments + 1]; var triangles = new int[segments * 3];
            var uv = new Vector2[segments + 1];
            uv[0] = new Vector2(.5f,.5f);
            float phase = (float)random.NextDouble() * Mathf.PI * 2;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                // Continuous lobes avoid the jagged independent-radius polygon silhouette.
                float reach = radius * (.82f + .09f * Mathf.Sin(angle*3+phase) + .055f * Mathf.Sin(angle*5-phase));
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * reach, 0, Mathf.Sin(angle) * reach * .6f);
                uv[i+1] = new Vector2(.5f+.5f*Mathf.Cos(angle),.5f+.5f*Mathf.Sin(angle));
                triangles[i * 3] = 0; triangles[i * 3 + 1] = (i + 1) % segments + 1; triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Irregular standing water" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            stormMeshes.Add(mesh);
            var obj = new GameObject("Dark rain puddle"); obj.transform.SetParent(parent, false); obj.transform.localPosition = position;
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var waterRenderer = obj.AddComponent<MeshRenderer>();waterRenderer.sharedMaterial = wetSurface;
            waterRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void UpdateWeather()
        {
            float currentRain = Mathf.Clamp(rainIntensity, 0, 2);
            if(wetSurface!=null)wetSurface.SetFloat("_RainStrength",currentRain);
            if (spaceportEveningWeather)
            {
                // Dark storm haze must not restore the old bright evening wash each frame.
                float gust = .5f + .5f * Mathf.Sin(Time.timeSinceLevelLoad * .045f);
                currentRain *= Mathf.Lerp(.72f, 1, gust);
                RenderSettings.fogDensity = Mathf.Lerp(.013f, .019f, gust);
                RenderSettings.fogColor = Color.Lerp(new Color(.0015f,.0025f,.004f), new Color(.003f,.0045f,.007f), gust);
            }
            if (Mathf.Abs(appliedRainIntensity - currentRain) > .005f || currentRain == 0 && appliedRainIntensity != 0)
            {
                appliedRainIntensity = currentRain;
                for (int i = 0; i < rainfall.Count; i++)
                {
                    var emission = rainfall[i].emission;
                    var p=rainfall[i].transform.localPosition;
                    bool indoorLeak=Mathf.Abs(p.x)<18&&Mathf.Abs(p.z)<18;
                    emission.rateOverTime = rainRates[i] * appliedRainIntensity * (indoorLeak?Mathf.Max(.45f,graphicsRainScale):graphicsRainScale*1.35f);
                    if(!indoorLeak && rainfall[i].name=="Slanted rainfall")
                    {
                        var velocity=rainfall[i].velocityOverLifetime;
                        velocity.x=-1.1f-.65f*Mathf.Sin(Time.timeSinceLevelLoad*.21f);
                        velocity.z=.45f+.3f*Mathf.Sin(Time.timeSinceLevelLoad*.17f);
                        var main=rainfall[i].main;main.startColor=new Color(.65f,.77f,.9f,.43f);
                    }
                }
            }
            if (stormAudio == null) return;
            stormAudio.mute = !rainEnabled || !audioUnlocked || rainIntensity <= 0 || rainVolume <= 0;
            if (stormAudio.mute)
            {
                if (stormAudio.isPlaying) stormAudio.Pause();
                return;
            }
            if (stormAudio.clip != null && !stormAudio.isPlaying)
            {
                stormAudio.UnPause();
                if (!stormAudio.isPlaying) stormAudio.Play();
            }
        }

        void OnDestroy()
        {
            DisposeInjuryFeedback();
            if (launchFloorShade != null) Destroy(launchFloorShade);
            if (weaponTrailMesh != null) Destroy(weaponTrailMesh);
            if (weaponTrailMaterial != null) Destroy(weaponTrailMaterial);
            if (waveWeaponHologram != null) Destroy(waveWeaponHologram);
            DisposeGraphics();
            DisposeDreamEffects();
            if (wetSurface != null) Destroy(wetSurface);
            if (bloodDropletMesh != null) Destroy(bloodDropletMesh);
            foreach (var mesh in stormMeshes) if (mesh != null) Destroy(mesh);
        }
    }
}
