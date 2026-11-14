using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class FacilityCirculationReview
{
    const string Request="Assets/Verification~/FacilityCirculation.request", Pending="D27.FacilityCirculationReview";
    static double ready;
    static FacilityCirculationReview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Request))
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}
            File.Delete(Request);SessionState.SetBool(Pending,true);ready=EditorApplication.timeSinceStartup+8;EditorApplication.isPlaying=true;
        }
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<ready)return;
        SessionState.SetBool(Pending,false);
        try
        {
            var network=UnityEngine.Object.FindAnyObjectByType<FacilityCirculation>();
            if(network==null)throw new Exception("Network missing");
            if(network.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Network obstructs movement");
            if(network.GetComponentsInChildren<MeshRenderer>().Length!=3)throw new Exception("Mesh batching regressed");
            var shader=Shader.Find("Dustline/Facility Vessel");
            if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Vessel shader error");
            int triangles=0;
            foreach(var filter in network.GetComponentsInChildren<MeshFilter>())
            {
                triangles+=filter.sharedMesh.triangles.Length/3;
                foreach(var p in filter.sharedMesh.vertices)if(float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.z))throw new Exception("Invalid mesh");
            }
            SpaceportAssetBuild.Capture(new Vector3(4,4,-15),new Vector3(0,4,0),"Organic-Takeover-Hall");
            SpaceportAssetBuild.Capture(new Vector3(9,2.6f,-7),new Vector3(11.2f,1.9f,-9.4f),"Organic-Takeover-Racks");
            // Neutral inspection light is capture-only, so surface colour can be judged independently of red room lamps.
            var inspection=new GameObject("Review-only neutral inspection light");
            var light=inspection.AddComponent<Light>();light.type=LightType.Spot;light.color=new Color(.85f,.91f,1);light.intensity=7;light.range=12;light.spotAngle=65;
            inspection.transform.position=new Vector3(9,3,-6);
            inspection.transform.LookAt(new Vector3(11.2f,1.9f,-9.4f));
            SpaceportAssetBuild.Capture(new Vector3(9,2.6f,-7),new Vector3(11.2f,1.9f,-9.4f),"Organic-Takeover-Surface-Inspection");
            inspection.transform.position=new Vector3(1.7f,4.4f,-3.5f);inspection.transform.LookAt(new Vector3(0,4.3f,0));
            SpaceportAssetBuild.Capture(new Vector3(1.7f,4.4f,-3.5f),new Vector3(0,4.3f,0),"Organic-Takeover-Heart-Connections");
            UnityEngine.Object.DestroyImmediate(inspection);
            File.WriteAllText("Assets/Verification~/FacilityCirculationChecks.txt",$"PASS: {network.BranchCount} branches, {network.MembraneCount} membranes, {network.JunctionCount} junctions; {triangles} triangles in 3 meshes; no colliders; valid vertices; shader compiled.\n");
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            File.AppendAllText("Assets/Verification~/FacilityCirculationChecks.txt",game.RunCorePresentationChecks(false));
        }
        catch(Exception e){File.WriteAllText("Assets/Verification~/FacilityCirculationChecks.txt","FAIL "+e);Debug.LogException(e);}
        finally{EditorApplication.isPlaying=false;}
    }
}
