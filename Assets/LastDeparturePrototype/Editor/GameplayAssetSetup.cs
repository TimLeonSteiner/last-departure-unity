using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;
public static class GameplayAssetSetup
{
    const string Root="Assets/LastDeparturePrototype/ThirdParty";
    public static void Probe()
    {
        var report=new StringBuilder();
        foreach(var folder in new[]{"Cultist","Hammer","ammo-healthbox"})foreach(var path in Directory.GetFiles(Root+"/"+folder,"*.fbx",SearchOption.AllDirectories))
        {
            string asset=path.Replace('\\','/');var source=AssetDatabase.LoadAssetAtPath<GameObject>(asset);if(source==null)continue;
            var obj=UnityEngine.Object.Instantiate(source);report.AppendLine(asset);
            foreach(var r in obj.GetComponentsInChildren<Renderer>())report.AppendLine("RENDER "+r.name+" "+r.bounds+" mats="+string.Join(",",r.sharedMaterials.Select(m=>m==null?"NULL":m.name)));
            var animator=obj.GetComponentInChildren<Animator>();report.AppendLine("RIG "+animator+" avatar="+(animator!=null?animator.avatar:null));
            report.AppendLine("CLIPS "+string.Join(",",AssetDatabase.LoadAllAssetsAtPath(asset).OfType<AnimationClip>().Select(c=>c.name+" "+c.length+" human="+c.humanMotion)));
            UnityEngine.Object.DestroyImmediate(obj);
        }
        File.WriteAllText("Assets/LastDeparturePrototype/GameplayAssetProbe.txt",report.ToString());
    }
    public static void Setup()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        game.playerSkinModels=new[]{Player("cultist-armed"),Player("extra")};
        game.hammerToolModel=Hammer(game);game.ammoBoxModel=Resources.Load<GameObject>("OriginalPickups/AmmoBox")??Supply(false);game.healthBoxModel=Supply(true);
        EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);AssetDatabase.SaveAssets();Probe();
    }
    static GameObject Player(string variant)
    {
        string folder=Root+"/Cultist"+(variant=="extra"?"":"/"+variant);
        string path=Directory.GetFiles(folder+"/source","*.fbx").Single().Replace('\\','/');
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.optimizeGameObjects=false;importer.SaveAndReimport();
        var wrapper=new GameObject("Cultist "+variant);var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),wrapper.transform);source.name="Authored body";
        try
        {
            var animator=source.GetComponentInChildren<Animator>();if(animator==null||animator.avatar==null||!animator.avatar.isValid||!animator.avatar.isHuman)throw new Exception("Cultist humanoid avatar invalid "+variant);
            foreach(var renderer in source.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>Textured(folder,m!=null?m.name:"body")).ToArray();
            var pose=wrapper.AddComponent<HumanoidPlayerAvatar>();pose.rig=animator;
            var clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")&&c.humanMotion).ToArray();pose.idle=variant=="extra"?AssetDatabase.LoadAllAssetsAtPath(Root+"/Cultist/cultist-armed/source/cultist1.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__")&&c.humanMotion):clips.FirstOrDefault();
            pose.walk=FindHumanClip("Z_Walk_InPlace");pose.run=pose.walk;pose.death=FindHumanClip("Z_FallingBack");
            pose.Tick(0,0,0,false);
            Bounds bounds=BoundsOf(source);source.transform.localScale*=1.8f/bounds.size.y;bounds=BoundsOf(source);source.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            foreach(var collider in source.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
            return PrefabUtility.SaveAsPrefabAsset(wrapper,folder+"/Player.prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(wrapper);}
    }
    static AnimationClip FindHumanClip(string name)=>AssetDatabase.FindAssets(name+" t:AnimationClip").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<AnimationClip>).First(c=>c!=null&&c.name==name&&c.humanMotion);
    static Bounds BoundsOf(GameObject obj){var renderers=obj.GetComponentsInChildren<Renderer>();if(renderers.Length==0)throw new Exception("No renderers "+obj.name);var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;}
    static Material Textured(string folder,string original)
    {
        var files=Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Where(p=>p.EndsWith(".png",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".jpg",StringComparison.OrdinalIgnoreCase)).ToArray();
        string key=original.ToLowerInvariant().Contains("ammohealth")?"ammo&healthbox":original.ToLowerInvariant().Contains("head")?"cultisthead":original.ToLowerInvariant().Contains("mask")?"mask":original.ToLowerInvariant().Contains("smg")?"smglow":original.ToLowerInvariant().Contains("hammer")?"hammer":"body";
        string Pick(string suffix)=>files.FirstOrDefault(p=>Path.GetFileName(p).ToLowerInvariant().Contains(key)&&Path.GetFileName(p).ToLowerInvariant().Contains(suffix))?.Replace('\\','/');
        string color=Pick("color");if(color==null&&files.Length>0)color=files.FirstOrDefault(p=>Path.GetFileName(p).ToLowerInvariant().Contains("color"))?.Replace('\\','/');
        string path=folder+"/"+key+"-Playable.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);bool create=material==null;if(create)material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.shader=Shader.Find("Universal Render Pipeline/Lit");material.SetColor("_BaseColor",Color.white);
        if(color!=null)material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(color));
        string normal=Pick("normal");if(normal!=null){var ni=AssetImporter.GetAtPath(normal) as TextureImporter;ni.textureType=TextureImporterType.NormalMap;ni.SaveAndReimport();material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normal));material.EnableKeyword("_NORMALMAP");}
        material.SetFloat("_Smoothness",.25f);if(create)AssetDatabase.CreateAsset(material,path);else EditorUtility.SetDirty(material);return material;
    }
    static GameObject Supply(bool medical)
    {
        string folder=Root+"/ammo-healthbox",path=Directory.GetFiles(folder+"/source","*.fbx").Single().Replace('\\','/');
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;importer.SaveAndReimport();
        var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));var root=new GameObject(medical?"Health box":"Ammo box");
        try
        {
            var renderer=source.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name==(medical?"healthbox":"ammobox"));var filter=renderer.GetComponent<MeshFilter>();var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);
            var bounds=renderer.bounds;float scale=(medical?.4f:.65f)/Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            mesh.vertices=mesh.vertices.Select(v=>(filter.transform.TransformPoint(v)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z))*scale).ToArray();
            if(filter.transform.localToWorldMatrix.determinant<0){var tris=mesh.triangles;for(int i=0;i<tris.Length;i+=3){int t=tris[i+1];tris[i+1]=tris[i+2];tris[i+2]=t;}mesh.triangles=tris;}
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();string meshPath=folder+(medical?"/Health":"/Ammo")+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(existing==null)AssetDatabase.CreateAsset(mesh,meshPath);else{EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}
            root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=Textured(folder,"ammohealth");
            return PrefabUtility.SaveAsPrefabAsset(root,folder+(medical?"/HealthBox":"/AmmoBox")+".prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(root);}
    }
    static GameObject Hammer(YardSurvival game)
    {
        var rig=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AnimatedArsenalImport.Root + "/knife-animated/Playable.prefab"));rig.name="Hammer with original weapon arms";
        try
        {
            foreach(var r in rig.GetComponentsInChildren<Renderer>())if(r.name=="knife")r.gameObject.SetActive(false);
            var hand=rig.GetComponentsInChildren<Transform>().First(t=>t.name=="R_palm");
            string path=Directory.GetFiles(Root+"/Hammer","*.fbx",SearchOption.AllDirectories).Single().Replace('\\','/');
            var hammer=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            foreach(var r in hammer.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>Textured(Root+"/Hammer","hammer")).ToArray();
            var bounds=BoundsOf(hammer);hammer.transform.localScale*=.42f/Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));bounds=BoundsOf(hammer);hammer.transform.position-=bounds.center;
            var grip=new GameObject("Hammer grip").transform;grip.SetParent(hand,false);grip.localRotation=Quaternion.Euler(0,0,90);grip.localPosition=new Vector3(0,0,.04f);hammer.transform.SetParent(grip,false);
            var anim=rig.GetComponent<AnimatedWeaponRig>();anim.fireDuration=.5f;anim.hipPosition=new Vector3(.14f,-.23f,.28f);anim.aimPosition=anim.hipPosition;
            HammerGripSetup.Fit(anim);
            return PrefabUtility.SaveAsPrefabAsset(rig,Root+"/Hammer/PlayableHammer.prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(rig);}
    }
}
