using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class FullMeshyRoofImport
{
    const string Folder = "Assets/LastDeparturePrototype/Resources/Spaceport/FullRoof/";
    const string Request = "Assets/Verification~/ImportFullMeshyRoof.request";
    static FullMeshyRoofImport() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Import(); }
        catch (Exception ex) { File.WriteAllText("Assets/Verification~/FullFullMeshyRoofImport.txt", "FAIL " + ex); Debug.LogException(ex); }
    }

    [MenuItem("Tools/Yard Survival/Import full Meshy roof")]
    public static void Import()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(Folder + "RoofModule.fbx") as ModelImporter;
        if (importer == null) throw new Exception("Meshy roof FBX is missing.");
        importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
        importer.addCollider = false; importer.isReadable = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
        foreach (var suffix in new[] { "Color", "Normal", "MetallicSmoothness" })
        {
            var texture = AssetImporter.GetAtPath(Folder + "Roof" + suffix + ".png") as TextureImporter;
            if (texture == null) throw new Exception("Missing roof texture: " + suffix);
            texture.textureType = suffix == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
            texture.sRGBTexture = suffix == "Color"; texture.maxTextureSize = 4096;
            texture.mipmapEnabled = true; texture.anisoLevel = 4; texture.SaveAndReimport();
        }
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Roof.mat");
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, Folder + "Roof.mat"); }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "RoofColor.png"));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "RoofNormal.png"));
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "RoofMetallicSmoothness.png"));
        material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", .65f);
        material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.SetFloat("_Cull", 2);
        material.enableInstancing = true; EditorUtility.SetDirty(material);

        var source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "RoofModule.fbx"));
        Mesh roofMesh;
        try
        {
            var combine = new List<CombineInstance>();
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>())
                for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                    combine.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub, transform = filter.transform.localToWorldMatrix });
            roofMesh = new Mesh { name = "Meshy roof module", indexFormat = IndexFormat.UInt32 };
            roofMesh.CombineMeshes(combine.ToArray());
            var bounds = roofMesh.bounds;
            if (bounds.size.y > bounds.size.x || bounds.size.y > bounds.size.z) throw new Exception("Roof FBX must lie flat in XZ before import. Bounds: " + bounds.size);
            var vertices = roofMesh.vertices;
            var scale = new Vector3(36 * YardSurvival.ArenaScale / bounds.size.x, 1.1f / bounds.size.y, 36 * YardSurvival.ArenaScale / bounds.size.z);
            for (int i = 0; i < vertices.Length; i++) vertices[i] = Vector3.Scale(vertices[i] - bounds.center, scale);
            roofMesh.vertices = vertices; roofMesh.RecalculateNormals(); roofMesh.RecalculateTangents(); roofMesh.RecalculateBounds();
            var frontVertices=roofMesh.vertices; var frontNormals=roofMesh.normals; var frontUV=roofMesh.uv; var frontFaces=roofMesh.triangles;
            var bothVertices=new List<Vector3>(frontVertices); bothVertices.AddRange(frontVertices);
            var bothNormals=new List<Vector3>(frontNormals); foreach(var normal in frontNormals)bothNormals.Add(-normal);
            var bothUV=new List<Vector2>(frontUV);bothUV.AddRange(frontUV);
            var bothFaces=new List<int>(frontFaces);int offset=frontVertices.Length;
            for(int i=0;i<frontFaces.Length;i+=3){bothFaces.Add(frontFaces[i]+offset);bothFaces.Add(frontFaces[i+2]+offset);bothFaces.Add(frontFaces[i+1]+offset);}
            roofMesh.SetVertices(bothVertices);roofMesh.SetNormals(bothNormals);roofMesh.SetUVs(0,bothUV);roofMesh.SetTriangles(bothFaces,0);roofMesh.RecalculateTangents();roofMesh.RecalculateBounds();
            roofMesh = SaveMesh(roofMesh, "FullRoof");
        }
        finally { UnityEngine.Object.DestroyImmediate(source); }
        
        string terminalPath = "Assets/LastDeparturePrototype/Resources/Spaceport/";
        var terminal = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(terminalPath + "MeshyRackTerminalSet.prefab"));
        try
        {
            terminal.name = "MeshyRoofTerminalSet";
            int removed = 0, number = 0;
            foreach (var filter in terminal.GetComponentsInChildren<MeshFilter>())
            {
                var original = filter.sharedMesh;
                var vertices = original.vertices;
                var matrix = terminal.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var mesh = UnityEngine.Object.Instantiate(original);
                bool changed = false;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var kept = new List<int>(); var indices = original.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        bool roof = true;
                        for (int corner = 0; corner < 3; corner++)
                        {
                            var p = matrix.MultiplyPoint3x4(vertices[indices[i + corner]]);
                            roof &= p.y >= 7.05f && p.y <= 8.26f;
                        }
                        if (roof) { removed++; changed = true; }
                        else { kept.Add(indices[i]); kept.Add(indices[i + 1]); kept.Add(indices[i + 2]); }
                    }
                    mesh.SetTriangles(kept, sub);
                }
                if (changed) { mesh.RecalculateBounds(); filter.sharedMesh = SaveMesh(mesh, "TerminalWithoutRoof" + number++); }
                else UnityEngine.Object.DestroyImmediate(mesh);
            }
            if (removed < 100) throw new Exception("Expected old roof geometry was not found: " + removed);
            var group = new GameObject("Meshy roof").transform; group.SetParent(terminal.transform, false);
            var tile = new GameObject("Rustworn Canopy / complete roof");
            tile.transform.SetParent(group, false);
            tile.transform.localPosition = new Vector3(0, 7.8f, 0);
            tile.transform.localRotation = Quaternion.Euler(0, 90, 0);
            tile.AddComponent<MeshFilter>().sharedMesh = roofMesh;
            var renderer = tile.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            renderer.allowOcclusionWhenDynamic = false; // The scene bake predates this replacement roof.
            var collider = tile.AddComponent<MeshCollider>(); collider.sharedMesh = roofMesh;
            Physics.SyncTransforms();
            BakeRain(collider);
            PrefabUtility.SaveAsPrefabAsset(terminal, terminalPath + "MeshyRoofTerminalSet.prefab");
            AssetDatabase.SaveAssets();
            File.WriteAllText("Assets/Verification~/FullMeshyRoofImport.txt", "PASS removed " + removed + " old roof triangles; complete Meshy roof and mesh collision.\nTriangles: " + roofMesh.triangles.Length / 3 + "\nBounds: " + roofMesh.bounds + "\n");
        }
        finally { UnityEngine.Object.DestroyImmediate(terminal); }
    }

    static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = Folder + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); return existing;
    }

    static void BakeRain(MeshCollider roof)
    {
        const int n = 36;
        var open = new bool[n,n];
        float scale = YardSurvival.ArenaScale;
        for(int x=1;x<n-1;x++) for(int z=1;z<n-1;z++)
        {
            bool clear = true;
            // Sample the whole patch, including a margin for slanted rain.
            for(int dx=-2;dx<=2;dx++) for(int dz=-2;dz<=2;dz++)
            {
                var p = new Vector3((x-17.5f+dx*.28f)*scale, 12, (z-17.5f+dz*.28f)*scale);
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
            patches.Add(new Rect(x-18+.15f,z-18+.15f,w-.3f,h-.3f));
        }
        if(patches.Count<3)throw new Exception("Could not find the roof openings.");
        var layout=AssetDatabase.LoadAssetAtPath<FullRoofLayout>(Folder+"Layout.asset");
        if(layout==null){layout=ScriptableObject.CreateInstance<FullRoofLayout>();AssetDatabase.CreateAsset(layout,Folder+"Layout.asset");}
        layout.rainPatches=patches.ToArray();EditorUtility.SetDirty(layout);
        File.WriteAllText("Assets/Verification~/FullRoofRainBake.txt",string.Join("\n",patches));
    }
}
