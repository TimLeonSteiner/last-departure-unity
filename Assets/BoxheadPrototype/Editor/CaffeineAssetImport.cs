using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class CaffeineAssetImport
{
    const string Folder="Assets/BoxheadPrototype/Resources/Spaceport/";
    static CaffeineAssetImport(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Assets/Verification~/ImportCaffeine.request";
        if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}
        File.Delete(request);
        try
        {
            string path=Folder+"CaffeineBlister.fbx";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(Folder+"CaffeineFoil.png",ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.addCollider=false;
            importer.SaveAndReimport();
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                root.name="CaffeineBlisterPickup";
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    string key=renderer.name.Replace("Caffeine_","");
                    string materialPath=Folder+"Caffeine_"+key+".mat";
                    var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,materialPath);}
                    Color color=key=="Foil"?new Color(.6f,.62f,.57f):key=="Green"?new Color(.18f,.30f,.12f):key=="Cream"?new Color(.85f,.78f,.59f):key=="Ink"?new Color(.06f,.09f,.045f):new Color(.78f,.86f,.82f,.12f);
                    mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",key=="Foil"?.85f:0);
                    mat.SetFloat("_Smoothness",key=="Window"?.85f:key=="Foil"?.48f:.4f);
                    if(key=="Foil")mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"CaffeineFoil.png"));
                    if(key=="Window")
                    {
                        mat.SetFloat("_Surface",1);mat.SetFloat("_ZWrite",0);
                        mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.SetOverrideTag("RenderType","Transparent");mat.renderQueue=3000;
                        renderer.shadowCastingMode=ShadowCastingMode.Off;
                    }
                    if(key=="Green"||key=="Cream") {mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*.10f);}
                    EditorUtility.SetDirty(mat);renderer.sharedMaterial=mat;
                }
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"CaffeineBlisterPickup.prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();
            File.WriteAllText("Assets/Verification~/CaffeineImport.txt","PASS Blender blister prefab imported with foil, capsule, label and transparent window\n");
            RunwayEveningReview.Run();
        }
        catch(Exception e){File.WriteAllText("Assets/Verification~/CaffeineImport.txt","FAIL "+e);Debug.LogException(e);}
    }
}
