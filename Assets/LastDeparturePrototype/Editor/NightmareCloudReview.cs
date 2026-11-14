using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class NightmareCloudReview
{
    const string Request = "Assets/Verification~/RunDreamCloudChecks.request";
    static NightmareCloudReview() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying) return;
        File.Delete(Request); Run();
    }
    public static void Run()
    {
        RunFor("Charger");
    }
    public static bool RunFor(string name)
    {
        string output = name=="Charger" ? "Assets/Verification~/MeshyDreamCharger/" : "Assets/Verification~/MeshyDreamRoster/"+name+"/";
        Directory.CreateDirectory(output);
        var report = new System.Text.StringBuilder();
        void Check(bool ok, string message)
        {
            if (!ok) throw new Exception(message);
            report.AppendLine("PASS " + message);
        }
        var previousScene = SceneManager.GetActiveScene();
        Scene reviewScene = default;
        bool passed = false;
        try
        {
            reviewScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(reviewScene);
            var bodyShader = Resources.Load<Shader>("NightmareSilhouette");
            var cloudShader = Resources.Load<Shader>("NightmareCloud");
            Check(bodyShader != null && cloudShader != null, "Both dream shaders load from Resources");
            var source = Resources.Load<GameObject>("MeshyDreamZombies/"+name);
            Check(source != null, "Actual new Meshy "+name+" prefab loads");
            var actor = UnityEngine.Object.Instantiate(source);
            foreach (var child in actor.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=29;
            actor.transform.position = Vector3.zero;
            var zombie = actor.GetComponent<ImportedZombie>();
            if (zombie != null)
            {
                // Edit-mode instances do not receive the gameplay Awake callback.
                zombie.SendMessage("Awake");
                zombie.ConfigureMotion(7);
                for (int i = 0; i < 40; i++) zombie.TickMovement(.025f,.025f,false);
            }
            foreach (var child in actor.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=29;
            var bakedMeshes = new System.Collections.Generic.List<Mesh>();
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.updateWhenOffscreen = true;
                var mesh = new Mesh(); skin.BakeMesh(mesh,false); bakedMeshes.Add(mesh);
                var baked = new GameObject("Review pose"); baked.transform.SetParent(actor.transform,false);
                baked.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation); baked.layer=29;
                var scale = actor.transform.lossyScale;
                var skinScale=skin.transform.lossyScale;
                baked.transform.localScale = new Vector3(skinScale.x/scale.x,skinScale.y/scale.y,skinScale.z/scale.z);
                baked.AddComponent<MeshFilter>().sharedMesh=mesh;
                baked.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials; skin.enabled=false;
            }
            var originals = actor.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToDictionary(r=>r,r=>r.sharedMaterials);
            var appearance = actor.GetComponent<NightmareAppearance>();
            if(appearance==null) appearance=actor.AddComponent<NightmareAppearance>();
            appearance.Initialize(bodyShader);
            var bounds = new Bounds(); bool first = true;
            foreach (var r in originals.Keys) { if (first) { bounds=r.bounds; first=false; } else bounds.Encapsulate(r.bounds); }
            var camera = new GameObject("Review camera").AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = Mathf.Max(.5f,Mathf.Max(bounds.size.y*.7f,bounds.size.x*.72f));
            camera.cullingMask=1<<29;
            camera.transform.position = bounds.center + new Vector3(0,bounds.size.y<1.3f ? 1.4f : .1f,5);
            camera.transform.LookAt(bounds.center); camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.30f,.32f,.34f);
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var light = new GameObject("Review light").AddComponent<Light>();
            light.type=LightType.Directional; light.intensity=2.2f; light.transform.rotation=Quaternion.Euler(35,150,0);
            light.cullingMask=1<<29;
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.45f,.45f,.45f); RenderSettings.fog=false;
            var target=new RenderTexture(900,1000,24); target.Create();
            var image=new Texture2D(900,1000,TextureFormat.RGB24,false);
            void Capture(string name)
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target; image.ReadPixels(new Rect(0,0,900,1000),0,0); image.Apply();
                File.WriteAllBytes(output+name+".png",image.EncodeToPNG()); RenderTexture.active=null;
            }
            appearance.SetAmount(0,0); Capture("01-awake");
            appearance.SetAmount(1,2); Capture("02-dream");
            var haze=actor.GetComponentInChildren<ParticleSystem>();
            Check(haze != null && haze.particleCount==20, "Dream creates bounded smoke particles");
            var before=new ParticleSystem.Particle[20]; haze.GetParticles(before);
            appearance.SetAmount(1,12); Capture("03-dream-later");
            var after=new ParticleSystem.Particle[20]; haze.GetParticles(after);
            Check(Vector3.Distance(before[0].position,after[0].position)>.01f,"Dream clock moves smoke");
            appearance.SetAmount(1,12); haze.GetParticles(before);
            Check(before[0].position==after[0].position,"Frozen clock keeps smoke still");
            appearance.SetAmount(0,12);
            Check(!haze.gameObject.activeSelf && haze.particleCount==0,"Waking clears smoke immediately");
            Check(originals.All(p=>p.Key.sharedMaterials.SequenceEqual(p.Value)),"Waking restores exact original materials");
            appearance.SetAmount(1,13);
            Check(actor.GetComponentsInChildren<ParticleSystem>(true).Length==1,"Repeated dream transitions reuse one particle system");
            Check(!ShaderUtil.ShaderHasError(bodyShader) && !ShaderUtil.ShaderHasError(cloudShader),"Both rendered shaders compile without errors");
            appearance.SetAmount(0,13);
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(actor);
            foreach(var mesh in bakedMeshes) UnityEngine.Object.DestroyImmediate(mesh);
            File.WriteAllText(output+"Checks.txt",report.ToString());
            passed = true;
        }
        catch(Exception e)
        {
            File.WriteAllText(output+"Checks.txt",report+"\nFAIL "+e);
            Debug.LogException(e);
        }
        finally
        {
            if(previousScene.IsValid()) SceneManager.SetActiveScene(previousScene);
            if(reviewScene.IsValid()) EditorSceneManager.CloseScene(reviewScene,true);
        }
        return passed;
    }
}
