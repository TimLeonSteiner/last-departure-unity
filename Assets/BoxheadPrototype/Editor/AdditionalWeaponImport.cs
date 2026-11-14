using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;
using UnityEditor.SceneManagement;

public static class AdditionalWeaponImport
{
    public const string Folder = "Assets/BoxheadPrototype/ThirdParty/LibraryGuns/Guns&Explosives/";
    public static readonly string[] Directories = { "AK-47", "Shotgun", "Suomi_KP", "GreaseGun" };
    public static readonly string[] Names = { "AK47", "Shotgun", "Suomi_KP", "Grease_Gun" };

    [MenuItem("Tools/Yard Survival/Connect additional weapon models")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before connecting weapons");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Boxhead.unity") throw new System.InvalidOperationException("Open Boxhead first");
        var game = Object.FindAnyObjectByType<YardSurvival>();
        game.additionalWeaponModels = new GameObject[4];
        for (int i = 0; i < 4; i++) game.additionalWeaponModels[i] = Build(i);
        ConfigureAudio(game);
        EditorUtility.SetDirty(game); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("ADDITIONAL WEAPONS READY: four distinct normalized library prefabs saved");
    }

    public static void ConfigureAudio(YardSurvival game)
    {
        if (WeaponCatalog.All.Length < 8) return;
        var banks = new YardSurvival.WeaponSoundBank[8];
        if (game.weaponSounds != null) System.Array.Copy(game.weaponSounds, banks, System.Math.Min(game.weaponSounds.Length, 8));
        for (int i = 0; i < 4; i++)
        {
            string dir = "Assets/BoxheadPrototype/Audio/AdditionalWeapons/" + Names[i];
            AudioClip Clip(string suffix)
            {
                string path = dir + suffix + ".wav";
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings; settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM; settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings; importer.forceToMono = true; importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            var variations = new[] { Clip("_Shot_01"), Clip("_Shot_02"), Clip("_Shot_03") };
            banks[i + 4] = new YardSurvival.WeaponSoundBank { weapon = WeaponCatalog.All[i + 4].name,
                shot = variations[0], shotVariations = variations, reload = Clip("_Reload"), action = Clip("_Action") };
        }
        game.weaponSounds = banks; EditorUtility.SetDirty(game);
    }

    static GameObject Build(int id)
    {
        string dir = Folder + Directories[id] + "/";
        var source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(dir + Names[id] + ".fbx"));
        var root = new GameObject(Names[id]);
        Vector3[] triggers = { new Vector3(.0025f, -.0532f, .2413f), new Vector3(.0011f, -.0434f, .388f), new Vector3(.0043f, -.0358f, .0701f), new Vector3(-.0001f, -.0681f, .1214f) };
        float scale = new[] { 1.15f, 1f, 1.15f, 1.3f }[id];
        Quaternion rotation = Quaternion.Euler(0, 180, 0);
        Vector3 offset = new Vector3(0, -.05f, -.035f) - rotation * triggers[id] * scale;
        Matrix4x4 normalize = Matrix4x4.TRS(offset, rotation, Vector3.one * scale);
        var material = MaterialFor(id);
        Transform magazine = null, bolt = null;
        foreach (var filter in source.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = Object.Instantiate(filter.sharedMesh);
            Matrix4x4 matrix = normalize * filter.transform.localToWorldMatrix;
            var vertices = mesh.vertices; for (int v = 0; v < vertices.Length; v++) vertices[v] = matrix.MultiplyPoint3x4(vertices[v]);
            mesh.vertices = vertices; mesh.RecalculateBounds(); Vector3 pivot = mesh.bounds.center;
            for (int v = 0; v < vertices.Length; v++) vertices[v] -= pivot;
            mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            string partName = filter.name.Replace("(Clone)", ""); mesh.name = partName;
            string meshPath = dir + "View_" + partName + ".asset";
            var previous = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (previous == null) AssetDatabase.CreateAsset(mesh, meshPath);
            else { EditorUtility.CopySerialized(mesh, previous); Object.DestroyImmediate(mesh); mesh = previous; }
            var part = new GameObject(partName); part.transform.SetParent(root.transform, false); part.transform.localPosition = pivot;
            part.AddComponent<MeshFilter>().sharedMesh = mesh; part.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (partName == "Magazine") magazine = part.transform;
            if (partName == "Bolt" || (id == 0 && partName == "Ejection_Port") || (id == 1 && partName == "Fore_Stock")) bolt = part.transform;
        }
        var rig = root.AddComponent<WeaponModelRig>(); rig.magazine = magazine; rig.bolt = bolt;
        rig.rightWrist = new Vector3(.043f, -.083f, -.18f);
        rig.supportWrist = new[] { new Vector3(-.075f, -.04f, .23f), new Vector3(-.075f, -.056f, .36f), new Vector3(-.075f, -.02f, .24f), new Vector3(-.075f, -.015f, .16f) }[id];
        Vector3[] muzzles = { new Vector3(.0025f, .035f, -.316f), new Vector3(.0011f, .033f, -.405f), new Vector3(.0047f, .026f, -.449f), new Vector3(.0009f, .001f, -.3383f) };
        rig.muzzle = normalize.MultiplyPoint3x4(muzzles[id]);
        if (magazine != null) rig.magazineWrist = magazine.localPosition + new Vector3(-.055f, id == 2 ? -.065f : -.025f, -.08f);
        rig.boltWrist = bolt.localPosition + new Vector3(-.07f, .025f, -.035f);
        rig.sightPosition = new Vector3(0, -rig.muzzle.y - .016f, .38f);
        rig.supportRotation = new Vector3(0, -35, 0);
        if (id == 1)
        {
            rig.shellLoading = true;
            rig.loadingPort = normalize.MultiplyPoint3x4(new Vector3(.0011f, -.026f, .285f));
            rig.boltTravel = new Vector3(0, 0, -.085f);
            rig.boltWrist = rig.supportWrist;
        }
        string path = dir + Names[id] + "Playable.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Render(root, Names[id] + "-normalized"); Object.DestroyImmediate(root); Object.DestroyImmediate(source);
        return prefab;
    }

    [MenuItem("Tools/Yard Survival/Inspect additional weapon models")]
    public static void Inspect()
    {
        var report = new StringBuilder();
        for (int i = 0; i < Names.Length; i++)
        {
            string path = Folder + Directories[i] + "/" + Names[i] + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (!importer.isReadable || importer.importCameras || importer.importLights)
            { importer.isReadable = true; importer.importCameras = false; importer.importLights = false; importer.SaveAndReimport(); }
            var obj = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var material = MaterialFor(i);
            report.AppendLine(path);
            foreach (var t in obj.GetComponentsInChildren<Transform>())
                report.AppendLine($"NODE {t.name}: pos={t.position:F4} rot={t.eulerAngles:F2} scale={t.lossyScale:F4}");
            foreach (var r in obj.GetComponentsInChildren<Renderer>())
            { r.sharedMaterial = material; report.AppendLine($"MESH {r.name}: center={r.bounds.center:F4} size={r.bounds.size:F4}"); }
            Render(obj, Names[i] + "-source");
            Object.DestroyImmediate(obj);
        }
        File.WriteAllText("Assets/BoxheadPrototype/AdditionalWeaponInspection.txt", report.ToString());
        Debug.Log("ADDITIONAL WEAPON INSPECTION COMPLETE");
    }

    static Material MaterialFor(int id)
    {
        string dir = Folder + Directories[id] + "/", name = Names[id];
        string path = dir + name + " PBR.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.color = Color.white;
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + name + "_Base_Color.png"));
        material.SetFloat("_Metallic", .45f); material.SetFloat("_Smoothness", .27f);
        string normal = dir + name + (id == 2 ? "_Normal.png" : "_Normal_DirectX.png");
        var importer = (TextureImporter)AssetImporter.GetAtPath(normal);
        if (importer.textureType != TextureImporterType.NormalMap)
        { importer.textureType = TextureImporterType.NormalMap; importer.SaveAndReimport(); }
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal)); material.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssets(); return material;
    }

    public static void Render(GameObject obj, string label)
    {
        string output = "Assets/BoxheadPrototype/WeaponReview"; Directory.CreateDirectory(output);
        foreach (var t in obj.GetComponentsInChildren<Transform>()) t.gameObject.layer = 31;
        foreach (var c in obj.GetComponentsInChildren<Camera>()) Object.DestroyImmediate(c);
        foreach (var l in obj.GetComponentsInChildren<Light>()) Object.DestroyImmediate(l);
        Vector3 original = obj.transform.position; obj.transform.position += Vector3.one * 1000;
        var bounds = obj.GetComponentInChildren<Renderer>().bounds;
        foreach (var r in obj.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
        var cameraObject = new GameObject("Additional weapon review camera"); var cam = cameraObject.AddComponent<Camera>();
        cam.cullingMask = 1 << 31; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.16f, .18f, .21f);
        cam.nearClipPlane = .01f; cam.farClipPlane = 100; cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(bounds.size.y * .7f, Mathf.Max(bounds.size.x, bounds.size.z) * .4f);
        var lamp = new GameObject("Additional weapon review light").AddComponent<Light>();
        lamp.type = LightType.Directional; lamp.intensity = 2; lamp.cullingMask = 1 << 31; lamp.transform.rotation = Quaternion.Euler(35, -30, 0);
        var target = new RenderTexture(1200, 800, 24); target.Create(); var tex = new Texture2D(1200, 800, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            Vector3[] views = { new Vector3(2, .1f, 0), new Vector3(-2, .1f, 0), new Vector3(0, .1f, -2), new Vector3(1, 1, -1) };
            for (int i = 0; i < views.Length; i++)
            {
                cam.transform.position = bounds.center + views[i] * Mathf.Max(1, bounds.size.magnitude); cam.transform.LookAt(bounds.center);
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; tex.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); tex.Apply();
                File.WriteAllBytes(output + "/" + label + "-" + i + ".jpg", tex.EncodeToJPG(92));
            }
        }
        finally
        {
            obj.transform.position = original; RenderTexture.active = previous; target.Release();
            Object.DestroyImmediate(target); Object.DestroyImmediate(tex); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(lamp.gameObject);
        }
    }
}
