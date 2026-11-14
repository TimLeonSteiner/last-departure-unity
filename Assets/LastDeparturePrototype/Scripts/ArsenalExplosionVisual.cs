using UnityEngine;
using UnityEngine.Rendering;

namespace YardSurvivalPrototype
{
    // Cosmetic only: the projectile owns damage and blast radius.
    public sealed class ArsenalExplosionVisual : MonoBehaviour
    {
        float age;
        Light glow;
        LineRenderer wave;
        Material fireMaterial, smokeMaterial;
        Texture2D fireTexture, smokeTexture;
        readonly Vector3[] ring = new Vector3[65];

        public void Initialize(Material fire, Material dust)
        {
            if (glow != null) return;
            fireTexture = Texture(false); smokeTexture = Texture(true);
            fireMaterial = MaterialFor(fire, fireTexture, true);
            smokeMaterial = MaterialFor(dust, smokeTexture, false);
            Burst("White hot ignition", fireMaterial, 1, .12f, 1.4f, 0, 0, new Color(1, .92f, .7f), false);
            Burst("Rolling fireball", fireMaterial, 16, .5f, .7f, 2.3f, -.15f, new Color(1, .38f, .05f), false);
            var sparks = Burst("Incandescent sparks", fireMaterial, 32, .85f, .035f, 7, .7f, new Color(1, .65f, .18f), false);
            var sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            sparkRenderer.velocityScale = .06f; sparkRenderer.lengthScale = 2;
            var debris = Burst("Ballistic dark fragments", smokeMaterial, 18, 1.35f, .07f, 4.5f, 1, new Color(.17f, .14f, .11f), false);
            var debrisRenderer = debris.GetComponent<ParticleSystemRenderer>();
            debrisRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            debrisRenderer.velocityScale = .02f; debrisRenderer.lengthScale = 1.3f;
            var smoke = Burst("Rising turbulent smoke", smokeMaterial, 22, 3.4f, .8f, .65f, -.065f, new Color(.2f, .18f, .16f, .65f), true);
            var noise = smoke.noise; noise.enabled = true;
            noise.strength = .23f; noise.frequency = .8f; noise.scrollSpeed = .3f;
            var dustCloud = Burst("Radial ground dust", smokeMaterial, 0, 1.4f, .45f, 0, 0, new Color(.32f, .28f, .22f, .4f), true);
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2 / 24;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                dustCloud.Emit(new ParticleSystem.EmitParams {
                    position = transform.position + radial * .18f + Vector3.up * .06f,
                    velocity = radial * (1.4f + i % 4 * .25f) + Vector3.up * .16f }, 1);
            }
            var child = new GameObject("Expanding pressure wave"); child.transform.SetParent(transform, false);
            wave = child.AddComponent<LineRenderer>(); wave.sharedMaterial = fireMaterial;
            wave.useWorldSpace = false; wave.positionCount = ring.Length; wave.widthMultiplier = .065f;
            wave.shadowCastingMode = ShadowCastingMode.Off; wave.receiveShadows = false;
            glow = gameObject.AddComponent<Light>(); glow.type = LightType.Point;
            glow.color = new Color(1, .58f, .22f); glow.range = 8; glow.intensity = 8;
            glow.shadows = LightShadows.None;
            Destroy(gameObject, 3.8f);
        }

        ParticleSystem Burst(string label, Material material, int count, float life, float size,
            float speed, float gravity, Color color, bool expanding)
        {
            var child = new GameObject(label); child.transform.SetParent(transform, false);
            var system = child.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main; main.loop = false; main.playOnAwake = false; main.duration = .1f;
            main.maxParticles = Mathf.Max(count, 24); main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * .7f, life);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .65f, size);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .6f, speed);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.gravityModifier = gravity; main.startColor = color;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .12f;
            var tint = system.colorOverLifetime; tint.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0),
                new GradientColorKey(expanding ? new Color(.65f, .65f, .65f) : new Color(1, .3f, .06f), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .06f),
                    new GradientAlphaKey(.7f, .45f), new GradientAlphaKey(0, 1) });
            tint.color = gradient;
            var sizing = system.sizeOverLifetime; sizing.enabled = true;
            sizing.size = new ParticleSystem.MinMaxCurve(1, expanding
                ? AnimationCurve.EaseInOut(0, .55f, 1, 2.7f) : AnimationCurve.EaseInOut(0, 1, 1, .15f));
            var renderer = system.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            system.Play(); if (count > 0) system.Emit(count);
            return system;
        }

        static Material MaterialFor(Material fallback, Texture2D texture, bool additive)
        {
            // Serialized Resources materials retain the transparent shader variant in WebGL builds.
            var template = Resources.Load<Material>(additive ? "BoltExplosionGlow" : "BoltExplosionSmoke");
            var shader = template != null ? template.shader : RuntimeShaders.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            var material = template != null ? new Material(template) : shader != null ? new Material(shader) : new Material(fallback);
            material.name = additive ? "Bolt blast glow" : "Bolt blast smoke";
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", additive ? 2 : 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_ColorMode", 0); material.SetFloat("_Mode", additive ? 4 : 2);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent; material.color = Color.white; material.mainTexture = texture;
            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            return material;
        }

        static Texture2D Texture(bool smoke)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = smoke ? "Turbulent smoke" : "Soft blast glow"; texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size, v = (y + .5f) / size;
                float alpha = Mathf.Pow(Mathf.Clamp01(1 - new Vector2(u * 2 - 1, v * 2 - 1).magnitude), smoke ? 1.2f : 2.2f);
                if (smoke) alpha *= .45f + .55f * Mathf.PerlinNoise(u * 7 + 13, v * 7 + 29);
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels(pixels); texture.Apply(false, true); return texture;
        }

        void Update()
        {
            age += Time.deltaTime; if (glow == null) return;
            glow.intensity = 8 * Mathf.Exp(-age * 12); glow.enabled = age < .5f;
            wave.enabled = age < .32f; if (!wave.enabled) return;
            float radius = Mathf.Lerp(.12f, 2.6f, Mathf.Clamp01(age / .32f));
            for (int i = 0; i < ring.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / (ring.Length - 1);
                ring[i] = new Vector3(Mathf.Cos(angle) * radius, .05f, Mathf.Sin(angle) * radius);
            }
            wave.SetPositions(ring);
            wave.startColor = wave.endColor = new Color(1, .55f, .18f, Mathf.Clamp01(1 - age / .32f) * .5f);
        }

        void OnDestroy()
        {
            if (fireMaterial != null) Destroy(fireMaterial); if (smokeMaterial != null) Destroy(smokeMaterial);
            if (fireTexture != null) Destroy(fireTexture); if (smokeTexture != null) Destroy(smokeTexture);
        }
    }
}
