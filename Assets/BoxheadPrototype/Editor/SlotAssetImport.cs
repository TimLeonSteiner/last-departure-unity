using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad] public static class SlotAssetImport
{
    const string Root="Assets/BoxheadPrototype/ThirdParty/BlenderKitSlots/";
    static SlotAssetImport(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Assets/BoxheadPrototype/SlotAssetImport.request";
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(request))return;
        File.Delete(request);
        try
        {
            AssetDatabase.Refresh();
            string normalPath=Root+"igrovoi_avtomat_avtomat_Normal_jpg.png";
            var normal=(TextureImporter)AssetImporter.GetAtPath(normalPath);normal.textureType=TextureImporterType.NormalMap;normal.SaveAndReimport();
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"SlotMachine.mat");
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Root+"SlotMachine.mat");}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"igrovoi_avtomat_avtomat_BaseColor_png.png"));
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"igrovoi_avtomat_avtomat_Emissive_jpg.png"));
            material.SetColor("_EmissionColor",Color.white*1.4f);material.EnableKeyword("_EMISSION");material.SetFloat("_Metallic",.45f);material.SetFloat("_Smoothness",.35f);
            var root=new GameObject("Gaming slot machine — Yuliya Kaminskaya");
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"GamingSlotMachine.fbx"),root.transform);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=material;
            var bounds=model.GetComponentInChildren<Renderer>().bounds;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())bounds.Encapsulate(renderer.bounds);
            float scale=2.1f/bounds.size.y;model.transform.localScale*=scale;model.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z)*scale;
            if(!AssetDatabase.IsValidFolder("Assets/Resources"))AssetDatabase.CreateFolder("Assets","Resources");
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/DustlineSlotMachine.prefab");Object.DestroyImmediate(root);EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
            File.WriteAllText(Root+"ImportResult.txt","PASS imported original BlenderKit mesh, base colour, normal and emissive maps; normalized to 2.1m.\n");
        }
        catch(System.Exception e){File.WriteAllText(Root+"ImportResult.txt","FAIL "+e);Debug.LogException(e);}
    }
}
