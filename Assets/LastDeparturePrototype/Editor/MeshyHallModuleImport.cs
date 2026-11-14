using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MeshyHallModuleImport
{
    const string Folder = "Assets/LastDeparturePrototype/Resources/Spaceport/MeshyModules/";
    const string Request = "Assets/Verification~/ImportMeshyHallModules.request";

    static MeshyHallModuleImport() => EditorApplication.update += Poll;

    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        Import();
    }

    [MenuItem("Tools/Dustline/Import Meshy hall modules")]
    public static void Import()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string name in new[] { "ServerRack", "WindowWall", "DoorwayBay", "FloorTile", "RoofTruss" })
        {
            string path = Folder + name;
            if (!File.Exists(path + ".fbx")) continue;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".fbx");
            var color = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "Color.png");
            if (model == null || color == null) continue;

            string normalPath = path + "Normal.png";
            if (AssetImporter.GetAtPath(normalPath) is TextureImporter normalImporter &&
                normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path + ".mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path + ".mat");
            }
            material.SetTexture("_BaseMap", color);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_Metallic", .18f);
            material.SetFloat("_Smoothness", .33f);
            if (normal != null) material.EnableKeyword("_NORMALMAP");
            else material.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);

            var instance = Object.Instantiate(model);
            try
            {
                instance.name = name;
                var renderers = instance.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length == 0) continue;
                foreach (var renderer in renderers)
                {
                    var slots = renderer.sharedMaterials;
                    for (int slot = 0; slot < slots.Length; slot++) slots[slot] = material;
                    renderer.sharedMaterials = slots;
                }
                PrefabUtility.SaveAsPrefabAsset(instance, path + ".prefab");
            }
            finally { Object.DestroyImmediate(instance); }
        }
        AssembleHall();
        AssetDatabase.SaveAssets();
    }

    static void AssembleHall()
    {
        GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
        var wall = Load("WindowWall");
        var doorway = Load("DoorwayBay");
        var floor = Load("FloorTile");
        var truss = Load("RoofTruss");
        var rack = Load("ServerRack");
        if (wall == null || doorway == null || floor == null || truss == null || rack == null) return;

        const float halfSpan = 23f;
        const float bayWidth = 6f;
        const float wallHeight = 7.5f;
        var root = new GameObject("Meshy Heart Chamber");
        try
        {
            // Reserve the center for the Heart and align the four gates with the existing arena axes.
            for (int side = 0; side < 4; side++)
            {
                var rotation = Quaternion.Euler(0, 90 * side, 0);
                Place(doorway, root.transform, rotation * new Vector3(0, 0, halfSpan), rotation,
                    new Vector3(bayWidth, wallHeight, .65f));
                for (int bay = 0; bay < 8; bay++)
                {
                    float width = (halfSpan * 2 - bayWidth) / 8;
                    float offset = (bay < 4 ? -1 : 1) * (bayWidth * .5f + (bay % 4 + .5f) * width);
                    Place(wall, root.transform, rotation * new Vector3(offset, 0, halfSpan), rotation,
                        new Vector3(width, wallHeight, .65f));
                }
            }
            float tileSize = halfSpan / 4;
            for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
                Place(floor, root.transform, new Vector3((x - 3.5f) * tileSize, -.16f, (z - 3.5f) * tileSize),
                    Quaternion.identity, new Vector3(tileSize, .16f, tileSize));
            for (int row = -3; row <= 3; row++)
                Place(truss, root.transform, new Vector3(0, wallHeight - .8f, row * 6f),
                    Quaternion.identity, new Vector3(halfSpan * 2, .8f, .45f));
            foreach (int side in new[] { -1, 1 })
            foreach (int row in new[] { -1, 1 })
            for (int cabinet = 0; cabinet < 6; cabinet++)
            for (int face = -1; face <= 1; face += 2)
                Place(rack, root.transform, new Vector3(side * 13.53f + (cabinet - 2.5f) * 1.33f,
                        0, row * 12.18f + face * 1.4f), Quaternion.Euler(0, face < 0 ? 180 : 0, 0),
                    new Vector3(1.3f, 2.6f, 2.6f));
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "HeartChamberLayout.prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    static void Place(GameObject prefab, Transform root, Vector3 position, Quaternion rotation, Vector3 size)
    {
        var slot = new GameObject(prefab.name).transform;
        slot.SetParent(root, false);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, slot);
        var renderers = model.GetComponentsInChildren<MeshRenderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        var center = slot.InverseTransformPoint(bounds.center);
        model.transform.localPosition -= new Vector3(center.x, center.y - bounds.extents.y, center.z);
        slot.localScale = new Vector3(size.x / Mathf.Max(.001f, bounds.size.x),
            size.y / Mathf.Max(.001f, bounds.size.y), size.z / Mathf.Max(.001f, bounds.size.z));
        slot.SetLocalPositionAndRotation(position, rotation);
    }
}
