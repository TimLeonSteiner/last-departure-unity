using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;
[InitializeOnLoad]
public static class AdaptiveGraphicsReview
{
    const string Request="Assets/Verification~/AdaptiveGraphics.request",Pending="D27.AdaptiveGraphicsReview";
    static AdaptiveGraphicsReview(){EditorApplication.update+=Tick;}
    public static void VerifyBatch()
    {
        if(!Application.isBatchMode)
        {
            var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var view=EditorWindow.GetWindow(type);view.position=new Rect(50,50,1280,720);view.Show();view.Focus();
        }
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/LastDeparture.unity");
        SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Request))
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}
            File.Delete(Request);SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
        }
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying)return;
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null)return;
        SessionState.SetBool(Pending,false);game.StartCoroutine(Verify(game));
    }
    static object Field(YardSurvival game,string name)=>typeof(YardSurvival).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
    static void Call(YardSurvival game,string name,params object[] args)=>typeof(YardSurvival).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,args);
    static int Triangles(FacilityCirculation network){int sum=0;foreach(var f in network.GetComponentsInChildren<MeshFilter>())if(f.gameObject.activeSelf)sum+=f.sharedMesh.triangles.Length/3;return sum;}
    static IEnumerator Verify(YardSurvival game)
    {
        yield return null;
        string result="";
        try
        {
            Call(game,"StopLaunchSounds");
            var camera=((Camera)Field(game,"arenaCamera")).GetUniversalAdditionalCameraData();
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline==null)throw new Exception("Missing pipeline");
            if(YardSurvival.GraphicsScale(3,3840,2160)>640f/3840+.001f)throw new Exception("Fast pixel cap failed");
            Call(game,"ApplyGraphicsPreset",2,false);
            result="High";
        }
        catch(Exception e){result="FAIL "+e;}
        if(result.StartsWith("FAIL")){Finish(result);yield break;}
        yield return null;yield return null;
        var network=UnityEngine.Object.FindAnyObjectByType<FacilityCirculation>();
        int high=network==null?0:Triangles(network);
        Call(game,"ApplyGraphicsPreset",0,false);
        yield return null;yield return null;
        try
        {
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var camera=((Camera)Field(game,"arenaCamera")).GetUniversalAdditionalCameraData();
            if(camera.renderShadows||camera.renderPostProcessing||pipeline.shadowDistance!=0)throw new Exception("Low effects not disabled");
            int low=network==null?0:Triangles(network);
            if(high==0||low>=high*.6f)throw new Exception("Low geometry reduction insufficient: "+low+" / "+high);
            var harness=UnityEngine.Object.FindAnyObjectByType<HeartCableHarness>();harness.RefreshCables();
            foreach(var filter in harness.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh.isReadable)foreach(var v in filter.sharedMesh.vertices)
                if(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z))throw new Exception("Invalid heart mesh");
            if(ShaderUtil.ShaderHasError(Shader.Find("Dustline/Facility Vessel")))throw new Exception("Tissue shader error");
            result="PASS High to Low geometry: "+high+" -> "+low+" triangles; cheap shader compiled; valid heart meshes.\n";
            Call(game,"ApplyGraphicsPreset",3,false);
        }
        catch(Exception e){result="FAIL "+e;}
        if(result.StartsWith("FAIL")){Finish(result);yield break;}
        yield return new WaitForSecondsRealtime(.7f);
        try
        {
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(Math.Abs(pipeline.renderScale-YardSurvival.GraphicsScale(3,Screen.width,Screen.height))>.001f)throw new Exception("Fast scale failed");
            int active=0;
            foreach(string name in new[]{"Spaceport terminal","Spaceport exterior"})
                foreach(var light in game.transform.Find(name).GetComponentsInChildren<Light>())
                    if(light.enabled&&light.cullingMask!=0&&light.type!=LightType.Directional)active++;
            if(active>6)throw new Exception("Fast light budget exceeded: "+active);
            result+="PASS Fast resolution cap, shadow/post-processing flags, at most six decorative lights.\n";
            Call(game,"ApplyGraphicsPreset",2,false);
        }
        catch(Exception e){result="FAIL "+e;}
        if(result.StartsWith("FAIL")){Finish(result);yield break;}
        yield return null;yield return null;
        try
        {
            if(Triangles(network)!=high)throw new Exception("High geometry not restored");
            var camera=((Camera)Field(game,"arenaCamera")).GetUniversalAdditionalCameraData();
            if(!camera.renderPostProcessing||!camera.renderShadows)throw new Exception("High effects not restored");
            result+="PASS High restores full geometry and camera effects.\n";
            Call(game,"ApplyGraphicsPreset",0,false);
            var front=typeof(YardSurvival).GetField("frontScreen",BindingFlags.Instance|BindingFlags.NonPublic);
            front.SetValue(game,Enum.Parse(front.FieldType,"Options"));
        }
        catch(Exception e){result="FAIL "+e;}
        yield return null;
        ScreenCapture.CaptureScreenshot("Assets/Verification~/AdaptiveGraphicsOptions.png");
        yield return new WaitForSecondsRealtime(1);
        Finish(result);
    }
    static void Finish(string result){File.WriteAllText("Assets/Verification~/AdaptiveGraphicsChecks.txt",result);if(Application.isBatchMode||Array.IndexOf(Environment.GetCommandLineArgs(),"-adaptiveGraphicsReview")>=0)EditorApplication.Exit(result.StartsWith("FAIL")?1:0);else EditorApplication.isPlaying=false;}
}
