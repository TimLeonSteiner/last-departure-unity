using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class IndustrialRoofBuild
{
    const string Folder = "Assets/LastDeparturePrototype/Resources/Spaceport/IndustrialRoof/";
    const string Terminal = "Assets/LastDeparturePrototype/Resources/Spaceport/MeshyRoofTerminalSet.prefab";
    const string Request = "Assets/Verification~/IndustrialRoof.request";
    const string Phase = "IndustrialRoof.Review";
    static readonly List<Vector3> vertices = new List<Vector3>();
    static readonly List<Vector2> uv = new List<Vector2>();
    static readonly List<int>[] faces = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
    static IndustrialRoofBuild() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Request))
        {
            File.Delete(Request);
            SessionState.SetInt(Phase, 1);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
        int phase = SessionState.GetInt(Phase, 0);
        if (phase == 0) return;
        try
        {
            if (phase == 1 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Build(); SessionState.SetInt(Phase, 2);
                EditorApplication.isPlaying = true;
            }
            else if (phase == 2 && EditorApplication.isPlaying && Time.timeSinceLevelLoad > 4)
            {
                SessionState.SetInt(Phase, 0); Review(); EditorApplication.isPlaying = false;
            }
        }
        catch (Exception e)
        {
            SessionState.SetInt(Phase, 0);
            File.WriteAllText("Assets/Verification~/IndustrialRoofReview.txt", "FAIL " + e);
            Debug.LogException(e);
        }
    }

    [MenuItem("Tools/Yard Survival/Build intact industrial roof")]
    public static void Build()
    {
        vertices.Clear(); uv.Clear(); foreach (var list in faces) list.Clear();
        var texture = (TextureImporter)AssetImporter.GetAtPath(Folder + "GalvanizedColor.png");
        texture.wrapMode = TextureWrapMode.Repeat; texture.anisoLevel = 8;
        texture.maxTextureSize = 2048; texture.SaveAndReimport();
        var materials = new[] {
            Material("Galvanized panels", new Color(.8f,.84f,.87f), .62f, .32f, true),
            Material("Replacement panels", new Color(.71f,.76f,.80f), .60f, .29f, true),
            Material("Painted steel frame", new Color(.15f,.19f,.21f), .45f, .26f, true),
            Material("Seam oxidation", new Color(.22f,.15f,.105f), .15f, .20f, true)
        };
        float span = 36 * YardSurvival.ArenaScale, half = span / 2;
        float width = span / 24, length = span / 12;
        var patches = new List<Rect>();
        for (int x = 0; x < 24; x++) for (int z = 0; z < 12; z++)
        {
            float cx = -half + (x + .5f) * width, cz = -half + (z + .5f) * length;
            // Two isolated missing sheets, away from the major girders: over 99% coverage.
            if ((x == 3 && z == 3) || (x == 20 && z == 8))
            {
                float s = YardSurvival.ArenaScale;
                patches.Add(new Rect((cx-width/2+.3f)/s, (cz-length/2+.3f)/s,
                    (width-.6f)/s, (length-.6f)/s));
                continue;
            }
            int material = (x * 7 + z * 13) % 19 == 0 ? 1 : 0;
            Box(new Vector3(cx, 7.88f, cz), new Vector3(width, .12f, length), material);
            // Raised standing seams give the sheets scale without cutting gaps in the roof.
            for (int rib = 0; rib < 4; rib++)
                Box(new Vector3(cx-width/2+rib*width/4+.025f, 7.97f, cz), new Vector3(.045f,.10f,length), material);
            Box(new Vector3(cx, 7.96f, cz-length/2+.025f), new Vector3(width,.045f,.055f), 2);
            if ((x * 11 + z) % 23 == 0)
                Box(new Vector3(cx, 7.815f, cz-length/2+.06f),new Vector3(width*.7f,.008f,.07f),3);
        }
        // Deep primary I-girders and lighter cross-purlins with restrained connection plates.
        for (int i = 0; i <= 6; i++)
        {
            float x = -half+i*span/6;
            Box(new Vector3(x,7.45f,0),new Vector3(.12f,.64f,span),2);
            Box(new Vector3(x,7.15f,0),new Vector3(.38f,.08f,span),2);
            Box(new Vector3(x,7.75f,0),new Vector3(.38f,.08f,span),2);
        }
        for (int i = 0; i <= 12; i++)
        {
            float z = -half+i*length;
            Box(new Vector3(0,7.69f,z),new Vector3(span,.22f,.14f),2);
            Box(new Vector3(0,7.59f,z),new Vector3(span,.045f,.25f),2);
            for (int j=0;j<=6;j++)
                Box(new Vector3(-half+j*span/6,7.12f,z),new Vector3(.46f,.045f,.43f),2);
        }
        foreach (int sign in new[]{-1,1})
        {
            Box(new Vector3(sign*half,7.87f,0),new Vector3(.16f,.38f,span+.16f),2);
            Box(new Vector3(0,7.87f,sign*half),new Vector3(span,.38f,.16f),2);
        }
        var mesh = new Mesh { name="Intact industrial roof", indexFormat=IndexFormat.UInt32 };
        mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.subMeshCount=4;
        for(int i=0;i<4;i++)mesh.SetTriangles(faces[i],i);
        mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
        string meshPath=Folder+"IndustrialRoof.asset";
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(saved==null){AssetDatabase.CreateAsset(mesh,meshPath);saved=mesh;}
        else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);}
        var root=PrefabUtility.LoadPrefabContents(Terminal);
        try
        {
            var roof=root.transform.Find("Meshy roof");
            if(roof==null)throw new Exception("Existing roof group not found");
            for(int i=roof.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(roof.GetChild(i).gameObject);
            var surface=new GameObject("Standing seam roof and steel girders");surface.transform.SetParent(roof,false);
            surface.AddComponent<MeshFilter>().sharedMesh=saved;
            var renderer=surface.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials;
            renderer.allowOcclusionWhenDynamic=false;renderer.shadowCastingMode=ShadowCastingMode.On;
            surface.AddComponent<MeshCollider>().sharedMesh=saved;
            PrefabUtility.SaveAsPrefabAsset(root,Terminal);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var layout=FullRoofLayout.Load();layout.rainPatches=patches.ToArray();EditorUtility.SetDirty(layout);
        AssetDatabase.SaveAssets();
        File.WriteAllText("Assets/Verification~/IndustrialRoofBuild.txt","PASS 286 intact panels; 2 localized missing sheets; 99.3% panel coverage.\nFour materials with metre-scaled UVs. One combined renderer and matching mesh collider. Rain limited to two openings.\nTriangles: "+saved.triangles.Length/3);
    }

    static Material Material(string name, Color color, float metallic, float smoothness, bool textured)
    {
        string path=Folder+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",smoothness);
        if(textured)mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"GalvanizedColor.png"));
        mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
    }

    static void Box(Vector3 center, Vector3 size, int material)
    {
        var h=size*.5f;
        Quad(center+new Vector3(0,h.y,0),Vector3.right*h.x,Vector3.back*h.z,material);
        Quad(center+new Vector3(0,-h.y,0),Vector3.right*h.x,Vector3.forward*h.z,material);
        Quad(center+new Vector3(h.x,0,0),Vector3.back*h.z,Vector3.up*h.y,material);
        Quad(center+new Vector3(-h.x,0,0),Vector3.forward*h.z,Vector3.up*h.y,material);
        Quad(center+new Vector3(0,0,h.z),Vector3.right*h.x,Vector3.up*h.y,material);
        Quad(center+new Vector3(0,0,-h.z),Vector3.left*h.x,Vector3.up*h.y,material);
    }
    static void Quad(Vector3 center,Vector3 u,Vector3 v,int material)
    {
        int i=vertices.Count;vertices.Add(center-u-v);vertices.Add(center+u-v);vertices.Add(center+u+v);vertices.Add(center-u+v);
        uv.Add(Vector2.zero);uv.Add(new Vector2(u.magnitude,0));uv.Add(new Vector2(u.magnitude,v.magnitude));uv.Add(new Vector2(0,v.magnitude));
        faces[material].AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
    }

    static void Review()
    {
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if(game==null)throw new Exception("Game missing");
        var roof=game.transform.Find("Spaceport terminal/Meshy roof");
        var collider=roof.GetComponentInChildren<MeshCollider>();
        int covered=0,total=0;
        for(int x=-23;x<=23;x++)for(int z=-23;z<=23;z++)
        {total++;if(collider.Raycast(new Ray(new Vector3(x,12,z),Vector3.down),out var hit,8))covered++;}
        if(covered<total*.98f)throw new Exception("Roof coverage below 98 percent");
        string warehouse=game.RunWarehouseChecks();
        SpaceportAssetBuild.Capture(new Vector3(-13,3,-13),new Vector3(0,8,-1),"IndustrialRoof-Interior");
        var light=new GameObject("Temporary roof review lighting").AddComponent<Light>();
        try
        {
            light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(60,-25,0);
            CaptureAbove();
            light.transform.rotation=Quaternion.Euler(-65,-25,0);light.intensity=1.2f;
            SpaceportAssetBuild.Capture(new Vector3(-13,3,-13),new Vector3(0,8,-1),"IndustrialRoof-Inspection");
        }
        finally{UnityEngine.Object.DestroyImmediate(light.gameObject);}
        File.WriteAllText("Assets/Verification~/IndustrialRoofReview.txt","PASS roof visible in active game; "+covered+"/"+total+" roof coverage rays hit.\n"+warehouse);
    }

    static void CaptureAbove()
    {
        var camera=new GameObject("Roof exterior inspection camera").AddComponent<Camera>();
        camera.transform.position=new Vector3(30,37,-30);camera.transform.LookAt(new Vector3(0,7.8f,0));
        camera.fieldOfView=65;camera.farClipPlane=800;camera.cullingMask=~(1<<30);
        camera.useOcclusionCulling=false;
        var target=new RenderTexture(1440,900,24);var image=new Texture2D(1440,900,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try
        {
            target.Create();
            RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
            RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();
            File.WriteAllBytes("Assets/Verification~/IndustrialRoof-Above.png",image.EncodeToPNG());
        }
        finally{RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(camera.gameObject);}
    }
}
