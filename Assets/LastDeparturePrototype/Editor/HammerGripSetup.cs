using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class HammerGripSetup
{
    const string PrefabPath = "Assets/LastDeparturePrototype/ThirdParty/Hammer/PlayableHammer.prefab";
    const string ReviewPath = "Assets/LastDeparturePrototype/HammerGripReview";
    const string RequestPath = "Assets/LastDeparturePrototype/HammerGrip.request";
    static HammerGripSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(RequestPath);
        try { RepairAndReview(); }
        catch (Exception ex) { Directory.CreateDirectory(ReviewPath); File.WriteAllText(ReviewPath + "/Result.txt", "FAIL " + ex); Debug.LogException(ex); }
    }
    static Transform Grip(AnimatedWeaponRig rig) => rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Hammer grip");
    static void Rest(AnimatedWeaponRig rig)
    {
        var clip = rig.idle != null ? rig.idle : rig.fire;
        var range = rig.idle != null ? rig.idleFrames : rig.fireFrames;
        if (clip == null) throw new InvalidOperationException("Hammer requires its authored hand pose.");
        clip.SampleAnimation(rig.model, range.y > range.x ? range.x / clip.frameRate : 0);
    }
    static Vector3 HandleAxis(Transform grip)
    {
        var renderers = grip.GetComponentsInChildren<Renderer>();
        return (renderers.Single(r => r.name == "tools_1_low").bounds.center - renderers.Single(r => r.name == "tools_3_low").bounds.center).normalized;
    }
    // Calibrate once in the authored rest pose. The stored local rotation then
    // follows the palm naturally; it is not forced upright during a strike.
    public static void Fit(AnimatedWeaponRig rig)
    {
        Rest(rig);
        var grip = Grip(rig);
        grip.rotation = Quaternion.FromToRotation(HandleAxis(grip), rig.transform.up) * grip.rotation;
        // The head's local X axis points toward the striking face.
        // Upright alone left that face pointing sideways across the screen.
        var head = grip.GetComponentsInChildren<Renderer>().Single(r => r.name == "tools_1_low");
        Vector3 face = Vector3.ProjectOnPlane(head.transform.right, rig.transform.up).normalized;
        if(face.sqrMagnitude < .5f) throw new InvalidOperationException("Hammer head axis is not horizontal.");
        grip.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(face, rig.transform.forward, rig.transform.up),rig.transform.up) * grip.rotation;
        if (Vector3.Angle(HandleAxis(grip), rig.transform.up) > 1)
            throw new InvalidOperationException("Hammer handle could not be aligned upright.");
    }
    [MenuItem("Tools/Yard Survival/Repair and review upright hammer grip")]
    public static void RepairAndReview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before repairing the hammer prefab.");
        Directory.CreateDirectory(ReviewPath);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var rig = root.GetComponent<AnimatedWeaponRig>();
            Rest(rig);
            var report = new StringBuilder("Hammer grip calibration " + DateTime.Now.ToString("s") + "\n");
            report.AppendLine("Before handle/up angle: " + Vector3.Angle(HandleAxis(Grip(rig)), rig.transform.up));
            Capture(root, "Before", 1);
            Fit(rig);
            report.AppendLine("After handle/up angle: " + Vector3.Angle(HandleAxis(Grip(rig)), rig.transform.up));
            report.AppendLine("Saved palm-local grip rotation: " + Grip(rig).localRotation);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            foreach (var pose in new[] { ("Idle", 1f), ("Windup", .08f), ("Impact", .18f), ("Recovery", .4f) })
                Capture(root, pose.Item1, pose.Item2);
            report.AppendLine("PASS: upright resting handle; prefab saved; original hand attachment and swing retained. Inspect rendered poses.");
            File.WriteAllText(ReviewPath + "/Result.txt", report.ToString());
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static void Capture(GameObject prefab, string name, float age)
    {
        var stage = new GameObject("Hammer grip review stage"); stage.transform.position = new Vector3(300, 300, 300);
        var cameraObject = new GameObject("Hammer grip review camera");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.transform.position = stage.transform.position; camera.transform.rotation = Quaternion.identity;
        camera.fieldOfView = 70; camera.nearClipPlane = .035f; camera.farClipPlane = 5;
        camera.cullingMask = 1 << 29; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .16f, .19f);
        cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        var target = new RenderTexture(1200, 750, 24); var image = new Texture2D(1200, 750, TextureFormat.RGB24, false);
        var previous = RenderTexture.active; bool fog = RenderSettings.fog;
        try
        {
            RenderSettings.fog = false;
            var obj = UnityEngine.Object.Instantiate(prefab, stage.transform);
            var rig = obj.GetComponent<AnimatedWeaponRig>(); obj.transform.localPosition = rig.hipPosition; obj.transform.localRotation = Quaternion.identity;
            foreach (var transform in obj.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 29;
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>()) renderer.renderingLayerMask = 1;
            foreach (var animator in obj.GetComponentsInChildren<Animator>()) animator.enabled = false;
            var swing = obj.AddComponent<HeldHammerSwing>(); swing.Initialize(rig); swing.Pose(age);
            var light = new GameObject("Hammer preview light").AddComponent<Light>(); light.transform.SetParent(stage.transform, false);
            light.transform.localPosition = new Vector3(-.4f, .6f, -.4f); light.type = LightType.Point; light.range = 5; light.intensity = 12; light.cullingMask = 1 << 29;
            target.Create();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1200, 750), 0, 0); image.Apply();
            File.WriteAllBytes(ReviewPath + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            RenderSettings.fog = fog; RenderTexture.active = previous; target.Release();
            UnityEngine.Object.DestroyImmediate(stage); UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
