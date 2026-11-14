using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class ServerHallBuild
{
    const string Folder="Assets/BoxheadPrototype/Resources/Spaceport/ServerHall/";
    const string Prefab="Assets/BoxheadPrototype/Resources/Spaceport/ModularHallTerminalSet.prefab";
    static ServerHallBuild(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Assets/Verification~/ServerHallBuild.request";
        if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}
        File.Delete(request);
        try{Build();}catch(Exception e){File.WriteAllText("Assets/Verification~/ServerHallBuild.txt","FAIL "+e);Debug.LogException(e);}
    }
    static Mesh Save(Mesh mesh,string name)
    {
        string path=Folder+name+".asset";mesh.name=name;
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,saved);EditorUtility.SetDirty(saved);UnityEngine.Object.DestroyImmediate(mesh);return saved;
    }
    static Material Material(string name,Color color,float emission=0)
    {
        var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+name+".mat");
        if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,Folder+name+".mat");}
        mat.name="SolidHall_"+name;mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.23f);
        if(emission>0){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*emission);}
        EditorUtility.SetDirty(mat);return mat;
    }
    static void Box(Transform parent,string name,Vector3 p,Vector3 size,Material material)
    {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.localPosition=p;obj.transform.localScale=size;
        obj.GetComponent<MeshRenderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(obj.GetComponent<BoxCollider>());
    }
    public static void Build()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var steel=Material("Server service rails",new Color(.10f,.12f,.13f));
            var white=Material("Cool inspection lens",new Color(.55f,.72f,.85f),2);
            var walls=root.transform.Find("Hall wall panels");
            for(int side=0;side<4;side++)
            {
                var wall=walls.GetChild(side);var previous=wall.Find("Perimeter server cabinets");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
                var bank=new GameObject("Perimeter server cabinets").transform;bank.SetParent(wall,false);
                int index=0;
                foreach(float x in new[]{-17f,-13f,-9f,9f,13f,17f})
                {
                    string kind=index%3==0?"Cooling":index%3==1?"Compute":"Network";index++;
                    var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BoxheadPrototype/Resources/Spaceport/ServerRacks/"+kind+".prefab"),bank);
                    model.name=kind+" perimeter cabinet";model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.Euler(0,kind=="Cooling"?0:180,0);
                    var bounds=new Bounds();bool first=true;
                    foreach(var renderer in model.GetComponentsInChildren<Renderer>())
                    {if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
                    float scale=3.45f/bounds.size.y;model.transform.localScale*=scale;
                    // Recompute in bank coordinates so all four walls use the same floor registration.
                    var local=new Bounds();first=true;
                    foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
                    {
                        var b=filter.sharedMesh.bounds;
                        for(int corner=0;corner<8;corner++)
                        {var p=bank.InverseTransformPoint(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1))));if(first){local=new Bounds(p,Vector3.zero);first=false;}else local.Encapsulate(p);}
                    }
                    model.transform.localPosition=new Vector3(x-local.center.x,.04f-local.min.y,22.35f-local.center.z);
                    var collision=new GameObject("Server cabinet collision");collision.transform.SetParent(bank,false);collision.transform.localPosition=new Vector3(x,local.size.y/2,22.35f);collision.AddComponent<BoxCollider>().size=local.size;
                    Box(bank,"Server plinth",new Vector3(x,.08f,22.35f),new Vector3(local.size.x+.12f,.16f,local.size.z+.12f),steel);
                }
                foreach(int sign in new[]{-1,1})
                {
                    Box(bank,"Cable ladder rail",new Vector3(sign*13,4.25f,22.65f),new Vector3(12,.10f,.10f),steel);
                    Box(bank,"Cable ladder rail",new Vector3(sign*13,4.25f,22.12f),new Vector3(12,.10f,.10f),steel);
                    for(int i=0;i<25;i++)Box(bank,"Cable ladder rung",new Vector3(sign*(7+i*.5f),4.23f,22.38f),new Vector3(.045f,.065f,.61f),steel);
                    Box(bank,"Server inspection light housing",new Vector3(sign*13,4.8f,22.8f),new Vector3(3,.15f,.3f),steel);
                    Box(bank,"Server inspection light lens",new Vector3(sign*13,4.7f,22.78f),new Vector3(2.8f,.035f,.22f),white);
                    var lamp=new GameObject("Cool server inspection light");lamp.transform.SetParent(bank,false);lamp.transform.localPosition=new Vector3(sign*13,4.6f,21.9f);lamp.transform.localRotation=Quaternion.Euler(70,0,0);
                    var light=lamp.AddComponent<Light>();light.type=LightType.Spot;light.color=new Color(.65f,.80f,1);light.intensity=22;light.range=10;light.spotAngle=110;light.innerSpotAngle=70;light.shadows=LightShadows.None;light.cullingMask=~(1<<30);
                }
                // Collapse all simple service hardware into two draw calls per wall.
                foreach(var material in new[]{steel,white})
                {
                    var parts=new List<CombineInstance>();var remove=new List<GameObject>();
                    foreach(Transform child in bank)if(child.TryGetComponent<MeshRenderer>(out var renderer)&&renderer.sharedMaterial==material)
                    {parts.Add(new CombineInstance{mesh=child.GetComponent<MeshFilter>().sharedMesh,transform=bank.worldToLocalMatrix*child.localToWorldMatrix});remove.Add(child.gameObject);}
                    var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.ToArray());mesh=Save(mesh,"ServiceHardware"+side+material.name);
                    foreach(var obj in remove)UnityEngine.Object.DestroyImmediate(obj);
                    var batch=new GameObject(material.name);batch.transform.SetParent(bank,false);batch.AddComponent<MeshFilter>().sharedMesh=mesh;batch.AddComponent<MeshRenderer>().sharedMaterial=material;
                }
            }
            var roof=root.transform.Find("Meshy roof");var centers=new List<Vector3>();
            foreach(Transform tile in roof)
            {
                if(!tile.name.StartsWith("Damaged ceiling cassette"))continue;
                centers.Add(tile.localPosition);
                foreach(var filter in tile.GetComponentsInChildren<MeshFilter>())
                {
                    var source=filter.sharedMesh;var vertices=source.vertices;var indices=source.triangles;var inside=new bool[vertices.Length];
                    for(int i=0;i<vertices.Length;i++)
                    {
                        var p=roof.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]))-tile.localPosition;
                        float angle=Mathf.Atan2(p.z,p.x),edge=1+.055f*Mathf.Sin(angle*9)+.03f*Mathf.Sin(angle*17);
                        inside[i]=p.x*p.x/(1.35f*1.35f)+p.z*p.z/(1.7f*1.7f)<edge*edge;
                    }
                    var keep=new List<int>();for(int i=0;i<indices.Length;i+=3)
                    {if(inside[indices[i]]||inside[indices[i+1]]||inside[indices[i+2]])continue;keep.Add(indices[i]);keep.Add(indices[i+1]);keep.Add(indices[i+2]);}
                    var mesh=UnityEngine.Object.Instantiate(source);mesh.SetTriangles(keep,0);filter.sharedMesh=Save(mesh,tile.name+"_"+filter.name+"_Open");
                }
            }
            // Keep the remaining structure and regenerate collision from the same visible roof parts.
            var collisionParts=new List<CombineInstance>();
            foreach(Transform tile in roof)
            {
                var lod=tile.Find("LOD2");if(lod==null)continue;
                collisionParts.Add(new CombineInstance{mesh=lod.GetComponent<MeshFilter>().sharedMesh,transform=roof.worldToLocalMatrix*lod.localToWorldMatrix});
            }
            var collisionMesh=new Mesh{indexFormat=IndexFormat.UInt32};collisionMesh.CombineMeshes(collisionParts.ToArray());
            var collider=roof.GetComponent<MeshCollider>();collider.sharedMesh=Save(collisionMesh,"RoofWithRainOpenings");Physics.SyncTransforms();
            var layout=AssetDatabase.LoadAssetAtPath<FullRoofLayout>("Assets/BoxheadPrototype/Resources/Spaceport/ModularHall/Layout.asset");
            var patches=new List<Rect>();
            foreach(var center in centers)
            {
                // Narrower than the torn rim to leave margin for slanted drops and all detail levels.
                var patch=new Rect((center.x-.65f)/YardSurvival.ArenaScale,(center.z-.8f)/YardSurvival.ArenaScale,1.3f/YardSurvival.ArenaScale,1.6f/YardSurvival.ArenaScale);
                for(int x=0;x<=4;x++)for(int z=0;z<=4;z++)
                {var p=new Vector3(Mathf.Lerp(patch.xMin,patch.xMax,x/4f)*YardSurvival.ArenaScale,12,Mathf.Lerp(patch.yMin,patch.yMax,z/4f)*YardSurvival.ArenaScale);if(collider.Raycast(new Ray(p,Vector3.down),out var hit,7))throw new Exception("Roof still blocks rain at "+p);}
                patches.Add(patch);
            }
            if(patches.Count!=3)throw new Exception("Expected three damaged panels");
            BuildFloorWear(root.transform,centers);
            layout.rainPatches=patches.ToArray();EditorUtility.SetDirty(layout);
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);AssetDatabase.SaveAssets();
            File.WriteAllText("Assets/Verification~/ServerHallBuild.txt","PASS: 24 perimeter server cabinets, eight cool service lamps, cable ladders, three enlarged roof openings with matching collision and rain patches. Existing open entrances retained.\n"+string.Join("\n",patches));
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static void BuildFloorWear(Transform root,List<Vector3> leaks)
    {
        var previous=root.Find("Server floor dirt and tide marks");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var group=new GameObject("Server floor dirt and tide marks").transform;group.SetParent(root,false);
        for(int kind=0;kind<2;kind++)
        {
            string name=kind==0?"Settled server dust":"Rusty dried water marks";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+name+".mat");
            if(mat==null){mat=new Material(Shader.Find("Dustline/Server Floor Grime"));AssetDatabase.CreateAsset(mat,Folder+name+".mat");}
            mat.name="SolidHall_"+name;mat.SetColor("_BaseColor",kind==0?new Color(.032f,.028f,.023f,.65f):new Color(.075f,.055f,.035f,.45f));mat.SetFloat("_Tide",kind);EditorUtility.SetDirty(mat);
            var points=new List<Vector3>();var uv=new List<Vector2>();var faces=new List<int>();
            void Patch(Vector3 center,float radius,float yaw)
            {
                int first=points.Count;var rotation=Quaternion.Euler(0,yaw,0);
                foreach(var q in new[]{new Vector2(-1,-1),new Vector2(-1,1),new Vector2(1,1),new Vector2(1,-1)})
                {points.Add(new Vector3(center.x,.026f+kind*.002f,center.z)+rotation*new Vector3(q.x*radius,0,q.y*radius*.64f));uv.Add((q+Vector2.one)*.5f);}
                faces.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
            }
            if(kind==0)
            {
                for(int side=0;side<4;side++)foreach(float x in new[]{-17f,-13f,-9f,9f,13f,17f})
                    Patch(Quaternion.Euler(0,side*90,0)*new Vector3(x,0,20.5f),2.5f,side*90+x);
                foreach(int sign in new[]{-1,1})foreach(float z in new[]{-13f,-9f,9f,13f})Patch(new Vector3(sign*13,0,z),2.4f,z*9);
            }
            foreach(var leak in leaks)Patch(leak,kind==0?3.2f:3.5f,17);
            var mesh=new Mesh();mesh.SetVertices(points);mesh.SetUVs(0,uv);mesh.SetTriangles(faces,0);mesh.RecalculateNormals();mesh=Save(mesh,name);
            var obj=new GameObject(name);obj.transform.SetParent(group,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.allowOcclusionWhenDynamic=false;
        }
    }
}
