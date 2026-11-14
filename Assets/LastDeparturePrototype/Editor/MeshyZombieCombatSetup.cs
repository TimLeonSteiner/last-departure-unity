using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

public static partial class RealisticZombieImport
{
    const string MeshyRoot = "Assets/LastDeparturePrototype/Characters/Meshy/";
    const string MeshyCombatResources = "Assets/LastDeparturePrototype/Resources/MeshyCombat";

    [MenuItem("Tools/Yard Survival/Prepare Meshy combat prefabs")]
    public static void PrepareMeshyCombatPrefabs()
    {
        Directory.CreateDirectory(MeshyCombatResources);
        AssetDatabase.Refresh();
        BuildMeshyCombatPrefab("ArmoredWorker", ZombieKind.Brute, "Slow_Orc_Walk",
            "_01a1cb5f_2bca_709e_b1e2_574a462dd5ec", "Crawl_and_Look_Back",
            "_01a1cb60_a985_7155_b3ef_15917a614c9a");
        BuildMeshyCombatPrefab("Spitter", ZombieKind.Limper, "Limping_Walk_1",
            "_01a1cb55_dd5f_7623_bb69_f614ee794d19", "_01a1cb59_388d_731b_9256_81f270cbad14",
            "_01a1cb57_a453_748b_ad9e_155347594cc0");
        BuildMeshyCombatPrefab("Screamer", ZombieKind.Twitcher, "Injured_Walk",
            "_01a1cb52_f616_77fa_a128_f56b97230fe9", "_01a1cb6c_0c20_730d_a0f7_0a523ac3d18b",
            "Shot_and_Fall_Forward");
        AssetDatabase.SaveAssets();
        Debug.Log("Meshy combat prefabs ready: ArmoredWorker, Spitter, Screamer.");
    }

    public static void PrepareMeshyChargerCombatPrefab()
    {
        Directory.CreateDirectory(MeshyCombatResources);
        AssetDatabase.Refresh();
        BuildMeshyCombatPrefab("Charger", ZombieKind.Brute, "Walking",
            "_01a1cb2c_a45f_72db_8273_5b2089c68c7c", "Crawl_and_Look_Back", "dying_backwards");
    }

    static void BuildMeshyCombatPrefab(string name, ZombieKind kind, string walk, string attack, string crawl, string death, string idle = null, string encounterName = null, string run = null)
    {
        string folder = MeshyRoot + name + "/";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".prefab");
        if (source == null) throw new InvalidOperationException("Missing Meshy prefab: " + name);
        var sourcePath = Directory.GetFiles(folder + "Source", "*.fbx").Single().Replace('\\', '/');
        var clips = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().ToArray();
        AnimationClip ClipByName(string clipName) => clips.Single(clip => clip.name == clipName);
        var root = new GameObject(encounterName ?? name);
        Mesh merged = null;
        try
        {
            var rig = UnityEngine.Object.Instantiate(source, root.transform);
            var animator = rig.GetComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            var transforms = rig.GetComponentsInChildren<Transform>(true);
            Transform Bone(string boneName) => transforms.Single(t => t.name == "mixamorig:" + boneName);
            var limbRoots = new[] { Bone("Head"), Bone("LeftArm"), Bone("RightArm"), Bone("LeftUpLeg"), Bone("RightUpLeg") };
            var sourceSkins = rig.GetComponentsInChildren<SkinnedMeshRenderer>();
            merged = Merge(sourceSkins, root.transform, name, out var bones, out var materials, preserveMaterials: true);
            var regions = bones.Select(bone => RegionFor(bone, limbRoots)).ToArray();
            var vertexRegions = merged.boneWeights.Select(weight => regions[Dominant(weight)]).ToArray();
            var zombie = root.AddComponent<ImportedZombie>();
            root.AddComponent<ZombieArchetype>().kind = kind;
            if (name == "ArmoredWorker")
                zombie.encounterPoseClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/LastDeparturePrototype/ThirdParty/PxltigerZombie/Animations/Z_Idle.anim");
            zombie.preserveSourceAppearance = true;
            zombie.animationRig = animator;
            zombie.hitBones = bones;
            zombie.hitBoneRegions = regions;
            zombie.severJoints = new[] { Bone("Neck"), limbRoots[1], limbRoots[2], limbRoots[3], limbRoots[4] };
            zombie.walkClip = ClipByName(walk);
            zombie.runClip = run == null ? null : ClipByName(run);
            zombie.attackClip = ClipByName(attack);
            zombie.crawlClip = ClipByName(crawl);
            zombie.deathClip = ClipByName(death);
            zombie.idleClip = idle == null ? null : ClipByName(idle);
            zombie.parts = new SkinnedMeshRenderer[6];
            string[] partNames = { "Head", "Left arm", "Right arm", "Left leg", "Right leg", "Torso" };
            for (int part = 0; part < partNames.Length; part++)
            {
                var mesh = DepotAssetImport.Extract(merged, vertexRegions, part);
                mesh.name = name + " " + partNames[part];
                string path = folder + "CombatRegion" + part + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) AssetDatabase.CreateAsset(mesh, path);
                else
                {
                    EditorUtility.CopySerialized(mesh, existing);
                    UnityEngine.Object.DestroyImmediate(mesh);
                    mesh = existing;
                }
                var region = new GameObject(partNames[part]);
                region.transform.SetParent(root.transform, false);
                var renderer = region.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.sharedMaterials = materials;
                renderer.bones = bones;
                renderer.rootBone = Bone("Hips");
                renderer.localBounds = merged.bounds;
                renderer.updateWhenOffscreen = true;
                zombie.parts[part] = renderer;
            }
            foreach (var skin in sourceSkins) skin.enabled = false;
            ZombieDeathVariations.AssignExisting(zombie, encounterName ?? name);
            PrefabUtility.SaveAsPrefabAsset(root, MeshyCombatResources + "/" + (encounterName ?? name) + ".prefab");
        }
        finally
        {
            if (merged != null) UnityEngine.Object.DestroyImmediate(merged);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
