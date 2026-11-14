using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class ZombieDeathVerification
{
    const string Request = "Library/VerifyZombieDeaths.request";
    const string Pending = "ZombieDeathVerification.Pending";
    const string Folder = "Assets/Verification~/ZombieDeaths/";
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static ZombieDeathVerification()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false); EditorApplication.delayCall += Run;
        };
    }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(Request);
        if (EditorApplication.isPlaying) Run();
        else { SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true; }
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Pose(ImportedZombie z, float seconds)
    {
        typeof(ImportedZombie).GetField("deathAt", Private).SetValue(z, Time.time - seconds);
        typeof(ImportedZombie).GetField("transitionRemaining", Private).SetValue(z, 0f);
        z.SendMessage("Update");
    }
    static Bounds Bounds(ImportedZombie z)
    {
        var mesh = new Mesh(); var bounds = new Bounds(); bool first = true;
        try
        {
            foreach (var skin in z.parts.Where(p => p.enabled))
            {
                skin.BakeMesh(mesh);
                foreach (var v in mesh.vertices)
                {
                    var p = skin.transform.TransformPoint(v);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(mesh); }
        return bounds;
    }
    static void Run()
    {
        Directory.CreateDirectory(Folder);
        var report = new StringBuilder();
        var savedRandom = UnityEngine.Random.state;
        var savedAmbient = RenderSettings.ambientLight; var savedMode = RenderSettings.ambientMode; bool savedFog = RenderSettings.fog;
        GameObject stage = new GameObject("Death verification stage"); stage.transform.position = new Vector3(500, 0, 500);
        try
        {
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.gray; RenderSettings.fog = false;
            var light = new GameObject("Death review key").AddComponent<Light>(); light.transform.SetParent(stage.transform, false);
            light.type = LightType.Directional; light.intensity = 2.5f; light.cullingMask = 1 << 29; light.transform.rotation = Quaternion.Euler(40, 150, 0);
            var camera = new GameObject("Death review camera").AddComponent<Camera>(); camera.transform.SetParent(stage.transform, false);
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 1.65f; camera.cullingMask = 1 << 29;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f,.15f,.18f);
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var names = new[] { "01-Dockworker-Shambler", "Spitter", "Screamer", "ArmoredWorker", "Charger", "04-Cargo-Brute" };
            foreach (string name in names)
            {
                var prefab = Resources.Load<GameObject>("MeshyCombat/" + name);
                var config = prefab.GetComponent<ImportedZombie>();
                Check(config.deathClips.Length == 3 && config.deathClips.Distinct().Count() == 3 && config.deathClips.All(c => c != null && !c.isLooping), name + " clip configuration");
                var seeds = new int[3];
                for (int index = 0; index < 3; index++)
                {
                    seeds[index] = Enumerable.Range(0, 100).First(seed => { UnityEngine.Random.InitState(seed); return UnityEngine.Random.Range(0, 3) == index; });
                    var actor = new GameObject("Death actor"); actor.transform.SetParent(stage.transform, false);
                    var z = UnityEngine.Object.Instantiate(prefab, actor.transform).GetComponent<ImportedZombie>();
                    try
                    {
                        foreach (var t in z.GetComponentsInChildren<Transform>()) t.gameObject.layer = 29;
                        z.TickMovement(.2f, .2f, false);
                        UnityEngine.Random.InitState(seeds[index]); z.Die();
                        Check(z.ActiveDeathClip == config.deathClips[index] && z.RagdollBodyCount == 0, name + " selected authored death " + index);
                        z.Die(); Check(z.ActiveDeathClip == config.deathClips[index], "Repeated death changed selection");
                        float duration = z.ActiveDeathClip.length;
                        var sheet = new Texture2D(1200, 400, TextureFormat.RGB24, false);
                        for (int frame = 0; frame < 3; frame++)
                        {
                            Pose(z, duration * new[] { .05f, .55f, 1f }[frame]);
                            var bounds = Bounds(z);
                            Check(bounds.min.y > -.04f && bounds.size.magnitude < 7, name + " ground penetration or stretched skeleton");
                            camera.orthographicSize = Mathf.Max(1.65f, bounds.extents.magnitude * 1.1f);
                            camera.transform.position = bounds.center + new Vector3(4, 2.7f, 5);
                            camera.transform.LookAt(bounds.center);
                            Capture(camera, sheet, frame * 400);
                        }
                        sheet.Apply(); File.WriteAllBytes(Folder + name + "-" + index + ".png", sheet.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(sheet);
                        var end = Bounds(z); Pose(z, duration + 2); var held = Bounds(z);
                        Check(Vector3.Distance(end.center, held.center) < .001f, name + " final pose does not hold");
                        Check(held.size.y < 1.65f, name + " corpse remains standing");
                        report.AppendLine("PASS " + name + " / " + z.ActiveDeathClip.name + ": runtime selection, repeated Die, ground clearance, final hold; corpse height " + held.size.y.ToString("F2"));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(actor); }
                }
                var injuredActor = new GameObject("Injured death actor"); injuredActor.transform.SetParent(stage.transform, false);
                try
                {
                    var injured = UnityEngine.Object.Instantiate(prefab, injuredActor.transform).GetComponent<ImportedZombie>();
                    injured.parts[1].enabled = false; injured.Die();
                    Check(injured.RagdollBodyCount >= 8, name + " missing arm did not use ragdoll");
                    report.AppendLine("PASS " + name + ": injured death retains articulated ragdoll");
                }
                finally { UnityEngine.Object.DestroyImmediate(injuredActor); }
            }
            report.AppendLine("ALL 18 DEATH ANIMATIONS PASS");
        }
        catch (Exception e) { report.AppendLine("FAIL " + e); Debug.LogException(e); }
        finally
        {
            UnityEngine.Object.DestroyImmediate(stage);
            UnityEngine.Random.state = savedRandom; RenderSettings.ambientLight = savedAmbient; RenderSettings.ambientMode = savedMode; RenderSettings.fog = savedFog;
            File.WriteAllText(Folder + "Verification.txt", report.ToString()); EditorApplication.isPlaying = false;
        }
    }
    static void Capture(Camera camera, Texture2D sheet, int x)
    {
        var previous = RenderTexture.active; var target = new RenderTexture(400, 400, 24); target.Create();
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; sheet.ReadPixels(new Rect(0, 0, 400, 400), x, 0);
        }
        finally { RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
    }
}
