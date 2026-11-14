using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

public static class DepotAssetImport
{
    const string Folder = "Assets/LastDeparturePrototype/ThirdParty/Zombies";
    public static void Configure(YardSurvival game)
    {
        game.woundMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/LastDeparturePrototype/Materials/Exposed zombie wounds.mat");
        if (RealisticZombieImport.UsePrepared(game)) return;
        game.zombieModels = new GameObject[2];
        for (int variant = 0; variant < 2; variant++)
        {
            string name = variant == 0 ? "Zombie_Male" : "Zombie_Female", path = Folder + "/" + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = true; importer.animationType = ModelImporterAnimationType.Legacy; importer.SaveAndReimport();
            var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            instance.name = name + " - detachable";
            foreach (var animator in instance.GetComponentsInChildren<Animator>()) UnityEngine.Object.DestroyImmediate(animator);
            foreach (var animation in instance.GetComponentsInChildren<Animation>()) UnityEngine.Object.DestroyImmediate(animation);
            var zombie = instance.AddComponent<ImportedZombie>(); zombie.parts = new SkinnedMeshRenderer[6];
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            zombie.walkClip = clips.First(c => c.name.EndsWith("|Walk")); zombie.attackClip = clips.First(c => c.name.EndsWith("|Punch")); zombie.deathClip = clips.First(c => c.name.EndsWith("|Defeat"));
            zombie.walkClip.SampleAnimation(instance, 0);
            var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>(); var source = skin.sharedMesh;
            var materials = skin.sharedMaterials.Select(m => MaterialFor(name, m)).ToArray();
            var points = skin.bones.Where(b => b.name == "Head" || b.name.Contains("Foot")).Select(b => b.position).ToArray();
            Bounds bounds = new Bounds(points[0], Vector3.zero); foreach (var p in points) bounds.Encapsulate(p);
            float scale = 1.7f / Mathf.Max(.1f, bounds.size.y);
            Debug.Log("Zombie normalization: " + name + " baked=" + bounds + " renderer=" + skin.bounds + " scale=" + scale);
            instance.transform.localScale *= scale; instance.transform.localPosition = Vector3.down * bounds.min.y * scale;
            int[] groups = source.boneWeights.Select(w => ImportedZombie.Classify(skin.bones[Dominant(w)].name)).ToArray();
            for (int part = 0; part < 6; part++)
            {
                var mesh = Extract(source, groups, part); string meshPath = Folder + "/" + name + "-region-" + part + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
                else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(existing); }
                var obj = new GameObject(new[] { "Head", "Left arm", "Right arm", "Left leg", "Right leg", "Torso" }[part]); obj.transform.SetParent(skin.transform, false);
                var region = obj.AddComponent<SkinnedMeshRenderer>(); region.sharedMesh = mesh; region.sharedMaterials = materials;
                region.bones = skin.bones; region.rootBone = skin.rootBone; region.localBounds = skin.localBounds; region.updateWhenOffscreen = true; zombie.parts[part] = region;
            }
            UnityEngine.Object.DestroyImmediate(skin);
            game.zombieModels[variant] = PrefabUtility.SaveAsPrefabAsset(instance, Folder + "/" + name + "-detachable.prefab");
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
    static int Dominant(BoneWeight w)
    {
        int bone = w.boneIndex0; float weight = w.weight0;
        if (w.weight1 > weight) { weight = w.weight1; bone = w.boneIndex1; }
        if (w.weight2 > weight) { weight = w.weight2; bone = w.boneIndex2; }
        if (w.weight3 > weight) bone = w.boneIndex3; return bone;
    }
    public static Mesh Extract(Mesh source, int[] groups, int part)
    {
        var map = new Dictionary<int, int>(); var oldIndices = new List<int>(); var submeshes = new List<int[]>();
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            var original = source.GetTriangles(sub); var triangles = new List<int>();
            for (int t = 0; t < original.Length; t += 3)
            {
                int a = groups[original[t]], b = groups[original[t + 1]], c = groups[original[t + 2]];
                int group = a == b || a == c ? a : b == c ? b : a;
                if (group != part) continue;
                for (int k = 0; k < 3; k++)
                {
                    int old = original[t + k];
                    if (!map.TryGetValue(old, out int index)) { index = oldIndices.Count; map.Add(old, index); oldIndices.Add(old); }
                    triangles.Add(index);
                }
            }
            submeshes.Add(triangles.ToArray());
        }
        if (oldIndices.Count == 0) throw new Exception("Zombie damage region has no geometry: " + part);
        var mesh = new Mesh { name = "Zombie region " + part,
            indexFormat = oldIndices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        var vertices = source.vertices; mesh.vertices = oldIndices.Select(i => vertices[i]).ToArray();
        var normals = source.normals; if (normals.Length == vertices.Length) mesh.normals = oldIndices.Select(i => normals[i]).ToArray();
        var uv = source.uv; if (uv.Length == vertices.Length) mesh.uv = oldIndices.Select(i => uv[i]).ToArray();
        var tangents = source.tangents; if (tangents.Length == vertices.Length) mesh.tangents = oldIndices.Select(i => tangents[i]).ToArray();
        var weights = source.boneWeights; mesh.boneWeights = oldIndices.Select(i => weights[i]).ToArray(); mesh.bindposes = source.bindposes;
        mesh.subMeshCount = submeshes.Count; for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
        mesh.RecalculateBounds(); return mesh;
    }
    static Material MaterialFor(string model, Material original)
    {
        string path = "Assets/LastDeparturePrototype/Materials/" + model + "-" + original.name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        string n = original.name.ToLowerInvariant();
        material.color = n.Contains("skin") || n.Contains("face") ? new Color(.72f, .76f, .67f)
            : n.Contains("guts") || n.Contains("brain") ? new Color(.29f, .035f, .025f)
            : n.Contains("bones") ? new Color(.61f, .53f, .37f)
            : n.Contains("hair") ? new Color(.07f, .065f, .05f)
            : n.Contains("darkclothes") ? new Color(.16f, .18f, .13f) : new Color(.26f, .27f, .19f);
        material.SetFloat("_Smoothness", n.Contains("guts") || n.Contains("brain") ? .42f : .16f);
        string texture = original.name.Contains("Skin") || original.name.Contains("Face") ? "CorpseSkin" : "Canvas";
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LastDeparturePrototype/Textures/" + texture + ".png"));
        material.mainTextureScale = Vector2.one * (texture == "CorpseSkin" ? 1 : 4); EditorUtility.SetDirty(material); return material;
    }
}
