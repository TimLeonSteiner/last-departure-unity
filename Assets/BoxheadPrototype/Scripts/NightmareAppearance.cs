using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    // Presentation only: never disable renderers, bones or colliders used by hit/dismemberment logic.
    public sealed class NightmareAppearance : MonoBehaviour
    {
        sealed class Surface { public Renderer renderer; public Material[] original, dream; }
        readonly List<Surface> surfaces = new List<Surface>();
        readonly List<Material> owned = new List<Material>();
        bool applied;
        void Awake() { Initialize(Resources.Load<Shader>("NightmareSilhouette")); }
        ParticleSystem haze;
        Material hazeMaterial;
        readonly ParticleSystem.Particle[] puffs = new ParticleSystem.Particle[20];

        void EnsureHaze()
        {
            if (haze != null) return;
            var shader = Resources.Load<Shader>("NightmareCloud");
            if (shader == null) return;
            var child = new GameObject("Dream cloud veil");
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);
            haze = child.AddComponent<ParticleSystem>();
            haze.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = haze.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = puffs.Length;
            main.startSize3D = true;
            var emission = haze.emission; emission.enabled = false;
            var shape = haze.shape; shape.enabled = false;
            var renderer = haze.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = .8f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            hazeMaterial = new Material(shader) { name = "Dream cloud veil" };
            renderer.sharedMaterial = hazeMaterial;
            // The perception clock controls motion, including when pause/preview freezes time.
            haze.Pause();
        }

        void UpdateHaze(float amount, float clock)
        {
            if (amount <= .001f)
            {
                if (haze != null) { haze.Clear(); haze.gameObject.SetActive(false); }
                return;
            }
            Bounds bounds = default;
            bool found = false;
            foreach (var surface in surfaces)
            {
                var r = surface.renderer;
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!found) { if (haze != null) haze.gameObject.SetActive(false); return; }
            EnsureHaze();
            if (haze == null) return;
            haze.gameObject.SetActive(true);
            hazeMaterial.SetFloat("_DreamTime", clock);
            float height = Mathf.Max(.3f, bounds.size.y);
            float width = Mathf.Max(.25f, Mathf.Max(bounds.size.x, bounds.size.z));
            float phase = (GetEntityId().GetHashCode() & 1023) * .071f;
            for (int i = 0; i < puffs.Length; i++)
            {
                float seed = i * 2.399963f + phase;
                float cycle = Mathf.Repeat(i * .618034f + clock * .035f, 1);
                float fade = Mathf.SmoothStep(0, 1, Mathf.Min(cycle, 1 - cycle) * 7);
                float angle = seed + clock * (i % 2 == 0 ? .12f : -.09f);
                Vector3 offset = new Vector3(Mathf.Cos(angle) * bounds.extents.x * .65f,
                    (cycle - .5f) * height * 1.12f,
                    Mathf.Sin(angle) * (bounds.extents.z * 1.8f + width * .12f));
                float size = Mathf.Clamp(width * .8f, height * .32f, height * .7f);
                size *= .85f + .2f * Mathf.Sin(seed + clock * .18f);
                puffs[i].position = bounds.center + offset;
                puffs[i].startSize3D = new Vector3(size * 1.45f, size, size);
                puffs[i].rotation = seed * Mathf.Rad2Deg + clock * (i % 2 == 0 ? 3 : -2);
                puffs[i].startColor = new Color(.025f, .028f, .033f, amount * fade * .9f);
                puffs[i].startLifetime = 1000;
                puffs[i].remainingLifetime = 1000;
                puffs[i].randomSeed = (uint)(i + 1);
            }
            haze.SetParticles(puffs, puffs.Length);
        }
        public void Initialize(Shader shader)
        {
            if (shader == null || surfaces.Count > 0) return;
            var cache = new Dictionary<Material, Material>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
                var originals = renderer.sharedMaterials;
                var dreams = new Material[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                {
                    var source = originals[i];
                    if (source == null) continue;
                    if (!cache.TryGetValue(source, out var material))
                    {
                        material = new Material(shader) { name = "Dream / " + source.name };
                        string texture = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                        if (source.HasProperty(texture))
                        {
                            material.SetTexture("_BaseMap", source.GetTexture(texture));
                            material.SetTextureScale("_BaseMap", source.GetTextureScale(texture));
                            material.SetTextureOffset("_BaseMap", source.GetTextureOffset(texture));
                        }
                        material.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
                        if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") != null)
                        {
                            material.SetTexture("_BumpMap",source.GetTexture("_BumpMap"));
                            material.SetTextureScale("_BumpMap",source.GetTextureScale("_BumpMap"));
                            material.SetTextureOffset("_BumpMap",source.GetTextureOffset("_BumpMap"));
                            if(source.HasProperty("_BumpScale")) material.SetFloat("_BumpScale",source.GetFloat("_BumpScale"));
                        }
                        cache.Add(source, material); owned.Add(material);
                    }
                    dreams[i] = material;
                }
                surfaces.Add(new Surface { renderer = renderer, original = originals, dream = dreams });
            }
        }
        public static float DarknessAmount(float sleepiness) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, 1f, sleepiness));
        public void SetAmount(float amount, float clock)
        {
            amount = DarknessAmount(amount);
            bool active = amount > .001f;
            if (active != applied)
            {
                foreach (var surface in surfaces)
                    if (surface.renderer != null) surface.renderer.sharedMaterials = active ? surface.dream : surface.original;
                applied = active;
            }
            UpdateHaze(amount, clock);
            if (!active) return;
            foreach (var material in owned)
            {
                material.SetFloat("_Dream", amount);
                material.SetFloat("_DreamTime", clock);
                material.SetVector("_DreamOrigin", transform.position);
            }
        }
        void OnDisable() { SetAmount(0, 0); }
        void OnDestroy()
        {
            SetAmount(0, 0);
            foreach (var material in owned) Release(material);
            Release(hazeMaterial);
            if (haze != null) Release(haze.gameObject);
        }
        static void Release(Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
        }
    }
}
