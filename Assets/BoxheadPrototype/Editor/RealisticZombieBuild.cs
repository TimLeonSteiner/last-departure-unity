using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;

public static partial class RealisticZombieImport
{
    public const string Output = "Assets/BoxheadPrototype/ThirdParty/PlayableZombies";
    static readonly HumanBodyBones[] LimbRoots = { HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg };
    public static bool UsePrepared(YardSurvival game)
    {
        var models = new[] { "Pxltiger", "Modern", "FatZombie", "AAB" }.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/" + n + ".prefab")).Where(m=>m!=null).ToArray();
        if (models.Length==0) return false;
        game.zombieModels = (game.zombieModels??Array.Empty<GameObject>()).Concat(models).Where(m=>m!=null).Distinct().ToArray(); return true;
    }
    [MenuItem("Tools/Yard Survival/Prepare new zombies")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before preparing zombie assets.");
        Directory.CreateDirectory(Output); AssetDatabase.Refresh();
        Build("Pxltiger", Tiger + "/FBXs/Zombie3.FBX", Tiger + "/Prefabs/Zombie3.prefab");
        Build("Modern", Modern + "/ZombieRig.fbx", Modern + "/ZombieRig.prefab");
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if (game != null) { UsePrepared(game); EditorUtility.SetDirty(game); EditorSceneManager.MarkSceneDirty(game.gameObject.scene); EditorSceneManager.SaveScene(game.gameObject.scene); }
        AssetDatabase.SaveAssets();
        Debug.Log("NEW ZOMBIES READY: Pxltiger and Modern, six detachable regions each.");
    }
    static void Build(string name, string modelPath, string prefabPath)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
        var root = new GameObject(name + " zombie");
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var source = UnityEngine.Object.Instantiate(reference, root.transform);
        foreach (var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var authored = reference.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(s => s.name == skin.name);
            if (authored != null) skin.sharedMaterials = authored.sharedMaterials;
        }
        if (source.GetComponentInChildren<Animator>() == null)
        {
            var rig = source.AddComponent<Animator>(); rig.avatar = reference.GetComponentInChildren<Animator>().avatar;
        }
        source.name = "Rig";
        try
        {
            var animator = source.GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman) throw new Exception(name + " requires a valid humanoid avatar");
            animator.runtimeAnimatorController = null; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true; animator.Rebind();
            var allSkins=source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var excluded=new HashSet<Renderer>();
            foreach(var lod in source.GetComponentsInChildren<LODGroup>()){var levels=lod.GetLODs();for(int level=1;level<levels.Length;level++)foreach(var renderer in levels[level].renderers)excluded.Add(renderer);UnityEngine.Object.DestroyImmediate(lod);}
            var skins = allSkins.Where(s=>s.gameObject.activeInHierarchy&&s.enabled&&s.sharedMesh!=null&&!excluded.Contains(s)).ToArray();
            Transform[] limbRoots = LimbRoots.Select(animator.GetBoneTransform).ToArray();
            if (limbRoots.Any(b => b == null)) throw new Exception(name + " missing humanoid limb roots");
            Transform[] bones; Material[] materials;
            Mesh merged = Merge(skins, root.transform, name, out bones, out materials);
            int[] boneRegions = bones.Select(b => RegionFor(b, limbRoots)).ToArray();
            int[] groups = merged.boneWeights.Select(w => boneRegions[Dominant(w)]).ToArray();
            var zombie = root.AddComponent<ImportedZombie>();
            zombie.preserveSourceAppearance = true; zombie.animationRig = animator; zombie.parts = new SkinnedMeshRenderer[6];
            zombie.hitBones = bones; zombie.hitBoneRegions = boneRegions;
            zombie.severJoints = new[] { animator.GetBoneTransform(HumanBodyBones.Neck), limbRoots[1], limbRoots[2], limbRoots[3], limbRoots[4] };
            zombie.walkClip = Clip("Z_Walk_InPlace"); zombie.attackClip = Clip("Z_Attack"); zombie.deathClip = Clip("Z_FallingBack");
            for (int part = 0; part < 6; part++)
            {
                var mesh = DepotAssetImport.Extract(merged, groups, part); mesh.name = name + " region " + part;
                string path = Output + "/" + name + "-region-" + part + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) AssetDatabase.CreateAsset(mesh, path);
                else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
                var obj = new GameObject(new[] { "Head", "Left arm", "Right arm", "Left leg", "Right leg", "Torso" }[part]); obj.transform.SetParent(root.transform, false);
                var skin = obj.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = mesh; skin.sharedMaterials = materials;
                skin.bones = bones; skin.rootBone = animator.GetBoneTransform(HumanBodyBones.Hips); skin.localBounds = merged.bounds; skin.updateWhenOffscreen = true; zombie.parts[part] = skin;
            }
            foreach (var skin in allSkins) UnityEngine.Object.DestroyImmediate(skin);
            // Humanoid retargeting can change the imported bind-pose scale.
            // Normalize against the evaluated pose, rather than raw FBX vertices.
            zombie.SamplePose(zombie.walkClip, 0);
            Bounds posed = BakedBounds(zombie);
            root.transform.localScale = Vector3.one * (1.8f / posed.size.y);
            UnityEngine.Object.DestroyImmediate(merged);
            PrefabUtility.SaveAsPrefabAsset(root, Output + "/" + name + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static AnimationClip Clip(string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Tiger + "/Animations/" + name + ".anim");
        if (clip == null || !clip.humanMotion) throw new Exception("Missing humanoid zombie animation " + name); return clip;
    }
    static int RegionFor(Transform bone, Transform[] roots)
    {
        for (int i = 0; i < roots.Length; i++) if (bone == roots[i] || bone.IsChildOf(roots[i])) return i; return 5;
    }
    static int Dominant(BoneWeight w)
    {
        int index = w.boneIndex0; float value = w.weight0;
        if (w.weight1 > value) { index = w.boneIndex1; value = w.weight1; }
        if (w.weight2 > value) { index = w.boneIndex2; value = w.weight2; }
        if (w.weight3 > value) index = w.boneIndex3; return index;
    }
    static Mesh Merge(SkinnedMeshRenderer[] skins, Transform root, string name, out Transform[] bones, out Material[] materials, bool preserveMaterials = false)
    {
        var allBones = new List<Transform>(); var bindposes = new List<Matrix4x4>();
        var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var tangents = new List<Vector4>(); var weights = new List<BoneWeight>();
        var triangles = new List<int[]>(); var mats = new List<Material>();
        foreach (var skin in skins)
        {
            Mesh mesh = skin.sharedMesh; int start = vertices.Count;
            Matrix4x4 matrix = root.worldToLocalMatrix * skin.transform.localToWorldMatrix, normalMatrix = matrix.inverse.transpose;
            int[] remap = new int[skin.bones.Length];
            for (int i = 0; i < remap.Length; i++)
            {
                // Distinct source skins can use different bind poses for one bone.
                remap[i] = allBones.Count; allBones.Add(skin.bones[i]); bindposes.Add(mesh.bindposes[i] * matrix.inverse);
            }
            var positions = mesh.vertices; var n = mesh.normals; var u = mesh.uv; var t = mesh.tangents; var w = mesh.boneWeights;
            for (int i = 0; i < positions.Length; i++)
            {
                vertices.Add(matrix.MultiplyPoint3x4(positions[i])); normals.Add(normalMatrix.MultiplyVector(n[i]).normalized); uv.Add(u[i]);
                Vector3 tangent = t.Length == positions.Length ? matrix.MultiplyVector(new Vector3(t[i].x, t[i].y, t[i].z)).normalized : Vector3.right;
                tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, t.Length == positions.Length ? t[i].w : 1));
                BoneWeight bw = w[i]; bw.boneIndex0 = remap[bw.boneIndex0]; bw.boneIndex1 = remap[bw.boneIndex1]; bw.boneIndex2 = remap[bw.boneIndex2]; bw.boneIndex3 = remap[bw.boneIndex3]; weights.Add(bw);
            }
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                triangles.Add(mesh.GetTriangles(sub).Select(i => i + start).ToArray());
                var sourceMaterial = skin.sharedMaterials[sub];
                mats.Add(preserveMaterials ? sourceMaterial : ConvertMaterial(sourceMaterial, name));
            }
        }
        var result = new Mesh { name = name + " merged", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        result.SetVertices(vertices); result.SetNormals(normals); result.SetUVs(0, uv); result.SetTangents(tangents); result.boneWeights = weights.ToArray(); result.bindposes = bindposes.ToArray();
        result.subMeshCount = triangles.Count; for (int i = 0; i < triangles.Count; i++) result.SetTriangles(triangles[i], i);
        result.RecalculateBounds(); bones = allBones.ToArray(); materials = mats.ToArray(); return result;
    }
    static Material ConvertMaterial(Material original, string model)
    {
        if (original == null) throw new Exception(model + " has an unassigned source material");
        string path = Output + "/" + model + "-" + original.name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path); bool create=material==null;
        if(create)material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = model + " " + original.name };
        material.shader=Shader.Find("Universal Render Pipeline/Lit");
        material.SetColor("_BaseColor", original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white);
        string baseProperty=original.HasProperty("_BaseMap")&&original.GetTexture("_BaseMap")!=null?"_BaseMap":"_MainTex";
        foreach (var pair in new[] { new[] { baseProperty, "_BaseMap" }, new[] { "_BumpMap", "_BumpMap" }, new[] { "_MetallicGlossMap", "_MetallicGlossMap" }, new[] { "_OcclusionMap", "_OcclusionMap" }, new[] { "_SpecGlossMap", "_SpecGlossMap" } })
            if (original.HasProperty(pair[0])) { material.SetTexture(pair[1], original.GetTexture(pair[0]));material.SetTextureScale(pair[1],original.GetTextureScale(pair[0]));material.SetTextureOffset(pair[1],original.GetTextureOffset(pair[0])); }
        if (material.GetTexture("_BumpMap") != null) material.EnableKeyword("_NORMALMAP");
        if (material.GetTexture("_MetallicGlossMap") != null) material.EnableKeyword("_METALLICSPECGLOSSMAP");
        if (material.GetTexture("_SpecGlossMap") != null) { material.SetFloat("_WorkflowMode", 0); material.EnableKeyword("_SPECULAR_SETUP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); }
        if (material.GetTexture("_OcclusionMap") != null) material.EnableKeyword("_OCCLUSIONMAP");
        material.SetFloat("_Smoothness", original.HasProperty("_Smoothness")?original.GetFloat("_Smoothness"):original.HasProperty("_Glossiness")?original.GetFloat("_Glossiness"):.25f); material.SetFloat("_Metallic", original.HasProperty("_Metallic")?original.GetFloat("_Metallic"):0);
        if (original.HasProperty("_Mode") && original.GetFloat("_Mode") > 0) { material.SetFloat("_AlphaClip", 1); material.SetFloat("_Cutoff", .35f); material.EnableKeyword("_ALPHATEST_ON"); material.SetOverrideTag("RenderType", "TransparentCutout"); material.renderQueue = 2450; }
        if(create)AssetDatabase.CreateAsset(material, path);else EditorUtility.SetDirty(material); return material;
    }
}
