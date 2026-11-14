using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class WeatherOpticsSetup
{
    const string Root = "Assets/BoxheadPrototype/Resources/";
    static WeatherOpticsSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Assets/BoxheadPrototype/WeatherOptics.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try
        {
            var rain = AssetDatabase.LoadAssetAtPath<Material>(Root + "NightRain.mat");
            if (rain == null) { rain = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit")); AssetDatabase.CreateAsset(rain, Root + "NightRain.mat"); }
            var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/SourceFiles/VFX/RaindropMaterial.mat");
            rain.SetTexture("_BaseMap", original.GetTexture("_BaseMap"));
            rain.SetColor("_BaseColor", new Color(.65f,.65f,.65f,1));
            rain.SetColor("_Color", new Color(.65f,.65f,.65f,1));
            rain.SetColor("_EmissionColor", new Color(.045f,.045f,.045f));
            rain.SetFloat("_Surface",1); rain.SetFloat("_Blend",0); rain.SetFloat("_SrcBlend",5); rain.SetFloat("_DstBlend",10);
            rain.SetFloat("_SrcBlendAlpha",1); rain.SetFloat("_DstBlendAlpha",10); rain.SetFloat("_ZWrite",0); rain.SetFloat("_Cull",0);
            rain.SetFloat("_Metallic",0); rain.SetFloat("_Smoothness",.25f); rain.SetFloat("_ReceiveShadows",1);
            rain.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); rain.EnableKeyword("_EMISSION"); rain.DisableKeyword("_RECEIVE_SHADOWS_OFF");
            rain.SetOverrideTag("RenderType","Transparent"); rain.renderQueue=3000;
            EditorUtility.SetDirty(rain); AssetDatabase.SaveAssetIfDirty(rain);
            var led=AssetDatabase.LoadAssetAtPath<Material>(Root+"NightEntranceLED.mat");
            led.EnableKeyword("_EMISSION"); led.SetColor("_EmissionColor",new Color(1.7f,2.25f,2));
            EditorUtility.SetDirty(led); AssetDatabase.SaveAssetIfDirty(led);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root+"NightOptics.asset");
            if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Root+"NightOptics.asset");}
            if(!profile.TryGet<Bloom>(out var bloom)){bloom=profile.Add<Bloom>();AssetDatabase.AddObjectToAsset(bloom,profile);}
            bloom.threshold.Override(1.3f); bloom.intensity.Override(.18f); bloom.scatter.Override(.4f); bloom.highQualityFiltering.Override(false);
            EditorUtility.SetDirty(bloom);EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
            var cookie=new Texture2D(64,64,TextureFormat.RGB24,false,true);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){
                float radius=new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f).magnitude;
                float gain=1-Mathf.SmoothStep(.25f,1,radius);cookie.SetPixel(x,y,new Color(gain,gain,gain));
            }
            cookie.Apply(); File.WriteAllBytes(Root+"TorchFalloff.png",cookie.EncodeToPNG());UnityEngine.Object.DestroyImmediate(cookie);
            AssetDatabase.ImportAsset(Root+"TorchFalloff.png",ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(Root+"TorchFalloff.png");
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=false;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            DepotNightLighting.EnsureGlow(game.transform);DepotNightLighting.Warehouse(game,game.transform.Find("Warehouse yard/Warehouse hall"));
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            File.WriteAllText("Assets/BoxheadPrototype/WeatherOpticsSetup.txt","PASS "+DateTime.Now.ToString("s")+": neutral URP lit rain/shadows/depth, localized HDR entrance bloom, smooth torch cookie; scene saved. Runtime visual verification pending.");
        }
        catch(Exception e){File.WriteAllText("Assets/BoxheadPrototype/WeatherOpticsSetup.txt","FAIL "+e);Debug.LogException(e);}
    }
}
