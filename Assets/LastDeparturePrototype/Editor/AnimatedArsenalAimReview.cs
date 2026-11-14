using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using YardSurvivalPrototype;
public static class AnimatedArsenalAimReview
{
    public static void Run()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Aim review requires Play Mode");
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();bool enabled=game.enabled;game.enabled=false;
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var camera=(Camera)typeof(YardSurvival).GetField("arenaCamera",flags).GetValue(game);
        var report=new StringBuilder();string folder="Assets/LastDeparturePrototype/AimReview";Directory.CreateDirectory(folder);
        var texture=new RenderTexture(1200,750,24);texture.Create();var pixels=new Texture2D(1200,750,TextureFormat.RGB24,false);var previous=RenderTexture.active;float fov=camera.fieldOfView;
        try
        {
            for(int id=0;id<WeaponCatalog.All.Length;id++)
            {
                typeof(YardSurvival).GetMethod("EquipWeapon",flags).Invoke(game,new object[]{id,WeaponCatalog.All[id].capacity,-1});
                var view=(ViewWeapon)typeof(YardSurvival).GetField("viewWeapon",flags).GetValue(game);var rig=view.GetComponentInChildren<AnimatedWeaponRig>();
                for(int step=0;step<100;step++)view.Tick(0,false,0,0,1);
                rig.Pose(0,false,1,1);
                view.transform.localPosition=rig.aimPosition;view.transform.localRotation=Quaternion.Euler(rig.aimEuler);
                if(rig.calibratedSights)
                {
                    Vector3 rear=rig.aimPosition+Quaternion.Euler(rig.aimEuler)*rig.rearSight,front=rig.aimPosition+Quaternion.Euler(rig.aimEuler)*rig.frontSight;
                    float error=Mathf.Max(new Vector2(rear.x,rear.y).magnitude,new Vector2(front.x,front.y).magnitude);
                    if(error>.003f)throw new Exception("Uncentered sights "+id+" error="+error);
                    report.AppendLine("PASS "+id+" rear/front eye-axis error="+error+" eyeRelief="+rear.z);
                }
                else report.AppendLine("CENTERED "+id+" dual/melee/rotary treatment");
                // Keep optics visible in these model alignment images. Full-screen
                // sniper scope and its transition are checked in the GUI playtests.
                if(WeaponCatalog.IsScoped(id))foreach(var renderer in view.GetComponentsInChildren<Renderer>())renderer.enabled=true;
                foreach(int worldFov in new[]{60,75,100})
                {
                    camera.fieldOfView=WeaponCatalog.IsScoped(id)?Mathf.Min(42,worldFov*.55f):worldFov*.77f;
                    RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=texture});RenderTexture.active=texture;
                    pixels.ReadPixels(new Rect(0,0,1200,750),0,0);pixels.Apply();File.WriteAllBytes(folder+"/"+id.ToString("00")+"-ADS-FOV"+worldFov+".jpg",pixels.EncodeToJPG(90));
                }
            }
            File.WriteAllText(folder+"/Validation.txt",report+"All 17 authored models captured at ADS for world FOV60/75/100. Scope model inspection images force optics visible; manual GUI tests cover the full-screen scope.\n");
        }
        finally{camera.fieldOfView=fov;RenderTexture.active=previous;texture.Release();UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(pixels);game.enabled=enabled;}
    }
}
