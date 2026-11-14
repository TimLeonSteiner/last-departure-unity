using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;
public static class LuckyChestSetup
{
    const string Root="Assets/BoxheadPrototype/ThirdParty/LuckyChest";
    [MenuItem("Tools/Yard Survival/Install lucky chest and repair starter sights")]
    public static void Setup()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        string path=Root+"/source/TreasureChest.fbx";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        var root=new GameObject("Lucky chest");source.transform.SetParent(root.transform,false);
        try
        {
            foreach(var animator in source.GetComponentsInChildren<Animator>())animator.enabled=false;
            foreach(var collider in source.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
            var materials=new System.Collections.Generic.Dictionary<string,Material>();
            foreach(var r in source.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(old=>
            {
                string key=old.name.Replace(" (Instance)","");if(materials.TryGetValue(key,out var cached))return cached;
                string file=Root+"/"+key+"-URP.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(file);bool create=mat==null;
                if(create)mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                string Texture(string suffix)=>Directory.GetFiles(Root+"/textures").FirstOrDefault(p=>Path.GetFileNameWithoutExtension(p).Equals(key+"_"+suffix,StringComparison.OrdinalIgnoreCase))?.Replace('\\','/');
                string albedo=Texture("albedo"),normal=Texture("normal"),metal=Texture("metallic"),emission=Texture("emissive");
                if(albedo!=null)mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",.25f);
                if(normal!=null){var importer=(TextureImporter)AssetImporter.GetAtPath(normal);if(importer.textureType!=TextureImporterType.NormalMap){importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();}mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normal));mat.EnableKeyword("_NORMALMAP");}
                if(metal!=null){mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(metal));mat.EnableKeyword("_METALLICSPECGLOSSMAP");}
                if(emission!=null){mat.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(emission));mat.SetColor("_EmissionColor",new Color(.2f,.65f,.8f));mat.EnableKeyword("_EMISSION");}
                if(create)AssetDatabase.CreateAsset(mat,file);else EditorUtility.SetDirty(mat);materials[key]=mat;return mat;
            }).ToArray();
            var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
            var lid=source.GetComponentsInChildren<Transform>().First(t=>t.name=="Couvercle1");clip.SampleAnimation(source,0);var closed=lid.localRotation;
            float end=clip.length;for(float time=0;time<clip.length;time+=.1f){clip.SampleAnimation(source,time);if(Quaternion.Angle(closed,lid.localRotation)>175){end=time;break;}}
            clip.SampleAnimation(source,0);
            var visual=root.AddComponent<LuckyChestVisual>();visual.model=source;visual.opening=clip;visual.startTime=0;visual.endTime=end;
            game.luckyChestModel=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Playable.prefab");game.luckyChestCost=6;
            RepairStarterSights();
            foreach(var pair in new[]{(folder:"lmg-animated",id:0),(folder:"battle-rifle-animated",id:15)})
            {
                string prefab="Assets/BoxheadPrototype/ThirdParty/AnimatedArsenal/"+pair.folder+"/Playable.prefab";
                var obj=PrefabUtility.LoadPrefabContents(prefab);
                try{AnimatedArsenalImport.RepairLongGunSights(obj.GetComponent<AnimatedWeaponRig>(),pair.id);PrefabUtility.SaveAsPrefabAsset(obj,prefab);}
                finally{PrefabUtility.UnloadPrefabContents(obj);}
            }
            var yard=game.transform.Find("Warehouse yard");if(yard!=null)DepotModels.ExteriorVariation(game,yard);
            EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);AssetDatabase.SaveAssets();
            File.WriteAllText("Assets/BoxheadPrototype/LuckyChestImport.txt","PASS: textured animated chest assigned; original opening segment 0.."+end+"s played in .65s; price 6 scrap. Starter pistol sight coordinates repaired.\n");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    static void RepairStarterSights()
    {
        string path="Assets/BoxheadPrototype/ThirdParty/AnimatedArsenal/pistol-animated/Playable.prefab";
        var obj=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var rig=obj.GetComponent<AnimatedWeaponRig>();
            if(rig.aimPosition.magnitude>2)
            {
                var skin=rig.model.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name=="pistol");
                float scale=skin.transform.lossyScale.x;
                rig.rearSight/=scale;rig.frontSight/=scale;
                var rotation=Quaternion.FromToRotation((rig.frontSight-rig.rearSight).normalized,Vector3.forward);
                rig.aimEuler=rotation.eulerAngles;rig.aimPosition=Vector3.forward*.3f-rotation*rig.rearSight;
                if(rig.aimPosition.magnitude>1)throw new Exception("P9 sight repair outside camera range");
                PrefabUtility.SaveAsPrefabAsset(obj,path);
            }
        }
        finally{PrefabUtility.UnloadPrefabContents(obj);}
    }
    public static void Probe()
    {
        var path=Root+"/source/TreasureChest.fbx";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var obj=UnityEngine.Object.Instantiate(source);
        var report=new StringBuilder();
        foreach(var r in obj.GetComponentsInChildren<Renderer>())report.AppendLine(r.name+" bounds="+r.bounds+" mats="+string.Join(",",r.sharedMaterials.Select(m=>m.name)));
        foreach(var c in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())report.AppendLine("CLIP "+c.name+" seconds="+c.length);
        var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
        foreach(float time in new[]{0f,5f,10f,20f,40f,60f,78f})
        {
            clip.SampleAnimation(obj,time);
            foreach(var t in obj.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("Couvercle")||t.name=="Haut"))report.AppendLine("SAMPLE "+time+" "+t.name+" rot="+t.localEulerAngles+" pos="+t.localPosition);
        }
        foreach(var t in obj.GetComponentsInChildren<Transform>())report.AppendLine("NODE "+t.name+" scale="+t.localScale);
        UnityEngine.Object.DestroyImmediate(obj);
        var rig=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BoxheadPrototype/ThirdParty/AnimatedArsenal/pistol-animated/Playable.prefab")).GetComponent<AnimatedWeaponRig>();
        rig.Pose(0,false,1,1);
        foreach(var s in rig.model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh=new Mesh();s.BakeMesh(mesh,false);
            var p=mesh.vertices.Select(v=>s.transform.TransformPoint(v)).ToArray();
            report.AppendLine("P9 "+s.name+" scale="+s.transform.lossyScale+" rendered="+s.bounds+" bakedY="+p.Min(v=>v.y)+".."+p.Max(v=>v.y));
            UnityEngine.Object.DestroyImmediate(mesh);
        }
        UnityEngine.Object.DestroyImmediate(rig.gameObject);
        File.WriteAllText("Assets/BoxheadPrototype/LuckyChestProbe.txt",report.ToString());
    }
}
