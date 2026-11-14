using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class MeshyDreamChargerSetup
{
    const string Root = "Assets/BoxheadPrototype/ThirdParty/MeshyDream/Charger/";
    const string Request = "Assets/Verification~/PrepareMeshyDreamCharger.request";
    static MeshyDreamChargerSetup() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying) return;
        File.Delete(Request);
        try { Build(); NightmareCloudReview.Run(); }
        catch(Exception e) { File.WriteAllText("Assets/Verification~/MeshyDreamCharger-error.txt",e.ToString()); Debug.LogException(e); }
    }
    public static void Build()
    {
        BuildModel("Charger",2.3f);
    }
    public static void BuildModel(string name, float height, bool rigged = false)
    {
        string Root="Assets/BoxheadPrototype/ThirdParty/MeshyDream/"+name+"/";
        AssetDatabase.ImportAsset(Root+name+".fbx",ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(Root+"BaseColor.png",ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(Root+"Normal.png",ImportAssetOptions.ForceSynchronousImport);
        var normalImporter = (TextureImporter)AssetImporter.GetAtPath(Root+"Normal.png");
        normalImporter.textureType=TextureImporterType.NormalMap; normalImporter.SaveAndReimport();
        var modelImporter = (ModelImporter)AssetImporter.GetAtPath(Root+name+".fbx");
        modelImporter.animationType=rigged ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
        modelImporter.importAnimation=false; modelImporter.materialImportMode=ModelImporterMaterialImportMode.None;
        modelImporter.SaveAndReimport();
        var material = AssetDatabase.LoadAssetAtPath<Material>(Root+name+".mat");
        if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,Root+name+".mat"); }
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"BaseColor.png"));
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Normal.png"));
        material.EnableKeyword("_NORMALMAP"); material.SetFloat("_Smoothness",.22f);
        EditorUtility.SetDirty(material);
        var holder = new GameObject("Meshy "+name);
        try
        {
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+name+".fbx"),holder.transform);
            var renderers=model.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0) throw new Exception("Meshy Charger FBX has no renderers");
            Bounds b=renderers[0].bounds; foreach(var r in renderers) b.Encapsulate(r.bounds);
            if(b.size.y<.001f) throw new Exception("Invalid Meshy Charger bounds");
            model.transform.localScale *= height/b.size.y;
            b=renderers[0].bounds; foreach(var r in renderers) b.Encapsulate(r.bounds);
            model.transform.position -= new Vector3(b.center.x,b.min.y,b.center.z);
            foreach(var r in renderers)
            {
                var slots=r.sharedMaterials;
                for(int i=0;i<slots.Length;i++) slots[i]=material;
                r.sharedMaterials=slots;
            }
            holder.AddComponent<NightmareAppearance>();
            Directory.CreateDirectory("Assets/BoxheadPrototype/Resources/MeshyDreamZombies");
            AssetDatabase.Refresh();
            PrefabUtility.SaveAsPrefabAsset(holder,"Assets/BoxheadPrototype/Resources/MeshyDreamZombies/"+name+".prefab");
            AssetDatabase.SaveAssets();
        }
        finally { UnityEngine.Object.DestroyImmediate(holder); }
    }
}
