using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class ImportedZombie
    {
        Mesh gauntHead;
        Transform faceBone;
        float facePhase;

        void ApplyCreepyAppearance()
        {
            if (gauntHead != null || audioOwner == null) return;
            facePhase = transform.parent.position.x * .7f + transform.parent.position.z * .31f;
            var source = parts[0].sharedMesh;
            gauntHead = Instantiate(source); gauntHead.name = "Gaunt undead head";
            var vertices = gauntHead.vertices;
            Bounds bounds = source.bounds;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                float y = (v.y - bounds.min.y) / Mathf.Max(.0001f, bounds.size.y);
                float neckBlend = Mathf.SmoothStep(0, 1, Mathf.Clamp01((y - .15f) / .25f));
                float cheek = Mathf.Exp(-Mathf.Pow((y - .48f) * 5, 2));
                v.x = bounds.center.x + (v.x - bounds.center.x) * (1 - neckBlend * (.06f + cheek * .13f));
                v.z = bounds.center.z + (v.z - bounds.center.z) * (1 - neckBlend * cheek * .045f);
                // Small broad asymmetry gives the sculpt character while preserving topology and weights.
                v.x += Mathf.Sin(y * 5 + facePhase) * bounds.size.x * .014f * neckBlend;
                vertices[i] = v;
            }
            gauntHead.vertices = vertices; gauntHead.RecalculateNormals(); gauntHead.RecalculateBounds();
            parts[0].sharedMesh = gauntHead;
            faceBone = FindBone("Head");
            if (faceBone == null) return;
            var baked = new Mesh(); parts[0].BakeMesh(baked);
            Transform facing = transform.parent;
            var points = baked.vertices;
            Bounds headBounds = new Bounds(facing.InverseTransformPoint(parts[0].transform.TransformPoint(points[0])), Vector3.zero);
            foreach (var point in points) headBounds.Encapsulate(facing.InverseTransformPoint(parts[0].transform.TransformPoint(point)));
            Vector3 center = facing.TransformPoint(headBounds.center);
            Vector3 size = headBounds.size;
            Destroy(baked);
            Material dark = audioOwner.woodMaterial, pale = audioOwner.creamMaterial;
            Material previous = woundMaterial;
            foreach (int sign in new[] { -1, 1 })
            {
                Vector3 eye = center + facing.forward * (size.z * .52f) + facing.up * (size.y * .12f) + facing.right * (sign * size.x * .22f);
                woundMaterial = dark;
                attachedDetails[0].Add(Wound(faceBone, eye, new Vector3(size.x * .24f, size.y * .13f, .07f), "Recessed eye shadow"));
                woundMaterial = pale;
                attachedDetails[0].Add(Wound(faceBone, eye + facing.forward * .035f, new Vector3(size.x * .075f, size.y * .05f, .035f), "Clouded undead eye"));
            }
            woundMaterial = dark;
            attachedDetails[0].Add(Wound(faceBone, center + facing.forward * (size.z * .53f) - facing.up * (size.y * .18f), new Vector3(size.x * .3f, size.y * .09f, .07f), "Sunken undead mouth"));
            foreach (var detail in attachedDetails[0]) detail.transform.rotation = facing.rotation;
            woundMaterial = previous;
        }
        void AnimateCreepyFace(float time)
        {
            if (faceBone != null && parts[0].enabled)
                faceBone.localRotation *= Quaternion.Euler(3 + Mathf.Sin(time * .7f + facePhase) * 2, 0, Mathf.Sin(time * .5f + facePhase) * 6);
        }
        void OnDestroy() { ReleaseCostumeMeshes(); ReleasePoseGraph(); if (gauntHead != null) Destroy(gauntHead); }
    }
}
