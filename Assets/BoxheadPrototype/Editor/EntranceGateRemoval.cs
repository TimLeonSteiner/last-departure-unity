using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EntranceGateRemoval
{
    const string Prefab="Assets/BoxheadPrototype/Resources/Spaceport/ModularHallTerminalSet.prefab";
    const string Folder="Assets/BoxheadPrototype/Resources/Spaceport/OpenEntrances/";
    static EntranceGateRemoval(){EditorApplication.update+=()=>{
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Library/RemoveEntranceGates.request"))return;
        File.Delete("Library/RemoveEntranceGates.request");Remove();
    };}
    public static bool InGate(Vector3 p)
    {
        for(int side=0;side<4;side++)
        {
            var q=Quaternion.Euler(0,side*90,0)*p;float x=Mathf.Abs(q.x);
            if(x>2.75f&&x<3.58f&&q.z>21.2f&&q.z<22.15f&&q.y>-.1f&&q.y<4.25f)return true;
            if(x>3.1f&&x<4.15f&&q.z>22.42f&&q.z<23.15f&&q.y>3.35f&&q.y<5.4f)return true;
        }
        return false;
    }
    public static void Remove()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var root=PrefabUtility.LoadPrefabContents(Prefab);int removed=0;
        try
        {
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if(!filter.name.StartsWith("Spaceport_"))continue;
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
            if(root.transform.Find("Open entrances without gate posts")==null)new GameObject("Open entrances without gate posts").transform.SetParent(root.transform,false);
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);AssetDatabase.SaveAssets();
            File.WriteAllText("Assets/Verification~/EntranceGateRemoval.txt","Removed "+removed+" gate triangles from terminal copies; source models retained.");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
