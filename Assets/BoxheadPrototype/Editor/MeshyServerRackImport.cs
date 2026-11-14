using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class MeshyServerRackImport
{
    const string Folder = "Assets/BoxheadPrototype/Resources/Spaceport/ServerRacks/";
    const string Spaceport = "Assets/BoxheadPrototype/Resources/Spaceport/";
    const string Request = "Assets/Verification~/ImportServerRacks.request";
    const string ReviewRequest = "Assets/Verification~/ReviewServerRacks.request";
    static readonly string[] Variants = { "Compute", "Network", "Cooling" };
    static MeshyServerRackImport() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (File.Exists(ReviewRequest) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            File.Delete(ReviewRequest);
            if (EditorApplication.isPlaying) SessionState.SetBool("ServerRacks.GameReview", true);
            else ReviewInGame();
        }
        if (SessionState.GetBool("ServerRacks.GameReview", false) && EditorApplication.isPlaying)
        {
            SessionState.SetBool("ServerRacks.GameReview", false);
            try
            {
                var game = UnityEngine.Object.FindAnyObjectByType<YardSurvivalPrototype.YardSurvival>();
                if (game == null) throw new Exception("Game did not start");
                var hall = game.transform.Find("Spaceport terminal");
                var racks = hall != null ? hall.Find("Textured server racks") : null;
                if (racks == null || racks.childCount != 48) throw new Exception("Expected 48 rack instances in the active game");
                if (UnityEngine.Object.FindAnyObjectByType<YardSurvivalPrototype.FacilityCirculation>() != null ||
                    hall.Find("Rack ceiling connections") != null)
                    throw new Exception("Old organic dressing still reaches the racks");
                var counts = new int[3];
                foreach (Transform rack in racks)
                {
                    float bankZ = Mathf.Sign(rack.localPosition.z) * 9 * YardSurvivalPrototype.YardSurvival.ArenaScale;
                    if (Mathf.Abs(Mathf.Abs(rack.localPosition.z - bankZ) - .55f) > .001f)
                        throw new Exception("Rack rows are not packed back to back");
                    for (int i = 0; i < Variants.Length; i++) if (rack.name.StartsWith(Variants[i] + " ")) counts[i]++;
                    foreach (var renderer in rack.GetComponentsInChildren<MeshRenderer>())
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null || !material.name.StartsWith("ServerRack_") ||
                            material.GetTexture("_BaseMap") == null || material.GetTexture("_BumpMap") == null ||
                            material.GetTexture("_MetallicGlossMap") == null || ShaderUtil.ShaderHasError(material.shader))
                            throw new Exception("Invalid runtime rack material: " + rack.name);
                    }
                }
                foreach (int count in counts) if (count != 16) throw new Exception("Expected 16 instances of each rack variant");
                if (racks.GetComponentsInChildren<Collider>().Length != 0) throw new Exception("Unexpected new collision blockers");
                int bankColliders = 0;
                var legacyHall = game.transform.Find("Warehouse yard/Warehouse hall");
                foreach (var collider in legacyHall.GetComponentsInChildren<BoxCollider>())
                {
                    if (collider.name != "Freight stack collision") continue;
                    bankColliders++;
                    if (Mathf.Abs(collider.bounds.size.x - 7.72f) > .02f ||
                        Mathf.Abs(collider.bounds.size.z - 2.15f) > .02f)
                        throw new Exception("Rack collision extends beyond the compact bank");
                }
                if (bankColliders != 4) throw new Exception("Expected one collision volume per rack bank");
                SpaceportAssetBuild.Capture(new Vector3(9.6f, 2.5f, -18f), new Vector3(13.5f, 1.35f, -12.2f), "ServerRacks-Compact");
                File.WriteAllText("Assets/Verification~/ServerRacksGameReview.txt",
                    "PASS active game loads replacement terminal\nPASS 48 racks: 16 compute, 16 network, 16 cooling\nPASS compact banks and no organic rack dressing\nPASS all runtime materials have color, normal and metallic/smoothness maps\nPASS no added collision blockers\n");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                else EditorApplication.isPaused = true;
            }
            catch (Exception e)
            {
                File.WriteAllText("Assets/Verification~/ServerRacksGameReview.txt", "FAIL " + e);
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
            return;
        }
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Import(); }
        catch (Exception e)
        {
            File.WriteAllText("Assets/Verification~/ServerRackImport.txt", "FAIL " + e);
            Debug.LogException(e);
        }
    }

    public static void ReviewInGame()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Boxhead.unity")
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Boxhead.unity");
        SessionState.SetBool("ServerRacks.GameReview", true);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Tools/Dustline/Import textured Meshy server racks")]
    public static void Import()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var report = new StringBuilder();
        var prefabs = new GameObject[3];
        for (int i = 0; i < Variants.Length; i++) prefabs[i] = ImportRack(Variants[i], report);
        BuildTerminal(prefabs, report);
        AssetDatabase.SaveAssets();
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) CaptureRacks(prefabs);
        File.WriteAllText("Assets/Verification~/ServerRackImport.txt", report.ToString());
        Debug.Log(report.ToString());
    }

    static GameObject ImportRack(string name, StringBuilder report)
    {
        string path = Folder + name;
        var importer = AssetImporter.GetAtPath(path + ".fbx") as ModelImporter;
        if (importer == null) throw new Exception("Missing Meshy rack: " + name);
        importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
        importer.addCollider = false; importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        foreach (string suffix in new[] { "Color", "Normal", "MetallicSmoothness" })
        {
            var texture = AssetImporter.GetAtPath(path + suffix + ".png") as TextureImporter;
            if (texture == null) throw new Exception("Missing rack map: " + name + suffix);
            texture.textureType = suffix == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
            texture.sRGBTexture = suffix == "Color"; texture.maxTextureSize = 4096;
            texture.mipmapEnabled = true; texture.anisoLevel = 4;
            texture.textureCompression = TextureImporterCompression.CompressedHQ;
            texture.SaveAndReimport();
        }
        var material = AssetDatabase.LoadAssetAtPath<Material>(path + ".mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path + ".mat");
        }
        material.name = "ServerRack_" + name;
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path + "Color.png"));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path + "Normal.png"));
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path + "MetallicSmoothness.png"));
        material.SetFloat("_BumpScale", 1); material.SetFloat("_Smoothness", 1);
        material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.enableInstancing = true; EditorUtility.SetDirty(material);
        var root = new GameObject("Meshy " + name + " rack");
        try
        {
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path + ".fbx"), root.transform);
            var renderers = model.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) throw new Exception("Empty rack: " + name);
            var bounds = renderers[0].bounds;
            int triangles = 0;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                var slots = renderer.sharedMaterials;
                for (int j = 0; j < slots.Length; j++) slots[j] = material;
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                for (int sub = 0; sub < mesh.subMeshCount; sub++) triangles += (int)mesh.GetIndexCount(sub) / 3;
            }
            if (triangles < 100 || triangles > 30000) throw new Exception("Unexpected rack polygon count: " + triangles);
            // Normalize all three variants to the established cabinet footprint.
            model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            root.transform.localScale = new Vector3(1.27f / bounds.size.x, 2.6f / bounds.size.y, 1.05f / bounds.size.z);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path + ".prefab");
            report.AppendLine("PASS " + name + ": " + triangles + " triangles; base color, normal and metallic/smoothness maps assigned");
            return prefab;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static bool InOldRack(Vector3 p)
    {
        return p.y > .025f && p.y < 2.85f &&
            Mathf.Abs(Mathf.Abs(p.x) - 10f * 23f / 17f) < 4.2f &&
            Mathf.Abs(Mathf.Abs(p.z) - 9f * 23f / 17f) < 2.95f;
    }

    static void CaptureRacks(GameObject[] racks)
    {
        var stage = new GameObject("Rack import preview");
        var target = new RenderTexture(1500, 900, 24);
        var pixels = new Texture2D(1500, 900, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            stage.transform.position = new Vector3(3000, 0, 3000);
            for (int i = 0; i < racks.Length; i++)
            {
                var model = UnityEngine.Object.Instantiate(racks[i], stage.transform);
                model.transform.localPosition = new Vector3((i - 1) * 2.1f, 0, 0);
                foreach (var child in model.GetComponentsInChildren<Transform>()) child.gameObject.layer = 30;
            }
            foreach (float angle in new[] { -35f, 130f })
            {
                var light = new GameObject("Rack preview light").AddComponent<Light>();
                light.transform.SetParent(stage.transform, false);
                light.type = LightType.Directional; light.intensity = angle < 0 ? 2.4f : 1.2f;
                light.transform.rotation = Quaternion.Euler(35, angle, 0);
                light.cullingMask = 1 << 30;
            }
            var camera = new GameObject("Rack preview camera").AddComponent<Camera>();
            camera.transform.SetParent(stage.transform, false);
            camera.transform.localPosition = new Vector3(4.1f, 3.4f, 8.2f);
            camera.transform.LookAt(stage.transform.position + new Vector3(0, 1.3f, 0));
            camera.fieldOfView = 36; camera.nearClipPlane = .1f; camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.11f, .13f, .15f);
            camera.cullingMask = 1 << 30;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            target.Create();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1500, 900), 0, 0); pixels.Apply();
            File.WriteAllBytes("Assets/Verification~/ServerRacks-Unity.png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; target.Release();
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(stage);
        }
    }

    static void BuildTerminal(GameObject[] racks, StringBuilder report)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Spaceport + "CraftedTerminalSet.prefab");
        if (source == null) throw new Exception("Missing existing terminal prefab");
        var root = UnityEngine.Object.Instantiate(source);
        root.name = "MeshyRackTerminalSet";
        try
        {
            int removed = 0, meshNumber = 0;
            // Source meshes are batched by material. Strip only triangles wholly inside
            // the four rack islands; leave the floor, roof and wall architecture intact.
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                var vertices = mesh.vertices;
                bool changed = false;
                var matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var indices = mesh.GetTriangles(sub);
                    var kept = new List<int>(indices.Length);
                    for (int j = 0; j < indices.Length; j += 3)
                    {
                        if (InOldRack(matrix.MultiplyPoint3x4(vertices[indices[j]])) &&
                            InOldRack(matrix.MultiplyPoint3x4(vertices[indices[j + 1]])) &&
                            InOldRack(matrix.MultiplyPoint3x4(vertices[indices[j + 2]])))
                        { removed++; changed = true; }
                        else { kept.Add(indices[j]); kept.Add(indices[j + 1]); kept.Add(indices[j + 2]); }
                    }
                    mesh.SetTriangles(kept, sub);
                }
                if (!changed) { UnityEngine.Object.DestroyImmediate(mesh); continue; }
                mesh.RecalculateBounds();
                string meshPath = Folder + "TerminalWithoutRacks" + meshNumber++ + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing == null) { AssetDatabase.CreateAsset(mesh, meshPath); existing = mesh; }
                else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); }
                filter.sharedMesh = existing;
            }
            if (removed < 100) throw new Exception("Rack replacement removed too little geometry: " + removed);
            var group = new GameObject("Textured server racks").transform;
            group.SetParent(root.transform, false);
            int count = 0;
            foreach (int side in new[] { -1, 1 })
            foreach (int row in new[] { -1, 1 })
            for (int cabinet = 0; cabinet < 6; cabinet++)
            foreach (int face in new[] { -1, 1 })
            {
                int variant = (cabinet + (row > 0 ? 1 : 0)) % 3;
                var rack = (GameObject)PrefabUtility.InstantiatePrefab(racks[variant], group);
                rack.name = Variants[variant] + " " + count++;
                rack.transform.localPosition = new Vector3(side * 10f * 23f / 17f + (cabinet - 2.5f) * 1.33f,
                    .025f, row * 9f * 23f / 17f + face * 2.1f);
                rack.transform.localRotation = Quaternion.Euler(0, face < 0 ? 180 : 0, 0);
            }
            if (root.GetComponentsInChildren<Collider>().Length != 0) throw new Exception("Decorative racks must retain existing gameplay collision");
            PrefabUtility.SaveAsPrefabAsset(root, Spaceport + "MeshyRackTerminalSet.prefab");
            report.AppendLine("PASS replaced " + removed + " old rack triangles with " + count + " textured cabinets, three variants; no added colliders");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
