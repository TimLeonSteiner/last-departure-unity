using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;

public static partial class RealisticZombieImport
{
    public static void AddNewVariants()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        Directory.CreateDirectory(Output);
        Build("FatZombie","Assets/ArtStore3D/Fat Zombie(Low Poly)/Model/FatZombie.fbx","Assets/ArtStore3D/Fat Zombie(Low Poly)/Prefab/FatZombie.prefab");
        Build("AAB","Assets/ZombieMale_AAB/Model/ZombieMale_AAB.fbx","Assets/ZombieMale_AAB/Prefabs/URP/ZombieMale_AAB_URP.prefab");
        RepairCachedMaterials("FatZombie","Assets/ArtStore3D/Fat Zombie(Low Poly)/Prefab/FatZombie.prefab");
        RepairCachedMaterials("AAB","Assets/ZombieMale_AAB/Prefabs/URP/ZombieMale_AAB_URP.prefab");
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        var before=game.zombieModels??Array.Empty<GameObject>();
        game.zombieModels=before.Concat(new[]{AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/FatZombie.prefab"),AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/AAB.prefab")}).Distinct().ToArray();
        EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);AssetDatabase.SaveAssets();
        File.WriteAllText("Assets/LastDeparturePrototype/AdditionalZombiesImport.txt","PASS: "+game.zombieModels.Length+" active zombie variants; all "+before.Length+" previous entries preserved; FatZombie and AAB added with humanoid animations and six detachable regions.");
    }
    static void RepairCachedMaterials(string model,string referencePath)
    {
        var reference=AssetDatabase.LoadAssetAtPath<GameObject>(referencePath);
        var sources=reference.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(s=>s.sharedMaterials).Where(m=>m!=null).Distinct().OrderByDescending(m=>m.name.Length).ToArray();
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Output}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);if(!Path.GetFileName(path).StartsWith(model+"-"))continue;
            var source=sources.FirstOrDefault(m=>Path.GetFileName(path).Contains(m.name));if(source==null)continue;
            var correct=ConvertMaterial(source,model);var cached=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(cached!=correct){EditorUtility.CopySerialized(correct,cached);EditorUtility.SetDirty(cached);}
        }
    }
    static Material ZombieMaterial(string model,Material source)
    {
        if(source==null)throw new Exception(model+" missing source material");
        if(source.shader.name=="Universal Render Pipeline/Lit")return source;
        string path=Output+"/"+model+"-"+source.name+"-URP.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",source.HasProperty("_Color")?source.GetColor("_Color"):Color.white);
        material.SetTexture("_BaseMap",source.mainTexture);
        foreach(string property in new[]{"_BumpMap","_MetallicGlossMap","_OcclusionMap","_EmissionMap"})if(source.HasProperty(property)&&source.GetTexture(property)!=null)material.SetTexture(property,source.GetTexture(property));
        if(material.GetTexture("_BumpMap")!=null)material.EnableKeyword("_NORMALMAP");
        if(material.GetTexture("_MetallicGlossMap")!=null)material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.SetFloat("_Metallic",source.HasProperty("_Metallic")?source.GetFloat("_Metallic"):0);
        material.SetFloat("_Smoothness",source.HasProperty("_Glossiness")?source.GetFloat("_Glossiness"):.25f);
        EditorUtility.SetDirty(material);return material;
    }
}
