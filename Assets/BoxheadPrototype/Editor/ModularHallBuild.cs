using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class ModularHallBuild
{
    const string Folder="Assets/BoxheadPrototype/Resources/Spaceport/ModularHall/";
    const string Request="Assets/Verification~/ModularHallBuild.request";
    static ModularHallBuild(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(Request);
        try{Build();}catch(Exception e){File.WriteAllText("Assets/Verification~/ModularHallBuild.txt","FAIL "+e);Debug.LogException(e);}
    }
    class Module {public Mesh mesh;public float coverage;public int id;}
    public static void Build()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BoxheadPrototype/Resources/Spaceport/FullRoof/FullRoof.asset");
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/BoxheadPrototype/Resources/Spaceport/FullRoof/Roof.mat");
        float span=36*YardSurvival.ArenaScale, cell=span/4, crop=span/3;
        var modules=new List<Module>();
        for(int x=0;x<3;x++)for(int z=0;z<3;z++)
        {
            var rect=new Rect(-span/2+x*crop,-span/2+z*crop,crop,crop);
            var mesh=Extract(source,rect,cell/crop);
            int id=x*3+z;mesh=Save(mesh,"MeshySection"+id);
            var probe=new GameObject("Module coverage probe");
            try
            {
                var collider=probe.AddComponent<MeshCollider>();collider.sharedMesh=mesh;Physics.SyncTransforms();
                int hits=0,total=0;
                for(float a=-cell/2+.2f;a<cell/2;a+=.4f)for(float b=-cell/2+.2f;b<cell/2;b+=.4f)
                {total++;if(collider.Raycast(new Ray(new Vector3(a,3,b),Vector3.down),out var hit,6))hits++;}
                modules.Add(new Module{mesh=mesh,coverage=(float)hits/total,id=id});
            }finally{UnityEngine.Object.DestroyImmediate(probe);}
        }
        modules.Sort((a,b)=>b.coverage.CompareTo(a.coverage));
        var intact=modules.Where(m=>m.coverage>=.93f).Take(4).ToArray();
        if(intact.Length<2)throw new Exception("Not enough intact Meshy sections: "+string.Join(", ",modules.Select(m=>m.id+"="+m.coverage)));
        var damaged=modules.FirstOrDefault(m=>m.id==3);
        if(damaged==null)throw new Exception("No localized damaged Meshy module found");
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BoxheadPrototype/Resources/Spaceport/RestoredRoofTerminalSet.prefab"));
        try
        {
            var old=root.transform.Find("Restored original ceiling");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var roof=new GameObject("Meshy roof").transform;roof.SetParent(root.transform,false);
            var combine=new List<CombineInstance>();
            for(int x=0;x<4;x++)for(int z=0;z<4;z++)
            {
                bool breach=(x==1&&z==0)||(x==3&&z==2)||(x==0&&z==3);
                var module=breach?damaged:intact[(x+z*3)%intact.Length];
                var tile=new GameObject((breach?"Damaged":"Intact")+" Meshy section "+x+"-"+z+" variant "+module.id);
                tile.transform.SetParent(roof,false);tile.transform.localPosition=new Vector3(-span/2+(x+.5f)*cell,7.8f,-span/2+(z+.5f)*cell);
                tile.transform.localRotation=Quaternion.Euler(0,((x+z)%2)*180,0);
                tile.AddComponent<MeshFilter>().sharedMesh=module.mesh;
                var renderer=tile.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.allowOcclusionWhenDynamic=false;renderer.shadowCastingMode=ShadowCastingMode.TwoSided;
                combine.Add(new CombineInstance{mesh=module.mesh,transform=tile.transform.localToWorldMatrix});
                if(!breach)
                {
                    var backing=GameObject.CreatePrimitive(PrimitiveType.Cube);backing.name="Intact section backing "+x+"-"+z;backing.transform.SetParent(roof,false);
                    backing.transform.localPosition=tile.transform.localPosition+Vector3.up*.6f;backing.transform.localScale=new Vector3(cell+.035f,.06f,cell+.035f);
                    var backRenderer=backing.GetComponent<MeshRenderer>();backRenderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/BoxheadPrototype/Resources/Spaceport/IndustrialRoof/Galvanized panels.mat");backRenderer.allowOcclusionWhenDynamic=false;
                    UnityEngine.Object.DestroyImmediate(backing.GetComponent<BoxCollider>());
                    combine.Add(new CombineInstance{mesh=backing.GetComponent<MeshFilter>().sharedMesh,transform=backing.transform.localToWorldMatrix});
                }
            }
            var collision=new Mesh{indexFormat=IndexFormat.UInt32};collision.CombineMeshes(combine.ToArray());
            collision=Save(collision,"CompleteRoofCollision");
            var roofCollider=roof.gameObject.AddComponent<MeshCollider>();roofCollider.sharedMesh=collision;
            Physics.SyncTransforms();BakeRain(roofCollider);
            BuildWalls(root.transform,span);
            root.name="ModularHallTerminalSet";
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/BoxheadPrototype/Resources/Spaceport/ModularHallTerminalSet.prefab");
            AssetDatabase.SaveAssets();
            File.WriteAllText("Assets/Verification~/ModularHallBuild.txt","PASS 16 Meshy ceiling sections, three localized damaged sections, four enclosed walls with open central doorways.\nModule coverage: "+string.Join(", ",modules.Select(m=>m.id+"="+m.coverage.ToString("P1"))));
        }finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    static void BuildWalls(Transform root,float span)
    {
        var group=new GameObject("Hall wall panels").transform;group.SetParent(root,false);
        var steel=AssetDatabase.LoadAssetAtPath<Material>("Assets/BoxheadPrototype/Resources/Spaceport/IndustrialRoof/Painted steel frame.mat");
        var panel=AssetDatabase.LoadAssetAtPath<Material>("Assets/BoxheadPrototype/Resources/Spaceport/IndustrialRoof/Galvanized panels.mat");
        var dark=new Material(panel);dark.name="Hall aged cladding";dark.SetColor("_BaseColor",new Color(.24f,.27f,.28f));
        string path=Folder+"WallCladding.mat";var saved=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(saved==null){AssetDatabase.CreateAsset(dark,path);saved=dark;}else{EditorUtility.CopySerialized(dark,saved);UnityEngine.Object.DestroyImmediate(dark);}
        for(int side=0;side<4;side++)
        {
            var wall=new GameObject("Hall wall "+side).transform;wall.SetParent(group,false);wall.localRotation=Quaternion.Euler(0,side*90,0);
            float edge=span/2-.4f, gate=3.5f, length=edge-gate;
            Box(wall,"Continuous roof-to-wall fascia",new Vector3(0,8.12f,edge),new Vector3(span,.72f,.24f),steel,true);
            foreach(int sign in new[]{-1,1})
            {
                float center=sign*(gate+length/2);
                Box(wall,"Concrete-height lower wall",new Vector3(center,1.9f,edge),new Vector3(length,3.8f,.22f),saved,true);
                Box(wall,"Upper wall cladding",new Vector3(center,6.65f,edge),new Vector3(length,2.7f,.22f),saved,true);
                // Narrow high windows keep daylight and views without deleting the surrounding walls.
                for(int bay=0;bay<5;bay++)
                {
                    float x=sign*(gate+(bay+.5f)*length/5);
                    Box(wall,"Window sill",new Vector3(x,3.85f,edge-.08f),new Vector3(length/5,.10f,.38f),steel,false);
                    Box(wall,"Window header",new Vector3(x,5.25f,edge-.08f),new Vector3(length/5,.10f,.38f),steel,false);
                    Box(wall,"Window upright",new Vector3(x-length/10,4.55f,edge-.08f),new Vector3(.12f,1.4f,.38f),steel,true);
                    // Vertical ribs carry the industrial panel pattern down the walls.
                    Box(wall,"Wall panel rib",new Vector3(x,1.9f,edge-.18f),new Vector3(.09f,3.8f,.1f),steel,false);
                    Box(wall,"Upper panel rib",new Vector3(x,6.65f,edge-.18f),new Vector3(.09f,2.7f,.1f),steel,false);
                }
            }
            Box(wall,"Door header",new Vector3(0,6.15f,edge),new Vector3(gate*2,3.7f,.24f),saved,true);
            foreach(int sign in new[]{-1,1})Box(wall,"Door jamb",new Vector3(sign*gate,2.15f,edge-.1f),new Vector3(.2f,4.3f,.45f),steel,true);
        }
    }
    static void Box(Transform parent,string name,Vector3 center,Vector3 size,Material material,bool collision)
    {
        var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.name=name;item.transform.SetParent(parent,false);item.transform.localPosition=center;item.transform.localScale=size;
        var renderer=item.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.allowOcclusionWhenDynamic=false;
        if(!collision)UnityEngine.Object.DestroyImmediate(item.GetComponent<BoxCollider>());
    }
    static Mesh Save(Mesh mesh,string name)
    {
        mesh.name=name;var path=Folder+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);return existing;
    }
    static Mesh Extract(Mesh source,Rect rect,float scale)
    {
        var vertices=source.vertices;var uv=source.uv;var indices=source.triangles;
        var points=new List<Vector3>();var coords=new List<Vector2>();var faces=new List<int>();
        for(int i=0;i<indices.Length;i+=3)
        {
            var poly=new List<Vertex>();for(int j=0;j<3;j++)poly.Add(new Vertex{p=vertices[indices[i+j]],uv=uv[indices[i+j]]});
            poly=Clip(poly,0,rect.xMin,true);poly=Clip(poly,0,rect.xMax,false);poly=Clip(poly,2,rect.yMin,true);poly=Clip(poly,2,rect.yMax,false);
            int start=points.Count;
            foreach(var v in poly){points.Add(new Vector3((v.p.x-rect.center.x)*scale,v.p.y,(v.p.z-rect.center.y)*scale));coords.Add(v.uv);}
            for(int j=1;j+1<poly.Count;j++){faces.Add(start);faces.Add(start+j);faces.Add(start+j+1);}
        }
        var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(points);mesh.SetUVs(0,coords);mesh.SetTriangles(faces,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
    }
    struct Vertex
    {
        public Vector3 p; public Vector2 uv;
        public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex { p = Vector3.Lerp(a.p, b.p, t), uv = Vector2.Lerp(a.uv, b.uv, t) };
    }
    static List<Vertex> Clip(List<Vertex> input, int axis, float edge, bool greater)
    {
        var output = new List<Vertex>();
        for (int i = 0; i < input.Count; i++)
        {
            var a = input[i]; var b = input[(i + 1) % input.Count];
            float da = (a.p[axis] - edge) * (greater ? 1 : -1), db = (b.p[axis] - edge) * (greater ? 1 : -1);
            if (da >= 0) output.Add(a);
            if ((da >= 0) != (db >= 0)) output.Add(Vertex.Lerp(a, b, da / (da - db)));
        }
        return output;
    }
    static void BakeRain(MeshCollider roof)
    {
        const int n = 72;
        var open = new bool[n,n];
        float scale = YardSurvival.ArenaScale;
        for(int x=1;x<n-1;x++) for(int z=1;z<n-1;z++)
        {
            bool clear = true;
            // Sample the whole patch, including a margin for slanted rain.
            for(int dx=-2;dx<=2;dx++) for(int dz=-2;dz<=2;dz++)
            {
                var p = new Vector3((x*.5f-17.75f+dx*.14f)*scale, 12, (z*.5f-17.75f+dz*.14f)*scale);
                if(roof.Raycast(new Ray(p,Vector3.down),out var hit,8)) clear=false;
                p.y=5;
                if(roof.Raycast(new Ray(p,Vector3.up),out hit,8)) clear=false;
            }
            open[x,z]=clear;
        }
        var patches = new List<Rect>();
        for(int z=1;z<n-1;z++) for(int x=1;x<n-1;x++)
        {
            if(!open[x,z])continue;
            int w=1,h=1;
            while(x+w<n-1&&open[x+w,z])w++;
            bool more=true;
            while(z+h<n-1&&more)
            {
                for(int i=0;i<w;i++)if(!open[x+i,z+h])more=false;
                if(more)h++;
            }
            for(int i=0;i<w;i++)for(int j=0;j<h;j++)open[x+i,z+j]=false;
            patches.Add(new Rect(x*.5f-18+.08f,z*.5f-18+.08f,w*.5f-.16f,h*.5f-.16f));
        }
        if(patches.Count<1)throw new Exception("Could not find the roof openings.");
        var layout=AssetDatabase.LoadAssetAtPath<FullRoofLayout>(Folder+"Layout.asset");
        if(layout==null){layout=ScriptableObject.CreateInstance<FullRoofLayout>();AssetDatabase.CreateAsset(layout,Folder+"Layout.asset");}
        layout.rainPatches=patches.ToArray();EditorUtility.SetDirty(layout);
        File.WriteAllText("Assets/Verification~/ModularHallRainBake.txt",string.Join("\n",patches));
    }
}
