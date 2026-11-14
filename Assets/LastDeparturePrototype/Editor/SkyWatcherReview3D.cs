using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class SkyWatcherReview3D
{
    const string Request="Assets/Verification~/SkyEye/Review3D.request";
    const string Pending="SkyWatcherReview3D.Pending";
    static double ready;
    static SkyWatcherReview3D(){EditorApplication.update+=Poll;}
    static async void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Request))
        {
            if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
            File.Delete(Request);SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
            ready=EditorApplication.timeSinceStartup+8;
        }
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<ready)return;
        SessionState.SetBool(Pending,false);
        try
        {
            var watcher=UnityEngine.Object.FindAnyObjectByType<SkyWatcher>();
            if(watcher==null)throw new Exception("3D watcher did not spawn");
            watcher.enabled=false;
            var skin=watcher.GetComponentInChildren<SkinnedMeshRenderer>();
            if(skin==null||skin.sharedMesh.blendShapeCount==0)throw new Exception("Blink geometry missing");
            if(Shader.GetGlobalFloat("_Watcher3DActive")<.5f)throw new Exception("Sky image is not suppressed");
            if(ShaderUtil.ShaderHasError(Resources.Load<Shader>("SkyWatcherSurface")))throw new Exception("3D surface shader error");
            if(skin.bounds.size.x<100)throw new Exception("Sky watcher scale is too small");
            if(ShaderUtil.ShaderHasError(Resources.Load<Shader>("SkyWatcherCloud")))throw new Exception("Cloud shader error");
            Transform globe=null;foreach(var child in watcher.GetComponentsInChildren<Transform>())if(child.name=="Eyeball")globe=child;
            if(globe==null)throw new Exception("Separate eyeball missing");
            watcher.Pose(1);var gazeStart=globe.localRotation;watcher.Pose(7);
            if(Quaternion.Angle(gazeStart,globe.localRotation)<1)throw new Exception("Eyeball gaze did not rotate");
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            string warehouse=game.RunWarehouseChecks();
            watcher.Pose(2);await System.Threading.Tasks.Task.Delay(150);
            SpaceportAssetBuild.Capture(new Vector3(0,1.65f,11),new Vector3(0,200,12),"SkyEye/3D-Hall");
            SpaceportAssetBuild.Capture(new Vector3(0,1.65f,6),new Vector3(0,200,12),"SkyEye/3D-Framed");
            SpaceportAssetBuild.Capture(new Vector3(-10,1.65f,12),new Vector3(0,200,12),"SkyEye/3D-HallSide");
            SpaceportAssetBuild.Capture(new Vector3(0,1.65f,30),new Vector3(0,200,12),"SkyEye/3D-Outside");
            var baked=new Mesh();skin.BakeMesh(baked);var open=baked.vertices;
            watcher.Pose(10.6f);await System.Threading.Tasks.Task.Delay(150);skin.BakeMesh(baked);var closed=baked.vertices;float change=0;
            for(int i=0;i<open.Length;i++)change=Mathf.Max(change,skin.transform.TransformVector(open[i]-closed[i]).magnitude);
            UnityEngine.Object.DestroyImmediate(baked);
            if(change<.01f)throw new Exception("Eyelid vertices did not move");
            SpaceportAssetBuild.Capture(new Vector3(0,1.65f,11),new Vector3(0,200,12),"SkyEye/3D-Blink");
            watcher.Pose(7);await System.Threading.Tasks.Task.Delay(150);
            SpaceportAssetBuild.Capture(new Vector3(0,1.65f,11),new Vector3(0,200,12),"SkyEye/3D-Gaze");
            watcher.Pose(2);await System.Threading.Tasks.Task.Delay(150);
            SpaceportAssetBuild.Capture(new Vector3(110,135,65),new Vector3(0,200,12),"SkyEye/3D-Depth");
            File.WriteAllText("Assets/Verification~/SkyEye/Result3D.txt","PASS: rebuilt separate eyeball rotates, 2D sky eye suppressed, geometric eyelids move "+change+" units; seven scene captures.\n"+warehouse+"\nBounds "+skin.bounds);
        }
        catch(Exception ex){File.WriteAllText("Assets/Verification~/SkyEye/Result3D.txt","FAIL "+ex);Debug.LogException(ex);}
        finally{EditorApplication.isPlaying=false;}
    }
}




