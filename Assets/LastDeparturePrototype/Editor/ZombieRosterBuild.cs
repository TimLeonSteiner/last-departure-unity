using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class ZombieRosterBuild
{
    const string Folder = "Assets/LastDeparturePrototype/Resources/ZombieTypes";
    const string Review = "Assets/Verification~/ZombieRoster";
    static ZombieRosterBuild() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying || !File.Exists(Review+"/Build.request")) return;
        File.Delete(Review+"/Build.request");
        try { Build(); File.WriteAllText(Review+"/Result.txt", "PASS: four distinct persistent prefabs; unique materials, proportions and archetypes; six detachable body regions retained."); }
        catch (Exception e) { File.WriteAllText(Review+"/Result.txt", "FAIL: "+e); Debug.LogException(e); }
    }
    static void Build()
    {
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        string[] sources = { "ThirdParty/PlayableZombies/Modern", "ThirdParty/PlayableZombies/Pxltiger", "ThirdParty/PlayableZombies/AAB", "ThirdParty/PlayableZombies/FatZombie" };
        string[] names = { "01-Dockworker-Shambler", "02-Maintenance-Limper", "03-Pale-Twitcher", "04-Cargo-Brute" };
        Color[] tints = { new Color(.66f,.73f,.68f), new Color(.82f,.67f,.44f), new Color(.74f,.88f,.82f), new Color(.65f,.47f,.40f) };
        Vector3[] scales = { Vector3.one, new Vector3(.91f,.97f,.94f), new Vector3(.84f,1.08f,.87f), new Vector3(1.22f,1.16f,1.22f) };
        for (int type = 0; type < 4; type++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LastDeparturePrototype/"+sources[type]+".prefab");
            if (prefab == null) throw new Exception("Missing base model "+sources[type]);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.name = names[type]; instance.transform.localScale = Vector3.Scale(instance.transform.localScale,scales[type]);
                var archetype = instance.AddComponent<ZombieArchetype>(); archetype.kind = (ZombieKind)type;
                var zombie = instance.GetComponent<ImportedZombie>();
                if (zombie == null || zombie.parts.Length != 6 || zombie.walkClip == null || zombie.attackClip == null) throw new Exception("Incomplete combat rig: "+names[type]);
                var materials = new Dictionary<Material,Material>();
                foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int slot = 0; slot < slots.Length; slot++)
                    {
                        var source = slots[slot]; if (source == null) continue;
                        if (!materials.TryGetValue(source,out var material))
                        {
                            string path = Folder+"/"+names[type]+"-Material-"+materials.Count+".mat";
                            material = AssetDatabase.LoadAssetAtPath<Material>(path);
                            if (material == null) { material = new Material(source); AssetDatabase.CreateAsset(material,path); }
                            else EditorUtility.CopySerialized(source,material);
                            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",source.GetColor("_BaseColor")*tints[type]);
                            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness",type==2?.34f:.16f);
                            materials[source] = material; EditorUtility.SetDirty(material);
                        }
                        slots[slot] = material;
                    }
                    renderer.sharedMaterials = slots;
                }
                PrefabUtility.SaveAsPrefabAsset(instance,Folder+"/"+names[type]+".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
    }
    public static void CaptureLineup()
    {
        var roster = Resources.LoadAll<GameObject>("ZombieTypes");
        if (roster.Length != 4) throw new Exception("Four zombie types required");
        Array.Sort(roster,(a,b)=>string.CompareOrdinal(a.name,b.name));
        var root = new GameObject("Zombie roster review");
        var camera = new GameObject("Roster camera").AddComponent<Camera>(); camera.enabled=false;
        camera.transform.SetParent(root.transform); camera.transform.position=new Vector3(0,1.7f,1007);
        camera.transform.LookAt(new Vector3(0,1.1f,1000)); camera.orthographic=true; camera.orthographicSize=2.5f;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.045f,.055f,.065f); camera.cullingMask=1<<29;
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        bool fog=RenderSettings.fog; RenderSettings.fog=false;
        var rt=new RenderTexture(1600,900,24); rt.Create(); var old=RenderTexture.active;
        var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
        var bakedMeshes=new List<Mesh>();
        try
        {
            for(int i=0;i<4;i++)
            {
                var holder=new GameObject("Review actor").transform;holder.SetParent(root.transform);holder.position=new Vector3(3-i*2,0,1000);
                var instance=UnityEngine.Object.Instantiate(roster[i],holder);
                foreach(var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())skin.updateWhenOffscreen=true;
                var zombie=instance.GetComponent<ImportedZombie>(); zombie.ConfigureMotion(i+7);
                for(int frame=0;frame<40;frame++)zombie.TickMovement(.025f,.025f,false);
                foreach(var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh=new Mesh();skin.BakeMesh(mesh);bakedMeshes.Add(mesh);
                    var baked=new GameObject("Review pose");baked.transform.SetParent(root.transform,false);
                    baked.transform.position=skin.transform.position;baked.transform.rotation=skin.transform.rotation;baked.layer=29;
                    baked.AddComponent<MeshFilter>().sharedMesh=mesh;baked.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;
                }
                if(zombie.ArchetypeHealth!=(i==3?9:i==2?2:3))throw new Exception("Wrong archetype health");
                foreach(var item in instance.GetComponentsInChildren<Transform>(true))item.gameObject.layer=29;
                if(instance.GetComponentsInChildren<MeshRenderer>().Length<8)throw new Exception("Missing archetype costume details");
                var label=new GameObject("Label");label.transform.SetParent(root.transform);
                label.transform.position=new Vector3(3-i*2,-.3f,1000);label.transform.rotation=Quaternion.Euler(0,180,0);label.layer=29;
                var text=label.AddComponent<TextMesh>();text.text=new[]{"DOCKWORKER","LIMPER","TWITCHER","BRUTE"}[i];text.fontSize=48;text.characterSize=.03f;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;
            }
            for(int i=0;i<2;i++)
            {
                var lamp=new GameObject("Review light").AddComponent<Light>();lamp.transform.SetParent(root.transform);
                lamp.type=LightType.Directional;lamp.intensity=i==0?2.2f:.8f;lamp.color=i==0?new Color(1,.88f,.75f):new Color(.45f,.65f,1);
                lamp.transform.rotation=Quaternion.Euler(i==0?35:20,i==0?150:20,0);lamp.cullingMask=1<<29;
            }
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
            File.WriteAllBytes(Review+"/Lineup.png",pixels.EncodeToPNG());
            File.AppendAllText(Review+"/Result.txt","\nPASS: all four instantiated, walking poses sampled, archetype health validated, lineup captured.");
        }
        finally {RenderSettings.fog=fog;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(root);foreach(var mesh in bakedMeshes)UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
    }
}
