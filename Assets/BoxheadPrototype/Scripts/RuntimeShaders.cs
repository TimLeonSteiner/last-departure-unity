using UnityEngine;

namespace YardSurvivalPrototype
{
    // Resource-backed references also work before the first scene material loads.
    internal static class RuntimeShaders
    {
        internal static Shader Find(string name)
        {
            string resource = name == "Universal Render Pipeline/Unlit" ? "RuntimeUnlit" :
                name == "Universal Render Pipeline/Lit" ? "NightEntranceLED" :
                name == "Universal Render Pipeline/Particles/Unlit" ? "BoltExplosionGlow" : null;
            var material = resource == null ? null : Resources.Load<Material>(resource);
            var shader = material != null ? material.shader : Shader.Find(name);
            if (shader == null) throw new System.InvalidOperationException("Missing runtime shader: " + name);
            return shader;
        }
    }
}
