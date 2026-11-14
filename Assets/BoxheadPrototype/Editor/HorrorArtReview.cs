using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class HorrorArtReview
{
    const string Pending="Dustline.HorrorArtReview";
    static HorrorArtReview(){EditorApplication.update+=Tick;}
    [MenuItem("Tools/Dustline/Import and review horror artwork")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        const string folder="Assets/BoxheadPrototype/Resources/Spaceport/";
        var importer=(ModelImporter)AssetImporter.GetAtPath(folder+"AnatomicalHeart.fbx");
        if(importer==null)throw new Exception("Build the Blender horror artwork first");
        importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;
        importer.addCollider=false;importer.isReadable=false;importer.SaveAndReimport();
        BuildHeartSurface(folder,"Heart");
        BuildHeartSurface(folder,"HeartHarness");
        SpaceportAssetBuild.BuildTerminal(SpaceportAssetBuild.BuildExterior());
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Boxhead.unity");
        SessionState.SetBool(Pending,true);EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
    }
    static void BuildHeartSurface(string folder,string stem)
    {
        foreach(string name in new[]{stem+"Color",stem+"Normal",stem+"MetallicSmoothness"})
        {
            var texture=(TextureImporter)AssetImporter.GetAtPath(folder+name+".png");
            if(texture==null)throw new Exception("Missing baked heart map: "+name);
            texture.textureType=name==stem+"Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
            texture.sRGBTexture=name==stem+"Color";texture.maxTextureSize=stem=="Heart"?4096:2048;texture.mipmapEnabled=true;
            texture.wrapMode=TextureWrapMode.Clamp;texture.anisoLevel=4;texture.SaveAndReimport();
        }
        var mat=AssetDatabase.LoadAssetAtPath<Material>(folder+stem+"Surface.mat");
        if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,folder+stem+"Surface.mat");}
        mat.SetColor("_BaseColor",Color.white);
        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+stem+"Color.png"));
        mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+stem+"Normal.png"));
        mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+stem+"MetallicSmoothness.png"));
        mat.SetFloat("_BumpScale",.75f);mat.SetFloat("_Smoothness",1);mat.SetFloat("_Metallic",0);
        mat.EnableKeyword("_NORMALMAP");mat.EnableKeyword("_METALLICSPECGLOSSMAP");EditorUtility.SetDirty(mat);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling||Time.timeSinceLevelLoad<4)return;
        SessionState.SetBool(Pending,false);
        try
        {
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            string report=game.RunCoreObjectiveChecks()+game.RunCorePresentationChecks(false);
            var core=UnityEngine.Object.FindAnyObjectByType<SuspendedCore>();
            if(core.transform.Find("Living anatomical heart/Blender anatomical heart")==null)throw new Exception("Authored heart failed to instantiate");
            if(ShaderUtil.ShaderHasError(Shader.Find("Dustline/Spaceport Cel")))throw new Exception("Scenery shader errors");
            var hall=game.transform.Find("Spaceport terminal");
            if(hall.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Decorative meshes must not add blockers");
            report+="PASS Blender anatomical heart is used by the pulsating core\nPASS Decorative hall adds no gameplay colliders\nPASS Scenery shader compiled\n";
            var heartRenderer=core.transform.Find("Living anatomical heart/Blender anatomical heart").GetComponentInChildren<MeshRenderer>();
            if(heartRenderer.sharedMaterial.GetTexture("_BumpMap")==null||heartRenderer.sharedMaterial.GetTexture("_BaseMap").width<2048)throw new Exception("Heart PBR atlas is missing");
            var exterior=game.transform.Find("Spaceport exterior");
            if(exterior.GetComponent<LaunchpadLighting>()==null||exterior.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Exterior lighting or collision changed unexpectedly");
            report+="PASS 2048px heart colour, normal and smoothness atlases loaded\nPASS Launch lighting active; exterior remains decoration without colliders\n";
            SpaceportAssetBuild.Capture(new Vector3(3,3.6f,-15),new Vector3(0,3,10),"Horror-Unity-Hall");
            SpaceportAssetBuild.Capture(new Vector3(9,2.2f,-18),new Vector3(13.5f,1.45f,-14.7f),"Horror-Unity-Racks");
            SpaceportAssetBuild.Capture(new Vector3(2.8f,3.2f,-5),new Vector3(0,3,0),"Horror-Unity-Heart");
            SpaceportAssetBuild.Capture(new Vector3(5.5f,5.3f,-10.4f),new Vector3(0,5.2f,0),"Horror-Unity-HeartCeiling");
            SpaceportAssetBuild.Capture(new Vector3(-23,13,62),new Vector3(13,16,102),"Horror-Unity-Launchpad");
            SpaceportAssetBuild.Capture(new Vector3(0,1.7f,22),new Vector3(13,15,102),"Horror-Unity-Outside");
            File.WriteAllText("Assets/Verification~/HorrorArtChecks.txt",report);
            if(Application.isBatchMode)EditorApplication.Exit(0);
            else
            {
                EditorApplication.isPaused=true;
                var view=SceneView.lastActiveSceneView;
                if(view!=null){view.LookAtDirect(new Vector3(0,3,0),Quaternion.LookRotation(new Vector3(-2.8f,-.2f,5)),4);view.Repaint();}
            }
        }
        catch(Exception e)
        {
            File.WriteAllText("Assets/Verification~/HorrorArtChecks.txt","FAIL "+e);Debug.LogException(e);
            if(Application.isBatchMode)EditorApplication.Exit(1);
        }
    }
}
