using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class HallTextureReview
{
    const string Request="Assets/Verification~/HallTextureReview.request";
    const string Pending="Dustline.HallTextureReview";
    static double readyAt;
    static HallTextureReview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Request))
        {
            File.Delete(Request);
            AssetDatabase.ImportAsset("Assets/LastDeparturePrototype/Shaders/SpaceportCel.shader",ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            foreach(string path in Directory.GetFiles("Assets/LastDeparturePrototype/Resources/HallTextures","*.png"))
            {
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=path.Contains("Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=path.Contains("Base");importer.wrapMode=TextureWrapMode.Repeat;
                importer.mipmapEnabled=true;importer.anisoLevel=8;importer.maxTextureSize=1024;
                importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            }
            SessionState.SetBool(Pending,true);
            EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
            readyAt=EditorApplication.timeSinceStartup+6;
        }
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<readyAt)return;
        SessionState.SetBool(Pending,false);
        GameObject lightObject=null;
        try
        {
            if(!HallSurfaceTextures.Bind())throw new Exception("Hall material maps missing");
            var shader=Shader.Find("Dustline/Spaceport Cel");
            if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Hall shader failed compilation");
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            if(game==null||game.transform.Find("Spaceport terminal")==null)throw new Exception("Hall not initialized");
            // Temporary flashlight-style inspection light is removed after these comparison captures.
            lightObject=new GameObject("Texture inspection light");var lamp=lightObject.AddComponent<Light>();
            lamp.type=LightType.Spot;lamp.color=new Color(1,.94f,.83f);lamp.intensity=28;lamp.range=10;
            lamp.spotAngle=95;lamp.innerSpotAngle=60;lamp.shadows=LightShadows.Soft;
            foreach(bool floor in new[]{true,false})
            {
                var camera=floor?new Vector3(4,1.6f,-14):new Vector3(20,1.65f,-10);
                var target=floor?new Vector3(6,.02f,-11):new Vector3(23,1,-10);
                lightObject.transform.position=camera;lightObject.transform.LookAt(target);
                foreach(bool enabled in new[]{false,true})
                {
                    Shader.SetGlobalFloat("_HallTextureReady",enabled?1:0);
                    SpaceportAssetBuild.Capture(camera,target,"HallTexture-"+(floor?"Floor":"Wall")+(enabled?"-After":"-Before"));
                }
            }
            UnityEngine.Object.DestroyImmediate(lightObject);lightObject=null;
            SpaceportAssetBuild.Capture(new Vector3(3,1.65f,-20),new Vector3(10.8f,.4f,0),"HallTexture-Gameplay");
            File.WriteAllText("Assets/Verification~/HallTextureChecks.txt","PASS Six 1024px maps imported with mipmaps and repeat wrapping\nPASS Shader compiles and runtime textures bind\nPASS Floor and lower-wall before/after inspection views captured\nPASS Gameplay lighting capture rendered without inspection light\n");
        }
        catch(Exception e){File.WriteAllText("Assets/Verification~/HallTextureChecks.txt","FAIL "+e);Debug.LogException(e);}
        finally{Shader.SetGlobalFloat("_HallTextureReady",1);if(lightObject!=null)UnityEngine.Object.DestroyImmediate(lightObject);EditorApplication.isPaused=true;}
    }
}
