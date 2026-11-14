using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class HeartPlatformImport
{
    const string Folder="Assets/LastDeparturePrototype/Resources/HeartPlatform/";
    static HeartPlatformImport(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Assets/Verification~/ImportHeartPlatform.request";
        if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        File.Delete(request);
        try{Import();}catch(Exception e){File.WriteAllText("Assets/Verification~/HeartPlatformImport.txt","FAIL "+e);Debug.LogException(e);}
    }
    [MenuItem("Tools/Yard Survival/Import heart platform")]
    public static void Import()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"PlatformSource.fbx");
        importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.isReadable=true;importer.addCollider=false;importer.SaveAndReimport();
        foreach(var suffix in new[]{"Color","Normal","Metal","Roughness"})
        {
            var texture=(TextureImporter)AssetImporter.GetAtPath(Folder+suffix+".png");
            texture.textureType=suffix=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
            texture.sRGBTexture=suffix=="Color";texture.maxTextureSize=suffix=="Color"?4096:2048;texture.mipmapEnabled=true;texture.SaveAndReimport();
        }
        var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Platform.mat");
        if(material==null){material=new Material(Shader.Find("Dustline/Heart Platform"));AssetDatabase.CreateAsset(material,Folder+"Platform.mat");}
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Color.png"));
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Normal.png"));
        material.SetTexture("_MetalMask",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Metal.png"));
        material.SetTexture("_Roughness",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Roughness.png"));
        EditorUtility.SetDirty(material);
        var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"PlatformSource.fbx"));
        var root=new GameObject("Biomechanical heart platform");var combined=new Mesh{indexFormat=IndexFormat.UInt32};
        try
        {
            var parts=new List<CombineInstance>();
            foreach(var f in source.GetComponentsInChildren<MeshFilter>())for(int sub=0;sub<f.sharedMesh.subMeshCount;sub++)parts.Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=sub,transform=f.transform.localToWorldMatrix});
            combined.CombineMeshes(parts.ToArray());var b=combined.bounds;var vertices=combined.vertices;
            var scale=new Vector3(8.4f/b.size.x,4.7f/b.size.y,8.4f/b.size.z);
            for(int i=0;i<vertices.Length;i++)
            {
                var v=Vector3.Scale(vertices[i]-new Vector3(b.center.x,b.min.y,b.center.z),scale);
                v.x=-v.x;v.z=-v.z; // Face the open front toward the player spawn.
                if(v.y<.5f)v.y=Mathf.Max(0,v.y-.14f*Mathf.Clamp01((new Vector2(v.x,v.z).magnitude-3.5f)/.65f));
                vertices[i]=v;
            }
            combined.vertices=vertices;combined.RecalculateNormals();combined.RecalculateTangents();combined.RecalculateBounds();combined.name="Heart platform game mesh";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"PlatformMesh.asset");
            if(mesh==null){AssetDatabase.CreateAsset(combined,Folder+"PlatformMesh.asset");mesh=combined;}else EditorUtility.CopySerialized(combined,mesh);
            EditorUtility.SetDirty(mesh);
            root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=material;
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"Platform.prefab");AssetDatabase.SaveAssets();
            File.WriteAllText("Assets/Verification~/HeartPlatformImport.txt","PASS "+mesh.triangles.Length/3+" triangles; bounds "+mesh.bounds+"\n");
        }
        finally{UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(root);if(!AssetDatabase.Contains(combined))UnityEngine.Object.DestroyImmediate(combined);}
    }
}
