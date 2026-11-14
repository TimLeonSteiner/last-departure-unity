using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class AllWeaponPoseReview
{
    const string Request="Library/AllWeaponPoseReview.request";
    public const string Output="Assets/Verification~/AllWeaponPoses";
    static readonly string[] Poses={"hip","aim25","aim50","aim75","aim","fire10","fire50","fire90","aimfire10","aimfire50","aimfire90","reload00","reload10","reload25","reload50","reload75","reload90","reload99","returnaim","hipburst","aimburst"};
    static int id=-1;
    static string run;
    static AllWeaponPoseReview(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Request))
        {
            run=File.ReadAllText(Request).Trim();File.Delete(Request);
            if(EditorApplication.isPlaying){File.WriteAllText(Output+"-error.txt","Stop play first");return;}
            if(run=="riflepoints")
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AnimatedArsenalImport.Root+"/"+AnimatedArsenalImport.Packages[9]+"/Playable.prefab");
                var root=UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var rig=root.GetComponent<AnimatedWeaponRig>();rig.Pose(0,false,1,1);
                    var method=typeof(AnimatedArsenalImport).GetMethod("SightVertices",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                    using(var writer=new StreamWriter(Output+"/riflepoints.csv"))
                    foreach(var renderer in rig.model.GetComponentsInChildren<Renderer>())if(renderer.name=="base")
                    foreach(var p in (Vector3[])method.Invoke(null,new object[]{renderer,9,rig}))writer.WriteLine(FormattableString.Invariant($"{p.x},{p.y},{p.z}"));
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
                return;
            }
            if(run=="anchors")
            {
                for(int weaponId=0;weaponId<WeaponCatalog.All.Length;weaponId++)
                {
                    if(WeaponCatalog.IsMelee(weaponId)||weaponId==7)continue;
                    string path=AnimatedArsenalImport.Root+"/"+AnimatedArsenalImport.Packages[weaponId]+"/Playable.prefab";
                    var root=PrefabUtility.LoadPrefabContents(path);
                    try{var rig=root.GetComponent<AnimatedWeaponRig>();rig.Pose(0,false,1,1);AnimatedArsenalImport.CalibrateMuzzle(rig,weaponId);PrefabUtility.SaveAsPrefabAsset(root,path);}
                    finally{PrefabUtility.UnloadPrefabContents(root);}
                }
                run="final";
            }
            if(run=="calibrate")
            {
                foreach(int weaponId in new[]{1,4,7,9,15})
                {
                    string path=AnimatedArsenalImport.Root+"/"+AnimatedArsenalImport.Packages[weaponId]+"/Playable.prefab";
                    var root=PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var rig=root.GetComponent<AnimatedWeaponRig>();
                        if(weaponId==1)AnimatedArsenalImport.RepairLongGunSights(rig,weaponId);
                        else AnimatedArsenalImport.RepairLongGunSights(rig,weaponId);
                        if(weaponId==9)rig.aimFireMotion=.15f;
                        PrefabUtility.SaveAsPrefabAsset(root,path);
                    }
                    finally{PrefabUtility.UnloadPrefabContents(root);}
                }
                run="framing";
            }
            Directory.CreateDirectory(Output+"/"+run);id=0;
        }
        if(id<0)return;
        try
        {
            foreach(string pose in Poses)Capture(id,pose,run.EndsWith("wide")?1260:run.EndsWith("narrow")?720:960,540);
            File.AppendAllText(Output+"/"+run+"/Progress.txt",id+" "+WeaponCatalog.All[id].name+" captured\n");
            if(++id==WeaponCatalog.All.Length){id=-1;File.AppendAllText(Output+"/"+run+"/Progress.txt","COMPLETE\n");}
        }
        catch(Exception e){File.WriteAllText(Output+"/"+run+"/Error.txt",e.ToString());id=-1;Debug.LogException(e);}
    }
    static void Capture(int weaponId,string pose,int width,int height)
    {
        var stage=new GameObject("Temporary weapon pose review");stage.transform.position=new Vector3(300,300,300);
        var camera=stage.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=70;camera.nearClipPlane=.035f;camera.farClipPlane=5;
        camera.cullingMask=1<<29;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.11f,.14f,.17f);
        camera.AddComponentIfNeeded();
        var target=new RenderTexture(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;bool fog=RenderSettings.fog;
        try
        {
            RenderSettings.fog=false;
            var obj=new GameObject("View weapon");obj.transform.SetParent(stage.transform,false);
            var view=obj.AddComponent<ViewWeapon>();
            view.Initialize(AssetDatabase.LoadAssetAtPath<GameObject>(AnimatedArsenalImport.Root+"/"+AnimatedArsenalImport.Packages[weaponId]+"/Playable.prefab"),null,weaponId);
            var rig=view.GetComponentInChildren<AnimatedWeaponRig>();
            view.ReviewPose(0,false,0,0,1);
            bool aim=pose.StartsWith("aim")||pose=="returnaim";
            if(aim)view.ReviewPose(0,false,0,1,pose=="aim25"?.03125f:pose=="aim50"?.0625f:pose=="aim75"?.09375f:1);
            if(pose.Contains("fire"))
            {
                float phase=int.Parse(pose.Substring(pose.Length-2))/100f;
                view.OnShot();
                var profile=Recoil(weaponId);
                for(int n=1;n<=10;n++)view.ReviewPose(0,false,Mathf.Max(0,profile.x*(aim?.7f:1)-profile.y*rig.fireDuration*phase*n/10),aim?1:0,rig.fireDuration*phase/10);
            }
            if(pose.EndsWith("burst"))
            {
                view.OnShot();view.ReviewPose(0,false,Recoil(weaponId).x*1.7f,aim?1:0,.016f,3);
            }
            if(pose.StartsWith("reload")||pose=="returnaim")
            {
                view.ReviewPose(0,false,0,1,1);
                float end=pose=="returnaim"?1:int.Parse(pose.Substring(6))/100f;
                for(int n=0;n<=60;n++)view.ReviewPose(end*n/60,true,0,0,Mathf.Max(.001f,WeaponCatalog.All[weaponId].reload*end/60));
                if(pose=="returnaim")for(int n=0;n<30;n++)view.ReviewPose(0,false,0,1,1/60f);
            }
            // Inspect the hidden sniper model too; gameplay replaces it with its scope overlay.
            if(rig.ReviewArmLengthError>.002f)throw new Exception("Arm length changed in "+weaponId+" "+pose+": "+rig.ReviewArmLengthError);
            if(weaponId==16)foreach(var renderer in rig.model.GetComponentsInChildren<Renderer>())renderer.enabled=true;
            foreach(var t in stage.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;
            foreach(var renderer in view.GetComponentsInChildren<Renderer>())renderer.renderingLayerMask=1;
            foreach(var light in view.GetComponentsInChildren<Light>())light.enabled=false;
            var lamp=new GameObject("Review lamp").AddComponent<Light>();lamp.transform.SetParent(stage.transform,false);lamp.transform.localPosition=new Vector3(-.4f,.6f,-.4f);lamp.type=LightType.Point;lamp.intensity=12;lamp.range=5;lamp.cullingMask=1<<29;
            target.Create();camera.aspect=width/(float)height;
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
            RenderPipeline.SubmitRenderRequest(camera,request);RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
            File.WriteAllBytes(Output+"/"+run+"/"+weaponId.ToString("00")+"-"+pose+".jpg",image.EncodeToJPG(90));
        }
        finally{RenderSettings.fog=fog;RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(stage);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
    }
    static void AddComponentIfNeeded(this Camera camera){camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;}
    static Vector2 Recoil(int weaponId)
    {
        var profiles=(Array)typeof(YardSurvival).GetField("recoilProfiles",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);
        var profile=profiles.GetValue(weaponId);var type=profile.GetType();
        return new Vector2((float)type.GetField("weapon").GetValue(profile),(float)type.GetField("weaponReturn").GetValue(profile));
    }
}
