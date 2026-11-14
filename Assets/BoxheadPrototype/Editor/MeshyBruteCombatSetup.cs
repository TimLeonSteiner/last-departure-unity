using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class MeshyBruteCombatSetup
{
    const string Folder = "Assets/BoxheadPrototype/Characters/Meshy/Brute/";
    const string Request = "Library/MeshyBruteCombat.request";
    const string Report = "Library/MeshyBruteCombat.txt";

    static MeshyBruteCombatSetup() => EditorApplication.update += Poll;

    static void Poll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || !File.Exists(Request)) return;
        File.Delete(Request);
        try { Build(); File.WriteAllText(Report, "PASS: Meshy Brute combat prefab built with six skinned regions."); }
        catch (Exception error) { File.WriteAllText(Report, error.ToString()); Debug.LogException(error); }
    }

    [MenuItem("Tools/Yard Survival/Build Meshy Brute combat prefab")]
    public static void Build()
    {
        const string modelPath = Folder + "CombatBody.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var rig = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/BoxheadPrototype/Resources/ZombieTypes/04-Cargo-Brute.prefab");
        var material = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/BoxheadPrototype/ThirdParty/MeshyDream/Brute/Brute.mat");
        var instance = UnityEngine.Object.Instantiate(rig);
        var source = UnityEngine.Object.Instantiate(model);
        try
        {
            var zombie = instance.GetComponent<ImportedZombie>();
            var input = source.GetComponentInChildren<MeshFilter>();
            var mesh = UnityEngine.Object.Instantiate(input.sharedMesh);
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var bounds = new Bounds(input.transform.TransformPoint(vertices[0]), Vector3.zero);
            for (int i = 0; i < vertices.Length; i++) bounds.Encapsulate(input.transform.TransformPoint(vertices[i]));
            var bones = zombie.parts[0].bones;
            int Bone(string name) => Array.FindIndex(bones, bone => bone.name == "FatZombie_" + name);
            var names = new[] { "Head", "Neck", "Spine2", "Spine", "Hips", "LeftArm", "LeftForeArm", "LeftHand",
                "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot",
                "RightUpLeg", "RightLeg", "RightFoot" };
            var indices = names.Select(Bone).ToArray();
            if (indices.Any(index => index < 0)) throw new InvalidOperationException("Brute rig is missing a required bone.");
            float left = Mathf.Sign(instance.transform.InverseTransformPoint(bones[indices[5]].position).x);
            float w = bounds.extents.x;
            var weights = new BoneWeight[vertices.Length];
            var groups = new int[vertices.Length];
            var frame = zombie.parts[0].transform;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 point = input.transform.TransformPoint(vertices[i]);
                float x = point.x - bounds.center.x;
                float y = (point.y - bounds.min.y) / bounds.size.y;
                float wide = Mathf.Abs(x) / w;
                bool leftSide = x * left >= 0;
                int a, b;
                float blend;
                if (y > .846f && wide < .4f)
                {
                    groups[i] = 0; a = 0; b = 1; blend = Mathf.InverseLerp(.91f, .846f, y) * .55f;
                }
                else if (wide > Mathf.Lerp(.59f, .62f, Mathf.InverseLerp(.47f, .83f, y)) && y > .38f)
                {
                    groups[i] = leftSide ? 1 : 2;
                    int arm = leftSide ? 5 : 8;
                    if (y > .635f) { a = arm; b = arm + 1; blend = Mathf.InverseLerp(.73f, .635f, y); }
                    else { a = arm + 1; b = arm + 2; blend = Mathf.InverseLerp(.56f, .45f, y); }
                }
                else if (y < .46f)
                {
                    groups[i] = leftSide ? 3 : 4;
                    int leg = leftSide ? 11 : 14;
                    if (y > .29f) { a = leg; b = 4; blend = Mathf.InverseLerp(.34f, .46f, y) * .8f; }
                    else if (y > .135f) { a = leg + 1; b = leg; blend = Mathf.InverseLerp(.19f, .29f, y); }
                    else { a = leg + 2; b = leg + 1; blend = Mathf.InverseLerp(.068f, .135f, y); }
                }
                else
                {
                    groups[i] = 5;
                    if (y > .71f) { a = 2; b = 1; blend = Mathf.InverseLerp(.78f, .85f, y) * .6f; }
                    else if (y > .58f) { a = 3; b = 2; blend = Mathf.InverseLerp(.58f, .71f, y); }
                    else { a = 4; b = 3; blend = Mathf.InverseLerp(.46f, .58f, y); }
                }
                weights[i] = new BoneWeight { boneIndex0 = indices[a], boneIndex1 = indices[b], weight0 = 1 - blend, weight1 = blend };
                vertices[i] = frame.InverseTransformPoint(instance.transform.position +
                    new Vector3(x, point.y - bounds.min.y, point.z - bounds.center.z) * (2.15f / bounds.size.y));
                if (normals.Length == vertices.Length)
                    normals[i] = frame.InverseTransformDirection(input.transform.TransformDirection(normals[i])).normalized;
            }
            mesh.vertices = vertices;
            if (normals.Length == vertices.Length) mesh.normals = normals;
            mesh.bindposes = bones.Select(bone => bone.worldToLocalMatrix * frame.localToWorldMatrix).ToArray();
            mesh.boneWeights = weights;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            for (int i = 0; i < zombie.parts.Length; i++)
            {
                var part = DepotAssetImport.Extract(mesh, groups, i);
                string path = Folder + "CombatRegion" + i + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (asset == null) { AssetDatabase.CreateAsset(part, path); asset = part; }
                else { EditorUtility.CopySerialized(part, asset); UnityEngine.Object.DestroyImmediate(part); }
                var skin = zombie.parts[i];
                skin.sharedMesh = asset;
                skin.bones = bones;
                skin.sharedMaterials = Enumerable.Repeat(material, asset.subMeshCount).ToArray();
                skin.localBounds = mesh.bounds;
                skin.updateWhenOffscreen = true;
            }
            zombie.preserveSourceAppearance = true;
            instance.name = "04-Cargo-Brute";
            ZombieDeathVariations.AssignExisting(zombie, instance.name);
            PrefabUtility.SaveAsPrefabAsset(instance, "Assets/BoxheadPrototype/Resources/MeshyCombat/04-Cargo-Brute.prefab");
            AssetDatabase.SaveAssets();
            UnityEngine.Object.DestroyImmediate(mesh);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}
