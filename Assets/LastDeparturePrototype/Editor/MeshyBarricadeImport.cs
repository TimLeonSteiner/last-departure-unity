using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class MeshyBarricadeImport
{
    const string Folder="Assets/LastDeparturePrototype/Resources/Construction/";
    static MeshyBarricadeImport(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Assets/Verification~/ImportBarricade.request";
        if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        File.Delete(request);
        try{Import();}catch(Exception e){File.WriteAllText("Assets/Verification~/BarricadeImport.txt","FAIL "+e);Debug.LogException(e);}
    }
    [MenuItem("Tools/Yard Survival/Import Meshy barricade")]
    public static void Import()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"BarricadeSource.fbx");
        importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.isReadable=true;importer.addCollider=false;importer.SaveAndReimport();
        foreach(var suffix in new[]{"Color","Normal","MetallicSmoothness"})
        {
            var texture=(TextureImporter)AssetImporter.GetAtPath(Folder+"Barricade"+suffix+".png");
            texture.textureType=suffix=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
            texture.sRGBTexture=suffix=="Color";texture.maxTextureSize=2048;texture.mipmapEnabled=true;texture.SaveAndReimport();
        }
        var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Barricade.mat");
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Folder+"Barricade.mat");}
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"BarricadeColor.png"));
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"BarricadeNormal.png"));
        material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"BarricadeMetallicSmoothness.png"));
        material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.65f);
        material.EnableKeyword("_NORMALMAP");material.EnableKeyword("_METALLICSPECGLOSSMAP");material.enableInstancing=true;EditorUtility.SetDirty(material);
        var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"BarricadeSource.fbx"));
        var root=new GameObject("Meshy salvaged barricade");
        var combined=new Mesh{indexFormat=IndexFormat.UInt32};
        try
        {
            var parts=new List<CombineInstance>();
            foreach(var f in source.GetComponentsInChildren<MeshFilter>())for(int sub=0;sub<f.sharedMesh.subMeshCount;sub++)parts.Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=sub,transform=f.transform.localToWorldMatrix});
            combined.CombineMeshes(parts.ToArray());var b=combined.bounds;
            var vertices=combined.vertices;
            if(b.size.x<.001f||b.size.y<.001f||b.size.z<.001f)throw new Exception("Invalid barricade bounds");
            // Match the existing 2.8 x 1.2 x .65 gameplay volume, centered at y=.6.
            for(int i=0;i<vertices.Length;i++)vertices[i]=Vector3.Scale(vertices[i]-b.center,new Vector3(2.8f/b.size.x,1.2f/b.size.y,.65f/b.size.z));
            combined.vertices=vertices;combined.RecalculateNormals();combined.RecalculateTangents();combined.RecalculateBounds();
            var triangles=combined.triangles;var bands=new List<int>[5];for(int i=0;i<5;i++)bands[i]=new List<int>();
            for(int i=0;i<triangles.Length;i+=3)
            {
                float y=(vertices[triangles[i]].y+vertices[triangles[i+1]].y+vertices[triangles[i+2]].y)/3;
                int band=Mathf.Clamp(Mathf.FloorToInt((y+.6f)/.24f),0,4);
                bands[band].Add(triangles[i]);bands[band].Add(triangles[i+1]);bands[band].Add(triangles[i+2]);
            }
            for(int i=0;i<5;i++)
            {
                var mesh=UnityEngine.Object.Instantiate(combined);mesh.name="Barricade damage section "+i;mesh.triangles=bands[i].ToArray();mesh.RecalculateBounds();
                string path=Folder+"Section"+i+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}
                var child=new GameObject("Damage "+i);child.transform.SetParent(root.transform,false);
                child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"Barricade.prefab");AssetDatabase.SaveAssets();
            File.WriteAllText("Assets/Verification~/BarricadeImport.txt","PASS Meshy barricade, "+triangles.Length/3+" triangles; five damage sections; 2.8 x 1.2 x .65 m.\n");
        }
        finally{UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(combined);}
    }
}
