using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class SkyWatcherImport
{
    const string Folder="Assets/LastDeparturePrototype/Resources/SkyWatcher/";
    const string Request="Assets/Verification~/SkyEye/Import3D.request";
    static SkyWatcherImport(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
        File.Delete(Request);
        try{Build();File.WriteAllText("Assets/Verification~/SkyEye/Import3D.txt","PASS: volume mesh, gaze/blink blendshapes and materials imported.");}
        catch(Exception ex){File.WriteAllText("Assets/Verification~/SkyEye/Import3D.txt","FAIL "+ex);Debug.LogException(ex);}
    }
    public static void Build()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"Watcher.fbx");
        importer.importAnimation=false;importer.importBlendShapes=true;importer.isReadable=true;
        importer.importCameras=false;importer.importLights=false;importer.addCollider=false;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
        var normalImporter=(TextureImporter)AssetImporter.GetAtPath(Folder+"SkinNormal.png");
        normalImporter.textureType=TextureImporterType.NormalMap;normalImporter.maxTextureSize=2048;normalImporter.SaveAndReimport();
        var shader=Resources.Load<Shader>("SkyWatcherSurface");
        if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("3D eye surface shader failed");
        foreach(string name in new[]{"Skin","Iris"})
        {
            var texturePath=Folder+"Iris.png";
            var texImporter=(TextureImporter)AssetImporter.GetAtPath(texturePath);
            texImporter.wrapMode=TextureWrapMode.Clamp;texImporter.maxTextureSize=2048;texImporter.mipmapEnabled=true;texImporter.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(texture==null)throw new Exception("Missing "+name+" texture");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+name+".mat");
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,Folder+name+".mat");}
            material.shader=shader;material.renderQueue=name=="Iris"?2980:2990;material.SetFloat("_Eye",name=="Iris"?1:0);material.SetTexture("_BaseMap",texture);material.SetFloat("_Brightness",name=="Iris"?.95f:.85f);EditorUtility.SetDirty(material);
            if(name=="Skin")material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"SkinNormal.png"));
        }
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Watcher.fbx");
        var obj=new GameObject("Watcher");
        var model=UnityEngine.Object.Instantiate(source,obj.transform,false);model.name="Watcher sculpt";
        try
        {
            bool gaze=false,blink=false;int vertices=0;
            foreach(var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                bool eyeball=renderer.name=="Eyeball";gaze|=eyeball;
                renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+(eyeball?"Iris":"Skin")+".mat");
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                if(renderer is SkinnedMeshRenderer skin)
                {
                    skin.updateWhenOffscreen=true;skin.localBounds=skin.sharedMesh.bounds;vertices+=skin.sharedMesh.vertexCount;
                    skin.sharedMaterial.SetVector("_BoundsCenter",skin.sharedMesh.bounds.center);
                    skin.sharedMaterial.SetVector("_BoundsExtent",skin.sharedMesh.bounds.extents);
                    EditorUtility.SetDirty(skin.sharedMaterial);
                    for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)
                    {blink|=skin.sharedMesh.GetBlendShapeName(i).Contains("Blink");gaze|=skin.sharedMesh.GetBlendShapeName(i).Contains("LookHorizontal");}
                }
                else if(renderer.TryGetComponent<MeshFilter>(out var filter)){vertices+=filter.sharedMesh.vertexCount;renderer.sharedMaterial.SetVector("_BoundsCenter",filter.sharedMesh.bounds.center);renderer.sharedMaterial.SetVector("_BoundsExtent",filter.sharedMesh.bounds.extents);EditorUtility.SetDirty(renderer.sharedMaterial);}
            }
            if(!gaze||!blink)throw new Exception("Missing gaze or eyelid blendshape");
            if(vertices>180000)throw new Exception("Sky watcher exceeds vertex budget: "+vertices);
            PrefabUtility.SaveAsPrefabAsset(obj,Folder+"SkyWatcherRig.prefab");AssetDatabase.SaveAssets();
        }
        finally{UnityEngine.Object.DestroyImmediate(obj);}
    }
}




