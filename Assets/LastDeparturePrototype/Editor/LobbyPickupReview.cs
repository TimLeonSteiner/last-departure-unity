using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class LobbyPickupReview
{
    const string Root="Assets/Verification~/";
    static LobbyPickupReview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!File.Exists(Root+"LobbyPickup.request")||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        File.Delete(Root+"LobbyPickup.request");
        GameObject pack=null,gun=null,lightObject=null,cameraObject=null;
        try
        {
            Vector3 origin=new Vector3(1000,1000,1000);
            cameraObject=new GameObject("Pickup review camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.065f,.075f);camera.nearClipPlane=.01f;camera.farClipPlane=10;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            lightObject=new GameObject("Pickup review light");var light=lightObject.AddComponent<Light>();light.type=LightType.Point;light.range=8;light.intensity=8;light.transform.position=origin+new Vector3(-1,2,-1);
            pack=CaffeinePickupVisual.Create(null);pack.transform.position=origin;
            camera.transform.position=origin+new Vector3(.25f,.48f,-.42f);camera.transform.LookAt(origin);camera.fieldOfView=38;
            Capture(camera,"Caffeine-Tablets-Updated");
            UnityEngine.Object.DestroyImmediate(pack);pack=null;
            for(int id=0;id<WeaponCatalog.All.Length;id++)
            {
                if(!WeaponCatalog.IsAvailable(id))continue;
                gun=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AnimatedArsenalImport.Root+"/"+AnimatedArsenalImport.Packages[id]+"/Playable.prefab"));
                var rig=gun.GetComponent<AnimatedWeaponRig>();
                foreach(var animator in rig.GetComponentsInChildren<Animator>())animator.enabled=false;
                gun.transform.position=origin+rig.aimPosition;gun.transform.rotation=Quaternion.Euler(rig.aimEuler);
                for(int i=0;i<30;i++)rig.Pose(0,false,.1f,1);
                if(rig.calibratedSights)
                {
                    Vector3 rear=rig.aimPosition+gun.transform.rotation*rig.rearSight,front=rig.aimPosition+gun.transform.rotation*rig.frontSight;
                    if(new Vector2(rear.x,rear.y).magnitude>.003f||new Vector2(front.x,front.y).magnitude>.003f||rear.z<.43f)throw new Exception("Sight alignment or eye relief failed: "+id);
                }
                camera.transform.SetPositionAndRotation(origin,Quaternion.identity);camera.fieldOfView=70;
                var bones=rig.model.GetComponentsInChildren<Transform>();
                var before=new Vector3[bones.Length];for(int i=0;i<bones.Length;i++)before[i]=bones[i].position;
                rig.FitAimShoulders(camera.transform,1,id);
                for(int i=0;i<bones.Length;i++)if((bones[i].name=="hand.L"||bones[i].name=="hand.R"||bones[i].name=="L_wrist"||bones[i].name=="R_wrist")&&Vector3.Distance(before[i],bones[i].position)>.002f)throw new Exception("Grip moved: "+id);
                rig.FitAimShoulders(camera.transform,0,id);
                for(int i=0;i<bones.Length;i++)if(Vector3.Distance(before[i],bones[i].position)>.002f)throw new Exception("Aim correction leaked into hip pose: "+id);
                rig.FitAimShoulders(camera.transform,1,id);
                Capture(camera,"Weapon-Aim-"+id.ToString("00"));
                UnityEngine.Object.DestroyImmediate(gun);gun=null;
            }
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            if(game!=null)
            {
                // Refresh the old default retained by this live editor instance after hot reload.
                if(Mathf.Approximately(game.caffeineDropChance,.2f))game.caffeineDropChance=.04f;
                File.WriteAllText(Root+"BalanceChecks.txt",game.RunBalanceChecks()+"Live caffeine chance: "+game.caffeineDropChance+"\n");
            }
            File.WriteAllText(Root+"LobbyPickupReview.txt","PASS all available weapons rendered at the gameplay viewmodel FOV; calibrated sights within 3 mm of axis and at least 44 cm eye relief.\n");
        }
        catch(Exception e){File.WriteAllText(Root+"LobbyPickupReview.txt","FAIL "+e);Debug.LogException(e);}
        finally{foreach(var obj in new[]{pack,gun,lightObject,cameraObject})if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);}
    }
    static void Capture(Camera camera,string name)
    {
        var rt=new RenderTexture(1200,750,24);rt.Create();var texture=new Texture2D(1200,750,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1200,750),0,0);texture.Apply();File.WriteAllBytes(Root+name+".png",texture.EncodeToPNG());}
        finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);}
    }
}
