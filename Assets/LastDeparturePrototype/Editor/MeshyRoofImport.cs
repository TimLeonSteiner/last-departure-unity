using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class MeshyRoofImport
{
    const string Folder = "Assets/LastDeparturePrototype/Resources/Spaceport/MeshyRoof/";
    const string Request = "Assets/Verification~/ImportMeshyRoof.request";
    static MeshyRoofImport() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Import(); }
        catch (Exception ex) { File.WriteAllText("Assets/Verification~/MeshyRoofImport.txt", "FAIL " + ex); Debug.LogException(ex); }
    }

    [MenuItem("Tools/Yard Survival/Import Meshy roof")]
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
            texture.sRGBTexture = suffix == "Color"; texture.maxTextureSize = 2048;
            texture.mipmapEnabled = true; texture.anisoLevel = 4; texture.SaveAndReimport();
        }
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Roof.mat");
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, Folder + "Roof.mat"); }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "RoofColor.png"));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "RoofNormal.png"));
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "RoofMetallicSmoothness.png"));
        material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", .65f);
        material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
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
            var scale = new Vector3(6 / bounds.size.x, .25f / bounds.size.y, 6 / bounds.size.z);
            for (int i = 0; i < vertices.Length; i++) vertices[i] = Vector3.Scale(vertices[i] - bounds.center, scale);
            roofMesh.vertices = vertices; roofMesh.RecalculateNormals(); roofMesh.RecalculateTangents(); roofMesh.RecalculateBounds();
            roofMesh = SaveMesh(roofMesh, "RoofTile");
        }
        finally { UnityEngine.Object.DestroyImmediate(source); }
        var breached = SaveMesh(Breach(roofMesh), "RoofTileBreached");
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
                            roof &= p.y >= 7.94f && p.y <= 8.26f;
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
            int count = 0;
            for (int x = -15; x <= 15; x += 6)
                for (int z = -15; z <= 15; z += 6)
                {
                    if (DepotModels.IsCollapsedRoofTile(x, z)) continue;
                    bool broken = Array.Exists(DepotModels.RoofLeaks, p => p == new Vector2(x, z));
                    var tile = new GameObject(broken ? "Meshy roof / rain opening" : "Meshy roof / intact");
                    tile.transform.SetParent(group, false);
                    tile.transform.localPosition = new Vector3(x * YardSurvival.ArenaScale, 8.1f, z * YardSurvival.ArenaScale);
                    tile.transform.localScale = new Vector3(YardSurvival.ArenaScale, 1, YardSurvival.ArenaScale);
                    tile.AddComponent<MeshFilter>().sharedMesh = broken ? breached : roofMesh;
                    var renderer = tile.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
                    count++;
                }
            if (count != 28) throw new Exception("Unexpected surviving tile count: " + count);
            PrefabUtility.SaveAsPrefabAsset(terminal, terminalPath + "MeshyRoofTerminalSet.prefab");
            AssetDatabase.SaveAssets();
            File.WriteAllText("Assets/Verification~/MeshyRoofImport.txt", "PASS removed " + removed + " old roof triangles; 28 Meshy tiles; rain and north collapse openings retained; no new colliders.\nTile triangles: " + roofMesh.triangles.Length / 3 + "\n");
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
    static Mesh Breach(Mesh source)
    {
        var vertices = source.vertices; var uv = source.uv; var indices = source.triangles;
        var points = new List<Vector3>(); var coords = new List<Vector2>(); var triangles = new List<int>();
        foreach (var region in new[] { new Rect(-3,-3,1,6), new Rect(2,-3,1,6), new Rect(-2,-3,4,1), new Rect(-2,2,4,1) })
            for (int i = 0; i < indices.Length; i += 3)
            {
                var poly = new List<Vertex>();
                for (int j = 0; j < 3; j++) poly.Add(new Vertex { p = vertices[indices[i + j]], uv = uv[indices[i + j]] });
                poly = Clip(poly, 0, region.xMin, true); poly = Clip(poly, 0, region.xMax, false);
                poly = Clip(poly, 2, region.yMin, true); poly = Clip(poly, 2, region.yMax, false);
                int start = points.Count;
                foreach (var v in poly) { points.Add(v.p); coords.Add(v.uv); }
                for (int j = 1; j + 1 < poly.Count; j++) { triangles.Add(start); triangles.Add(start + j); triangles.Add(start + j + 1); }
            }
        var mesh = new Mesh { name = "Meshy roof with open rain shaft", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(points); mesh.SetUVs(0, coords); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
    }
}
