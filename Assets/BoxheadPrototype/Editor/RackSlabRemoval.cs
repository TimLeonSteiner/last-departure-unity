using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RackSlabRemoval
{
    const string Prefab="Assets/BoxheadPrototype/Resources/Spaceport/ModularHallTerminalSet.prefab";
    const string Folder="Assets/BoxheadPrototype/Resources/Spaceport/WithoutRackSlabs/";
    static RackSlabRemoval(){EditorApplication.update+=()=>{
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/RemoveRackSlabs.request"))return;
        File.Delete("Library/RemoveRackSlabs.request");Remove();
    };}
    public static bool InGate(Vector3 p)
    {
        const float scale=23f/17f;
        return p.y>=-.015f&&p.y<=.245f&&Mathf.Abs(Mathf.Abs(p.x)-10*scale)<4.06f&&Mathf.Abs(Mathf.Abs(p.z)-9*scale)<2.71f;
    }
    public static void Remove()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var root=PrefabUtility.LoadPrefabContents(Prefab);int removed=0;
        try
        {
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.name!="Spaceport_RackMetal")continue;
                var source=filter.sharedMesh;if(source==null)continue;var vertices=source.vertices;var mask=new bool[vertices.Length];
                for(int v=0;v<vertices.Length;v++)mask[v]=InGate(root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[v])));
                Mesh clone=null;
                for(int sub=0;sub<source.subMeshCount;sub++)
                {
                    var triangles=source.GetTriangles(sub);var kept=new List<int>();
                    for(int i=0;i<triangles.Length;i+=3)
                    {if(mask[triangles[i]]&&mask[triangles[i+1]]&&mask[triangles[i+2]]){removed++;continue;}kept.Add(triangles[i]);kept.Add(triangles[i+1]);kept.Add(triangles[i+2]);}
                    if(kept.Count==triangles.Length)continue;
                    if(clone==null)clone=Object.Instantiate(source);clone.SetTriangles(kept,sub);
                }
                if(clone==null)continue;
                string path=Folder+filter.name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null){saved=clone;AssetDatabase.CreateAsset(saved,path);}else{EditorUtility.CopySerialized(clone,saved);EditorUtility.SetDirty(saved);Object.DestroyImmediate(clone);}
                filter.sharedMesh=saved;
            }
            if(root.transform.Find("Rack slabs removed")==null)new GameObject("Rack slabs removed").transform.SetParent(root.transform,false);
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);AssetDatabase.SaveAssets();
            File.WriteAllText("Assets/Verification~/RackSlabRemoval.txt","Removed "+removed+" rack slab triangles from terminal copies; source models retained.");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
