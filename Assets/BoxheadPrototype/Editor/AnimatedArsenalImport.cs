using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using YardSurvivalPrototype;

public static partial class AnimatedArsenalImport
{
    public const string Root = "Assets/BoxheadPrototype/ThirdParty/AnimatedArsenal";
    public static readonly string[] Packages = {"lmg-animated","minigun-animated","pistol-animated","shotgun-animated","grenadelauncher-animated","pitchfork-animated","sawnoffs-animated","crossbow-animated","dual-smgs","heavyrifle-animated","knife-animated","animated-pistol","animated-pistol (1)","heavy-pistol-animated","persuader-animated","battle-rifle-animated","sniper-animated"};
    // Published frame ranges where available. Older single-take packs are reviewed in the contact sheets.
    static readonly int[][] Ranges = {
        new[]{0,10,11,164,378,425,331,377},new[]{0,6,61,128,7,60,140,159},
        new[]{0,0,0,0,0,0,0,0},new[]{0,0,0,0,0,0,0,0},
        new[]{0,12,13,110,131,160,121,130},new[]{0,44,0,0,91,115,56,90},
        new[]{0,20,21,59,81,123,66,80},new[]{0,15,16,95,124,150,112,123},
        new[]{0,6,7,50,81,105,62,80},new[]{0,10,11,98,199,239,171,198},
        new[]{0,60,0,0,96,116,71,95},new[]{0,14,15,96,219,248,170,218},
        new[]{0,11,95,175,234,264,187,233},new[]{0,20,21,155,221,248,165,220},
        new[]{0,8,77,159,210,232,171,209},new[]{0,7,8,88,199,228,162,198},
        new[]{0,11,61,115,143,165,127,142}};
    static AnimationClip Find(AnimationClip[] clips,string suffix) => clips.FirstOrDefault(c=>c.name.EndsWith(suffix,StringComparison.OrdinalIgnoreCase));
    public static void Setup()
    {
        if(EditorApplication.isPlaying) throw new Exception("Stop Play first");
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        game.animatedWeaponModels=new GameObject[Packages.Length];
        for(int i=0;i<Packages.Length;i++) game.animatedWeaponModels[i]=Build(i);
        ZombieAudioSetup.Configure(game);
        WeaponAudioSetup.Configure(game);
        game.weaponModel = game.pistolModel = game.sniperModel = game.heavyModel = game.firstPersonArms = null;
        game.additionalWeaponModels = Array.Empty<GameObject>();
        game.turretModel=BuildTurret();
        EditorUtility.SetDirty(game); EditorSceneManager.MarkSceneDirty(game.gameObject.scene); EditorSceneManager.SaveScene(game.gameObject.scene); AssetDatabase.SaveAssets();
        File.WriteAllText("Assets/BoxheadPrototype/AnimatedArsenalImportResult.txt","PASS: 17 animated weapon prefabs and DAE sentry saved in Boxhead.");
    }
    public static void ConfigureAudio(YardSurvival game)
    {
        if(game.weaponSounds==null || game.weaponSounds.Length<8)return;
        var banks=new YardSurvival.WeaponSoundBank[Packages.Length];
        var families=game.weaponSounds;
        for(int i=0;i<Packages.Length;i++){
            var b=families[YardSurvival.ArsenalAudio[i]];
            banks[i]=new YardSurvival.WeaponSoundBank{weapon=WeaponCatalog.All[i].name,shot=b.shot,reload=b.reload,action=b.action,shotVariations=b.shotVariations,automaticShotVariations=b.automaticShotVariations};
            if(i==5 || i==10){ banks[i].shot=game.slideSound; banks[i].shotVariations=Array.Empty<AudioClip>(); banks[i].automaticShotVariations=Array.Empty<AudioClip>(); }
        }
        game.weaponSounds=banks;
    }
    static Material MaterialFor(string folder,string prefix)
    {
        var files=Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Where(p=>p.EndsWith(".png")||p.EndsWith(".jpg")||p.EndsWith(".jpeg")).ToArray();
        string Pick(string key)=>files.FirstOrDefault(p=>Path.GetFileName(p).StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(p).IndexOf(key,StringComparison.OrdinalIgnoreCase)>=0)?.Replace('\\','/');
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=prefix,color=Color.white};
        var color=Pick("color")??Pick("albedo"); if(color!=null)mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(color));
        string normal=Pick("normal"); if(normal!=null){var importer=(TextureImporter)AssetImporter.GetAtPath(normal);if(importer.textureType!=TextureImporterType.NormalMap){importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();}mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normal));mat.EnableKeyword("_NORMALMAP");}
        mat.SetFloat("_Metallic",prefix.StartsWith("arm")?0:.7f);mat.SetFloat("_Smoothness",.28f);
        string ao=Pick("ao"); if(ao!=null)mat.SetTexture("_OcclusionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(ao));
        string path=folder+"/"+prefix+"_URP.mat"; var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(existing!=null){EditorUtility.CopySerialized(mat,existing);UnityEngine.Object.DestroyImmediate(mat);mat=existing;}else AssetDatabase.CreateAsset(mat,path);
        return mat;
    }
    static GameObject Build(int index)
    {
        string folder=Root+"/"+Packages[index]; string path=Directory.GetFiles(folder,"*.fbx",SearchOption.AllDirectories).Single().Replace('\\','/');
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;importer.importAnimation=true;importer.animationType=ModelImporterAnimationType.Generic;importer.animationCompression=ModelImporterAnimationCompression.Off;importer.SaveAndReimport();
        var root=new GameObject(Packages[index]);var rig=root.AddComponent<AnimatedWeaponRig>();
        var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.transform);model.name=Path.GetFileNameWithoutExtension(path);rig.model=model;
        foreach(var anim in model.GetComponentsInChildren<Animator>())anim.enabled=false;
        var clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
        if(clips.Length==1){rig.fire=rig.reload=rig.idle=rig.ready=clips[0];var r=Ranges[index];rig.fireFrames=new Vector2(r[0],r[1]);rig.reloadFrames=new Vector2(r[2],r[3]);rig.idleFrames=new Vector2(r[4],r[5]);rig.readyFrames=new Vector2(r[6],r[7]);}
        else {rig.fire=Find(clips,"|Fire");rig.reload=Find(clips,"|EmptyClipReload")??Find(clips,"|ReloadOne");rig.idle=Find(clips,"|Idle");rig.ready=Find(clips,"|Unhide");rig.cycle=Find(clips,"|PostFire");rig.reloadStart=Find(clips,"|PriorToReload");rig.reloadEnd=Find(clips,"|ReloadLastOne");rig.shellReload=index==3;}
        if(WeaponCatalog.IsMelee(index))rig.fireDuration=WeaponCatalog.All[index].interval;
        if(index==16){rig.cycle=rig.fire;rig.cycleFrames=new Vector2(12,60);}
        var mats=new System.Collections.Generic.Dictionary<string,Material>();
        foreach(var renderer in model.GetComponentsInChildren<Renderer>()){
            var assigned=renderer.sharedMaterials;
            for(int j=0;j<assigned.Length;j++){
                string name=assigned[j]==null?"":assigned[j].name;
                string prefix=name.ToLowerInvariant().Contains("arm")?(index==10?"arm":index==12?"arms":index==13?"armhand":"arm"):name;
                if(name=="lens"||name=="scopereticle")prefix="sniper";
                if(!mats.ContainsKey(prefix))mats[prefix]=MaterialFor(folder,prefix);
                assigned[j]=mats[prefix];
            }renderer.sharedMaterials=assigned;
        }
        rig.hipPosition=index==1?new Vector3(.2f,-.461f,.68f):new Vector3(index==6||index==8?0:.14f,-.23f,.28f);
        rig.aimFireMotion=index==9?.15f:1;
        rig.aimPosition=new Vector3(0,index==16?-.17f:-.075f,.27f);
        root.transform.localScale=Vector3.one*.88f;
        rig.Pose(0,false,1,1);
        CalibrateSights(rig,index);
        var weapon=model.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name=="base"||r.name=="pistol"||r.name=="boomstick"||r.name=="pitchfork"||r.name=="knife"||r.name=="barrels");
        if(weapon!=null)rig.muzzle=root.transform.InverseTransformPoint(new Vector3(weapon.bounds.center.x,weapon.bounds.center.y,weapon.bounds.max.z));
        CalibrateMuzzle(rig,index);
        var asset=PrefabUtility.SaveAsPrefabAsset(root,folder+"/Playable.prefab");UnityEngine.Object.DestroyImmediate(root);return asset;
    }
    static GameObject BuildTurret()
    {
        string folder=Root+"/turret-01a";string path=Directory.GetFiles(folder,"*.dae",SearchOption.AllDirectories).Single().Replace('\\','/');
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;importer.SaveAndReimport();
        var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        var filter=source.GetComponentInChildren<MeshFilter>();var mesh=filter.sharedMesh;
        var vertices=mesh.vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();
        var bounds=source.GetComponentInChildren<Renderer>().bounds;float scale=1.5f/bounds.size.y;
        float width=Mathf.Min(1,1/(Mathf.Max(bounds.size.x,bounds.size.z)*scale));
        for(int i=0;i<vertices.Length;i++){vertices[i]=(vertices[i]-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z))*scale;vertices[i]=Vector3.Scale(vertices[i],new Vector3(width,1,width));}
        var root=new GameObject("Turret 01A");var head=new GameObject("TurretHead").transform;head.SetParent(root.transform,false);head.localPosition=Vector3.up*1.2f;
        var material=MaterialFor(folder,"material0_");
        var baseTriangles=new System.Collections.Generic.List<int>();var headTriangles=new System.Collections.Generic.List<int>();var triangles=mesh.triangles;
        if(filter.transform.localToWorldMatrix.determinant<0)for(int i=0;i<triangles.Length;i+=3){int swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
        for(int i=0;i<triangles.Length;i+=3){float y=(vertices[triangles[i]].y+vertices[triangles[i+1]].y+vertices[triangles[i+2]].y)/3;var list=y>.95f?headTriangles:baseTriangles;list.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});}
        void Part(string name,Transform parent,System.Collections.Generic.List<int> tris){var m=UnityEngine.Object.Instantiate(mesh);m.name=name;m.vertices=vertices.Select(v=>v-(parent==head?Vector3.up*1.2f:Vector3.zero)).ToArray();m.subMeshCount=1;m.triangles=tris.ToArray();m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();string p=folder+"/"+name+".asset";var cached=AssetDatabase.LoadAssetAtPath<Mesh>(p);if(cached==null)AssetDatabase.CreateAsset(m,p);else{EditorUtility.CopySerialized(m,cached);UnityEngine.Object.DestroyImmediate(m);m=cached;EditorUtility.SetDirty(m);}var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.AddComponent<MeshFilter>().sharedMesh=m;obj.AddComponent<MeshRenderer>().sharedMaterial=material;}
        Part("Stationary base",root.transform,baseTriangles);Part("Rotating gun",head,headTriangles);
        var asset=PrefabUtility.SaveAsPrefabAsset(root,folder+"/Sentry.prefab");UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(source);return asset;
    }
    public static void Inspect(){File.WriteAllText("Assets/BoxheadPrototype/ArsenalStatus.txt","Playing="+EditorApplication.isPlaying);}
}
