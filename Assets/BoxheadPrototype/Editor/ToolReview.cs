using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;
[InitializeOnLoad] public static class ToolReview
{
    const string Root="Assets/BoxheadPrototype/";
    static ToolReview(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Root+"ToolReview.request")&&!EditorApplication.isPlaying)
        {
            File.Delete(Root+"ToolReview.request");EditorSceneManager.OpenScene("Assets/Boxhead.unity");
            SessionState.SetInt("D27.ToolReview",1);EditorApplication.isPlaying=true;return;
        }
        if(SessionState.GetInt("D27.ToolReview",0)==1&&EditorApplication.isPlaying)
        {
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null)return;
            try{File.WriteAllText(Root+"ToolChecks.txt",game.RunToolChecks());File.WriteAllText(Root+"InventoryChecks.txt",game.RunInventoryChecks());Capture(game);}
            catch(Exception e){File.WriteAllText(Root+"ToolChecks.txt","FAIL "+e);}
            SessionState.SetInt("D27.ToolReview",0);EditorApplication.isPlaying=false;
        }
    }
    static void Capture(YardSurvival game)
    {
        var stage=new GameObject("Partner weapon review");stage.transform.position=new Vector3(200,20,200);
        var cameraObject=new GameObject("Partner review camera");var camera=cameraObject.AddComponent<Camera>();
        camera.enabled=false;camera.cullingMask=1<<29;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.15f,.17f);
        cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        camera.transform.position=stage.transform.position+new Vector3(2.7f,1.7f,4.5f);camera.transform.LookAt(stage.transform.position+Vector3.up*.95f);camera.fieldOfView=42;
        var lamp=new GameObject("Partner review lamp").AddComponent<Light>();lamp.type=LightType.Point;lamp.range=15;lamp.intensity=55;lamp.transform.position=stage.transform.position+new Vector3(1,3,3);
        var target=new RenderTexture(1100,800,24);target.Create();var image=new Texture2D(1100,800,TextureFormat.RGB24,false);
        bool fog=RenderSettings.fog;var previous=RenderTexture.active;
        try
        {
            for(int i=0;i<2;i++)
            {
                var avatar=UnityEngine.Object.Instantiate(game.playerSkinModels[i],stage.transform).GetComponent<HumanoidPlayerAvatar>();
                avatar.transform.localPosition=new Vector3(i==0?-.65f:.65f,0,0);avatar.Tick(.2f,0,0,false);
                int id=i==0?2:3;avatar.SetWeapon(game.animatedWeaponModels[id],id,0);
                if(i==0)File.WriteAllText(Root+"PartnerGripProbe.txt","");
                foreach(var t in avatar.GetComponentsInChildren<Transform>(true))if(t.name.ToLowerInvariant().Contains("hand")||t.name.ToLowerInvariant().Contains("wrist"))File.AppendAllText(Root+"PartnerGripProbe.txt",i+" "+t.name+" "+t.position+Environment.NewLine);
                foreach(var t in avatar.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;
            }
            RenderSettings.fog=false;var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1100,800),0,0);image.Apply();
            File.WriteAllBytes(Root+"PartnerWeaponsReview.png",image.EncodeToPNG());
        }
        finally{RenderSettings.fog=fog;RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(lamp.gameObject);UnityEngine.Object.DestroyImmediate(stage);}
    }
}
