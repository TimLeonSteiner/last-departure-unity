using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class SolidHallWallsBuild
{
    const string Folder="Assets/LastDeparturePrototype/Resources/Spaceport/SolidWalls/";
    const string Prefab="Assets/LastDeparturePrototype/Resources/Spaceport/ModularHallTerminalSet.prefab";
    const string Request="Assets/Verification~/SolidHallWalls.request";
    static SolidHallWallsBuild(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}
        File.Delete(Request);
        try{Build();}catch(Exception e){File.WriteAllText("Assets/Verification~/SolidHallWalls.txt","FAIL "+e);Debug.LogException(e);}
    }
    static Material Surface(string name,Color color,float metal,Texture texture)
    {
        string path=Folder+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
        mat.name="SolidHall_"+name;mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metal);mat.SetFloat("_Smoothness",.19f);
        mat.SetTexture("_BaseMap",texture);EditorUtility.SetDirty(mat);return mat;
    }
    static Mesh SaveMesh(Mesh mesh,string name)
    {
        string path=Folder+name+".asset";mesh.name=name;
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);return saved;
    }
    public static void Build()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var tex=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LastDeparturePrototype/Resources/Spaceport/IndustrialRoof/GalvanizedColor.png");
        var panel=Surface("Charcoal corrugated steel",new Color(.24f,.205f,.17f),.32f,tex);
        var steel=Surface("Oxidized riveted frame",new Color(.30f,.20f,.125f),.42f,tex);
        var concrete=Surface("Dark concrete plinth",new Color(.18f,.17f,.16f),0,tex);
        var lens=Surface("Red lamp glass",new Color(.65f,.018f,.008f),.1f,null);
        lens.EnableKeyword("_EMISSION");lens.SetColor("_EmissionColor",new Color(3,.025f,.008f));
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        var temps=new List<Mesh>();
        try
        {
            RemoveBakedSigns(root.transform);
            var old=root.transform.Find("Hall wall panels");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var walls=new GameObject("Hall wall panels").transform;walls.SetParent(root.transform,false);
            float edge=36*YardSurvival.ArenaScale/2-.4f,gate=3.5f,width=(edge-gate)/5;
            Mesh cube=Primitive(PrimitiveType.Cube),sphere=Rivet(),cylinder=Primitive(PrimitiveType.Cylinder);temps.Add(sphere);
            for(int side=0;side<4;side++)
            {
                var wall=new GameObject(side==0?"Solid wall design":"Hall wall "+side).transform;wall.SetParent(walls,false);wall.localRotation=Quaternion.Euler(0,side*90,0);
                var batches=new Dictionary<Material,List<CombineInstance>>();
                void Add(Mesh mesh,Material material,Vector3 p,Vector3 size,Quaternion rotation)
                {if(!batches.TryGetValue(material,out var list))batches[material]=list=new List<CombineInstance>();list.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(p,rotation,size)});}
                void Box(Vector3 p,Vector3 size,Material material)=>Add(cube,material,p,size,Quaternion.identity);
                void Bolt(float x,float y,float z)=>Add(sphere,steel,new Vector3(x,y,z),new Vector3(.075f,.075f,.045f),Quaternion.identity);
                void Collision(string name,Vector3 p,Vector3 size)
                {var obj=new GameObject(name);obj.transform.SetParent(wall,false);obj.transform.localPosition=p;obj.AddComponent<BoxCollider>().size=size;}
                void Cladding(float x,float y,float w,float h)
                {
                    // Solid backing and a real folded steel surface: no alpha holes or window gaps.
                    Box(new Vector3(x,y,edge),new Vector3(w+.025f,h,.16f),panel);
                    var mesh=Corrugated(w,h);temps.Add(mesh);
                    Add(mesh,panel,new Vector3(x,y,edge-.12f),Vector3.one,Quaternion.identity);
                }
                foreach(int sign in new[]{-1,1})
                {
                    float length=edge-gate,center=sign*(gate+length/2);
                    Collision("Solid full-height wall collision",new Vector3(center,4.1f,edge),new Vector3(length,8.2f,.3f));
                    Box(new Vector3(center,.45f,edge-.08f),new Vector3(length,.9f,.34f),concrete);
                    for(int bay=0;bay<5;bay++)
                    {
                        float x=sign*(gate+(bay+.5f)*width);
                        Cladding(x,4.55f,width,7.3f);
                        foreach(float y in new[]{.95f,4.2f,7.95f})
                        {
                            Box(new Vector3(x,y,edge-.22f),new Vector3(width,.18f,.18f),steel);
                            for(float bx=x-width/2+.2f;bx<x+width/2;bx+=.5f)Bolt(bx,y,edge-.326f);
                        }
                    }
                    for(int bay=0;bay<=5;bay++)
                    {
                        float x=sign*(gate+bay*width);
                        Box(new Vector3(x,4.1f,edge-.24f),new Vector3(.26f,8.2f,.26f),steel);
                        for(float y=.25f;y<8.2f;y+=.42f)Bolt(x,y,edge-.384f);
                    }
                }
                Cladding(0,6.3f,gate*2,3.8f);
                Collision("Solid lintel above open entrance",new Vector3(0,6.3f,edge),new Vector3(7,3.8f,.3f));
                foreach(float y in new[]{4.4f,7.95f})Box(new Vector3(0,y,edge-.24f),new Vector3(7.3f,.24f,.26f),steel);
                // Door leaves are retracted to the sides; the original seven-metre opening stays clear.
                foreach(int sign in new[]{-1,1})
                {
                    Box(new Vector3(sign*3.85f,2.18f,edge-.40f),new Vector3(.42f,4.3f,.12f),steel);
                    Box(new Vector3(sign*4.08f,2.18f,edge-.36f),new Vector3(.11f,4.3f,.24f),steel);
                }
                foreach(float x in new[]{-20f,-12f,-4.7f,4.7f,12f,20f})
                {
                    float y=3.65f,z=edge-.58f;
                    Box(new Vector3(x,y,edge-.38f),new Vector3(.42f,.78f,.18f),steel);
                    Add(cylinder,lens,new Vector3(x,y,z),new Vector3(.25f,.26f,.25f),Quaternion.identity);
                    foreach(float cap in new[]{-.29f,.29f})Add(cylinder,steel,new Vector3(x,y+cap,z),new Vector3(.34f,.045f,.34f),Quaternion.identity);
                    for(int bar=0;bar<8;bar++)
                    {
                        float angle=bar*Mathf.PI/4;
                        Box(new Vector3(x+Mathf.Cos(angle)*.16f,y,z+Mathf.Sin(angle)*.16f),new Vector3(.021f,.60f,.021f),steel);
                    }
                    var lamp=new GameObject("Caged red wall light");lamp.transform.SetParent(wall,false);lamp.transform.localPosition=new Vector3(x,y,z-.25f);
                    var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.09f,.035f);light.intensity=6;light.range=6.5f;light.shadows=LightShadows.None;light.cullingMask=~(1<<30);
                }
                foreach(var pair in batches)
                {
                    var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();
                    mesh=SaveMesh(mesh,"Wall"+side+"_"+pair.Key.name);
                    var obj=new GameObject(pair.Key.name);obj.transform.SetParent(wall,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key;renderer.allowOcclusionWhenDynamic=false;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);AssetDatabase.SaveAssets();
            File.WriteAllText("Assets/Verification~/SolidHallWalls.txt","PASS: four continuous corrugated steel walls, 40 panel bays, four open 7m entrances, 24 caged red lights. Existing roof retained.");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);foreach(var mesh in temps)UnityEngine.Object.DestroyImmediate(mesh);}
    }
    static Mesh Primitive(PrimitiveType type)
    {var obj=GameObject.CreatePrimitive(type);var mesh=obj.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(obj);return mesh;}
    static void RemoveBakedSigns(Transform root)
    {
        int removed=0;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if(filter.name!="Spaceport_Ink"&&filter.name!="Spaceport_Hull"&&filter.name!="Spaceport_TealGlow")continue;
            var source=filter.sharedMesh;var vertices=source.vertices;
            var mask=new bool[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var p=root.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
                for(int side=0;side<4;side++)
                {
                    var local=Quaternion.Euler(0,-side*90,0)*p;
                    if(p.y>=4.68f&&p.y<=5.93f&&Mathf.Abs(local.x)<3.05f&&local.z>23.15f&&local.z<23.43f)mask[i]=true;
                    // Retired cyan status displays next to the red wall cages, including their housings.
                    if(p.y>=2.98f&&p.y<=3.82f&&Mathf.Abs(Mathf.Abs(local.x)-5.3f)<.66f&&local.z>23.55f&&local.z<23.83f)mask[i]=true;
                }
            }
            Mesh clone=null;
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                var indices=source.GetTriangles(sub);var keep=new List<int>();
                for(int i=0;i<indices.Length;i+=3)
                {
                    if(mask[indices[i]]&&mask[indices[i+1]]&&mask[indices[i+2]]){removed++;continue;}
                    keep.Add(indices[i]);keep.Add(indices[i+1]);keep.Add(indices[i+2]);
                }
                if(keep.Count==indices.Length)continue;
                if(clone==null)clone=UnityEngine.Object.Instantiate(source);
                clone.SetTriangles(keep,sub);
            }
            if(clone!=null)filter.sharedMesh=SaveMesh(clone,filter.name+"_WithoutDoorSigns");
        }
        File.WriteAllText("Assets/Verification~/SolidHallSignRemoval.txt","Removed "+removed+" baked sign triangles from the hall copy; source art retained.");
    }
    static Mesh Rivet()
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
        for(int ring=0;ring<3;ring++)for(int i=0;i<8;i++)
        {
            float a=i*Mathf.PI/4,r=ring==2?.24f:.5f;
            v.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,-ring*.23f));uv.Add(new Vector2(Mathf.Cos(a)*r+.5f,Mathf.Sin(a)*r+.5f));
            if(ring>0){int b=(ring-1)*8+i,c=(ring-1)*8+(i+1)%8;t.AddRange(new[]{b,b+8,c,c,b+8,c+8});}
        }
        for(int i=1;i<7;i++)t.AddRange(new[]{16,16+i+1,16+i});
        var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateNormals();return mesh;
    }
    static Mesh Corrugated(float width,float height)
    {
        int folds=Mathf.CeilToInt(width/.18f),steps=folds*4;
        var v=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        for(int i=0;i<=steps;i++)
        {
            float x=width*(i/(float)steps-.5f),z=(i%4==1||i%4==2)?-.045f:0;
            v.Add(new Vector3(x,-height/2,z));v.Add(new Vector3(x,height/2,z));
            uv.Add(new Vector2(x*.4f,0));uv.Add(new Vector2(x*.4f,height*.4f));
            if(i>0){int a=(i-1)*2;tris.AddRange(new[]{a,a+1,a+2,a+2,a+1,a+3});}
        }
        var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateTangents();return mesh;
    }
}
