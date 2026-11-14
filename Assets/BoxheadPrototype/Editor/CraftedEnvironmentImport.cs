using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class CraftedEnvironmentImport
{
    static readonly Dictionary<string,Color> TerminalPalette=new Dictionary<string,Color>
    {
        {"Asphalt",new Color(.15f,.135f,.11f)},
        {"Steel",new Color(.16f,.24f,.19f)},
        {"Hull",new Color(.43f,.36f,.25f)},
        {"Rust",new Color(.36f,.13f,.045f)},
        {"Paint",new Color(.58f,.36f,.10f)},
        {"RackBody",new Color(.035f,.052f,.043f)},
        {"RackPanel",new Color(.095f,.17f,.125f)},
        {"RackMetal",new Color(.32f,.30f,.25f)}
    };
    const string Folder="Assets/BoxheadPrototype/Resources/Spaceport/";
    const string Request="Assets/BoxheadPrototype/CraftedEnvironment.request";
    static CraftedEnvironmentImport(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}
        File.Delete(Request);
        try
        {
            Import("TerminalCraft","CraftedTerminalSet");
            if(File.Exists(Folder+"RunwayCraft.fbx"))Import("RunwayCraft","CraftedRunwaySet");
            AssetDatabase.SaveAssets();
            File.WriteAllText("Assets/Verification~/CraftedImport.txt","PASS Blender meshes imported with assigned materials and no colliders\n");
            RunwayEveningReview.Run();
        }
        catch(Exception error){File.WriteAllText("Assets/Verification~/CraftedImport.txt","FAIL "+error);Debug.LogException(error);}
    }
    static void Import(string file,string prefab)
    {
        string path=Folder+file+".fbx";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;
        importer.addCollider=false;importer.isReadable=false;importer.SaveAndReimport();
        var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        try
        {
            instance.name=prefab;
            foreach(var renderer in instance.GetComponentsInChildren<MeshRenderer>())
            {
                string key=renderer.name.Replace("Spaceport_","");
                string materialKey=key=="AmberGlow"||key=="TealGlow"?"Terminal"+key:key;
                var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+materialKey+".mat");
                if(key=="EmergencyLens")
                {
                    if(material==null){material=new Material(Shader.Find("Dustline/Spaceport Cel"));AssetDatabase.CreateAsset(material,Folder+key+".mat");}
                    material.SetColor("_BaseColor",new Color(1,.012f,.008f));material.SetFloat("_Glow",2.5f);
                    material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.6f);EditorUtility.SetDirty(material);
                }
                if(material==null)throw new Exception("Missing crafted material: "+key);
                if(prefab=="CraftedTerminalSet"&&TerminalPalette.TryGetValue(key,out var tint))
                {
                    string localPath=Folder+"CraftedTerminal_"+key+".mat";
                    var local=AssetDatabase.LoadAssetAtPath<Material>(localPath);
                    if(local==null){local=new Material(material);AssetDatabase.CreateAsset(local,localPath);}
                    local.SetColor("_BaseColor",tint);EditorUtility.SetDirty(local);material=local;
                }
                renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                renderer.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
            }
            PrefabUtility.SaveAsPrefabAsset(instance,Folder+prefab+".prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(instance);}
    }
}
