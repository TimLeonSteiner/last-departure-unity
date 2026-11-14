using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Imports the original Meshy animation bundles without retargeting their skeletons.
[InitializeOnLoad]
public static partial class MeshyCharacterImport
{
    const string Root = "Assets/BoxheadPrototype/Characters/Meshy/";
    const string Request = "Assets/Verification~/ImportMeshyCharacters.request";
    const string ReportRoot = "Assets/Verification~/MeshyCharacters/";
    static MeshyCharacterImport() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        if (command == "inventory") BuildInventory();
        else if (command == "combat") RealisticZombieImport.PrepareMeshyCombatPrefabs();
        else if (command == "charger") BuildCharger();
        else if (command == "dockworker") BuildDockworker();
        else Build();
    }

    [MenuItem("Tools/Yard Survival/Import Meshy Charger bundle")]
    public static void BuildCharger()
    {
        Directory.CreateDirectory(ReportRoot);
        var report = new StringBuilder();
        try
        {
            Import("Charger", 8, report);
            RealisticZombieImport.PrepareMeshyChargerCombatPrefab();
            AssetDatabase.SaveAssets();
            report.AppendLine("PASS: Charger combat prefab saved with walk, shoulder rush, crawl and death.");
        }
        catch (Exception error)
        {
            report.AppendLine("FAIL: " + error);
            Debug.LogException(error);
        }
        File.WriteAllText(ReportRoot + "ChargerImport.txt", report.ToString());
    }
    public static void BuildInventory()
    {
        Directory.CreateDirectory(ReportRoot);
        var report = new StringBuilder();
        try
        {
            Import("InventorySurvivor", 1, report);
            const string folder = Root + "InventorySurvivor/";
            var sourcePath = Directory.GetFiles(folder + "Source", "*.fbx").Single().Replace('\\', '/');
            var sourceClip = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));
            var clip = UnityEngine.Object.Instantiate(sourceClip);
            clip.name = "Survivor Inventory Idle";
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true; settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.wrapMode = WrapMode.Loop;
            const string clipPath = folder + "InventoryIdle.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing == null) AssetDatabase.CreateAsset(clip, clipPath);
            else { EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); clip = existing; }
            var root = new GameObject("Survivor inventory character");
            try
            {
                var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(folder + "InventorySurvivor.prefab"), root.transform);
                var rig = model.GetComponent<Animator>(); rig.runtimeAnimatorController = null;
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                var bounds = skins[0].bounds; foreach (var skin in skins) bounds.Encapsulate(skin.bounds);
                model.transform.localScale *= 1.8f / bounds.size.y;
                bounds = skins[0].bounds; foreach (var skin in skins) bounds.Encapsulate(skin.bounds);
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var avatar = root.AddComponent<YardSurvivalPrototype.HumanoidPlayerAvatar>();
                avatar.rig = rig; avatar.idle = clip;
                Directory.CreateDirectory("Assets/BoxheadPrototype/Resources/Inventory"); AssetDatabase.Refresh();
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/BoxheadPrototype/Resources/Inventory/Survivor.prefab");
                avatar.Tick(.37f,0,0,false);
                var bones = rig.GetComponentsInChildren<Transform>();
                var pose = bones.Select(b => b.localRotation).ToArray();
                avatar.Tick(clip.length,0,0,false);
                float error = bones.Select((b,i) => Quaternion.Angle(pose[i],b.localRotation)).Max();
                if (error > .1f) throw new Exception("Idle failed to repeat its pose after one duration: " + error);
                report.AppendLine("PASS: Native Meshy idle repeats after " + clip.length + " seconds; loop enabled; Resources/Inventory/Survivor prefab ready.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
        }
        catch (Exception e) { report.AppendLine("FAIL: " + e); Debug.LogException(e); }
        File.WriteAllText(ReportRoot + "InventoryImport.txt", report.ToString());
    }
    [MenuItem("Tools/Yard Survival/Import Meshy character bundles")]
    public static void Build()
    {
        Directory.CreateDirectory(ReportRoot);
        var report = new StringBuilder();
        try
        {
            Import("ArmoredWorker", 7, report);
            Import("Spitter", 8, report);
            Import("Screamer", 9, report);
            Import("SurvivorReview", 1, report);
            AssetDatabase.SaveAssets();
            report.AppendLine("PASS: All four prefabs and their source animations imported. These are review prefabs, not a gameplay roster replacement.");
            report.AppendLine("OPEN: Survivor shoulder deformation is present in Meshy itself; this review prefab is not approved as the final player.");
        }
        catch (Exception e) { report.AppendLine("FAIL: " + e); Debug.LogException(e); }
        File.WriteAllText(ReportRoot + "ImportReport.txt", report.ToString());
    }
    static void Import(string name, int expectedClips, StringBuilder report)
    {
        string folder = Root + name + "/";
        string path = Directory.GetFiles(folder + "Source", "*.fbx").Single().Replace('\\','/');
        string stem = path.Substring(0, path.Length - 4);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.isReadable = true;
        var settings = importer.defaultClipAnimations;
        foreach (var setting in settings)
        {
            var lower = setting.name.ToLowerInvariant();
            setting.loopTime = lower.Contains("idle") || lower.Contains("walk") || lower.Contains("running");
        }
        importer.clipAnimations = settings;
        importer.SaveAndReimport();
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        if (clips.Length != expectedClips) throw new Exception(name + ": expected " + expectedClips + " clips, found " + clips.Length);
        var baseMap = Texture(stem + ".png", false);
        var normal = Texture(stem + "_normal.png", true);
        Texture(stem + "_metallic.png", false);
        Texture(stem + "_roughness.png", false);
        var matPath = folder + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, matPath); }
        material.SetTexture("_BaseMap", baseMap);
        material.SetTexture("_BumpMap", normal);
        material.EnableKeyword("_NORMALMAP");
        material.SetFloat("_Smoothness", .22f);
        EditorUtility.SetDirty(material);
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        model.name = name == "SurvivorReview" ? "Survivor - SHOULDER FIX REQUIRED" : name;
        try
        {
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (skins.Length == 0 || skins.Any(s => s.sharedMesh == null || s.bones.Length == 0)) throw new Exception(name + ": missing skinned mesh or bones");
            foreach (var skin in skins) { skin.sharedMaterials = Enumerable.Repeat(material, skin.sharedMaterials.Length).ToArray(); skin.updateWhenOffscreen = true; }
            var animator = model.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isValid) throw new Exception(name + ": invalid generic avatar");
            var controllerPath = folder + name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            AnimationClip primary = clips.FirstOrDefault(c => c.name.IndexOf(name == "SurvivorReview" ? "idle" : "walking", StringComparison.OrdinalIgnoreCase) >= 0) ?? clips[0];
            foreach (var clip in clips)
            {
                var state = machine.AddState(clip.name); state.motion = clip;
                if (clip == primary) machine.defaultState = state;
                report.AppendLine(name + " clip: " + clip.name + " (" + clip.length.ToString("F2") + "s)");
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.SaveAsPrefabAsset(model, folder + name + ".prefab");
            bool renderPreview = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            if (renderPreview) Capture(model, primary, ReportRoot + name + ".png");
            report.AppendLine("PASS " + name + ": " + clips.Length + " clips, " + skins.Sum(s => s.sharedMesh.triangles.Length / 3) + " triangles; valid native skeleton and textured prefab." + (renderPreview ? " Preview rendered." : ""));
        }
        finally { UnityEngine.Object.DestroyImmediate(model); }
    }
    static Texture2D Texture(string path, bool normal)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) throw new Exception("Missing texture: " + path);
        return texture;
    }
    static void Capture(GameObject model, AnimationClip clip, string path, float sampleTime = -1)
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var previousTarget = RenderTexture.active;
        RenderTexture target = null; Texture2D image = null;
        try
        {
            SceneManager.SetActiveScene(scene);
            SceneManager.MoveGameObjectToScene(model, scene);
            var rig = model.GetComponentInChildren<Animator>();
            rig.enabled = false;
            clip.SampleAnimation(rig.gameObject, sampleTime < 0 ? Mathf.Min(.5f, clip.length * .25f) : Mathf.Clamp(sampleTime, 0, clip.length));
            foreach (var t in model.GetComponentsInChildren<Transform>()) t.gameObject.layer = 29;
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s => s.enabled).ToArray();
            var bounds = skins[0].bounds;
            foreach (var skin in skins) bounds.Encapsulate(skin.bounds);
            var camera = new GameObject("Meshy review camera").AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.x * 900f / 700f) * .62f;
            camera.cullingMask = 1 << 29;
            camera.transform.position = bounds.center + new Vector3(0, 0, Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * 3);
            camera.transform.LookAt(bounds.center);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f,.09f,.1f);
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var light = new GameObject("Review key light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 2.2f; light.cullingMask = 1 << 29;
            light.transform.rotation = Quaternion.Euler(30,150,0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.5f,.5f,.5f); RenderSettings.fog = false;
            target = new RenderTexture(700,900,24); target.Create();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image = new Texture2D(700,900,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,700,900),0,0); image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());
            SceneManager.MoveGameObjectToScene(model, previous);
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene,true);
        }
    }
}
