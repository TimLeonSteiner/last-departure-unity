using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class OriginalPickupImport
{
    const string Folder="Assets/LastDeparturePrototype/Resources/OriginalPickups/";
    const string Request="Assets/Verification~/OriginalPickupsImport.request";
    static OriginalPickupImport(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        File.Delete(Request);
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Build("Caffeine");Build("AmmoBox");AssetDatabase.SaveAssets();
            File.WriteAllText("Assets/Verification~/OriginalPickupsImport.txt","PASS Both original pickup prefabs imported with URP PBR materials, two LODs and no blocking colliders.\n");
        }
        catch(Exception e){File.WriteAllText("Assets/Verification~/OriginalPickupsImport.txt","FAIL "+e);Debug.LogException(e);}
    }
    static void Build(string name)
    {
        for(int i=0;i<2;i++)
        {
            string path=Folder+name+"_LOD"+i+".fbx";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.addCollider=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.isReadable=false;
            importer.importTangents=ModelImporterTangents.CalculateMikk;importer.SaveAndReimport();
        }
        foreach(string suffix in new[]{"Color","Normal","MetallicSmoothness"})
        {
            string path=Folder+name+suffix+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var texture=(TextureImporter)AssetImporter.GetAtPath(path);
            texture.textureType=suffix=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
            texture.sRGBTexture=suffix=="Color";texture.alphaIsTransparency=false;texture.maxTextureSize=2048;texture.mipmapEnabled=true;
            texture.textureCompression=TextureImporterCompression.CompressedHQ;texture.SaveAndReimport();
        }
        string materialPath=Folder+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath);}
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+name+"Color.png"));
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+name+"Normal.png"));
        material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+name+"MetallicSmoothness.png"));
        material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",1);material.SetFloat("_BumpScale",1);
        material.EnableKeyword("_NORMALMAP");material.EnableKeyword("_METALLICSPECGLOSSMAP");material.enableInstancing=true;EditorUtility.SetDirty(material);
        var root=new GameObject(name);
        try
        {
            var lods=new LOD[2];
            for(int i=0;i<2;i++)
            {
                var child=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+"_LOD"+i+".fbx"),root.transform);
                child.name="LOD"+i;
                var renderers=child.GetComponentsInChildren<Renderer>();
                foreach(var renderer in renderers){renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;}
                lods[i]=new LOD(i==0?.08f:.001f,renderers);
            }
            var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
            if(group.size>1||group.size<.1f)throw new Exception("Pickup scale invalid: "+name+" size="+group.size);
            PrefabUtility.SaveAsPrefabAsset(root,Folder+name+".prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
