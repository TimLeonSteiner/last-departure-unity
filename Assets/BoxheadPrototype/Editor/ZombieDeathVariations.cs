using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class ZombieDeathVariations
{
    const string Root = "Assets/BoxheadPrototype/Resources/MeshyCombat/";
    const string Output = "Assets/BoxheadPrototype/Characters/DeathVariations";
    const string Report = "Assets/Verification~/ZombieDeathVariations.txt";
    static ZombieDeathVariations() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Library/ZombieDeathVariations.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Build(); } catch (Exception e) { File.WriteAllText(Report, "FAIL: " + e); Debug.LogException(e); }
    }

    static string Key(Transform bone) => bone.name.Replace("mixamorig:", "").Replace("FatZombie_", "");
    static Dictionary<string, Transform> Bones(ImportedZombie z) => z.parts.SelectMany(p => p.bones).Where(b => b != null).Distinct().GroupBy(Key).ToDictionary(g => g.Key, g => g.First());
    static ImportedZombie Spawn(string name) => UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + name + ".prefab")).GetComponent<ImportedZombie>();
    public static void AssignExisting(ImportedZombie zombie, string name)
    {
        var clips = new[] { "Backward", "Forward", "Collapse" }.Select(label => AssetDatabase.LoadAssetAtPath<AnimationClip>(Output + "/" + name + "-" + label + ".anim")).ToArray();
        if (clips.All(c => c != null)) zombie.deathClips = clips;
    }
    static AnimationClip FindClip(string model, string name)
    {
        var path = Directory.GetFiles("Assets/BoxheadPrototype/Characters/Meshy/" + model + "/Source", "*.fbx").Single();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => c.name == name);
    }

    [MenuItem("Tools/Yard Survival/Build three zombie death variations")]
    public static void Build()
    {
        Directory.CreateDirectory(Output);
        AssetDatabase.Refresh();
        var report = new StringBuilder();
        var names = new[] { "01-Dockworker-Shambler", "Spitter", "Screamer", "ArmoredWorker", "Charger", "04-Cargo-Brute" };
        var labels = new[] { "Backward", "Forward", "Collapse" };
        foreach (var name in names)
        {
            var clips = new AnimationClip[3];
            for (int variant = 0; variant < 3; variant++)
            {
                var donor = Spawn(variant == 2 ? "01-Dockworker-Shambler" : "Screamer");
                var target = Spawn(name);
                try
                {
                    var source = variant == 2 ? donor.deathClip : FindClip("Screamer", variant == 0 ? "dying_backwards" : "Shot_and_Fall_Forward");
                    if (target.animationRig.isHuman)
                        source = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/BoxheadPrototype/ThirdParty/PxltigerZombie/Animations/" + (variant == 1 ? "Z_FallingForward" : "Z_FallingBack") + ".anim");
                    clips[variant] = Bake(donor, target, source, name + "-" + labels[variant], report);
                }
                finally { UnityEngine.Object.DestroyImmediate(donor.gameObject); UnityEngine.Object.DestroyImmediate(target.gameObject); }
            }
            var prefab = PrefabUtility.LoadPrefabContents(Root + name + ".prefab");
            try
            {
                prefab.GetComponent<ImportedZombie>().deathClips = clips;
                PrefabUtility.SaveAsPrefabAsset(prefab, Root + name + ".prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText(Report, report.ToString());
    }

    static AnimationClip Bake(ImportedZombie donor, ImportedZombie target, AnimationClip source, string name, StringBuilder report)
    {
        RestoreBindPose(donor);
        RestoreBindPose(target);
        var from = Bones(donor);
        var to = Bones(target);
        // Bind-space world rotation deltas preserve each model's bone axes and proportions.
        var pairs = to.Where(p => from.ContainsKey(p.Key)).OrderBy(p => Depth(p.Value)).ToArray();
        if (!to.ContainsKey("Hips") || pairs.Length < 15) throw new Exception(name + " has an incompatible skeleton");
        var sourceRest = pairs.Select(p => from[p.Key].rotation).ToArray();
        var targetRest = pairs.Select(p => p.Value.rotation).ToArray();
        var sourceHips = from["Hips"].position;
        var targetHips = to["Hips"].position;
        float scale = Vector3.Distance(to["Hips"].position, to["Head"].position) / Vector3.Distance(from["Hips"].position, from["Head"].position);
        var bones = to.Values.OrderBy(Depth).ToArray();
        var curves = bones.Select(b => Enumerable.Range(0, 7).Select(_ => new AnimationCurve()).ToArray()).ToArray();
        var clip = new AnimationClip { name = name, legacy = true, frameRate = 30, wrapMode = WrapMode.ClampForever };
        var mesh = new Mesh();
        float duration = Mathf.Min(source.length - .01f, 6.5f);
        int frames = Mathf.CeilToInt(duration * 30);
        float initialY = 0, finalY = 0;
        try
        {
            for (int frame = 0; frame <= frames; frame++)
            {
                float time = duration * frame / frames;
                if (source.humanMotion && target.animationRig.isHuman)
                {
                    target.SamplePose(source, time);
                    if (name.EndsWith("-Collapse"))
                    {
                        float weight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.15f, .85f, time / duration));
                        to["Hips"].rotation = Quaternion.AngleAxis(65 * weight, target.transform.forward) * to["Hips"].rotation;
                        to["Hips"].position += target.transform.right * (.35f * weight);
                    }
                }
                else
                {
                    donor.SamplePose(source, time);
                    for (int i = 0; i < pairs.Length; i++)
                        pairs[i].Value.rotation = from[pairs[i].Key].rotation * Quaternion.Inverse(sourceRest[i]) * targetRest[i];
                    to["Hips"].position = targetHips + (from["Hips"].position - sourceHips) * scale;
                }
                float min = float.PositiveInfinity;
                foreach (var skin in target.parts)
                {
                    skin.BakeMesh(mesh);
                    foreach (var vertex in mesh.vertices) min = Mathf.Min(min, skin.transform.TransformPoint(vertex).y);
                }
                // Keep the body above the floor, including broad torsos and outstretched hands.
                if (min < .015f) to["Hips"].position += Vector3.up * (.015f - min);
                if (frame == 0) initialY = to["Hips"].position.y;
                if (frame == frames) finalY = to["Hips"].position.y;
                for (int i = 0; i < bones.Length; i++)
                {
                    Vector3 p = bones[i].localPosition; Quaternion q = bones[i].localRotation;
                    float[] values = { p.x, p.y, p.z, q.x, q.y, q.z, q.w };
                    for (int c = 0; c < 7; c++) curves[i][c].AddKey(time, values[c]);
                }
            }
            string[] properties = { "localPosition.x", "localPosition.y", "localPosition.z", "localRotation.x", "localRotation.y", "localRotation.z", "localRotation.w" };
            for (int i = 0; i < bones.Length; i++)
                for (int c = 0; c < 7; c++) clip.SetCurve(AnimationUtility.CalculateTransformPath(bones[i], target.transform), typeof(Transform), properties[c], curves[i][c]);
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false; AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (!float.IsFinite(finalY) || finalY >= initialY * .85f) throw new Exception(name + " does not settle: " + initialY + " -> " + finalY);
            string path = Output + "/" + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) { EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); clip = existing; }
            else AssetDatabase.CreateAsset(clip, path);
            report.AppendLine("PASS " + name + ": " + pairs.Length + " mapped bones, " + duration.ToString("F2") + "s, hips " + initialY.ToString("F2") + " -> " + finalY.ToString("F2"));
            return clip;
        }
        finally { UnityEngine.Object.DestroyImmediate(mesh); }
    }
    static int Depth(Transform t) { int depth = 0; while (t.parent != null) { depth++; t = t.parent; } return depth; }
    static void RestoreBindPose(ImportedZombie zombie)
    {
        // Prefabs can be saved mid-animation. Use skin bind matrices, not that preview pose.
        var matrices = new Dictionary<Transform, Matrix4x4>();
        foreach (var skin in zombie.parts)
        {
            var bind = skin.sharedMesh.bindposes;
            for (int i = 0; i < skin.bones.Length; i++)
                if (skin.bones[i] != null) matrices[skin.bones[i]] = skin.transform.localToWorldMatrix * bind[i].inverse;
        }
        foreach (var entry in matrices.OrderBy(p => Depth(p.Key)))
            entry.Key.SetPositionAndRotation(entry.Value.GetColumn(3), entry.Value.rotation);
    }
}
