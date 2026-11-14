using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class GripFeedbackReview
{
    const string Request = "Library/GripFeedbackReview.request";
    const string Output = "Assets/Verification~/GripFeedback";
    static GripFeedbackReview() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string action = File.ReadAllText(Request).Trim(); File.Delete(Request);
        Directory.CreateDirectory(Output);
        try { Run(action); File.WriteAllText(Output + "/Result.txt", "PASS " + action); AssetDatabase.Refresh(); }
        catch (Exception e) { File.WriteAllText(Output + "/Result.txt", e.ToString()); Debug.LogException(e); }
    }
    static void Run(string action)
    {
        if(action=="stop") {EditorApplication.isPlaying=false;return;}
        if(action=="repair")
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play before saving grip calibration.");
            foreach(int id in new[]{0,1,2,3,4,6,8,9,11,12,13,14,15,16})
            {
            string path=AnimatedArsenalImport.Root+"/"+AnimatedArsenalImport.Packages[id]+"/Playable.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try {var rig=root.GetComponent<AnimatedWeaponRig>();rig.Pose(0,false,1,1);if(id==14||id==3)AnimatedArsenalImport.RepairLongGunSights(rig,id);AnimatedArsenalImport.CalibrateMuzzle(rig,id);PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally {PrefabUtility.UnloadPrefabContents(root);}
            }
            HammerGripSetup.RepairAndReview();
        }
        foreach (int id in new[] {3, 9, 12, 14})
        foreach (float aim in new[] {0f, 1f})
            Capture(AnimatedArsenalImport.Root + "/" + AnimatedArsenalImport.Packages[id] + "/Playable.prefab", id, aim, action);
        Capture("Assets/BoxheadPrototype/ThirdParty/Hammer/PlayableHammer.prefab", -1, 0, action);
        if(action=="verify")
        {
            foreach(string name in new[]{"Select","Unlock"})
            {
                var clip=Resources.Load<AudioClip>("SkillFeedback/"+name);
                if(clip==null||clip.length<.1f)throw new Exception("Missing skill sound "+name);
            }
            var shader=Resources.Load<Shader>("MuzzleFlash");
            if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Muzzle shader failed");
            foreach(int id in new[]{3,12,14})Capture(AnimatedArsenalImport.Root+"/"+AnimatedArsenalImport.Packages[id]+"/Playable.prefab",id,0,"flash");
            foreach(int id in new[]{3,5,7,12,14}) CheckFiring(id);
        }
    }
    static void CheckFiring(int id)
    {
        var camera=new GameObject("Temporary weapon test camera");
        camera.transform.position=new Vector3(300,300,300);
        try
        {
            var viewObject=new GameObject("Test weapon");viewObject.transform.SetParent(camera.transform,false);
            var view=viewObject.AddComponent<ViewWeapon>();
            view.Initialize(AssetDatabase.LoadAssetAtPath<GameObject>(AnimatedArsenalImport.Root+"/"+AnimatedArsenalImport.Packages[id]+"/Playable.prefab"),null,id);
            var flash=view.GetComponentInChildren<WeaponMuzzleFlash>();
            view.OnShot();view.Tick(0,false,0,0);
            bool shouldFlash=!WeaponCatalog.IsMelee(id)&&id!=7;
            if(flash.GetComponent<MeshRenderer>().enabled!=shouldFlash)throw new Exception("Shot visibility incorrect for weapon "+id);
            view.Tick(.5f,true,0,0);
            if(flash.GetComponent<MeshRenderer>().enabled||flash.GetComponent<Light>().enabled)throw new Exception("Reload flash still visible for "+id);
        }
        finally{UnityEngine.Object.DestroyImmediate(camera);}
    }
    static void Capture(string path, int id, float aim, string label)
    {
        var stage = new GameObject("Temporary grip review"); stage.transform.position = new Vector3(300,300,300);
        var cameraObject = new GameObject("Grip review camera"); var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false; camera.transform.position = stage.transform.position;
        camera.fieldOfView = 70; camera.nearClipPlane = .035f; camera.farClipPlane = 5;
        camera.cullingMask = 1 << 29; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f,.16f,.19f);
        cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        var target = new RenderTexture(1440,810,24); var pixels = new Texture2D(1440,810,TextureFormat.RGB24,false);
        var previous = RenderTexture.active; bool fog = RenderSettings.fog;
        try
        {
            RenderSettings.fog = false;
            var obj = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),stage.transform);
            var rig = obj.GetComponent<AnimatedWeaponRig>();
            foreach (var animator in obj.GetComponentsInChildren<Animator>()) animator.enabled = false;
            rig.Pose(0,false,1,aim);
            obj.transform.localPosition = Vector3.Lerp(rig.hipPosition,rig.aimPosition,aim);
            obj.transform.localRotation = Quaternion.Euler(rig.aimEuler * aim);
            if (id < 0) {var swing=obj.AddComponent<HeldHammerSwing>();swing.Initialize(rig);swing.Pose(1);}
            else if (label != "raw") rig.FitAimShoulders(camera.transform,aim,id);
            if(label=="flash")
            {
                var flash=new GameObject("Muzzle flash").AddComponent<WeaponMuzzleFlash>();
                flash.transform.SetParent(rig.transform,false);flash.transform.localPosition=rig.muzzle;flash.Initialize();flash.Fire();
                flash.Tick(.5f,false);
                if(!flash.GetComponent<MeshRenderer>().enabled)throw new Exception("Muzzle flash lost on its firing frame");
                flash.Tick(0,true);
                if(flash.GetComponent<MeshRenderer>().enabled||flash.GetComponent<Light>().enabled)throw new Exception("Hidden flash still visible");
                flash.Fire();
            }
            foreach (var t in obj.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=29;
            foreach (var r in obj.GetComponentsInChildren<Renderer>()) r.renderingLayerMask=1;
            var lamp=new GameObject("Review light").AddComponent<Light>();lamp.transform.SetParent(stage.transform,false);
            lamp.transform.localPosition=new Vector3(-.4f,.6f,-.4f);lamp.type=LightType.Point;lamp.range=5;lamp.intensity=12;lamp.cullingMask=1<<29;
            target.Create();
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
            RenderPipeline.SubmitRenderRequest(camera,request);RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1440,810),0,0);pixels.Apply();
            File.WriteAllBytes(Output+"/"+label+"-"+id+"-"+aim+".png",pixels.EncodeToPNG());
        }
        finally {RenderSettings.fog=fog;RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(stage);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
    }
}
