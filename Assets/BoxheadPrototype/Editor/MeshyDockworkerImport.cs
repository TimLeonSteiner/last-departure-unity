using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static partial class MeshyCharacterImport
{
    [MenuItem("Tools/Yard Survival/Import Meshy Dockworker bundle")]
    public static void BuildDockworker()
    {
        Directory.CreateDirectory(ReportRoot);
        var report = new StringBuilder();
        try
        {
            Import("Dockworker", 7, report);
            string folder = Root + "Dockworker/";
            string sourcePath = Directory.GetFiles(folder + "Source", "*.fbx").Single().Replace('\\', '/');
            var importer = (ModelImporter)AssetImporter.GetAtPath(sourcePath);
            var settings = importer.clipAnimations;
            foreach (var setting in settings)
            {
                bool loop = setting.name == MeshyDockworkerMotions.Idle || setting.name == MeshyDockworkerMotions.Walk || setting.name == MeshyDockworkerMotions.Crawl || setting.name == "Walking" || setting.name == "Running";
                setting.loopTime = loop;
                setting.loopPose = loop;
            }
            importer.clipAnimations = settings;
            importer.SaveAndReimport();
            RealisticZombieImport.PrepareMeshyDockworkerCombatPrefab();
            var clips = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().ToArray();
            for (int i = 0; i < MeshyDockworkerMotions.Ids.Length; i++)
            {
                var clip = clips.Single(c => c.name == MeshyDockworkerMotions.Ids[i]);
                for (int frame = 0; frame < 3; frame++)
                {
                    var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BoxheadPrototype/Resources/MeshyCombat/01-Dockworker-Shambler.prefab"));
                    try { Capture(model, clip, ReportRoot + "Dockworker-" + MeshyDockworkerMotions.Labels[i] + "-" + frame + ".png", clip.length * (.05f + frame * .45f)); }
                    finally { UnityEngine.Object.DestroyImmediate(model); }
                }
                report.AppendLine("Mapped " + MeshyDockworkerMotions.Labels[i] + " = " + clip.name);
            }
            AssetDatabase.SaveAssets();
            report.AppendLine("PASS: Native Meshy Dockworker source, seven clips, five custom combat motions, and six detachable regions imported. Encounter name preserved.");
        }
        catch (Exception error)
        {
            report.AppendLine("FAIL: " + error);
            Debug.LogException(error);
        }
        File.WriteAllText(ReportRoot + "DockworkerImport.txt", report.ToString());
    }
}

public static class MeshyDockworkerMotions
{
    // UUIDs are the five Motion Prime jobs created on 2026-11-14, in generation order.
    public const string Walk = "_01a1d975_cda4_721a_9455_6d52c1d37faf";
    public const string Idle = "_01a1d979_7844_72e9_ae72_02556eb3b60f";
    public const string Attack = "_01a1d97b_b592_771a_ba3d_9cd0924f87c1";
    public const string Death = "_01a1d97d_4eec_76c6_9743_284598b6c1f2";
    public const string Crawl = "_01a1d97f_4f23_73d9_aee4_b65cdb37df5d";
    public static readonly string[] Ids = { Idle, Walk, Attack, Death, Crawl };
    public static readonly string[] Labels = { "Idle", "Shamble", "Melee", "Death", "Crawl" };
}

public static partial class RealisticZombieImport
{
    public static void PrepareMeshyDockworkerCombatPrefab()
    {
        Directory.CreateDirectory(MeshyCombatResources);
        AssetDatabase.Refresh();
        string oldPath = MeshyCombatResources + "/Dockworker.prefab";
        string newPath = MeshyCombatResources + "/01-Dockworker-Shambler.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(oldPath) != null && AssetDatabase.LoadAssetAtPath<GameObject>(newPath) == null)
        {
            string error = AssetDatabase.MoveAsset(oldPath, newPath);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        BuildMeshyCombatPrefab("Dockworker", YardSurvivalPrototype.ZombieKind.Shambler,
            MeshyDockworkerMotions.Walk, MeshyDockworkerMotions.Attack, MeshyDockworkerMotions.Crawl,
            MeshyDockworkerMotions.Death, MeshyDockworkerMotions.Idle, "01-Dockworker-Shambler", "Running");
    }
}
