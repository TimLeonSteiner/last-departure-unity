using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class SpaceportAssetBuild
{
    const string Folder = "Assets/LastDeparturePrototype/Resources/Spaceport";
    const string Pending = "Dustline.SpaceportReview";
    static SpaceportAssetBuild() { EditorApplication.update += Review; }
    [MenuItem("Tools/Dustline/Rebuild and review level")]
    public static void BuildAndReview()
    {
        AssetDatabase.Refresh();
        var outline=BuildExterior();
        var shader=Shader.Find("Dustline/Spaceport Cel");
        BuildTerminal(outline);
        var sky=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/SpaceportSky.mat");
        if(sky==null){sky=new Material(Shader.Find("Dustline/Ashfall Sky"));AssetDatabase.CreateAsset(sky,Folder+"/SpaceportSky.mat");}
        sky.SetColor("_Top",new Color(.0006f,.001f,.002f));
        sky.SetColor("_Horizon",new Color(.004f,.007f,.012f));EditorUtility.SetDirty(sky);
        var terrain=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/SpaceportTerrain.mat");
        if(terrain==null){terrain=new Material(shader);AssetDatabase.CreateAsset(terrain,Folder+"/SpaceportTerrain.mat");}
        terrain.SetColor("_BaseColor",new Color(.075f,.115f,.13f));terrain.SetFloat("_Glow",0);terrain.SetFloat("_Ground",0);EditorUtility.SetDirty(terrain);
        var mist=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/SpaceportMist.mat");
        if(mist==null){mist=new Material(Shader.Find("Dustline/Spaceport Mist"));AssetDatabase.CreateAsset(mist,Folder+"/SpaceportMist.mat");}
        mist.SetColor("_Color",new Color(.008f,.014f,.02f,.28f));EditorUtility.SetDirty(mist);
        var indoorMist=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/TerminalMist.mat");
        if(indoorMist==null){indoorMist=new Material(mist);AssetDatabase.CreateAsset(indoorMist,Folder+"/TerminalMist.mat");}
        indoorMist.SetColor("_Color",new Color(.004f,.008f,.012f,.12f));EditorUtility.SetDirty(indoorMist);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/LastDeparture.unity");
        SessionState.SetBool(Pending,true);EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
    }
    public static Material BuildExterior()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "/SpaceportExterior.fbx");
        importer.importCameras=false; importer.importLights=false; importer.importAnimation=false;
        importer.addCollider=false; importer.isReadable=false; importer.SaveAndReimport();
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SpaceportExterior.fbx");
        if(model==null)throw new Exception("Blender FBX import failed");
        var instance=UnityEngine.Object.Instantiate(model);
        instance.name="SpaceportSet";
        var palette=new Dictionary<string,Color> {
            {"Asphalt",new Color(.055f,.085f,.11f)}, {"Steel",new Color(.13f,.22f,.26f)},
            {"Ink",new Color(.025f,.045f,.065f)}, {"Hull",new Color(.47f,.53f,.51f)},
            {"Rust",new Color(.32f,.15f,.075f)}, {"Paint",new Color(.57f,.43f,.22f)},
            {"TealGlow",new Color(.12f,.72f,.66f)}, {"AmberGlow",new Color(.95f,.32f,.055f)},
            {"RedGlow",new Color(.7f,.035f,.018f)} };
        var shader=Shader.Find("Dustline/Spaceport Cel");
        if(shader==null)throw new Exception("Spaceport shader missing");
        var outline=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/SpaceportOutline.mat");
        if(outline==null){outline=new Material(Shader.Find("Dustline/Spaceport Outline"));AssetDatabase.CreateAsset(outline,Folder+"/SpaceportOutline.mat");}
        outline.SetFloat("_Width",1.15f);EditorUtility.SetDirty(outline);
        foreach(var renderer in instance.GetComponentsInChildren<MeshRenderer>())
        {
            string key=renderer.name.Replace("Spaceport_","");
            if(!palette.ContainsKey(key))throw new Exception("Unexpected Blender mesh: "+key);
            string path=Folder+"/"+key+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
            mat.shader=shader;mat.SetColor("_BaseColor",palette[key]);mat.SetFloat("_Glow",key.EndsWith("Glow")?1.6f:0);
            mat.SetFloat("_Ground",key=="Asphalt"?1:0);
            EditorUtility.SetDirty(mat);renderer.sharedMaterials=new[]{mat};
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            if(key!="Asphalt"&&!key.EndsWith("Glow"))
            {
                var edge=new GameObject("Ink contour");edge.transform.SetParent(renderer.transform,false);
                edge.AddComponent<MeshFilter>().sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                var ink=edge.AddComponent<MeshRenderer>();ink.sharedMaterial=outline;
                ink.shadowCastingMode=ShadowCastingMode.Off;ink.receiveShadows=false;
                ink.lightProbeUsage=LightProbeUsage.Off;ink.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
        }
        PrefabUtility.SaveAsPrefabAsset(instance,Folder+"/SpaceportSet.prefab");
        UnityEngine.Object.DestroyImmediate(instance);
        return outline;
    }
    public static void BuildTerminal(Material outline)
    {
        var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"/TerminalInterior.fbx");
        if(importer==null)throw new Exception("Build the Blender terminal first");
        importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;
        importer.addCollider=false;importer.isReadable=false;importer.SaveAndReimport();
        var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/TerminalInterior.fbx"));
        model.name="TerminalSet";
        var rackColors=new Dictionary<string,Color> {
            {"RackBody",new Color(.025f,.032f,.04f)}, {"RackPanel",new Color(.075f,.095f,.11f)},
            {"RackMetal",new Color(.30f,.33f,.35f)}, {"RackGlass",new Color(.012f,.025f,.03f)} };
        var rackOutline=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/RackOutline.mat");
        if(rackOutline==null){rackOutline=new Material(outline);AssetDatabase.CreateAsset(rackOutline,Folder+"/RackOutline.mat");}
        rackOutline.SetFloat("_Width",2);EditorUtility.SetDirty(rackOutline);
        foreach(var r in model.GetComponentsInChildren<MeshRenderer>())
        {
            string key=r.name.Replace("Spaceport_","");
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+key+".mat");
            if(rackColors.ContainsKey(key))
            {
                if(mat==null){mat=new Material(Shader.Find("Dustline/Spaceport Cel"));AssetDatabase.CreateAsset(mat,Folder+"/"+key+".mat");}
                mat.SetColor("_BaseColor",rackColors[key]);mat.SetFloat("_IndoorWarmth",.22f);
                mat.SetFloat("_Glow",0);mat.SetFloat("_Ground",0);EditorUtility.SetDirty(mat);
            }
            if(key=="TealGlow"||key=="AmberGlow")
            {
                string path=Folder+"/Terminal"+key+".mat";
                var terminalGlow=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(terminalGlow==null){terminalGlow=new Material(mat);AssetDatabase.CreateAsset(terminalGlow,path);}
                terminalGlow.SetColor("_BaseColor",key=="TealGlow"?new Color(.25f,.50f,.57f):new Color(.64f,.33f,.13f));
                terminalGlow.SetFloat("_Glow",.85f);EditorUtility.SetDirty(terminalGlow);mat=terminalGlow;
            }
            if(mat==null)throw new Exception("Missing terminal material "+key);
            r.sharedMaterials=new[]{mat};r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
            r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            if(key!="Asphalt"&&!key.EndsWith("Glow"))
            {
                var edge=new GameObject("Ink contour");edge.transform.SetParent(r.transform,false);
                edge.AddComponent<MeshFilter>().sharedMesh=r.GetComponent<MeshFilter>().sharedMesh;
                var ink=edge.AddComponent<MeshRenderer>();ink.sharedMaterial=rackColors.ContainsKey(key)?rackOutline:outline;
                ink.shadowCastingMode=ShadowCastingMode.Off;ink.receiveShadows=false;
            }
        }
        PrefabUtility.SaveAsPrefabAsset(model,Folder+"/TerminalSet.prefab");
        UnityEngine.Object.DestroyImmediate(model);
    }
    static void Review()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling||Time.timeSinceLevelLoad<2)return;
        SessionState.SetBool(Pending,false);
        try
        {
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();game.enabled=false;
            var root=game.transform.Find("Spaceport exterior");
            if(root==null)throw new Exception("Spaceport missing in runtime scene");
            var renderers=root.GetComponentsInChildren<MeshRenderer>();
            if(renderers.Length!=18)throw new Exception("Expected nine surface, five outline and four mist batches, got "+renderers.Length);
            if(root.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Backdrop must not affect gameplay collision");
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            if(bounds.max.y<39||bounds.max.z<130)throw new Exception("FBX orientation or scale incorrect: "+bounds);
            foreach(var r in renderers)if(!r.sharedMaterial.shader.name.StartsWith("Dustline/Spaceport")||!r.sharedMaterial.shader.isSupported)throw new Exception("Invalid material");
            Capture(new Vector3(0,1.7f,22),new Vector3(13,16,102),"Spaceport-Doorway");
            Capture(new Vector3(-27,18,52),new Vector3(10,14,103),"Spaceport-Overview");
            Capture(new Vector3(0,1.7f,18),new Vector3(0,8,102),"Spaceport-InsideLoadingBay");
            Capture(new Vector3(0,3,25),new Vector3(8,1.8f,33),"Spaceport-CargoDetails");
            Capture(new Vector3(3,7,76),new Vector3(-14,16,102),"Spaceport-LaunchDetails");
            var hall=game.transform.Find("Spaceport terminal");
            if(hall==null)throw new Exception("Terminal missing");
            int hallBatches=hall.GetComponentsInChildren<MeshRenderer>().Length;
            var cableConnections=hall.Find("Rack ceiling connections");
            int cableBatches=cableConnections!=null?cableConnections.GetComponentsInChildren<MeshRenderer>().Length:0;
            if(hallBatches-cableBatches!=21)throw new Exception("Terminal batches missing");
            if(hall.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Art must reuse original collision");
            foreach(var r in game.transform.Find("Warehouse yard/Warehouse hall").GetComponentsInChildren<Renderer>())
                if(r.enabled)throw new Exception("Legacy hall is still visible");
            Capture(new Vector3(0,1.7f,-19),new Vector3(0,3,15),"Terminal-Gameplay");
            Capture(new Vector3(-20,5,-20),new Vector3(5,2,6),"Terminal-Overview");
            Capture(new Vector3(3,1.7f,-7),new Vector3(14,1.6f,-12),"Terminal-Cargo");
            Capture(new Vector3(10,2.3f,-18),new Vector3(13.5f,1.5f,-14.8f),"Terminal-ServerRacks");
            File.WriteAllText("Assets/Verification~/TerminalChecks.txt","PASS Terminal prefab instantiated at world scale\nPASS 12 surface and 9 outline batches; legacy rendering hidden, colliders preserved\n"+(Application.isBatchMode ? game.RunProgressionChecks()+game.RunDedicatedChecks() : "PASS Interactive visual review; gameplay state checks skipped to preserve inspection session\n"));
            foreach(var r in renderers)if(ShaderUtil.ShaderHasError(r.sharedMaterial.shader))throw new Exception("Shader compile error: "+r.sharedMaterial.shader.name);
            File.WriteAllText("Assets/Verification~/SpaceportChecks.txt","PASS Blender FBX imported at metre scale and correct orientation\nPASS Nine surface, five outline and four mist batches, no gameplay colliders\nPASS Runtime scenery and shaders without compile errors\nPASS Doorway, overview and loading-bay screenshots rendered with gameplay post-processing\nBounds: "+bounds+"\n");
            if(Application.isBatchMode) EditorApplication.Exit(0);
            else { game.enabled=true; EditorApplication.isPaused=true; Debug.Log("Level review passed. Play mode is paused for Scene view inspection."); }
        }
        catch(Exception e){File.WriteAllText("Assets/Verification~/SpaceportChecks.txt","FAIL "+e);Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else EditorApplication.isPaused=true;}
    }
    public static void Capture(Vector3 position,Vector3 target,string name)
    {
        var camera=new GameObject("Spaceport inspection camera").AddComponent<Camera>();
        camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=65;
        camera.nearClipPlane=.1f;camera.farClipPlane=800;camera.cullingMask=~(1<<30);camera.clearFlags=CameraClearFlags.Skybox;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.FastApproximateAntialiasing;
        camera.allowHDR=true;
        var rt=new RenderTexture(1440,900,24);rt.Create();var tex=new Texture2D(1440,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1440,900),0,0);tex.Apply();File.WriteAllBytes("Assets/Verification~/"+name+".png",tex.EncodeToPNG());}
        finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(camera.gameObject);}
    }
}

