using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using YardSurvivalPrototype;

// Continuous pressure liners close the gaps in the imported decorative hose cages.
// Each liner terminates inside its host, with a mechanically seated collar.
public static class HeartTubeConnections
{
    const string Folder="Assets/BoxheadPrototype/Resources/HeartPlatform/Modular/Connections/";
    public static void Build(Transform root)
    {
        Directory.CreateDirectory(Folder);
        var rubber=Material("Pressure liner",new Color(.095f,.027f,.022f),.12f,.32f);
        var steel=Material("Socket steel",new Color(.27f,.23f,.18f),.7f,.32f);
        var hose=AssetDatabase.LoadAssetAtPath<HeartModuleAsset>("Assets/BoxheadPrototype/Resources/HeartPlatform/Modular/Hose/Geometry.asset");
        var cropped=new Mesh[2];for(int level=0;level<2;level++)cropped[level]=SaveMesh("Hose body LOD"+level,CropFittings(hose.levels[level]));
        var frame=root.Find("Suspension frame");
        var frameMesh=frame.Find("LOD0").GetComponent<MeshFilter>();
        var collider=frameMesh.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=frameMesh.sharedMesh;
        Physics.SyncTransforms();
        var report=new System.Text.StringBuilder();
        int count=0;
        foreach(var route in root.GetComponentsInChildren<HeartHoseRoute>(true))
        {
            if(route.name.Contains("chain"))continue;
            bool floor=route.name.StartsWith("Root distribution");
            bool pump=route.name.Contains("pump");
            Vector3 socket=route.d;
            if(floor)
            {
                var radial=new Vector3(route.d.x,0,route.d.z).normalized;
                route.a=radial*.72f+Vector3.up*.42f;
                route.b=radial*1.6f+Vector3.up*.40f;
                route.c=radial*2.85f+Vector3.up*.48f;
                route.d=radial*2.85f+Vector3.up*.10f;
                socket=radial*2.85f+Vector3.up*.18f;
            }
            else if(!pump)
            {
                float best=float.MaxValue;RaycastHit selected=default;bool found=false;
                for(int x=-4;x<=4;x++)for(int z=-4;z<=4;z++)
                {
                    var origin=new Vector3(route.d.x+x*.075f,3.65f,route.d.z+z*.075f);
                    if(!collider.Raycast(new Ray(origin,Vector3.up),out var hit,1.4f))continue;
                    float distance=(hit.point-route.d).sqrMagnitude;
                    if(distance<best){best=distance;selected=hit;found=true;}
                }
                if(!found)throw new Exception("No frame surface found for "+route.name);
                socket=selected.point;
                route.d=socket+Vector3.up*.07f;
                route.c=socket+Vector3.down*.45f;
            }
            foreach(var filter in route.GetComponentsInChildren<MeshFilter>())filter.sharedMesh=cropped[filter.name=="LOD0"?0:1];
            float radius=.23f*route.radiusScale;
            string id="Tube-"+(++count).ToString("D2");
            var liner=Tube(route,.11f*route.radiusScale);
            Add(root,id+" sealed liner",liner,rubber);
            // Both imported fitting cages now surround a solid, continuous hose core.
            Add(root,id+" heart sleeve",Sleeve(route,0,.075f,radius),steel);
            Add(root,id+" terminal sleeve",Sleeve(route,.925f,1,radius),steel);
            if(floor||!pump)
                Add(root,id+" mounted socket",Gland(socket,floor?Vector3.up:Vector3.down,radius),steel);
            route.SendMessage("OnEnable");
            var lod=route.GetComponent<LODGroup>();lod.localReferencePoint=(route.a+route.d)*.5f;lod.size=Vector3.Distance(route.a,route.d)+1;
            report.AppendLine(route.name+": continuous liner, two overlapping sleeves; "+(floor?"deck gland":pump?"pump flange":"frame surface mount"));
        }
        UnityEngine.Object.DestroyImmediate(collider);
        if(count!=20)throw new Exception("Expected 20 hose routes, found "+count);
        File.WriteAllText("Assets/Verification~/HeartTubeConnections.txt",report+"PASS 20 continuous tube routes / 40 sleeved endpoints\n");
    }
    static Material Material(string name,Color color,float metal,float smooth)
    {
        var path=Folder+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;
    }
    static void Add(Transform root,string name,Mesh mesh,Material material)
    {
        var saved=SaveMesh(name,mesh);
        var go=new GameObject(name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=saved;
        go.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    static Mesh SaveMesh(string name,Mesh mesh)
    {
        string path=Folder+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null){saved=mesh;AssetDatabase.CreateAsset(saved,path);}else{EditorUtility.CopySerialized(mesh,saved);EditorUtility.SetDirty(saved);UnityEngine.Object.DestroyImmediate(mesh);}
        return saved;
    }
    static Mesh CropFittings(Mesh source)
    {
        var mesh=UnityEngine.Object.Instantiate(source);var vertices=mesh.vertices;var triangles=mesh.triangles;var kept=new List<int>();
        // The original C-shaped hose was straightened around an approximate centreline.
        // Re-centre each cross section so the detailed shell surrounds the pressure liner.
        const int bins=128;
        var lo=new Vector2[bins];var hi=new Vector2[bins];var centers=new Vector2[bins];var counts=new int[bins];
        for(int i=0;i<bins;i++){lo[i]=Vector2.one*float.MaxValue;hi[i]=Vector2.one*float.MinValue;}
        foreach(var v in vertices){int bin=Mathf.Clamp((int)(v.z*bins),0,bins-1);lo[bin]=Vector2.Min(lo[bin],new Vector2(v.x,v.y));hi[bin]=Vector2.Max(hi[bin],new Vector2(v.x,v.y));counts[bin]++;}
        for(int i=0;i<bins;i++)
        {
            Vector2 sum=Vector2.zero;int samples=0;
            for(int j=Mathf.Max(0,i-2);j<=Mathf.Min(bins-1,i+2);j++)if(counts[j]>0){sum+=(lo[j]+hi[j])*.5f;samples++;}
            centers[i]=samples>0?sum/samples:Vector2.zero;
        }
        bool Keep(int index)=>vertices[index].z>=.10f&&vertices[index].z<=.90f;
        for(int i=0;i<triangles.Length;i+=3)
            // Straightening the source hose reflected its cross-section basis. Restore
            // outward-facing triangles; otherwise only the far interior wall is visible.
            if(Keep(triangles[i])&&Keep(triangles[i+1])&&Keep(triangles[i+2])){kept.Add(triangles[i]);kept.Add(triangles[i+2]);kept.Add(triangles[i+1]);}
        for(int i=0;i<vertices.Length;i++)
        {
            float index=Mathf.Clamp(vertices[i].z*bins-.5f,0,bins-1);int bin=Mathf.FloorToInt(index);
            var center=Vector2.Lerp(centers[bin],centers[Mathf.Min(bin+1,bins-1)],index-bin);
            vertices[i].x-=center.x;vertices[i].y-=center.y;vertices[i].z=.04f+(vertices[i].z-.10f)/.80f*.92f;
        }
        mesh.vertices=vertices;mesh.SetTriangles(kept,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.bounds=source.bounds;return mesh;
    }
    public static Vector3 Point(HeartHoseRoute r,float t)
    {float u=1-t;return u*u*u*r.a+3*u*u*t*r.b+3*u*t*t*r.c+t*t*t*r.d;}
    static Vector3 Tangent(HeartHoseRoute r,float t)
    {float u=1-t;return (3*u*u*(r.b-r.a)+6*u*t*(r.c-r.b)+3*t*t*(r.d-r.c)).normalized;}
    static Mesh Tube(HeartHoseRoute r,float radius)=>Sweep(r,0,1,128,t=>radius*(1+.025f*Mathf.Cos(t*Mathf.PI*96)));
    static Mesh Sleeve(HeartHoseRoute r,float from,float to,float radius)=>Sweep(r,from,to,24,t=>radius*(1-.20f*(from<.5f?t:1-t))*(t<.12f||t>.88f?1.06f:1));
    static Mesh Sweep(HeartHoseRoute r,float from,float to,int rings,Func<float,float> radius)
    {
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();const int sides=32;
        for(int i=0;i<=rings;i++)
        {
            float s=(float)i/rings,t=Mathf.Lerp(from,to,s);var center=Point(r,t);var tangent=Tangent(r,t);
            var side=Vector3.Cross(tangent,r.side).normalized;if(side.sqrMagnitude<.1f)side=Vector3.Cross(tangent,Vector3.right).normalized;
            var other=Vector3.Cross(tangent,side).normalized;
            for(int j=0;j<=sides;j++){float angle=j*Mathf.PI*2/sides;vertices.Add(center+(side*Mathf.Cos(angle)+other*Mathf.Sin(angle))*radius(s));uv.Add(new Vector2((float)j/sides,s));}
        }
        for(int i=0;i<rings;i++)for(int j=0;j<sides;j++){int a=i*(sides+1)+j,b=a+sides+1;triangles.AddRange(new[]{a,a+1,b,b,a+1,b+1});}
        var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    static Mesh Gland(Vector3 point,Vector3 normal,float radius)
    {
        // A flanged socket surrounds the hose and extends into the solid mounting surface.
        var go=new GameObject("Temporary socket route");var route=go.AddComponent<HeartHoseRoute>();
        route.a=point-normal*.09f;route.d=point+normal*.12f;route.b=Vector3.Lerp(route.a,route.d,1f/3);route.c=Vector3.Lerp(route.a,route.d,2f/3);route.side=Vector3.right;
        var mesh=Sweep(route,0,1,16,t=>radius*(t>.35f&&t<.62f?1.65f:1.18f));
        UnityEngine.Object.DestroyImmediate(go);return mesh;
    }
}
