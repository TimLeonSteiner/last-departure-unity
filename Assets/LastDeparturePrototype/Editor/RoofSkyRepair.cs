using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class RoofSkyRepair
{
    const string Folder="Assets/LastDeparturePrototype/Resources/Spaceport/RestoredRoof/";
    const string Source="Assets/LastDeparturePrototype/RestorationSource/TerminalBeforeCollapse.fbx";
    const string Phase="RoofSkyRepair.Phase";
    static RoofSkyRepair(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        const string request="Assets/Verification~/RoofSkyRepair.request";
        if(File.Exists(request)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {File.Delete(request);SessionState.SetInt(Phase,1);}
        int phase=SessionState.GetInt(Phase,0);if(phase==0)return;
        EditorApplication.isPaused=false;Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if(phase==1&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {Build();SessionState.SetInt(Phase,2);EditorApplication.isPlaying=true;}
            else if(phase==2&&EditorApplication.isPlaying&&Time.unscaledTime>4)
            {SessionState.SetInt(Phase,0);Review();EditorApplication.isPlaying=false;}
        }
        catch(Exception e)
        {SessionState.SetInt(Phase,0);File.WriteAllText("Assets/Verification~/RoofSkyRepair.txt","FAIL "+e);Debug.LogException(e);EditorApplication.isPlaying=false;}
    }

    static bool Roof(Vector3 p)=>p.y>=7.05f;
    public static void Build()
    {
        AssetDatabase.ImportAsset(Source,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(Source);
        importer.isReadable=true;importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
        var original=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
        var terminal=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LastDeparturePrototype/Resources/Spaceport/MeshyRackTerminalSet.prefab"));
        var materials=new Dictionary<string,Material[]>();
        int removed=0,restored=0,number=0;
        try
        {
            foreach(var filter in terminal.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();materials[filter.name]=renderer.sharedMaterials;
                var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);var vertices=mesh.vertices;
                var matrix=terminal.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                bool changed=false;
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var kept=new List<int>();var triangles=mesh.GetTriangles(sub);
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        bool roof=Roof(matrix.MultiplyPoint3x4(vertices[triangles[i]]))&&Roof(matrix.MultiplyPoint3x4(vertices[triangles[i+1]]))&&Roof(matrix.MultiplyPoint3x4(vertices[triangles[i+2]]));
                        if(roof){removed++;changed=true;}else kept.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});
                    }
                    mesh.SetTriangles(kept,sub);
                }
                if(changed){mesh.RecalculateBounds();filter.sharedMesh=Save(mesh,"RetainedInterior"+number++);}
                else UnityEngine.Object.DestroyImmediate(mesh);
                renderer.allowOcclusionWhenDynamic=false;
            }
            var group=new GameObject("Restored original ceiling").transform;group.SetParent(terminal.transform,false);
            number=0;
            foreach(var filter in original.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);var vertices=mesh.vertices;
                var matrix=filter.transform.localToWorldMatrix;
                for(int i=0;i<vertices.Length;i++)vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);
                mesh.vertices=vertices;int count=0;
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var kept=new List<int>();var triangles=mesh.GetTriangles(sub);
                    for(int i=0;i<triangles.Length;i+=3)
                        if(Roof(vertices[triangles[i]])&&Roof(vertices[triangles[i+1]])&&Roof(vertices[triangles[i+2]]))
                        {kept.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});count++;}
                    mesh.SetTriangles(kept,sub);
                }
                if(count==0){UnityEngine.Object.DestroyImmediate(mesh);continue;}
                if(!materials.TryGetValue(filter.name,out var slots))throw new Exception("Missing original roof material: "+filter.name);
                mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                var item=new GameObject(filter.name);item.transform.SetParent(group,false);
                item.AddComponent<MeshFilter>().sharedMesh=Save(mesh,"OriginalCeiling"+number++);
                var renderer=item.AddComponent<MeshRenderer>();renderer.sharedMaterials=slots;renderer.allowOcclusionWhenDynamic=false;
                restored+=count;
            }
            if(restored<1000||removed<1000)throw new Exception("Roof recovery did not find expected geometry");
            terminal.name="RestoredRoofTerminalSet";
            PrefabUtility.SaveAsPrefabAsset(terminal,"Assets/LastDeparturePrototype/Resources/Spaceport/RestoredRoofTerminalSet.prefab");
            AssetDatabase.SaveAssets();
            File.WriteAllText("Assets/Verification~/RoofSkyBuild.txt","Recovered pre-collapse ceiling from November 4 backup. Removed "+removed+" damaged roof triangles; restored "+restored+" original triangles. Preserved current racks/interior.\n");
        }
        finally{UnityEngine.Object.DestroyImmediate(original);UnityEngine.Object.DestroyImmediate(terminal);}
    }
    static Mesh Save(Mesh mesh,string name)
    {
        string path=Folder+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);return saved;
    }
    static void Review()
    {
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        var hall=game.transform.Find("Spaceport terminal");var roof=hall.Find("Restored original ceiling");
        if(roof==null||hall.Find("Textured server racks").childCount!=48)throw new Exception("Restored roof or racks missing");
        if(ShaderUtil.ShaderHasError(RenderSettings.skybox.shader))throw new Exception("Sky shader failed");
        // Raycast actual visible roof meshes, not just the old gameplay blockers.
        var probes=new List<MeshCollider>();
        foreach(var filter in roof.GetComponentsInChildren<MeshFilter>())probes.Add(filter.gameObject.AddComponent<MeshCollider>());
        foreach(var probe in probes)probe.sharedMesh=probe.GetComponent<MeshFilter>().sharedMesh;
        Physics.SyncTransforms();int hits=0;
        for(int x=-12;x<=12;x+=3)for(int z=9;z<=21;z+=3)
        {
            var ray=new Ray(new Vector3(x,12,z),Vector3.down);
            foreach(var probe in probes)if(probe.Raycast(ray,out var hit,5)){hits++;break;}
        }
        foreach(var probe in probes)UnityEngine.Object.DestroyImmediate(probe);
        if(hits<37)throw new Exception("North roof still missing: "+hits+"/45 rays");
        string warehouse=game.RunWarehouseChecks();
        Capture(new Vector3(-13,3,-13),new Vector3(0,8,9),"RepairedRoof-InGame");
        var light=new GameObject("Temporary roof inspection light").AddComponent<Light>();
        try
        {
            light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(60,-25,0);
            Capture(new Vector3(32,39,-32),new Vector3(0,8,0),"RepairedRoof-Above");
            light.transform.rotation=Quaternion.Euler(-60,-25,0);light.intensity=.9f;
            Capture(new Vector3(-13,3,-13),new Vector3(0,8,9),"RepairedRoof-Inspection");
        }
        finally{UnityEngine.Object.DestroyImmediate(light.gameObject);}
        for(int i=0;i<4;i++)
        {
            var direction=Quaternion.Euler(0,i*90,0)*new Vector3(0,.5f,1);
            Capture(new Vector3(0,15,0),new Vector3(0,15,0)+direction,"Sky-AllDirections-"+i,true);
        }
        Capture(new Vector3(0,15,0),new Vector3(.001f,16,0),"Sky-Zenith",true);
        File.WriteAllText("Assets/Verification~/RoofSkyRepair.txt","PASS original pre-collapse roof restored; north roof mesh coverage "+hits+"/45 rays; 48 textured racks preserved.\nPASS sky shader compiled; full hemisphere screenshots captured in four directions and zenith.\n"+warehouse);
    }
    static void Capture(Vector3 position,Vector3 target,string name,bool skyOnly=false)
    {
        var camera=new GameObject("Roof and sky inspection").AddComponent<Camera>();
        camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=80;camera.farClipPlane=800;
        camera.useOcclusionCulling=false;camera.cullingMask=skyOnly?0:~(1<<30);camera.clearFlags=CameraClearFlags.Skybox;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.FastApproximateAntialiasing;
        var rt=new RenderTexture(1440,900,24);var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try{rt.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();File.WriteAllBytes("Assets/Verification~/"+name+".png",pixels.EncodeToPNG());}
        finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(camera.gameObject);}
    }
}
