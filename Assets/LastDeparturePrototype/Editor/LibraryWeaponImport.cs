using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using YardSurvivalPrototype;
using UnityEditor.SceneManagement;

public static class LibraryWeaponImport
{
    public const string Folder = "Assets/LastDeparturePrototype/ThirdParty/LibraryGuns/Guns&Explosives/";
    public static void Configure(YardSurvival game)
    {
        game.sniperModel = Build(false); game.heavyModel = Build(true);
        EditorUtility.SetDirty(game); AssetDatabase.SaveAssets();
    }
    public static void Setup()
    {
        if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before importing weapon prefabs.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path != "Assets/LastDeparture.unity") scene = EditorSceneManager.OpenScene("Assets/LastDeparture.unity");
        Configure(Object.FindAnyObjectByType<YardSurvival>());
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("LIBRARY WEAPONS READY: distinct sniper and M60 prefabs saved in LastDeparture.");
    }
    static GameObject Build(bool heavy)
    {
        string dir = heavy ? "Assets/LastDeparturePrototype/ThirdParty/M60/" : Folder + "Sniper/";
        string path = dir + (heavy ? "m60.fbx" : "Sniper.fbx");
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if(!importer.isReadable || importer.importCameras || importer.importLights)
        { importer.isReadable=true; importer.importCameras=false; importer.importLights=false; importer.SaveAndReimport(); }
        var source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        var root = new GameObject(heavy ? "M60 machine gun" : "Scout sniper rifle");
        var material = MakeMaterial(heavy ? "M60" : "Sniper");
        Quaternion rotation = heavy ? Quaternion.identity : Quaternion.Euler(0,180,0)*Quaternion.Euler(-10.46f,0,0);
        float scale = heavy ? .8f : 1.15f;
        Vector3 sourceGrip = heavy ? new Vector3(-.0164f,.20f,-.29f) : new Vector3(.0037f,-.15f,.30f);
        Vector3 offset = new Vector3(0,-.083f,-.066f) - rotation*sourceGrip*scale;
        Matrix4x4 normalize = Matrix4x4.TRS(offset,rotation,Vector3.one*scale);
        Transform magazine=null, bolt=null;
        foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
        {
            var groups = heavy ? Components(filter.sharedMesh) : new List<int[]> { filter.sharedMesh.triangles };
            for(int i=0;i<groups.Count;i++)
            {
                string name = heavy ? i==13 || i==18 || i==11 ? "Ammunition " + i : i==30 ? "Charging_Handle" : "M60 part " + i : filter.name;
                Matrix4x4 matrix=normalize*filter.transform.localToWorldMatrix;
                var original=filter.sharedMesh; var ids=groups[i].Distinct().ToArray();
                var indices=new Dictionary<int,int>(); for(int v=0;v<ids.Length;v++) indices[ids[v]]=v;
                var sourceVertices=original.vertices; var sourceNormals=original.normals; var sourceUV=original.uv;
                var vertices=ids.Select(v=>matrix.MultiplyPoint3x4(sourceVertices[v])).ToArray();
                Mesh mesh=new Mesh { name=name, vertices=vertices, uv=ids.Select(v=>sourceUV[v]).ToArray(), triangles=groups[i].Select(v=>indices[v]).ToArray() };
                if(sourceNormals.Length==sourceVertices.Length) mesh.normals=ids.Select(v=>matrix.inverse.transpose.MultiplyVector(sourceNormals[v]).normalized).ToArray();
                else mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                Vector3 pivot=mesh.bounds.center;
                for(int v=0;v<vertices.Length;v++) vertices[v]-=pivot;
                mesh.vertices=vertices; mesh.RecalculateTangents(); mesh.RecalculateBounds();
                string meshPath=dir+(heavy ? "M60_"+i : filter.name)+".asset";
                var previous=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(previous==null) AssetDatabase.CreateAsset(mesh,meshPath); else { EditorUtility.CopySerialized(mesh,previous); Object.DestroyImmediate(mesh); mesh=previous; }
                var part=new GameObject(name); part.transform.SetParent(root.transform,false); part.transform.localPosition=pivot;
                part.AddComponent<MeshFilter>().sharedMesh=mesh; part.AddComponent<MeshRenderer>().sharedMaterial=material;
                if(name=="Magazine") magazine=part.transform;
                if(name=="Bolt" || name=="Charging_Handle") bolt=part.transform;
            }
        }
        if(heavy)
        {
            magazine=new GameObject("Ammunition box and belt").transform; magazine.SetParent(root.transform,false);
            magazine.localPosition=normalize.MultiplyPoint3x4(new Vector3(-.15f,.325f,-.193f));
            foreach(var part in root.GetComponentsInChildren<MeshFilter>()) if(part.name.StartsWith("Ammunition")) part.transform.SetParent(magazine,true);
            var bipod=new GameObject("Folded bipod").transform; bipod.SetParent(root.transform,false);
            bipod.localPosition=normalize.MultiplyPoint3x4(new Vector3(-.0164f,.32f,.65f));
            var legs=new HashSet<string>(new[] {4,5,21,22,24,25,27,28}.Select(i=>"M60 part "+i));
            foreach(var part in root.GetComponentsInChildren<MeshFilter>()) if(legs.Contains(part.name)) part.transform.SetParent(bipod,true);
            bipod.localRotation=Quaternion.Euler(90,0,0);
        }
        else magazine.localPosition+=Vector3.up*.018f;
        var rig=root.AddComponent<WeaponModelRig>(); rig.magazine=magazine; rig.bolt=bolt;
        rig.rightWrist=heavy ? new Vector3(.043f,-.058f,-.155f) : new Vector3(.043f,-.053f,-.11f);
        rig.supportWrist=heavy ? new Vector3(-.075f,-.042f,.24f) : new Vector3(-.075f,-.038f,.22f);
        rig.muzzle=normalize.MultiplyPoint3x4(heavy ? new Vector3(-.0164f,.3715f,.755f) : new Vector3(.0037f,.12f,-.525f));
        rig.magazineWrist=magazine.localPosition+new Vector3(-.065f,-.025f,-.075f);
        rig.boltWrist=bolt.localPosition+new Vector3(-.075f,.025f,-.025f);
        rig.sightPosition=new Vector3(0,heavy ? -.11f : -.16f,.38f);
        rig.magazineTravel=heavy ? new Vector3(-.18f,-.16f,0) : new Vector3(-.065f,-.24f,.06f);
        string prefabPath=dir+(heavy ? "M60" : "ScoutSniper")+".prefab";
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        Render(root,heavy ? "m60-normalized" : "sniper-normalized");
        Object.DestroyImmediate(root); Object.DestroyImmediate(source); return prefab;
    }
    public static void Inspect()
    {
        var report = new StringBuilder();
        foreach (string path in new[] { Folder + "Sniper/Sniper.fbx", "Assets/LastDeparturePrototype/ThirdParty/M60/m60.fbx" })
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            report.AppendLine(path);
            if (source == null) { report.AppendLine("MISSING"); continue; }
            var obj = Object.Instantiate(source);
            var mat = MakeMaterial(path.Contains("Sniper") ? "Sniper" : "M60");
            foreach (var r in obj.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            foreach (var t in obj.GetComponentsInChildren<Transform>())
                report.AppendLine($"NODE {t.name}: pos={t.position:F4} local={t.localPosition:F4} rot={t.eulerAngles:F2} scale={t.lossyScale:F4}");
            foreach (var r in obj.GetComponentsInChildren<Renderer>())
                report.AppendLine($"MESH {r.name}: bounds={r.bounds.center:F4} size={r.bounds.size:F4} materials={string.Join(",", System.Array.ConvertAll(r.sharedMaterials, m => m == null ? "NULL" : m.name))}");
            if (path.Contains("m60"))
            {
                var filter = obj.GetComponentInChildren<MeshFilter>();
                int index = 0;
                foreach (var group in Components(filter.sharedMesh))
                {
                    var vertices = filter.sharedMesh.vertices;
                    var b = new Bounds(filter.transform.TransformPoint(vertices[group[0]]), Vector3.zero);
                    foreach (int v in group) b.Encapsulate(filter.transform.TransformPoint(vertices[v]));
                    report.AppendLine($"PART {index++} triangles={group.Length/3} center={b.center:F4} size={b.size:F4}");
                }
            }
            Render(obj, path.Contains("Sniper") ? "sniper-source" : "m60-source");
            Object.DestroyImmediate(obj);
        }
        File.WriteAllText("Assets/LastDeparturePrototype/LibraryWeaponInspection.txt", report.ToString());
        Debug.Log("LIBRARY WEAPON INSPECTION COMPLETE");
    }
    static List<int[]> Components(Mesh mesh)
    {
        var vertices = mesh.vertices; var triangles = mesh.triangles;
        var parent = Enumerable.Range(0,vertices.Length).ToArray();
        int Find(int v) { while(parent[v]!=v) { parent[v]=parent[parent[v]]; v=parent[v]; } return v; }
        void Join(int a,int b) { parent[Find(a)] = Find(b); }
        var welded = new Dictionary<Vector3Int,int>();
        for(int i=0;i<vertices.Length;i++)
        {
            var key = Vector3Int.RoundToInt(vertices[i]*1000000);
            if(welded.TryGetValue(key,out int prior)) Join(i,prior); else welded[key]=i;
        }
        for(int i=0;i<triangles.Length;i+=3) { Join(triangles[i],triangles[i+1]); Join(triangles[i],triangles[i+2]); }
        var groups = new Dictionary<int,List<int>>();
        for(int i=0;i<triangles.Length;i+=3)
        {
            int root=Find(triangles[i]); if(!groups.TryGetValue(root,out var group)) groups[root]=group=new List<int>();
            group.Add(triangles[i]); group.Add(triangles[i+1]); group.Add(triangles[i+2]);
        }
        return groups.Values.Select(g=>g.ToArray()).OrderByDescending(g=>g.Length).ToList();
    }
    static Material MakeMaterial(string name)
    {
        string dir = name == "Sniper" ? Folder + "Sniper/" : "Assets/LastDeparturePrototype/ThirdParty/M60/";
        string path = dir + name + " PBR.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.color = Color.white;
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + (name == "Sniper" ? "Sniper_Base_Color.png" : "M60TEXT.png")));
        material.SetFloat("_Metallic", .65f); material.SetFloat("_Smoothness", .3f);
        if (name == "Sniper")
        {
            string normal = dir + "Sniper_Normal.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(normal);
            if (importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; importer.SaveAndReimport(); }
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal)); material.EnableKeyword("_NORMALMAP");
        }
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssets(); return material;
    }
    static void Render(GameObject obj, string label)
    {
        string output = "Assets/LastDeparturePrototype/WeaponReview"; Directory.CreateDirectory(output);
        foreach (var node in obj.GetComponentsInChildren<Transform>()) node.gameObject.layer = 31;
        foreach (var camera in obj.GetComponentsInChildren<Camera>()) Object.DestroyImmediate(camera);
        foreach (var light in obj.GetComponentsInChildren<Light>()) Object.DestroyImmediate(light);
        obj.transform.position += new Vector3(1000, 1000, 1000);
        var bounds = obj.GetComponentsInChildren<Renderer>()[0].bounds;
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
        var cameraObject = new GameObject("Weapon review camera"); var cam = cameraObject.AddComponent<Camera>();
        cam.cullingMask = 1 << 31; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.16f,.18f,.21f);
        cam.nearClipPlane = .01f; cam.farClipPlane = 10; cam.orthographic = true; cam.orthographicSize = .65f;
        var lamp = new GameObject("Weapon review light").AddComponent<Light>(); lamp.type = LightType.Directional; lamp.intensity = 2; lamp.cullingMask = 1 << 31; lamp.transform.rotation = Quaternion.Euler(35,-30,0);
        var target = new RenderTexture(1200,800,24); target.Create(); var tex = new Texture2D(1200,800,TextureFormat.RGB24,false); var previous = RenderTexture.active;
        try
        {
            Vector3[] views = { new Vector3(2,.2f,0), new Vector3(-2,.2f,0), new Vector3(1,1,-1) };
            for(int i=0;i<views.Length;i++)
            {
                cam.transform.position = bounds.center + views[i]; cam.transform.LookAt(bounds.center);
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; tex.ReadPixels(new Rect(0,0,1200,800),0,0); tex.Apply(); File.WriteAllBytes(output + "/" + label + "-" + i + ".jpg", tex.EncodeToJPG(92));
            }
        }
        finally { RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(tex); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(lamp.gameObject); }
    }
}
