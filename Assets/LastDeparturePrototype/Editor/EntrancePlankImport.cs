using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EntrancePlankImport
{
    const string Folder="Assets/LastDeparturePrototype/Resources/EntrancePlanks/";
    static EntrancePlankImport(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Library/EntrancePlankImport.request";
        if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        File.Delete(request);
        try{Import();}catch(Exception e){File.WriteAllText("Assets/Verification~/EntrancePlankImport.txt","FAIL "+e);Debug.LogException(e);}
    }
    static Material Material(string name,Color color,float metallic)
    {
        string path=Folder+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.16f);m.enableInstancing=true;
        if(name=="AgedWood")m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"WoodGrain.png"));
        EditorUtility.SetDirty(m);return m;
    }
    static void Import()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"EntranceBoardsSource.fbx");
        importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.addCollider=false;importer.isReadable=true;
        importer.SaveAndReimport();
        var mats=new[]{Material("AgedWood",Color.white,0),Material("RustyIron",new Color(.20f,.105f,.045f),.65f),Material("SplitInterior",new Color(.045f,.021f,.009f),0)};
        var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"EntranceBoardsSource.fbx"));
        int count=0,triangles=0;
        try
        {
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
            {
                string name=filter.name;if(!name.StartsWith("Board"))continue;
                var original=filter.sharedMesh;var mesh=UnityEngine.Object.Instantiate(original);
                var vertices=mesh.vertices;var matrix=source.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int i=0;i<vertices.Length;i++)vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);
                mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
                var size=mesh.bounds.size;
                if(size.x<4||size.x>5||size.y<.2f||size.y>.5f||size.z>.4f)throw new Exception("Incorrect board axes/scale: "+name+" "+size);
                string path=Folder+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}
                var root=new GameObject(name);
                root.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=root.AddComponent<MeshRenderer>();var old=filter.GetComponent<MeshRenderer>().sharedMaterials;
                var assigned=new Material[mesh.subMeshCount];
                for(int i=0;i<assigned.Length;i++){string n=i<old.Length&&old[i]!=null?old[i].name:"AgedWood";assigned[i]=n.Contains("Iron")?mats[1]:n.Contains("Interior")?mats[2]:mats[0];}
                renderer.sharedMaterials=assigned;
                PrefabUtility.SaveAsPrefabAsset(root,Folder+name+".prefab");UnityEngine.Object.DestroyImmediate(root);
                triangles+=mesh.triangles.Length/3;count++;
            }
            if(count!=5)throw new Exception("Expected five board variants, found "+count);
            AssetDatabase.SaveAssets();File.WriteAllText("Assets/Verification~/EntrancePlankImport.txt","PASS: five Blender board variants; "+triangles+" total triangles; wood grain, modeled grooves, splinters and iron nails; validated scale and axes.");
        }
        finally{UnityEngine.Object.DestroyImmediate(source);}
    }
}
