using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        [Header("Zombie damage appearance")]
        public Material woundMaterial;
        
        // This version keeps combat non-graphic, including replicated effects.
        public static bool GoreEnabled => false;
        Mesh bloodDropletMesh;

        void PlayBloodSpray(Vector3 position, Vector3 direction, bool severed, int seed)
        {
            if (!GoreEnabled || woundMaterial == null || actors == null || dedicatedServer) return;
            if (bloodDropletMesh == null)
            {
                bloodDropletMesh = new Mesh { name = "Blood droplet" };
                bloodDropletMesh.vertices = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
                bloodDropletMesh.triangles = new[] { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
                bloodDropletMesh.RecalculateNormals(); bloodDropletMesh.RecalculateBounds();
            }
            var spray = new GameObject(severed ? "Severing blood spray" : "Flesh impact spray");
            spray.transform.SetParent(actors, false); spray.transform.position = position;
            spray.transform.rotation = Quaternion.LookRotation(direction.normalized + Vector3.up * .35f);
            var particles = spray.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed=false; particles.randomSeed=(uint)Mathf.Max(1,seed);
            var main = particles.main; main.loop = false; main.duration = .1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, severed ? 3.8f : 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.012f, .045f);
            main.gravityModifier = .8f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            var emission = particles.emission; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)(severed ? 24 : 12)) });
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 38; shape.radius = .045f;
            var shrink = particles.sizeOverLifetime; shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, .15f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = bloodDropletMesh; renderer.sharedMaterial = woundMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particles.Play(); Destroy(spray, .7f);
        }
    }
}
