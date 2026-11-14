using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class SpitterAcidReview
{
    static Action capture; static double captureAt;
    public static void RunBatch()
    {
        File.WriteAllText("Assets/Verification~/AcidReview.request","render");
        Poll();
    }
    static SpitterAcidReview(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(capture!=null){if(EditorApplication.timeSinceStartup<captureAt)return;var action=capture;capture=null;action();if(Application.isBatchMode)EditorApplication.Exit(0);return;}
        const string request="Assets/Verification~/AcidReview.request";
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(request))return;
        File.Delete(request);
        var scene=EditorSceneManager.NewPreviewScene();
        var root=new GameObject("Acid visual review");SceneManager.MoveGameObjectToScene(root,scene);
        var target=new RenderTexture(1400,900,24);var image=new Texture2D(1400,900,TextureFormat.RGB24,false);
        var old=RenderTexture.active;
        try
        {
            var cameraObject=new GameObject("Review camera");cameraObject.transform.SetParent(root.transform);
            var camera=cameraObject.AddComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.useOcclusionCulling=false;camera.transform.position=new Vector3(4,4,-6);
            camera.transform.LookAt(new Vector3(0,.5f,0));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.043f,.052f);camera.fieldOfView=40;
            var lightObject=new GameObject("Review light");lightObject.transform.SetParent(root.transform);
            var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(45,-25,0);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.transform.localPosition=new Vector3(0,-.08f,0);floor.transform.localScale=new Vector3(8,.1f,7);
            var floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));floorMaterial.color=new Color(.13f,.14f,.15f);floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
            var puddle=new GameObject("Review puddle");puddle.transform.SetParent(root.transform);puddle.transform.position=new Vector3(.7f,.035f,0);
            var pool=puddle.AddComponent<SpitterAcidVisual>();pool.Initialize(true,1.25f,41,2.7f);pool.TickVisual(.06f);
            var streamObject=new GameObject("Review stream");streamObject.transform.SetParent(root.transform);streamObject.transform.position=new Vector3(-2.5f,1.8f,.1f);
            var stream=streamObject.AddComponent<SpitterAcidVisual>();stream.Initialize(false,1.25f,42);
            for(int i=0;i<25;i++){float t=i*.012f;stream.transform.position=new Vector3(-2.5f+t*8,1.8f-t*2-t*t*6,.1f);stream.TickVisual(.012f);}
            var splash=SpitterAcidVisual.CreateSplash(new Vector3(.7f,.04f,0),root.transform,41);splash.TickVisual(.13f);
            foreach(var visual in root.GetComponentsInChildren<SpitterAcidVisual>())foreach(var v in visual.Surface.vertices)
                if(float.IsNaN(v.x)||float.IsInfinity(v.x))throw new Exception("Invalid liquid mesh vertex");
            captureAt=EditorApplication.timeSinceStartup+.5; capture=()=>{try{target.Create();RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
            RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1400,900),0,0);image.Apply();
            File.WriteAllBytes("Assets/Verification~/Spitter-acid-preview.png",image.EncodeToPNG());
            var messages=ShaderUtil.GetShaderMessages(Resources.Load<Shader>("SpitterBile"));
            File.WriteAllText("Assets/Verification~/AcidReview.txt","Rendered stream, splash and bubbling puddle. Finite mesh vertices. Shader messages: "+string.Join("; ",Array.ConvertAll(messages,m=>m.severity+": "+m.message)));
            }catch(Exception e){File.WriteAllText("Assets/Verification~/AcidReview.txt",e.ToString());}
            finally{RenderTexture.active=old;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(floorMaterial);EditorSceneManager.ClosePreviewScene(scene);}};
            EditorApplication.QueuePlayerLoopUpdate();
        }
        catch(Exception e){File.WriteAllText("Assets/Verification~/AcidReview.txt",e.ToString());Debug.LogException(e);}
        finally{if(capture==null){RenderTexture.active=old;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}}
    }
}


