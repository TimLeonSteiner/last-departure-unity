using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class ModularHeartImport
{
    const string Source="Assets/BoxheadPrototype/ArtSource~/HeartPlatform/Modular/";
    const string Folder="Assets/BoxheadPrototype/Resources/HeartPlatform/Modular/";
    static readonly string[] Parts={"Heart","Pump","Frame","Deck","Ramp","Roots","Hose","Chain"};
    static bool busy;
    static ModularHeartImport(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string meshRequest="Assets/Verification~/UpdateModularHeartMeshes.request";
        if(!busy&&File.Exists(meshRequest)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)
        {
            File.Delete(meshRequest);
            foreach(var part in Parts)
            {
                var asset=AssetDatabase.LoadAssetAtPath<HeartModuleAsset>(Folder+part+"/Geometry.asset");
                for(int level=0;level<2;level++)
                {
                    string path=Source+part+"/LOD"+level+".meshbin";if(!File.Exists(path))continue;
                    var mesh=ReadMesh(path,part+" detail "+level,part=="Hose"||part=="Chain");
                    EditorUtility.CopySerialized(mesh,asset.levels[level]);EditorUtility.SetDirty(asset.levels[level]);UnityEngine.Object.DestroyImmediate(mesh);File.Delete(path);
                }
            }
            var chainMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Chain/Surface.mat");chainMaterial.SetFloat("_Kind",3);EditorUtility.SetDirty(chainMaterial);
            AssetDatabase.SaveAssets();BuildAssembly();return;
        }
        const string assemblyRequest="Assets/Verification~/AssembleModularHeart.request";
        if(!busy&&File.Exists(assemblyRequest)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)
        {File.Delete(assemblyRequest);BuildAssembly();AssetDatabase.SaveAssets();return;}
        const string request="Assets/Verification~/ImportModularHeart.request";
        if(busy||!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        foreach(var part in Parts)if(!File.Exists(Folder+part+"/Geometry.asset")&&!File.Exists(Source+part+"/LOD1.meshbin"))return;
        File.Delete(request);busy=true;
        try{Import();}catch(Exception e){File.WriteAllText("Assets/Verification~/ModularHeartImport.txt","FAIL "+e);Debug.LogException(e);}finally{busy=false;}
    }
    static float[] Floats(BinaryReader reader,int count)
    {var bytes=reader.ReadBytes(count*4);if(bytes.Length!=count*4)throw new EndOfStreamException();var values=new float[count];Buffer.BlockCopy(bytes,0,values,0,bytes.Length);return values;}
    static Mesh ReadMesh(string path,string name,bool hose)
    {
        using var reader=new BinaryReader(File.OpenRead(path));int count=reader.ReadInt32(),indices=reader.ReadInt32();
        var p=Floats(reader,count*3);var n=Floats(reader,count*3);var u=Floats(reader,count*2);
        var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];
        for(int i=0;i<count;i++){vertices[i]=new Vector3(p[i*3],p[i*3+1],p[i*3+2]);normals[i]=new Vector3(n[i*3],n[i*3+1],n[i*3+2]);uv[i]=new Vector2(u[i*2],u[i*2+1]);}
        var bytes=reader.ReadBytes(indices*4);var triangles=new int[indices];Buffer.BlockCopy(bytes,0,triangles,0,bytes.Length);
        var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateTangents();mesh.RecalculateBounds();
        if(hose)mesh.bounds=new Bounds(new Vector3(0,2,0),Vector3.one*12);
        return mesh;
    }
    [MenuItem("Tools/Yard Survival/Import modular heart")]
    public static void Import()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText("Assets/Verification~/ModularHeartImport.txt","");
        foreach(var part in Parts)
        {
            string dir=Folder+part+"/";Directory.CreateDirectory(dir);
            foreach(var map in new[]{"Color","Normal","Metal","Roughness"})
                if(!File.Exists(dir+map+".png"))File.Move(Source+part+"/"+map+".png",dir+map+".png");
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var part in Parts)
        {
            string dir=Folder+part+"/";
            foreach(var map in new[]{"Color","Normal","Metal","Roughness"})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(dir+map+".png");
                importer.textureType=map=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=map=="Color";importer.maxTextureSize=map=="Color"?8192:4096;
                importer.mipmapEnabled=true;importer.streamingMipmaps=true;importer.anisoLevel=8;
                importer.textureCompression=TextureImporterCompression.CompressedHQ;
                var platform=importer.GetPlatformTextureSettings("Standalone");platform.overridden=true;platform.maxTextureSize=importer.maxTextureSize;
                platform.format=map=="Normal"?TextureImporterFormat.BC5:TextureImporterFormat.BC7;importer.SetPlatformTextureSettings(platform);importer.SaveAndReimport();
            }
            var asset=AssetDatabase.LoadAssetAtPath<HeartModuleAsset>(dir+"Geometry.asset");
            if(asset==null)
            {
                asset=ScriptableObject.CreateInstance<HeartModuleAsset>();AssetDatabase.CreateAsset(asset,dir+"Geometry.asset");asset.levels=new Mesh[2];
                for(int level=0;level<2;level++){asset.levels[level]=ReadMesh(Source+part+"/LOD"+level+".meshbin",part+" detail "+level,part=="Hose");AssetDatabase.AddObjectToAsset(asset.levels[level],asset);}
                EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();
            }
            var mat=AssetDatabase.LoadAssetAtPath<Material>(dir+"Surface.mat");
            if(mat==null){mat=new Material(Shader.Find("Dustline/Modular Heart"));AssetDatabase.CreateAsset(mat,dir+"Surface.mat");}
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"Color.png"));mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"Normal.png"));
            mat.SetTexture("_MetalMask",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"Metal.png"));mat.SetTexture("_Roughness",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"Roughness.png"));mat.SetFloat("_Kind",part=="Heart"?1:part=="Hose"?2:part=="Chain"?3:0);EditorUtility.SetDirty(mat);
            var root=new GameObject(part);var lods=new LOD[2];
            for(int level=0;level<2;level++)
            {
                var child=new GameObject("LOD"+level);child.transform.SetParent(root.transform,false);child.AddComponent<MeshFilter>().sharedMesh=asset.levels[level];
                var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;lods[level]=new LOD(level==0?.32f:.001f,new Renderer[]{renderer});
            }
            var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(root,dir+part+".prefab");UnityEngine.Object.DestroyImmediate(root);
            File.AppendAllText("Assets/Verification~/ModularHeartImport.txt",part+" "+asset.levels[0].triangles.Length/3+" full triangles, "+asset.levels[1].triangles.Length/3+" distance triangles\n");
            // These intermediates are reproducible from the archived original FBX.
            for(int level=0;level<2;level++)File.Delete(Source+part+"/LOD"+level+".meshbin");
            GC.Collect();
        }
        BuildAssembly();AssetDatabase.SaveAssets();File.AppendAllText("Assets/Verification~/ModularHeartImport.txt","DONE\n");
    }
    static GameObject Place(string part,Transform parent,string name,Vector3 position,float yaw=0)
    {
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+part+"/"+part+".prefab"),parent,false);
        go.name=name;go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);return go;
    }
    static void Hose(Transform root,string name,Vector3 a,Vector3 direction,Vector3 d,Vector3 arrival,float scale)
    {
        var go=Place("Hose",root,name,Vector3.zero);var route=go.AddComponent<HeartHoseRoute>();float reach=Vector3.Distance(a,d);
        // Seat the couplers inside their sockets, rather than leaving capped ends at the rim.
        route.a=a-direction.normalized*.10f;route.b=a+direction.normalized*Mathf.Min(1.4f,reach*.5f);route.d=d-arrival.normalized*.16f;route.c=d+arrival.normalized*Mathf.Min(1.2f,reach*.45f);route.radiusScale=scale;
        var side=Vector3.Cross(route.b-a,d-a);if(side.sqrMagnitude<.01f)side=Vector3.forward;route.side=side.normalized;go.SendMessage("OnEnable");
        var group=go.GetComponent<LODGroup>();group.localReferencePoint=(a+d)*.5f;group.size=reach+1;
        var lods=group.GetLODs();lods[0].screenRelativeTransitionHeight=.5f;group.SetLODs(lods);
    }
    static Vector3 Port(float x,float y,float z)=>new Vector3(x*1.36827f,(z+.950435f)*1.36827f+1,y*1.36827f);
    static void Chain(Transform root,string name,Vector3 top,Vector3 hook)
    {
        var go=Place("Chain",root,name,Vector3.zero);var route=go.AddComponent<HeartHoseRoute>();
        route.a=top;route.d=hook+Vector3.up*.22f;
        route.b=Vector3.Lerp(top,route.d,.33f)+Vector3.down*.12f;
        route.c=Vector3.Lerp(top,route.d,.66f)+Vector3.down*.12f;
        route.side=Vector3.Cross((route.d-top).normalized,Vector3.up).normalized;
        if(route.side.sqrMagnitude<.1f)route.side=Vector3.forward;
        route.radiusScale=.55f;go.SendMessage("OnEnable");
        var group=go.GetComponent<LODGroup>();group.localReferencePoint=(top+hook)*.5f;group.size=(top-hook).magnitude+.5f;
    }
    public static void RepairConnections()
    {
        var asset=AssetDatabase.LoadAssetAtPath<HeartModuleAsset>(Folder+"Chain/Geometry.asset");
        for(int level=0;level<2;level++)
        {
            string path=Source+"Chain/LOD"+level+".meshbin";
            if(!File.Exists(path))throw new FileNotFoundException("Run fit_chain.py before rebuilding chain connections",path);
            var mesh=ReadMesh(path,"Chain detail "+level,true);
            EditorUtility.CopySerialized(mesh,asset.levels[level]);EditorUtility.SetDirty(asset.levels[level]);UnityEngine.Object.DestroyImmediate(mesh);
        }
        var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Chain/Surface.mat");
        material.SetFloat("_Kind",3);EditorUtility.SetDirty(material);
        BuildAssembly();AssetDatabase.SaveAssets();
    }
    static void BuildAssembly()
    {
        var root=new GameObject("Modular biomechanical heart platform");var t=root.transform;
        Place("Heart",t,"Heart",Vector3.zero);Place("Frame",t,"Suspension frame",Vector3.zero);Place("Roots",t,"Organic root hub",Vector3.zero);
        for(int i=0;i<4;i++)Place("Deck",t,"Deck sector "+(i+1),Vector3.zero,i*90);
        for(int i=0;i<8;i++)Place("Ramp",t,"Perimeter ramp "+(i+1),Vector3.zero,i*45);
        var leftPump=Place("Pump",t,"Left blood pump",new Vector3(-2.3f,.18f,-.6f),-20).transform;
        var rightPump=Place("Pump",t,"Right blood pump",new Vector3(2.3f,.18f,-.6f),20).transform;
        // Measured flange centre and outward normal in the imported pump's local coordinates.
        var pumpSocket=new Vector3(.30f,1.075f,-.30f);
        var pumpOut=new Vector3(.25f,.10f,-1).normalized;
        Hose(t,"Vessel 01 upper left",Port(-.405f,.05f,.675f),new Vector3(-.45f,.9f,0),new Vector3(-1.8f,4.35f,-.8f),Vector3.down,.65f);
        Hose(t,"Vessel 02 crown left",Port(-.14f,.018f,.895f),Vector3.up,new Vector3(-.7f,4.4f,2.25f),Vector3.down,.72f);
        Hose(t,"Vessel 03 crown right",Port(.12f,-.009f,.895f),Vector3.up,new Vector3(.8f,4.4f,2.2f),Vector3.down,.72f);
        Hose(t,"Vessel 04 upper right",Port(.33f,-.083f,.78f),new Vector3(.4f,.9f,0),new Vector3(1.9f,4.35f,-.8f),Vector3.down,.64f);
        Hose(t,"Vessel 05 left pump",Port(-.61f,.04f,.33f),Vector3.left,leftPump.TransformPoint(pumpSocket),leftPump.TransformDirection(pumpOut),.60f);
        Hose(t,"Vessel 06 lower left",Port(-.63f,0,.015f),Vector3.left,new Vector3(-2.55f,4.35f,1),Vector3.down,.58f);
        Hose(t,"Vessel 07 right pump",Port(.61f,-.14f,.35f),Vector3.right,rightPump.TransformPoint(pumpSocket),rightPump.TransformDirection(pumpOut),.60f);
        Hose(t,"Vessel 08 lower right",Port(.63f,-.02f,0),Vector3.right,new Vector3(2.55f,4.35f,1),Vector3.down,.58f);
        Hose(t,"Vessel 09 rear branch",Port(.5f,.10f,.32f),Vector3.right,new Vector3(1.9f,4.35f,1.65f),Vector3.down,.30f);
        Hose(t,"Vessel 10 small return",Port(.48f,.05f,.19f),Vector3.right,new Vector3(1.4f,4.35f,2.05f),Vector3.down,.18f);
        Hose(t,"Vessel 11 rear return",Port(.4f,.26f,.16f),new Vector3(.4f,0,1),new Vector3(.3f,4.35f,2.5f),Vector3.down,.18f);
        Hose(t,"Vessel 12 small branch",Port(.4f,-.01f,.12f),Vector3.right,new Vector3(2.4f,4.35f,1.2f),Vector3.down,.12f);
        for(int i=0;i<8;i++)
        {
            float angle=i*Mathf.PI/4;var radial=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
            var a=radial*1.08f+Vector3.up*.37f;var d=radial*2.85f+Vector3.up*.11f;
            var go=Place("Hose",t,"Root distribution hose "+(i+1),Vector3.zero);var route=go.AddComponent<HeartHoseRoute>();
            route.a=a;route.b=radial*1.65f+Vector3.up*.28f;route.c=radial*2.55f+Vector3.up*.24f;route.d=d;
            route.radiusScale=.30f;route.side=Vector3.Cross(radial,Vector3.up);go.SendMessage("OnEnable");
            var group=go.GetComponent<LODGroup>();group.localReferencePoint=(a+d)*.5f;group.size=2;
        }
        foreach(int side in new[]{-1,1})
        {
            Chain(t,(side<0?"Left":"Right")+" front suspension chain",new Vector3(side*2.05f,4.3f,-1.45f),new Vector3(side*.94f,2.02f,-.25f));
            Chain(t,(side<0?"Left":"Right")+" rear suspension chain",new Vector3(side*2.05f,4.3f,1.45f),new Vector3(side*.94f,2.02f,.4f));
        }
        HeartTubeConnections.Build(t);
        PrefabUtility.SaveAsPrefabAsset(root,Folder+"Platform.prefab");UnityEngine.Object.DestroyImmediate(root);
    }
}
