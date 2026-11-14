using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CeilingFinishUpdate
{
    const string Request="Assets/Verification~/CeilingFinishUpdate.request";
    static CeilingFinishUpdate(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        File.Delete(Request);
        try{Apply();}catch(Exception e){File.WriteAllText("Assets/Verification~/CeilingFinishUpdate.txt","FAIL "+e);Debug.LogException(e);}
    }
    static bool OnCable(Vector3 p)
    {
        if(p.y<2.60f||p.y>7.16f)return false;
        foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})for(int k=0;k<4;k++)
        {
            float x=sx*13.529f-1.2f+k*.45f,z=sz*12.176f;
            var points=new[]{new Vector3(x,2.66f,z),new Vector3(x,3.2f,z+.3f),new Vector3(x+.6f,5.7f,z+1),new Vector3(x+1,7.1f,z+2)};
            for(int i=0;i<3;i++){var d=points[i+1]-points[i];float t=Mathf.Clamp01(Vector3.Dot(p-points[i],d)/d.sqrMagnitude);if((p-points[i]-t*d).sqrMagnitude<.06f*.06f)return true;}
        }
        return false;
    }
    static void Apply()
    {
        const string folder="Assets/LastDeparturePrototype/Resources/Spaceport/CeilingKit/";
        foreach(string kind in new[]{"Intact","Damaged","Truss"})
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(folder+kind+".mat");
            mat.SetColor("_BaseColor",kind=="Damaged"?new Color(.50f,.42f,.35f,1):new Color(.62f,.51f,.42f,1));mat.SetFloat("_Smoothness",.22f);EditorUtility.SetDirty(mat);
        }
        const string path="Assets/LastDeparturePrototype/Resources/Spaceport/ModularHallTerminalSet.prefab";
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));int removed=0;
        try
        {
            foreach(var f in root.GetComponentsInChildren<MeshFilter>())
            {
                if(f.name!="Spaceport_Ink")continue;
                var source=f.sharedMesh;var mesh=UnityEngine.Object.Instantiate(source);var vertices=source.vertices;var candidate=new bool[vertices.Length];
                var parent=new int[vertices.Length];for(int i=0;i<parent.Length;i++)parent[i]=i;
                int Find(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
                foreach(int sub in System.Linq.Enumerable.Range(0,source.subMeshCount))
                {var indices=source.GetTriangles(sub);for(int i=0;i<indices.Length;i+=3){parent[Find(indices[i+1])]=Find(indices[i]);parent[Find(indices[i+2])]=Find(indices[i]);}}
                var bounds=new Dictionary<int,Bounds>();
                for(int i=0;i<vertices.Length;i++)
                {int id=Find(i);var p=root.transform.InverseTransformPoint(f.transform.TransformPoint(vertices[i]));if(bounds.TryGetValue(id,out var b)){b.Encapsulate(p);bounds[id]=b;}else bounds[id]=new Bounds(p,Vector3.zero);}
                for(int i=0;i<vertices.Length;i++)
                {var b=bounds[Find(i)];candidate[i]=b.min.y>2.3f&&b.max.y>6.7f&&b.max.y<8.4f&&b.size.y>3&&b.size.x<3&&b.size.z<3;}
                File.WriteAllText("Assets/Verification~/CeilingCableBounds.txt",f.name+" "+source.name+"\n"+string.Join("\n",System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(bounds.Values,b=>b.max.y>5),b=>b.ToString())));
                for(int sub=0;sub<source.subMeshCount;sub++)
                {
                    var faces=source.GetTriangles(sub);var keep=new List<int>();
                    for(int i=0;i<faces.Length;i+=3)
                    {if(candidate[faces[i]]&&candidate[faces[i+1]]&&candidate[faces[i+2]]){removed++;continue;}keep.Add(faces[i]);keep.Add(faces[i+1]);keep.Add(faces[i+2]);}
                    mesh.SetTriangles(keep,sub);
                }
                mesh.RecalculateBounds();string asset=folder+"TerminalInkWithoutRackLooms.asset";
                var previous=AssetDatabase.LoadAssetAtPath<Mesh>(asset);
                if(previous==null){AssetDatabase.CreateAsset(mesh,asset);f.sharedMesh=mesh;}else{EditorUtility.CopySerialized(mesh,previous);UnityEngine.Object.DestroyImmediate(mesh);f.sharedMesh=previous;}
            }
            if(removed==0)throw new Exception("No old rack cable triangles matched; inspect before changing the prefab.");
            PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
            File.WriteAllText("Assets/Verification~/CeilingFinishUpdate.txt","PASS warmer dark steel finish, smoothness 0.22; removed "+removed+" old rack cable triangles.");
        }finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
