using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BuildMenuImages
{
    static readonly string[] Names={"Barricade","Turret","Trap","Barrel","Hammer"};
    static readonly string[] Paths={"Assets/LastDeparturePrototype/Resources/Construction/Barricade.prefab","Assets/LastDeparturePrototype/ThirdParty/AnimatedArsenal/turret-01a/Sentry.prefab","Assets/LastDeparturePrototype/Prefabs/InductionTrap.prefab","Assets/LastDeparturePrototype/Prefabs/ExplosiveBarrel.prefab","Assets/LastDeparturePrototype/ThirdParty/Hammer/PlayableHammer.prefab"};
    const string Folder="Assets/LastDeparturePrototype/Resources/HudBuild/";
    static bool onlyTurret;static int index;static double ready;static GameObject root;static Camera camera;static Scene scene;
    static BuildMenuImages(){EditorApplication.update+=()=>{if(!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&File.Exists("Assets/Verification~/BuildMenuImages.request")){string mode=File.ReadAllText("Assets/Verification~/BuildMenuImages.request").Trim();File.Delete("Assets/Verification~/BuildMenuImages.request");if(mode=="turret"||mode=="hammer"){onlyTurret=true;index=mode=="hammer"?4:1;Prepare();EditorApplication.update+=Tick;}else Run();}};}
    public static void Run()
    {
        Directory.CreateDirectory(Folder);Directory.CreateDirectory("Assets/Verification~");
        onlyTurret=false;index=0;Prepare();
        while(index<Names.Length){ready=0;Tick();}
    }
    static void Prepare()
    {
        for(int s=SceneManager.sceneCount-1;s>=0;s--){var candidate=SceneManager.GetSceneAt(s);foreach(var obj in candidate.GetRootGameObjects())if(obj.name=="Build image studio"){if(Application.isPlaying)SceneManager.UnloadSceneAsync(candidate);else EditorSceneManager.CloseScene(candidate,true);break;}}
        scene=Application.isPlaying?SceneManager.CreateScene("Build image scene "+Guid.NewGuid()):EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);root=new GameObject("Build image studio");SceneManager.MoveGameObjectToScene(root,scene);root.transform.position=new Vector3(10000,10000,10000);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Paths[index]);if(prefab==null)throw new Exception("Missing build model "+Paths[index]);
        var model=UnityEngine.Object.Instantiate(prefab,root.transform);model.transform.localPosition=Vector3.zero;foreach(var t in model.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
        foreach(var behavior in model.GetComponentsInChildren<MonoBehaviour>())behavior.enabled=false;
        var renderers=model.GetComponentsInChildren<Renderer>();
        if(index==4)
        {
            foreach(var animator in model.GetComponentsInChildren<Animator>())animator.enabled=false;
            foreach(var renderer in renderers)renderer.enabled=renderer is MeshRenderer && renderer.name.StartsWith("tool");
            renderers=renderers.Where(r=>r.enabled).ToArray();
            var hammer=renderers[0].transform.parent;
            hammer.SetParent(root.transform,false);
            hammer.localPosition=Vector3.zero;
            hammer.localRotation=Quaternion.Euler(0,0,-30);
            hammer.localScale=Vector3.one;
        }
        if(index==1)foreach(var renderer in renderers)
        {
            var materials=renderer.sharedMaterials;
            for(int m=0;m<materials.Length;m++)
            {
                var copy=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                copy.SetTexture("_BaseMap",materials[m].GetTexture("_BaseMap"));copy.SetColor("_BaseColor",Color.white);materials[m]=copy;
            }
            renderer.sharedMaterials=materials;
        }
        var bounds=renderers[0].bounds;
        foreach(var r in renderers){bounds.Encapsulate(r.bounds);r.allowOcclusionWhenDynamic=false;}
        var camObj=new GameObject("Image camera");camObj.transform.SetParent(root.transform);camera=camObj.AddComponent<Camera>();camera.cullingMask=1<<30;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.orthographic=true;camera.aspect=4f/3;camera.useOcclusionCulling=false;
        camera.transform.position=bounds.center+new Vector3(3,2.1f,index==1?4:-4).normalized*Mathf.Max(5,bounds.size.magnitude*2);camera.transform.LookAt(bounds.center);
        float maxX=0,maxY=0;
        foreach(var r in renderers)for(int k=0;k<8;k++){var b=r.bounds;var corner=b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1));var p=camera.transform.InverseTransformPoint(corner);maxX=Mathf.Max(maxX,Mathf.Abs(p.x));maxY=Mathf.Max(maxY,Mathf.Abs(p.y));}
        camera.orthographicSize=Mathf.Max(maxY,maxX/camera.aspect)*1.14f;camera.farClipPlane=100;
        for(int i=0;i<3;i++){var o=new GameObject("Studio light");o.transform.SetParent(root.transform);var l=o.AddComponent<Light>();l.type=LightType.Directional;l.cullingMask=1<<30;l.intensity=i==0?2.4f:1.1f;l.color=i==2?new Color(.70f,.82f,1):new Color(1,.92f,.82f);o.transform.rotation=Quaternion.Euler(i==0?35:20,(i==0?-35:i==1?150:60)+(index==1?180:0),0);}
        ready=EditorApplication.timeSinceStartup+2;
    }
    static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<ready)return;
        try
        {
            var target=new RenderTexture(512,384,24,RenderTextureFormat.ARGB32);target.Create();var image=new Texture2D(512,384,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
            RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;image.ReadPixels(new Rect(0,0,512,384),0,0);image.Apply();int visible=0;foreach(var pixel in image.GetPixels32())if(pixel.a>10)visible++;if(visible<5000)throw new Exception("Empty model image: "+Names[index]);File.WriteAllBytes(Folder+Names[index]+".png",image.EncodeToPNG());RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(root);if(Application.isPlaying)SceneManager.UnloadSceneAsync(scene);else EditorSceneManager.CloseScene(scene,true);
            if(onlyTurret)index=Names.Length;else index++;
            if(index<Names.Length){Prepare();return;}
            EditorApplication.update-=Tick;AssetDatabase.Refresh();
            foreach(string name in Names){var t=(TextureImporter)AssetImporter.GetAtPath(Folder+name+".png");t.alphaIsTransparency=true;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=512;t.SaveAndReimport();}
            File.WriteAllText("Assets/Verification~/BuildMenuImages.txt","PASS: rendered requested gameplay prefab thumbnails.");AssetDatabase.SaveAssets();if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        catch(Exception e){index=Names.Length;File.WriteAllText("Assets/Verification~/BuildMenuImages.txt","FAIL "+e);EditorApplication.update-=Tick;if(Application.isBatchMode)EditorApplication.Exit(1);else Debug.LogException(e);}
    }
}

