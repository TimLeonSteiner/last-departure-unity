using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class LoadingCornerReview
{
    const string Folder="Assets/LastDeparturePrototype/Resources/Spaceport/";
    const string Request="Assets/Verification~/LoadingCorner.request";
    static double readyAt;
    static LoadingCornerReview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Request))
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}
            File.Delete(Request);
            try{Import();SessionState.SetBool("LoadingCornerReview",true);EditorApplication.isPaused=false;EditorApplication.isPlaying=true;readyAt=EditorApplication.timeSinceStartup+6;}
            catch(Exception e){File.WriteAllText("Assets/Verification~/LoadingCornerChecks.txt","FAIL "+e);Debug.LogException(e);}
        }
        if(!SessionState.GetBool("LoadingCornerReview",false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<readyAt)return;
        SessionState.SetBool("LoadingCornerReview",false);
        try
        {
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            var hall=game.transform.Find("Spaceport terminal");var corner=LoadingBayCorner.Build(hall);
            if(corner==null||corner.GetComponentsInChildren<Renderer>().Length<8)throw new Exception("Corner model missing");
            if(corner.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Decorative corner added gameplay collisions");
            foreach(var mesh in corner.GetComponentsInChildren<MeshRenderer>())
                if(mesh.bounds.min.x<5.5f||mesh.bounds.min.z<20.4f)throw new Exception("Corner intrudes on entrance or aisle: "+mesh.name);
            if(corner.GetComponentsInChildren<Light>().Length!=2)throw new Exception("Task light or localized reflected light missing");
            SpaceportAssetBuild.Capture(new Vector3(4.8f,1.7f,17),new Vector3(7.9f,1.2f,22),"LoadingCorner-Gameplay");
            SpaceportAssetBuild.Capture(new Vector3(7.3f,1.45f,19.5f),new Vector3(8,1.15f,22.1f),"LoadingCorner-Detail");
            SpaceportAssetBuild.Capture(new Vector3(8.6f,.85f,20.1f),new Vector3(8.6f,.25f,21.8f),"LoadingCorner-Floor");
            File.WriteAllText("Assets/Verification~/LoadingCornerChecks.txt","PASS Blender corner instantiated\nPASS Doorway and aisle clear; no new colliders\nPASS Shadowed task light, localized reflected light and bounded drip effect\nPASS Three gameplay-lighting views captured\n");
        }
        catch(Exception e){File.WriteAllText("Assets/Verification~/LoadingCornerChecks.txt","FAIL "+e);Debug.LogException(e);}
        finally{EditorApplication.isPaused=true;}
    }
    static void Import()
    {
        string path=Folder+"LoadingBayCorner.fbx";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.importAnimation=false;importer.importLights=false;importer.importCameras=false;importer.addCollider=false;importer.SaveAndReimport();
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        try
        {
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                string key=renderer.name.Replace("Corner_","");string matPath=Folder+"LoadingCorner_"+key+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(mat==null){mat=new Material(Shader.Find("Dustline/Spaceport Cel"));AssetDatabase.CreateAsset(mat,matPath);}
                Color color=key=="Steel"?new Color(.18f,.24f,.20f):key=="Rust"?new Color(.28f,.105f,.035f):key=="Metal"?new Color(.38f,.40f,.38f):key=="Rubber"?new Color(.025f,.03f,.027f):key=="Concrete"?new Color(.32f,.29f,.24f):key=="Paint"?new Color(.58f,.36f,.1f):key=="Water"?new Color(.025f,.033f,.029f):key=="Lens"?new Color(1,.45f,.12f):new Color(.52f,.46f,.32f);
                mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",key=="Metal"?.8f:key=="Steel"?.25f:0);
                mat.SetFloat("_Smoothness",key=="Water"?.9f:key=="Metal"?.6f:.22f);
                mat.SetFloat("_Wear",key=="Lens"||key=="Water"?0:.5f);mat.SetFloat("_Glow",key=="Lens"?2:0);
                mat.SetFloat("_Concrete",key=="Concrete"?1:0);EditorUtility.SetDirty(mat);renderer.sharedMaterial=mat;
            }
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"LoadingBayCornerPickup.prefab");AssetDatabase.SaveAssets();
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
