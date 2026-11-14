using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using YardSurvivalPrototype;

public static class FpsHandsImport
{
    public const string Source = "Assets/BoxheadPrototype/ThirdParty/FPSArms/FPS ARMS RIG 1.fbx";
    const string Folder = "Assets/BoxheadPrototype/ThirdParty/FPSArms";
    public static void Review()
    {
        if (!EditorApplication.isPlaying) throw new System.Exception("Enter Play Mode for hand review");
        var game = Object.FindAnyObjectByType<YardSurvival>();
        string output = "Assets/BoxheadPrototype/HandReview"; System.IO.Directory.CreateDirectory(output);
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); target.Create();
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        game.ResetRun();
        try
        {
            for (int weapon = 0; weapon < WeaponCatalog.All.Length; weapon++)
            for (int frame = 0; frame < 9; frame++)
            {
                float[] stages = { 0, 0, .10f, .28f, .48f, .70f, .86f, .97f, 0 };
                game.PrepareHandReview(weapon, stages[frame], frame >= 2 && frame <= 7, frame == 1, frame == 8);
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                string label = new[] { "pistol", "rifle", "marksman", "heavy", "ak47", "shotgun", "suomi", "greasegun" }[weapon];
                if (frame == 0)
                {
                    var view = Object.FindAnyObjectByType<ViewWeapon>();
                    var lines = new List<string>();
                    foreach (var filter in view.GetComponentsInChildren<MeshFilter>())
                    {
                        Bounds bounds = filter.sharedMesh.bounds;
                        lines.Add(filter.name + " center=" + view.transform.InverseTransformPoint(filter.transform.TransformPoint(bounds.center)).ToString("F4") + " size=" + Vector3.Scale(bounds.size, filter.transform.lossyScale).ToString("F4"));
                    }
                    foreach (var bone in view.GetComponentsInChildren<Transform>())
                        if (bone.name.StartsWith("hand.") || bone.name.StartsWith("f_middle.") || bone.name.StartsWith("f_index.") || bone.name.StartsWith("thumb."))
                            lines.Add(bone.name + "=" + view.transform.InverseTransformPoint(bone.position).ToString("F4"));
                    System.IO.File.WriteAllLines(output + "/" + label + "-contacts.txt", lines);
                }
                System.IO.File.WriteAllBytes(output + "/" + label + "-" + frame + ".jpg", image.EncodeToJPG(92));
                if (frame == 0 || frame == 4 || frame == 6)
                {
                    var view = Object.FindAnyObjectByType<ViewWeapon>();
                    var camera = Camera.main;
                    Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
                    float fov = camera.fieldOfView;
                    Transform parent = view.transform.parent;
                    view.transform.SetParent(null, true);
                    try
                    {
                        for (int side = 0; side < 2; side++)
                        {
                            Vector3 focus = view.transform.TransformPoint(new Vector3(0, -.06f, .12f));
                            camera.transform.position = focus + view.transform.TransformDirection(new Vector3(side == 0 ? -.85f : .85f, .15f, -.1f));
                            camera.transform.LookAt(focus, view.transform.up); camera.fieldOfView = 55;
                            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                            System.IO.File.WriteAllBytes(output + "/" + label + "-" + frame + (side == 0 ? "-left" : "-right") + ".jpg", image.EncodeToJPG(92));
                        }
                    }
                    finally { camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov; view.transform.SetParent(parent, true); }
                }
            }
            game.ResetRun(); Debug.Log("HANDS REVIEW CAPTURED: all " + WeaponCatalog.All.Length + " weapons, idle, fire, six reload phases, ADS and side views saved to " + output + ". Visual inspection is required to confirm alignment.");
        }
        finally { RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
    }
    public static void Configure(YardSurvival game)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(Source);
        importer.isReadable = true; importer.importAnimation = false; importer.SaveAndReimport();
        string texturePath = Folder + "/GlovesSleeves_Albedo.png";
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
        var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        textureImporter.maxTextureSize = 2048; textureImporter.anisoLevel = 4; textureImporter.SaveAndReimport();
        string materialPath = Folder + "/Worn gloves and sleeves.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, materialPath); }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.color = Color.white; material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .24f); EditorUtility.SetDirty(material);
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source)); instance.name = "Modeled tactical arms";
        foreach (var animator in instance.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(animator);
        foreach (var animation in instance.GetComponentsInChildren<Animation>()) Object.DestroyImmediate(animation);
        foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh = Object.Instantiate(skin.sharedMesh); mesh.name = "First person arm and finger mesh";
            var weights = mesh.boneWeights; var triangles = mesh.triangles; var kept = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int visible = 0;
                for (int k = 0; k < 3; k++)
                {
                    BoneWeight weight = weights[triangles[i + k]];
                    int bone = weight.boneIndex0; float largest = weight.weight0;
                    if (weight.weight1 > largest) { largest = weight.weight1; bone = weight.boneIndex1; }
                    if (weight.weight2 > largest) { largest = weight.weight2; bone = weight.boneIndex2; }
                    if (weight.weight3 > largest) bone = weight.boneIndex3;
                    string name = skin.bones[bone].name;
                    if (!name.StartsWith("clavicle") && !name.StartsWith("deltoid")) visible++;
                }
                if (visible >= 2) { kept.Add(triangles[i]); kept.Add(triangles[i + 1]); kept.Add(triangles[i + 2]); }
            }
            mesh.triangles = kept.ToArray(); mesh.RecalculateBounds();
            string meshPath = Folder + "/ViewArms.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
            else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(existing); }
            skin.sharedMesh = mesh; skin.sharedMaterials = new[] { material }; skin.updateWhenOffscreen = true;
        }
        instance.AddComponent<ModeledHands>();
        game.firstPersonArms = PrefabUtility.SaveAsPrefabAsset(instance, Folder + "/TacticalArms.prefab");
        Object.DestroyImmediate(instance); EditorUtility.SetDirty(game); AssetDatabase.SaveAssets();
    }
    public static void Inspect()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(Source);
        importer.isReadable = true; importer.importAnimation = false; importer.SaveAndReimport();
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
        foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Debug.Log("HAND MESH " + skin.name + " vertices=" + skin.sharedMesh.vertexCount + " bounds=" + skin.bounds + " local=" + skin.sharedMesh.bounds + " scale=" + skin.transform.lossyScale);
            foreach (var bone in skin.bones) Debug.Log("HAND BONE " + bone.name + " parent=" + bone.parent.name + " pos=" + bone.position.ToString("F4") + " local=" + bone.localPosition.ToString("F4") + " rot=" + bone.localEulerAngles.ToString("F1"));
        }
        Object.DestroyImmediate(instance);
    }
}
