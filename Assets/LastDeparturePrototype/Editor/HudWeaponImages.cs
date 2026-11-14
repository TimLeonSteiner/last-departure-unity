using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HudWeaponImages
{
    const string Folder="Assets/LastDeparturePrototype/Resources/HudWeapons/";
    const string Request="Assets/Verification~/HudWeaponImagesV2.request";
    static HudWeaponImages(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
        File.Delete(Request);
        try
        {
            Directory.CreateDirectory(Folder);
            for(int i=0;i<AnimatedArsenalImport.Packages.Length;i++)
                if (YardSurvivalPrototype.WeaponCatalog.IsAvailable(i))
                    Render(AssetDatabase.LoadAssetAtPath<GameObject>(AnimatedArsenalImport.Root+"/"+AnimatedArsenalImport.Packages[i]+"/Playable.prefab"),i.ToString("00"),false);
            Render(Resources.Load<GameObject>("Spaceport/AnatomicalHeart"),"Heart",true);
            AssetDatabase.Refresh();
            File.WriteAllText("Assets/Verification~/HudWeaponImages.txt","PASS: active weapon images and heart image rendered from project models.");
        }
        catch(Exception e){File.WriteAllText("Assets/Verification~/HudWeaponImages.txt","FAIL: "+e);Debug.LogException(e);}
    }
    static void Render(GameObject prefab,string name,bool heart)
    {
        if(prefab==null)throw new Exception("Missing HUD model "+name);
        var root=Object.Instantiate(prefab);root.hideFlags=HideFlags.HideAndDontSave;
        root.transform.position=new Vector3(1000,1000,1000);
        foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
        foreach(var script in root.GetComponentsInChildren<MonoBehaviour>())script.enabled=false;
        foreach(var animator in root.GetComponentsInChildren<Animator>())animator.enabled=false;
        foreach(var r in root.GetComponentsInChildren<Renderer>())
            if(!heart && (r.name.ToLowerInvariant().Contains("arm") || r.sharedMaterials.All(m=>m!=null&&m.name.ToLowerInvariant().Contains("arm"))))r.enabled=false;
        Material heartMaterial=null,veinMaterial=null;
        if(heart){heartMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));heartMaterial.color=new Color(.64f,.055f,.035f);heartMaterial.SetFloat("_Smoothness",.25f);
            veinMaterial=new Material(heartMaterial);veinMaterial.color=new Color(.15f,.025f,.018f);
            foreach(var r in root.GetComponentsInChildren<Renderer>())r.sharedMaterial=r.name.Contains("Vein")?veinMaterial:heartMaterial;}
        var renderers=root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();var bounds=renderers[0].bounds;
        foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        var cameraObject=new GameObject("HUD image camera"){hideFlags=HideFlags.HideAndDontSave};
        var cam=cameraObject.AddComponent<Camera>();cam.enabled=false;cam.cullingMask=1<<31;
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;
        cam.orthographic=true;cam.nearClipPlane=.01f;cam.farClipPlane=100;
        int width=heart?192:384,height=heart?256:144;cam.aspect=width/(float)height;
        var direction=heart?new Vector3(0,.05f,-1):new Vector3(1,.06f,.06f);
        cam.transform.position=bounds.center+direction.normalized*Mathf.Max(3,bounds.size.magnitude*2);cam.transform.LookAt(bounds.center);
        float halfWidth=0,halfHeight=0;
        for(int i=0;i<8;i++)
        {
            Vector3 corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            var local=cam.transform.InverseTransformPoint(corner);halfWidth=Mathf.Max(halfWidth,Mathf.Abs(local.x));halfHeight=Mathf.Max(halfHeight,Mathf.Abs(local.y));
        }
        cam.orthographicSize=Mathf.Max(halfHeight,halfWidth/cam.aspect)*1.08f;
        var lamp=new GameObject("HUD image key"){hideFlags=HideFlags.HideAndDontSave};var light=lamp.AddComponent<Light>();
        light.type=LightType.Directional;light.intensity=3;light.cullingMask=1<<31;lamp.transform.rotation=Quaternion.Euler(25,-65,0);
        var fill=new GameObject("HUD image fill"){hideFlags=HideFlags.HideAndDontSave};var fillLight=fill.AddComponent<Light>();
        fillLight.type=LightType.Directional;fillLight.intensity=1.5f;fillLight.cullingMask=1<<31;fill.transform.rotation=Quaternion.Euler(-20,110,0);
        var iconMaterials=new System.Collections.Generic.List<Material>();
        if(!heart)foreach(var r in renderers)
        {
            var materials=r.sharedMaterials;
            for(int i=0;i<materials.Length;i++)if(materials[i]!=null){var mat=new Material(materials[i]);iconMaterials.Add(mat);materials[i]=mat;
                if(mat.HasProperty("_EmissionColor")){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",new Color(.65f,.65f,.65f));if(mat.HasProperty("_BaseMap"))mat.SetTexture("_EmissionMap",mat.GetTexture("_BaseMap"));}}
            r.sharedMaterials=materials;
        }
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.Create();
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
            int minX=width,minY=height,maxX=0,maxY=0;
            var pixels=texture.GetPixels32();
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(pixels[y*width+x].a>16){minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
            if(minX>=maxX||minY>=maxY)throw new Exception("Empty HUD render "+name);
            minX=Mathf.Max(0,minX-4);minY=Mathf.Max(0,minY-4);maxX=Mathf.Min(width-1,maxX+4);maxY=Mathf.Min(height-1,maxY+4);
            var framed=new Texture2D(maxX-minX+1,maxY-minY+1,TextureFormat.RGBA32,false);framed.SetPixels(texture.GetPixels(minX,minY,framed.width,framed.height));framed.Apply();
            string path=Folder+name+".png";File.WriteAllBytes(path,framed.EncodeToPNG());Object.DestroyImmediate(framed);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
        finally{RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);Object.DestroyImmediate(root);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(lamp);Object.DestroyImmediate(fill);foreach(var mat in iconMaterials)Object.DestroyImmediate(mat);if(heartMaterial!=null)Object.DestroyImmediate(heartMaterial);if(veinMaterial!=null)Object.DestroyImmediate(veinMaterial);}
    }
}
