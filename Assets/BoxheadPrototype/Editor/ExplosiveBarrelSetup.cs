using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;

public static class ExplosiveBarrelSetup
{
    const string Root = "Assets/BoxheadPrototype/ThirdParty/ExplosiveBarrel";
    public const string PrefabPath = "Assets/BoxheadPrototype/Prefabs/ExplosiveBarrel.prefab";
    [MenuItem("Tools/Yard Survival/Create explosive barrel asset")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode before creating the barrel prefab.");
        AssetDatabase.Refresh();
        string modelPath = Root + "/Barrel_01_2k.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.isReadable = true; importer.SaveAndReimport();
        string TexturePath(string suffix) => Root + "/textures/Barrel_01_explosive_" + suffix + "_2k.png";
        foreach (string suffix in new[] { "nor_gl", "metallic", "roughness" })
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(TexturePath(suffix));
            ti.sRGBTexture = false; ti.isReadable = suffix != "nor_gl";
            if (suffix == "nor_gl") ti.textureType = TextureImporterType.NormalMap;
            ti.SaveAndReimport();
        }
        var metal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath("metallic"));
        var rough = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath("roughness"));
        var packed = new Texture2D(metal.width, metal.height, TextureFormat.RGBA32, false, true);
        var pixels = metal.GetPixels32(); var roughPixels = rough.GetPixels32();
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(pixels[i].r, 0, 0, (byte)(255 - roughPixels[i].r));
        packed.SetPixels32(pixels); packed.Apply();
        string packedPath = Root + "/textures/Barrel_MetallicSmoothness.png";
        File.WriteAllBytes(packedPath, packed.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(packed);
        AssetDatabase.ImportAsset(packedPath);
        var pi = (TextureImporter)AssetImporter.GetAtPath(packedPath); pi.sRGBTexture = false; pi.SaveAndReimport();
        string materialPath = Root + "/ExplosiveBarrel.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, materialPath); }
        material.SetColor("_BaseColor", Color.white); material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath("diff")));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath("nor_gl"))); material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath)); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.SetFloat("_Smoothness", 1);
        var root = new GameObject("Explosive Barrel");
        try
        {
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
            model.name = "Weathered steel drum";
            foreach (var renderer in model.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            Bounds BoundsOf() { var all = model.GetComponentsInChildren<Renderer>(); var b = all[0].bounds; foreach (var r in all) b.Encapsulate(r.bounds); return b; }
            Bounds bounds = BoundsOf(); model.transform.localScale *= .9f / bounds.size.y; bounds = BoundsOf();
            model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z); bounds = BoundsOf();
            var barrel = root.AddComponent<ExplosiveBarrel>(); barrel.localBounds = bounds;
            var collider = root.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if (game != null && game.transform.Find("Explosive barrels") == null)
        {
            var group = new GameObject("Explosive barrels"); group.transform.SetParent(game.transform, false);
            foreach (var position in new[] { new Vector3(4, 0, 5), new Vector3(6, 0, 5) })
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), group.transform);
                instance.transform.position = position;
            }
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene); EditorSceneManager.SaveScene(game.gameObject.scene);
        }
        Debug.Log("EXPLOSIVE BARREL ASSET READY: " + PrefabPath);
    }
    [MenuItem("GameObject/Yard Survival/Explosive barrel", false, 10)]
    public static void Place()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Setup(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Place explosive barrel");
        if (SceneView.lastActiveSceneView != null) { var point = SceneView.lastActiveSceneView.pivot; instance.transform.position = new Vector3(point.x, 0, point.z); }
        Selection.activeGameObject = instance; EditorSceneManager.MarkSceneDirty(instance.scene);
    }
}
