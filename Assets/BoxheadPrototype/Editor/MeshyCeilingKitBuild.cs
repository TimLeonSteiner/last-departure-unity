using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class MeshyCeilingKitBuild
{
    const string Folder="Assets/BoxheadPrototype/Resources/Spaceport/CeilingKit/";
    const string Prefab="Assets/BoxheadPrototype/Resources/Spaceport/ModularHallTerminalSet.prefab";
    const string Request="Assets/Verification~/BuildMeshyCeilingKit.request";
    static MeshyCeilingKitBuild(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(Request);
        try{Build();}catch(Exception e){File.WriteAllText("Assets/Verification~/MeshyCeilingKitBuild.txt","FAIL "+e);Debug.LogException(e);}
    }
    static Mesh SaveMesh(Mesh mesh,string name)
    {
        mesh.name=name;var path=Folder+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);return old;
    }
    static Mesh ReadMesh(string kind,int level)
    {
        string path=Folder+kind+"_LOD"+level+".fbx";
        var importer=AssetImporter.GetAtPath(path) as ModelImporter;
        if(importer==null)throw new Exception("Missing verified ceiling model: "+path);
        importer.isReadable=true;importer.importAnimation=false;importer.importLights=false;importer.importCameras=false;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
        var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        try
        {
            var parts=new List<CombineInstance>();
            foreach(var f in model.GetComponentsInChildren<MeshFilter>())for(int s=0;s<f.sharedMesh.subMeshCount;s++)
                parts.Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=s,transform=f.transform.localToWorldMatrix});
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.ToArray());mesh.RecalculateBounds();mesh.RecalculateTangents();
            if(Mathf.Abs(mesh.bounds.size.x-1)>.02f || (kind!="Truss" && Mathf.Abs(mesh.bounds.size.z-1)>.02f))throw new Exception("Unexpected imported orientation: "+kind+" "+mesh.bounds);
            return SaveMesh(mesh,kind+"MeshLOD"+level);
        }finally{UnityEngine.Object.DestroyImmediate(model);}
    }
    static Material MakeMaterial(string kind)
    {
        foreach(string suffix in new[]{"Color","Normal","MetallicSmoothness"})
        {
            var t=AssetImporter.GetAtPath(Folder+kind+suffix+".png") as TextureImporter;
            if(t==null)throw new Exception("Missing "+kind+suffix);
            t.textureType=suffix=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
            t.sRGBTexture=suffix=="Color";t.maxTextureSize=suffix=="MetallicSmoothness"?2048:4096;t.mipmapEnabled=true;t.anisoLevel=8;t.SaveAndReimport();
        }
        var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+kind+".mat");
        if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,Folder+kind+".mat");}
        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+kind+"Color.png"));
        mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+kind+"Normal.png"));
        mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+kind+"MetallicSmoothness.png"));
        mat.SetColor("_BaseColor",kind=="Damaged"?new Color(.50f,.42f,.35f,1):new Color(.62f,.51f,.42f,1));mat.SetFloat("_Smoothness",.22f);mat.SetFloat("_Cull",0);
        mat.EnableKeyword("_NORMALMAP");mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
    }
    static void Build()
    {
        var meshes=new Dictionary<string,Mesh[]>();var materials=new Dictionary<string,Material>();
        foreach(string kind in new[]{"Intact","Damaged","Truss"})
        {meshes[kind]=new[]{ReadMesh(kind,0),ReadMesh(kind,1),ReadMesh(kind,2)};materials[kind]=MakeMaterial(kind);}
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
        try
        {
            var previous=root.transform.Find("Meshy roof");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var roof=new GameObject("Meshy roof").transform;roof.SetParent(root.transform,false);
            var collisionParts=new List<CombineInstance>();float span=36*YardSurvival.ArenaScale,cell=span/8;
            void Place(string kind,string label,Vector3 position,Quaternion rotation,float size)
            {
                var piece=new GameObject(label).transform;piece.SetParent(roof,false);piece.localPosition=position;piece.localRotation=rotation;piece.localScale=Vector3.one*size;
                var levels=new LOD[3];
                for(int l=0;l<3;l++)
                {
                    var node=new GameObject("LOD"+l);node.transform.SetParent(piece,false);node.AddComponent<MeshFilter>().sharedMesh=meshes[kind][l];
                    var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[kind];renderer.allowOcclusionWhenDynamic=false;renderer.shadowCastingMode=ShadowCastingMode.TwoSided;
                    levels[l]=new LOD(l==0?.28f:l==1?.10f:.001f,new Renderer[]{renderer});
                }
                var lod=piece.gameObject.AddComponent<LODGroup>();lod.SetLODs(levels);lod.RecalculateBounds();
                collisionParts.Add(new CombineInstance{mesh=meshes[kind][2],transform=roof.worldToLocalMatrix*piece.localToWorldMatrix});
            }
            for(int x=0;x<8;x++)for(int z=0;z<8;z++)
            {
                bool damaged=(x==1&&z==2)||(x==5&&z==1)||(x==6&&z==5);
                string kind=damaged?"Damaged":"Intact";
                Place(kind,kind+" ceiling cassette "+x+"-"+z,new Vector3(-span/2+(x+.5f)*cell,8.22f,-span/2+(z+.5f)*cell),Quaternion.identity,cell+.12f);
            }
            for(int row=0;row<=8;row++)for(int x=0;x<8;x++)
                Place("Truss","Riveted steel truss "+row+"-"+x,new Vector3(-span/2+(x+.5f)*cell,7.70f,-span/2+row*cell),Quaternion.identity,cell+.04f);
            var collision=new Mesh{indexFormat=IndexFormat.UInt32};collision.CombineMeshes(collisionParts.ToArray());collision=SaveMesh(collision,"CompleteRoofCollision");
            var collider=roof.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=collision;Physics.SyncTransforms();
            int covered=0,total=0;
            for(float x=-24;x<=24;x+=.25f)for(float z=-24;z<=24;z+=.25f){total++;if(collider.Raycast(new Ray(new Vector3(x,12,z),Vector3.down),out var hit,8))covered++;}
            if(covered<total*.99f)throw new Exception("Roof coverage below 99%: "+covered+"/"+total+". Prefab was not replaced.");
            BakeRain(collider);
            root.name="ModularHallTerminalSet";PrefabUtility.SaveAsPrefabAsset(root,Prefab);AssetDatabase.SaveAssets();
            File.WriteAllText("Assets/Verification~/MeshyCeilingKitBuild.txt","PASS 64 new Meshy ceiling cassettes (61 intact, 3 locally damaged), 72 separate riveted steel trusses.\nCoverage "+covered+"/"+total+". Existing hall walls and contents retained.\n");
        }finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    static void BakeRain(MeshCollider roof)
    {
        const int n=144;const float step=.25f;float scale=YardSurvival.ArenaScale;var open=new bool[n,n];
        for(int x=1;x<n-1;x++)for(int z=1;z<n-1;z++)
        {
            bool clear=true;
            for(int dx=-2;dx<=2&&clear;dx++)for(int dz=-2;dz<=2&&clear;dz++)
            {
                var p=new Vector3((x*step-18+step/2+dx*.055f)*scale,12,(z*step-18+step/2+dz*.055f)*scale);
                if(roof.Raycast(new Ray(p,Vector3.down),out var hit,8))clear=false;p.y=5;
                if(roof.Raycast(new Ray(p,Vector3.up),out hit,8))clear=false;
            }
            open[x,z]=clear;
        }
        var patches=new List<Rect>();
        for(int z=1;z<n-1;z++)for(int x=1;x<n-1;x++)
        {
            if(!open[x,z])continue;int w=1,h=1;while(x+w<n-1&&open[x+w,z])w++;
            bool more=true;while(z+h<n-1&&more){for(int i=0;i<w;i++)if(!open[x+i,z+h])more=false;if(more)h++;}
            for(int i=0;i<w;i++)for(int j=0;j<h;j++)open[x+i,z+j]=false;
            patches.Add(new Rect(x*step-18+.04f,z*step-18+.04f,w*step-.08f,h*step-.08f));
        }
        if(patches.Count==0)throw new Exception("No verified rain opening in the damaged panels.");
        const string path="Assets/BoxheadPrototype/Resources/Spaceport/ModularHall/Layout.asset";
        var layout=AssetDatabase.LoadAssetAtPath<FullRoofLayout>(path);layout.rainPatches=patches.ToArray();EditorUtility.SetDirty(layout);
        File.WriteAllText("Assets/Verification~/MeshyCeilingKitRain.txt",string.Join("\n",patches));
    }
}
