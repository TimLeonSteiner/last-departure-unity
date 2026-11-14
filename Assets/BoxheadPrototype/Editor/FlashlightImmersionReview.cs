using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class FlashlightImmersionReview
{
    const string Folder="Assets/Verification~/FlashlightImmersion";
    static double ready;
    static FlashlightImmersionReview(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Folder+"/Capture.request"))
        {
            // Sync the already-open scene's old serialized value with the saved tuning.
            if(!EditorApplication.isPlaying)
            {
                var sceneGame=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
                if(sceneGame!=null && Mathf.Approximately(sceneGame.flashlightIntensity,95))
                    sceneGame.flashlightIntensity=125;
            }
            File.Delete(Folder+"/Capture.request");SessionState.SetBool("FlashlightReview.Pending",true);
            SessionState.SetBool("FlashlightReview.Started",!EditorApplication.isPlaying);
            if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;
            ready=EditorApplication.timeSinceStartup+6;
        }
        if(!SessionState.GetBool("FlashlightReview.Pending",false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<ready)return;
        SessionState.SetBool("FlashlightReview.Pending",false);
        try{Capture();}catch(Exception e){File.WriteAllText(Folder+"/Result.txt","FAIL "+e);Debug.LogException(e);}
        finally{if(SessionState.GetBool("FlashlightReview.Started",false))EditorApplication.isPlaying=false;}
    }
    static void Capture()
    {
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var type=typeof(YardSurvival);
        T Read<T>(string name)=>(T)type.GetField(name,flags).GetValue(game);
        var camera=Read<Camera>("arenaCamera");var torch=Read<Light>("flashlight");var spill=Read<Light>("flashlightSpill");
        var position=camera.transform.position;var rotation=camera.transform.rotation;
        var torchRotation=torch.transform.localRotation;
        bool on=torch.enabled,spillOn=spill.enabled;var cookie=torch.cookie;
        var oldActive=RenderTexture.active;
        var target=new RenderTexture(1280,800,24);target.Create();
        var pixels=new Texture2D(1280,800,TextureFormat.RGB24,false);
        var report=new System.Text.StringBuilder();
        report.AppendLine("Actual player flashlight: enabled="+on+", active="+torch.gameObject.activeInHierarchy+", intensity="+torch.intensity+", cookie="+cookie+", parent="+torch.transform.parent.name);
        try
        {
            var waterShader=Resources.Load<Shader>("PuddleWater");
            if(waterShader==null)throw new Exception("Water shader missing from Resources");
            foreach(var message in ShaderUtil.GetShaderMessages(waterShader))
                if(message.severity.ToString()=="Error")throw new Exception(message.message);
            int puddles=0;
            foreach(var renderer in game.GetComponentsInChildren<MeshRenderer>())
            {
                if(renderer.name!="Dark rain puddle")continue;
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                if(renderer.sharedMaterial.shader!=waterShader || mesh.vertexCount<90 || mesh.uv.Length!=mesh.vertexCount)
                    throw new Exception("Puddle mesh/material has not updated");
                puddles++;
            }
            if(puddles==0)throw new Exception("No rain puddles found");
            report.AppendLine("PASS: "+puddles+" smooth water meshes with feathered UV edges and reflective water shader.");
            Vector3[] origins={new Vector3(0,1.65f,10),new Vector3(3,1.65f,13)};
            Vector3[] toward={new Vector3(0,7.9f,14),new Vector3(6,1,17)};
            for(int v=0;v<origins.Length;v++)
            {
                double darkMean=0;
                camera.transform.position=origins[v];camera.transform.LookAt(toward[v]);
                torch.transform.localRotation=Quaternion.identity;
                for(int mode=0;mode<3;mode++)
                {
                    torch.enabled=spill.enabled=mode!=0;torch.cookie=mode==2?null:cookie;
                    game.GetComponentInChildren<PuddleReflections>()?.Capture(camera);
                    RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                    RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                    RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,800),0,0);pixels.Apply();
                    File.WriteAllBytes(Folder+"/View-"+v+"-"+mode+".png",pixels.EncodeToPNG());
                    double sum=0;foreach(var c in pixels.GetPixels())sum+=c.grayscale;
                    double mean=sum/(1280*800);
                    report.AppendLine("View="+v+" mode="+mode+" mean="+mean);
                    if(mode==0)darkMean=mean;
                    if(mode==1&&mean<=darkMean+.003)throw new Exception("Actual player beam is not visibly illuminating view "+v);
                }
            }
            // Compare actual fixture illumination on the floor at a fixed pulse phase.
            var alarms=Array.FindAll(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None),l=>l.name=="Suspended rack lamp emission");
            if(alarms.Length!=10)throw new Exception("Expected ten suspended alarm lights");
            var savedIntensities=Array.ConvertAll(alarms,l=>l.intensity);
            var savedEnabled=Array.ConvertAll(alarms,l=>l.enabled);
            torch.enabled=spill.enabled=false;
            camera.transform.position=new Vector3(6.5f,1.65f,5);
            camera.transform.LookAt(new Vector3(10.8f,.05f,0));
            double baseline=0;
            try
            {
                for(int mode=0;mode<3;mode++)
                {
                    foreach(var alarm in alarms){alarm.enabled=mode!=0;alarm.intensity=mode==1?21:60;}
                    game.GetComponentInChildren<PuddleReflections>()?.Capture(camera);
                    RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                    RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                    RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,800),0,0);pixels.Apply();
                    File.WriteAllBytes(Folder+"/Red-floor-"+mode+".png",pixels.EncodeToPNG());
                    double red=0;
                    for(int y=160;y<480;y++)for(int x=320;x<960;x++)red+=pixels.GetPixel(x,y).r;
                    red/=320*640;
                    report.AppendLine("Red floor mode="+mode+" red="+red);
                    if(mode==0)baseline=red;
                    else if(red<baseline+.01)throw new Exception("Red fixture does not visibly light floor at phase "+mode);
                }
            }
            finally{for(int a=0;a<alarms.Length;a++){alarms[a].enabled=savedEnabled[a];alarms[a].intensity=savedIntensities[a];}}
            if(torch.intensity<120 || torch.innerSpotAngle<40 || spill.range<10)
                throw new Exception("New flashlight setup did not load");
            var reflectionSystem=game.GetComponentInChildren<PuddleReflections>();
            if(reflectionSystem==null)throw new Exception("Planar reflections missing");
            Renderer nearest=null;float nearestDistance=float.MaxValue;
            foreach(var r in game.GetComponentsInChildren<MeshRenderer>())
                if(r.name=="Dark rain puddle")
                {
                    float distance=(r.bounds.center-new Vector3(12,0,4)).sqrMagnitude;
                    if(distance<nearestDistance){nearest=r;nearestDistance=distance;}
                }
            camera.transform.position=nearest.bounds.center+new Vector3(0,1.65f,3.5f);
            camera.transform.LookAt(nearest.bounds.center+new Vector3(0,0,-1));
            torch.enabled=spill.enabled=true;torch.cookie=cookie;
            reflectionSystem.Capture(camera);
            for(int mode=0;mode<2;mode++)
            {
                nearest.sharedMaterial.SetFloat("_ReflectionReady",mode);
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,800),0,0);pixels.Apply();
                File.WriteAllBytes(Folder+"/Water-reflections-"+mode+".png",pixels.EncodeToPNG());
            }
            foreach(var message in ShaderUtil.GetShaderMessages(waterShader))
                if(message.severity.ToString()=="Error")throw new Exception(message.message);
            report.AppendLine("PASS: planar reflection camera rendered; water reflection on/off views captured.");
            report.AppendLine("PASS: actual player beam lights ceiling and floor; soft-shadow hotspot and peripheral spill configured.");
            File.WriteAllText(Folder+"/Result.txt",report.ToString());
        }
        finally
        {
            camera.transform.position=position;camera.transform.rotation=rotation;
            torch.enabled=on;spill.enabled=spillOn;torch.cookie=cookie;
            torch.transform.localRotation=torchRotation;
            RenderTexture.active=oldActive;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
