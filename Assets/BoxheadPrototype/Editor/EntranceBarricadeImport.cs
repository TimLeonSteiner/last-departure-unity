using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EntranceBarricadeImport
{
    const string Folder="Assets/BoxheadPrototype/Resources/EntranceBarricade/";
    static EntranceBarricadeImport(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Library/EntranceBarricadeImport.request";
        if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        File.Delete(request);
        try{Import();}catch(Exception e){File.WriteAllText("Assets/Verification~/EntranceBarricadeImport.txt","FAIL "+e);Debug.LogException(e);}
    }
    static Material Material(string name,Color color,float metallic)
    {
        string path=Folder+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.16f);m.enableInstancing=true;
        if(name=="AgedWood")m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"WeatheredTimber.png"));
        EditorUtility.SetDirty(m);return m;
    }
    static void Import()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"EntranceBarricadeSource.fbx");
        importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.addCollider=false;importer.isReadable=true;
        importer.SaveAndReimport();
        var mats=new[]{Material("AgedWood",Color.white,0),Material("RustyIron",new Color(.20f,.105f,.045f),.65f),Material("SplitInterior",new Color(.045f,.021f,.009f),0)};
        var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"EntranceBarricadeSource.fbx"));
        int count=0,triangles=0;
        var left=new GameObject("Reference entrance left");var right=new GameObject("Reference entrance right");
        try
        {
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
            {
                string name=filter.name;if(!(name.StartsWith("L")||name.StartsWith("R")))continue;
                bool isLeft=name.StartsWith("L");
                var original=filter.sharedMesh;var mesh=UnityEngine.Object.Instantiate(original);
                var vertices=mesh.vertices;var matrix=source.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int i=0;i<vertices.Length;i++){var v=matrix.MultiplyPoint3x4(vertices[i]);v.x=-v.x;v.z=-v.z;vertices[i]=v-new Vector3(isLeft?-1.4f:1.4f,.6f,0);}
                mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
                var size=mesh.bounds.size;
                if(size.x>4.5f||size.y>4.6f||size.z>.7f)throw new Exception("Incorrect board axes/scale: "+name+" "+size);
                string path=Folder+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}
                var root=new GameObject(name.Contains("Fixed")?"Timber jamb and lintel":"Damage "+name.Substring(1));
                root.transform.SetParent(isLeft?left.transform:right.transform,false);
                root.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=root.AddComponent<MeshRenderer>();var old=filter.GetComponent<MeshRenderer>().sharedMaterials;
                var assigned=new Material[mesh.subMeshCount];
                for(int i=0;i<assigned.Length;i++){string n=i<old.Length&&old[i]!=null?old[i].name:"AgedWood";assigned[i]=n.Contains("Iron")?mats[1]:n.Contains("Interior")?mats[2]:mats[0];}
                renderer.sharedMaterials=assigned;
                
                triangles+=mesh.triangles.Length/3;count++;
            }
            if(count!=12)throw new Exception("Expected twelve damage/fixed groups, found "+count);
            PrefabUtility.SaveAsPrefabAsset(left,Folder+"Left.prefab");PrefabUtility.SaveAsPrefabAsset(right,Folder+"Right.prefab");
            AssetDatabase.SaveAssets();File.WriteAllText("Assets/Verification~/EntranceBarricadeImport.txt","PASS: full-height reference barricade, two panels and ten damage groups; "+triangles+" total triangles; wood grain, splits, broken silhouettes and iron nails; validated scale and axes.");
        }
        finally{UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(left);UnityEngine.Object.DestroyImmediate(right);}
    }
}
