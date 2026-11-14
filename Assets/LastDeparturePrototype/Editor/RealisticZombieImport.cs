using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static partial class RealisticZombieImport
{
    public const string Modern = "Assets/LastDeparturePrototype/ThirdParty/ModernZombie";
    public const string Tiger = "Assets/LastDeparturePrototype/ThirdParty/PxltigerZombie";
    [MenuItem("Tools/Yard Survival/Inspect new zombies")]
    public static void Inspect()
    {
        var report = new StringBuilder();
        foreach (string path in new[] { Modern + "/ZombieRig.fbx", Modern + "/ZombieRig.prefab", Tiger + "/Prefabs/Zombie3.prefab" })
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) { report.AppendLine("MISSING " + path); continue; }
            var obj = UnityEngine.Object.Instantiate(source);
            report.AppendLine("MODEL " + path);
            var animator = obj.GetComponentInChildren<Animator>();
            report.AppendLine("AVATAR " + (animator != null && animator.avatar != null ? animator.avatar.isValid + " human=" + animator.avatar.isHuman : "none"));
            if (animator != null && animator.isHuman) { animator.enabled = true; animator.Rebind(); foreach (var part in LimbRoots) report.AppendLine("HUMAN " + part + " " + animator.GetBoneTransform(part)); }
            foreach (var skin in obj.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                report.AppendLine("SKIN " + skin.name + " vertices=" + skin.sharedMesh.vertexCount + " bounds=" + skin.bounds + " scale=" + skin.transform.lossyScale);
                report.AppendLine("BONES " + string.Join(",", skin.bones.Select(b => b == null ? "NULL" : b.name)));
                foreach (var mat in skin.sharedMaterials)
                {
                    report.AppendLine("MATERIAL " + (mat == null ? "NULL" : mat.name));
                    if (mat != null) foreach (string prop in mat.GetTexturePropertyNames()) if (mat.GetTexture(prop) != null) report.AppendLine(prop + "=" + AssetDatabase.GetAssetPath(mat.GetTexture(prop)));
                }
            }
            UnityEngine.Object.DestroyImmediate(obj);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { Tiger }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview"))) report.AppendLine("CLIP " + path + " " + clip.name + " human=" + clip.humanMotion + " duration=" + clip.length);
        }
        File.WriteAllText("Assets/LastDeparturePrototype/NewZombiesInspection.log", report.ToString());
        Debug.Log("New zombie inspection saved.");
    }
}
