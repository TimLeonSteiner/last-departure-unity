using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

public static partial class RealisticZombieImport
{
    [MenuItem("Tools/Yard Survival/Review new zombies")]
    public static void Review()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play for zombie validation");
        string folder = "Assets/LastDeparturePrototype/ZombieReview"; Directory.CreateDirectory(folder);
        var report = new StringBuilder();
        var stage = new GameObject("Zombie validation stage"); stage.transform.position = new Vector3(200, 10, 200);
        var cameraObj = new GameObject("Zombie review camera"); var camera = cameraObj.AddComponent<Camera>();
        camera.enabled = false; camera.cullingMask = 1 << 29; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .14f, .16f); camera.fieldOfView = 38;
        cameraObj.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        var lightObj = new GameObject("Zombie review light"); var light = lightObj.AddComponent<Light>(); light.type = LightType.Point; light.range = 12; light.intensity = 45; light.cullingMask = 1 << 29;
        lightObj.transform.position = stage.transform.position + new Vector3(-2, 3, 3);
        var target = new RenderTexture(900, 1000, 24); target.Create(); var image = new Texture2D(900, 1000, TextureFormat.RGB24, false);
        var floor=new GameObject("Ragdoll review floor");floor.transform.position=stage.transform.position+new Vector3(0,-.15f,0);floor.AddComponent<BoxCollider>().size=new Vector3(20,.3f,20);
        var simulationMode=Physics.simulationMode;
        bool fog = RenderSettings.fog; var ambient = RenderSettings.ambientLight; var mode = RenderSettings.ambientMode; var previous = RenderTexture.active;
        RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.gray;
        try
        {
            foreach (string name in new[] { "Pxltiger", "Modern", "FatZombie", "AAB" })
            {
                var actor = new GameObject(name + " review actor"); actor.transform.SetParent(stage.transform, false);
                var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/" + name + ".prefab"), actor.transform);
                var zombie = instance.GetComponent<ImportedZombie>(); zombie.DressWounds(UnityEngine.Object.FindAnyObjectByType<YardSurvival>().woundMaterial);
                foreach (var tr in actor.GetComponentsInChildren<Transform>()) tr.gameObject.layer = 29;
                camera.transform.position = stage.transform.position + new Vector3(2.1f, 1.5f, 3.5f); camera.transform.LookAt(stage.transform.position + Vector3.up * .9f);
                zombie.Tick(0, false);
                Bounds first = BakedBounds(zombie);
                report.AppendLine(name + " BOUNDS " + first + " root=" + zombie.transform.position + " scale=" + zombie.transform.lossyScale + " rig=" + zombie.animationRig.transform.position + " rigScale=" + zombie.animationRig.transform.lossyScale + " hip=" + zombie.animationRig.GetBoneTransform(HumanBodyBones.Hips).position);
                camera.transform.position = first.center + new Vector3(2.1f, .6f, 3.5f); camera.transform.LookAt(first.center);
                Capture(camera, target, image, folder + "/" + name + "-diagnostic.jpg");
                Check(first.size.y > 1.4f && first.size.y < 2.2f, name + " human height " + first.size.y, report);
                Vector3 handBefore = zombie.animationRig.GetBoneTransform(HumanBodyBones.LeftHand).position;
                Capture(camera, target, image, folder + "/" + name + "-walk-0.jpg");
                zombie.Tick(.8f, false); Vector3 handAfter = zombie.animationRig.GetBoneTransform(HumanBodyBones.LeftHand).position;
                Check(Vector3.Distance(handBefore, handAfter) > .01f, name + " walk animates", report);
                Capture(camera, target, image, folder + "/" + name + "-walk-1.jpg");
                zombie.Tick(.4f, true); Capture(camera, target, image, folder + "/" + name + "-attack.jpg");
                Vector3 head = zombie.RegionCenter(0);
                Check(zombie.Trace(new Ray(head + Vector3.forward * 3, Vector3.back), 4, out float distance, out int region) && region == 0, name + " head ray detects head", report);
                foreach (var material in zombie.parts.SelectMany(p => p.sharedMaterials).Distinct()) Check(material.shader.name == "Universal Render Pipeline/Lit", name + " URP material " + material.name, report);
                for (int part = 0; part < 5; part++)
                {
                    var skin = zombie.parts[part]; var mesh = new Mesh(); skin.BakeMesh(mesh, false);
                    Bounds before = WorldBounds(mesh, skin.transform); UnityEngine.Object.DestroyImmediate(mesh);
                    Check(zombie.Sever(part, Vector3.right, actor.transform), name + " sever region " + part, report);
                    Check(!skin.enabled && !zombie.Sever(part, Vector3.right, actor.transform), name + " region removed once " + part, report);
                    Check(actor.GetComponentsInChildren<ZombieDebris>().Last().GetComponent<Rigidbody>()!=null&&actor.GetComponentsInChildren<ZombieDebris>().Last().GetComponent<Collider>()!=null,name+" severed part uses physical collision "+part,report);
                    var debris = actor.GetComponentsInChildren<ZombieDebris>().Last().GetComponent<Renderer>();
                    report.AppendLine(name + " part=" + part + " before=" + before + " debris=" + debris.bounds);
                    Check(Vector3.Distance(before.center, debris.bounds.center) < .01f && Vector3.Distance(before.size, debris.bounds.size) < .01f, name + " debris retains pose and scale " + part, report);
                    foreach (var tr in actor.GetComponentsInChildren<Transform>()) tr.gameObject.layer = 29;
                    debris.transform.position += new Vector3(part % 2 == 0 ? .6f : -.6f, .05f, 0);
                    Capture(camera, target, image, folder + "/" + name + "-sever-" + part + ".jpg");
                }
                Check(zombie.LostArms == 2 && zombie.LostLegs == 2, name + " limb loss tracked", report);
                zombie.Tick(.8f, false); Capture(camera, target, image, folder + "/" + name + "-crawler.jpg");
                UnityEngine.Object.DestroyImmediate(actor);
                var deathActor = new GameObject(name + " death actor"); deathActor.transform.SetParent(stage.transform, false);
                var death = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/" + name + ".prefab"), deathActor.transform).GetComponent<ImportedZombie>();
                foreach (var tr in deathActor.GetComponentsInChildren<Transform>()) tr.gameObject.layer = 29;
                death.Tick(.4f,false);death.Die();
                Check(death.RagdollBodyCount>=10&&!death.animationRig.enabled,name+" articulated death disables animation",report);
                var bodies=deathActor.GetComponentsInChildren<Rigidbody>();
                Check(deathActor.GetComponentsInChildren<CharacterJoint>().Length>=9,name+" articulated joints",report);
                Physics.simulationMode=SimulationMode.Script;Physics.SyncTransforms();
                for(int step=0;step<200;step++){Physics.Simulate(.02f);death.SyncRagdollPose();}
                Physics.simulationMode=simulationMode;
                Check(bodies.All(b=>float.IsFinite(b.position.y)&&b.position.y>=stage.transform.position.y-.12f&&b.linearVelocity.magnitude<8),name+" ragdoll remains above floor with bounded velocities",report);
                Bounds fallen=BakedBounds(death);Check(fallen.max.y<stage.transform.position.y+1.4f,name+" physical death settles on ground height="+(fallen.max.y-stage.transform.position.y),report);
                Capture(camera, target, image, folder + "/" + name + "-death.jpg");
                UnityEngine.Object.DestroyImmediate(deathActor);
            }
            File.WriteAllText(folder + "/Validation.txt", report + "ALL NEW ZOMBIE CHECKS PASSED\n");
            Debug.Log("NEW ZOMBIE REVIEW PASS; inspect images in " + folder);
        }
        catch (Exception e) { File.WriteAllText(folder + "/Validation.txt", report + "FAIL " + e + "\n"); throw; }
        finally
        {
            Physics.simulationMode=simulationMode;UnityEngine.Object.DestroyImmediate(floor);
            RenderTexture.active = previous; RenderSettings.fog = fog; RenderSettings.ambientLight = ambient; RenderSettings.ambientMode = mode;
            UnityEngine.Object.DestroyImmediate(stage); UnityEngine.Object.DestroyImmediate(cameraObj); UnityEngine.Object.DestroyImmediate(lightObj); target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
    static void Check(bool pass, string message, StringBuilder report) { if (!pass) throw new Exception(message); report.AppendLine("PASS " + message); }
    static Bounds WorldBounds(Mesh mesh, Transform transform)
    {
        var vertices = mesh.vertices; var bounds = new Bounds(transform.position + transform.rotation * vertices[0], Vector3.zero);
        foreach (var v in vertices) bounds.Encapsulate(transform.position + transform.rotation * v); return bounds;
    }
    static Bounds BakedBounds(ImportedZombie zombie)
    {
        Bounds bounds = default; bool first = true;
        foreach (var skin in zombie.parts)
        {
            var mesh = new Mesh(); skin.BakeMesh(mesh, false); var next = WorldBounds(mesh, skin.transform); UnityEngine.Object.DestroyImmediate(mesh);
            if (first) { bounds = next; first = false; } else bounds.Encapsulate(next);
        }
        return bounds;
    }
    static void Capture(Camera camera, RenderTexture target, Texture2D image, string path)
    {
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToJPG(90));
    }
}

