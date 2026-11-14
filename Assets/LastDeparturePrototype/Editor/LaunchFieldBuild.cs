using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class LaunchFieldBuild
{
    const string Folder="Assets/LastDeparturePrototype/Resources/Spaceport/LaunchField/";
    static Transform root,section;
    static Material concrete,dark,steel,white,yellow,amber,red;
    static readonly List<Mesh> temporary=new List<Mesh>();
    static LaunchFieldBuild(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        const string request="Assets/Verification~/LaunchField.request";
        if(File.Exists(request)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {File.Delete(request);SessionState.SetInt("LaunchField.Phase",1);}
        int phase=SessionState.GetInt("LaunchField.Phase",0);if(phase==0)return;
        EditorApplication.isPaused=false;Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if(phase==1){Build();SessionState.SetInt("LaunchField.Phase",2);EditorApplication.isPlaying=true;}
            else if(EditorApplication.isPlaying&&Time.unscaledTime>5)
            {SessionState.SetInt("LaunchField.Phase",0);Review();EditorApplication.isPlaying=false;}
        }
        catch(Exception e){SessionState.SetInt("LaunchField.Phase",0);File.WriteAllText("Assets/Verification~/LaunchFieldReview.txt","FAIL "+e);Debug.LogException(e);EditorApplication.isPlaying=false;}
    }
    [MenuItem("Tools/Yard Survival/Build rocket launch field")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        concrete=Surface("Concrete",new Color(.38f,.39f,.37f),0,.22f,true);
        dark=Surface("Blast resistant paving",new Color(.065f,.075f,.08f),.15f,.2f,true);
        steel=Surface("Service steel",new Color(.22f,.28f,.29f),.65f,.32f,false);
        white=Surface("Off white markings",new Color(.74f,.74f,.65f),.05f,.22f,false);
        yellow=Surface("Safety ochre",new Color(.68f,.43f,.10f),.05f,.23f,false);
        amber=Surface("Amber markers",new Color(.8f,.37f,.07f),0,.3f,false,new Color(2.2f,.75f,.12f));
        red=Surface("Obstruction beacons",new Color(.55f,.025f,.008f),0,.2f,false,new Color(2,.05f,.015f));
        root=new GameObject("LaunchFieldSet").transform;
        try
        {
            Begin("01 Launch complex");LaunchComplex();Batch();
            Begin("02 Booster recovery pad");RecoveryPad();Batch();
            Begin("03 Propellant storage");Propellant();Batch();
            Begin("04 Heavy transport routes");Routes();Batch();
            PrefabUtility.SaveAsPrefabAsset(root.gameObject,Folder+"LaunchFieldSet.prefab");AssetDatabase.SaveAssets();
        }
        finally{UnityEngine.Object.DestroyImmediate(root.gameObject);foreach(var mesh in temporary)UnityEngine.Object.DestroyImmediate(mesh);temporary.Clear();}
    }
    static Material Surface(string name,Color color,float metal,float smooth,bool texture,Color emission=default)
    {
        string path=Folder+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
        mat.name="LaunchField "+name;mat.color=color;mat.SetFloat("_Metallic",metal);mat.SetFloat("_Smoothness",smooth);
        if(texture){mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LastDeparturePrototype/Textures/Concrete.png"));mat.SetTextureScale("_BaseMap",new Vector2(3,3));}
        if(emission.maxColorComponent>0){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",emission);}
        EditorUtility.SetDirty(mat);return mat;
    }
    static void Begin(string name){section=new GameObject(name).transform;section.SetParent(root,false);section.localPosition=Vector3.up*.25f;}
    static Transform Shape(string name,PrimitiveType type,Vector3 p,Vector3 scale,Material material)
    {
        var o=GameObject.CreatePrimitive(type);o.name=name;o.transform.SetParent(section,false);o.transform.localPosition=p;o.transform.localScale=scale;
        UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());o.GetComponent<Renderer>().sharedMaterial=material;return o.transform;
    }
    static void Box(string name,Vector3 p,Vector3 size,Material m)=>Shape(name,PrimitiveType.Cube,p,size,m);
    static void Cylinder(string name,Vector3 p,float radius,float height,Material m)=>Shape(name,PrimitiveType.Cylinder,p,new Vector3(radius*2,height/2,radius*2),m);
    static void Beam(string name,Vector3 a,Vector3 b,float width,Material m)
    {var t=Shape(name,PrimitiveType.Cube,(a+b)/2,new Vector3(width,Vector3.Distance(a,b),width),m);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
    static void Ring(string name,Vector3 center,float inner,float outer,Material m,int segments=96)
    {
        var v=new List<Vector3>();var tris=new List<int>();var uv=new List<Vector2>();
        for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;var d=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));v.Add(center+d*inner);v.Add(center+d*outer);uv.Add(new Vector2(i/(float)segments,0));uv.Add(new Vector2(i/(float)segments,1));}
        for(int i=0;i<segments;i++){int k=i*2;tris.AddRange(new[]{k,k+1,k+2,k+1,k+3,k+2});}
        var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();temporary.Add(mesh);
        var o=new GameObject(name);o.transform.SetParent(section,false);o.AddComponent<MeshFilter>().sharedMesh=mesh;o.AddComponent<MeshRenderer>().sharedMaterial=m;
    }
    static void Label(string text,Vector3 p,float size,bool ground=false)
    {
        var o=new GameObject(text);o.transform.SetParent(section,false);o.transform.localPosition=p;if(ground)o.transform.localRotation=Quaternion.Euler(90,0,0);
        var label=o.AddComponent<TextMesh>();label.text=text;label.fontSize=100;label.characterSize=size*.1f;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=new Color(.72f,.72f,.60f);
        string path=Folder+"Label.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Dustline/Launch Field Stencil"));AssetDatabase.CreateAsset(material,path);}
        material.shader=Shader.Find("Dustline/Launch Field Stencil");material.name="LaunchField Stencils";
        material.SetTexture("_BaseMap",o.GetComponent<MeshRenderer>().sharedMaterial.mainTexture);material.SetColor("_BaseColor",new Color(.72f,.72f,.60f));EditorUtility.SetDirty(material);
        o.GetComponent<MeshRenderer>().sharedMaterial=material;
    }
    static void Marker(Vector3 p){Cylinder("Recessed light socket",p,.24f,.15f,dark);Cylinder("Amber perimeter lens",p+Vector3.up*.1f,.15f,.035f,amber);}
    static void Flood(Vector3 p,Vector3 target)
    {
        Beam("Floodlight mast",new Vector3(p.x,0,p.z),p,.22f,steel);Box("Floodlight crossbar",p,new Vector3(3,.2f,.3f),steel);
        for(int i=-1;i<=1;i++){Box("Shielded luminaire",p+Vector3.right*i,new Vector3(.7f,.32f,.65f),dark);Box("Lamp face",p+Vector3.right*i+Vector3.down*.18f,new Vector3(.6f,.04f,.5f),white);}
        var o=new GameObject("Launch field work light");o.transform.SetParent(section,false);o.transform.localPosition=p-Vector3.up*.3f;o.transform.LookAt(target);
        var l=o.AddComponent<Light>();l.type=LightType.Spot;l.spotAngle=100;l.innerSpotAngle=65;l.range=65;l.intensity=600;l.color=new Color(.72f,.81f,1);l.shadows=LightShadows.None;
    }
    static void LaunchComplex()
    {
        // The Blender FBX is mirrored on X on import: the existing rocket is at -13.
        section.localPosition+=Vector3.left*26;
        var c=new Vector3(13,.04f,102);
        Ring("Reinforced circular launch apron",c,9.8f,19,concrete);
        Ring("Scorched inner apron",c+Vector3.up*.015f,9.8f,12,dark);
        Ring("Pad exclusion circle",c+Vector3.up*.04f,17.5f,17.85f,yellow);
        for(int i=0;i<48;i++)
        {
            float a=i*Mathf.PI*2/48;var d=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
            Beam("Radial concrete joint",c+d*12,c+d*19,.035f,dark);
            if(i%3==0)Marker(c+d*18.6f+Vector3.up*.08f);
        }
        Label("LC - 27",new Vector3(13,.12f,83),1.4f,true);
        Label("EXCLUSION ZONE",new Vector3(13,.12f,79),.46f,true);
        // Channel begins at the existing raised mount and exhausts away from the terminal.
        Box("Exhaust channel bed",new Vector3(13,.025f,134),new Vector3(8,.05f,34),dark);
        for(int side=-1;side<=1;side+=2)
        {
            Box("Blast channel concrete wall",new Vector3(13+side*4.5f,.95f,134),new Vector3(1,1.9f,34),concrete);
            Box("Blast channel coping",new Vector3(13+side*4.5f,1.96f,134),new Vector3(1.15f,.12f,34),steel);
            for(int z=120;z<=148;z+=4)Box("Expansion joint",new Vector3(13+side*4.5f,1,z),new Vector3(1.02f,1.92f,.055f),dark);
        }
        for(int i=0;i<7;i++)Box("Flame deflector refractory step",new Vector3(13,.2f+i*.12f,117+i*.65f),new Vector3(7,.4f+i*.24f,.75f),steel);
        for(int i=0;i<8;i++)Box("Blast apron slab",new Vector3(13,.015f,153+i*2.5f),new Vector3(15,.04f,2.43f),i<4?dark:concrete);
        for(int side=-1;side<=1;side+=2)
        {
            float x=13+side*23;float z=110;
            Cylinder("Lightning mast footing",new Vector3(x,.35f,z),2,.7f,concrete);
            for(int leg=-1;leg<=1;leg+=2)Beam("Lightning mast leg",new Vector3(x+leg*.9f,.7f,z),new Vector3(x+leg*.18f,43,z),.2f,steel);
            for(int y=2;y<42;y+=3)Beam("Mast diagonal brace",new Vector3(x-.75f,y,z),new Vector3(x+.75f,y+3,z),.08f,steel);
            Cylinder("Air terminal",new Vector3(x,45,z),.055f,5,steel);Cylinder("Mast obstruction light",new Vector3(x,43,z),.2f,.25f,red);
        }
        Flood(new Vector3(-7,15,81),new Vector3(13,3,102));Flood(new Vector3(34,15,82),new Vector3(13,3,102));
    }
    static void RecoveryPad()
    {
        section.localPosition+=Vector3.left*16;
        var c=new Vector3(-89,.05f,157);
        Cylinder("Booster recovery foundation",c-Vector3.up*.15f,25,.3f,concrete);
        Cylinder("Landing pad scorched center",c+Vector3.up*.06f,15,.08f,dark);
        Ring("Touchdown target",c+Vector3.up*.13f,10.4f,10.8f,white);
        Ring("Landing safety perimeter",c+Vector3.up*.03f,22.5f,22.9f,yellow);
        Box("Touchdown cross",c+Vector3.up*.14f,new Vector3(11,.025f,.4f),white);Box("Touchdown cross",c+Vector3.up*.14f,new Vector3(.4f,.025f,11),white);
        for(int i=0;i<36;i++){float a=i*Mathf.PI*2/36;var d=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));if(i%2==0)Marker(c+d*24);Beam("Pad slab joint",c+d*15,c+d*25,.04f,dark);}
        Label("LZ - 02",c+new Vector3(0,.06f,-18),1.35f,true);
        Label("BOOSTER RECOVERY",c+new Vector3(0,.06f,18),.53f,true);
        Flood(new Vector3(-113,14,139),c);Flood(new Vector3(-66,14,177),c);
    }
    static void Propellant()
    {
        Box("Tank farm containment base",new Vector3(90,.04f,79),new Vector3(35,.18f,37),concrete);
        foreach(int side in new[]{-1,1}){Box("Containment bund",new Vector3(90+side*17.5f,.7f,79),new Vector3(.7f,1.4f,37),concrete);Box("Containment bund",new Vector3(90,.7f,79+side*18.5f),new Vector3(35,1.4f,.7f),concrete);}
        for(int i=0;i<4;i++)
        {
            float x=81+(i%2)*17,z=69+(i/2)*19;
            Cylinder("Tank plinth",new Vector3(x,.5f,z),5,1,concrete);
            Cylinder("Insulated propellant tank",new Vector3(x,6.5f,z),4,11,white);
            Shape("Domed tank crown",PrimitiveType.Sphere,new Vector3(x,12,z),new Vector3(8,2.2f,8),white);
            for(int y=2;y<=11;y+=3)Ring("Tank stiffening ring",new Vector3(x,y,z),4,4.13f,steel,48);
            Cylinder("Tank vent",new Vector3(x+1.8f,14,z),.16f,4,steel);
            Box("Tank identification band",new Vector3(x,7,z-4.01f),new Vector3(4,1.5f,.06f),dark);
            Label((i%2==0?"LOX":"CH4")+" / 0"+(i+1),new Vector3(x,7,z-4.06f),.3f);
            Beam("Tank outlet",new Vector3(x,2,z-4),new Vector3(x,2,57),.3f,steel);
            for(int rung=0;rung<20;rung++)Box("Service ladder rung",new Vector3(x+3.8f,1+rung*.55f,z-1.8f),new Vector3(.6f,.06f,.08f),steel);
        }
        // Parallel supply and return trunks follow a raised service rack to the launch pad.
        for(int pipe=0;pipe<3;pipe++)
        {
            float z=56+pipe*.6f;
            Beam("Cryogenic transfer line",new Vector3(99,3,z),new Vector3(47,3,z),.26f,steel);
            Beam("Cryogenic transfer line",new Vector3(47,3,z),new Vector3(47,3,99+pipe*.6f),.26f,steel);
            Beam("Launch supply branch",new Vector3(47,5,99+pipe*.6f),new Vector3(-1,5,99+pipe*.6f),.26f,steel);
            Beam("Supply riser",new Vector3(47,3,99+pipe*.6f),new Vector3(47,5,99+pipe*.6f),.26f,steel);
        }
        for(int x=49;x<=99;x+=8){Box("Pipe rack support",new Vector3(x,1.5f,56.6f),new Vector3(.22f,3,2.4f),steel);}
        Box("Propellant sign plate",new Vector3(90,2.7f,60),new Vector3(12,1.2f,.12f),dark);
        foreach(int side in new[]{-1,1})Beam("Propellant sign leg",new Vector3(90+side*5,.2f,60),new Vector3(90+side*5,3.3f,60),.12f,steel);
        Label("PROPELLANT STORAGE",new Vector3(90,2.7f,59.9f),.45f);
        Flood(new Vector3(70,12,59),new Vector3(90,5,79));
    }
    static void Road(Vector3 a,Vector3 b,float width)
    {
        var d=(b-a).normalized;var normal=new Vector3(d.z,0,-d.x);
        var road=Shape("Heavy transport road",PrimitiveType.Cube,(a+b)*.5f,new Vector3(width,.10f,Vector3.Distance(a,b)),dark);road.localRotation=Quaternion.LookRotation(d);
        for(int side=-1;side<=1;side+=2)Beam("Road edge stripe",a+normal*side*(width/2-.45f)+Vector3.up*.07f,b+normal*side*(width/2-.45f)+Vector3.up*.07f,.12f,white);
        for(float t=2;t<Vector3.Distance(a,b)-4;t+=8)Beam("Transport guide dash",a+d*t+Vector3.up*.08f,a+d*(t+3)+Vector3.up*.08f,.16f,yellow);
    }
    static void Routes()
    {
        Road(new Vector3(0,.11f,31),new Vector3(0,.11f,76),10);
        Road(new Vector3(13,.11f,76),new Vector3(13,.11f,224),9);
        Road(new Vector3(-7,.11f,36),new Vector3(-105,.11f,36),9);
        Road(new Vector3(-105,.11f,36),new Vector3(-105,.11f,131),9);
        Road(new Vector3(8,.11f,44),new Vector3(90,.11f,44),8);
        Road(new Vector3(90,.11f,44),new Vector3(90,.11f,59),8);
        Label("LAUNCH OPERATIONS",new Vector3(0,.18f,47),.8f,true);
        Label("CRAWLER ROUTE",new Vector3(-50,.23f,36),.65f,true);
        for(int x=-16;x<=16;x+=2)if(Mathf.Abs(x)>5)Box("Launch zone stop marking",new Vector3(x,.13f,74),new Vector3(1,.025f,3),yellow);
        foreach(int side in new[]{-1,1})
        {
            float x=side*7;
            Box("Access checkpoint base",new Vector3(x,.4f,72),new Vector3(.7f,.8f,.7f),concrete);
            Box("Checkpoint post",new Vector3(x,2,72),new Vector3(.16f,3.2f,.16f),steel);
            Box("Warning signal",new Vector3(x,3.2f,71.88f),new Vector3(.45f,.65f,.12f),dark);
            Cylinder("Access warning lamp",new Vector3(x,3.3f,71.76f),.12f,.15f,red);
        }
        Box("Checkpoint sign backing",new Vector3(0,4.4f,72.1f),new Vector3(13,1,.18f),dark);
        foreach(int side in new[]{-1,1})Beam("Sign support",new Vector3(side*6.7f,.5f,72.1f),new Vector3(side*6.7f,4.9f,72.1f),.15f,steel);
        Label("AUTHORIZED LAUNCH CREW ONLY",new Vector3(0,4.4f,71.98f),.38f);
    }
    static void Batch()
    {
        var groups=new Dictionary<Material,List<CombineInstance>>();var objects=new List<GameObject>();
        foreach(var filter in section.GetComponentsInChildren<MeshFilter>())
        {
            var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null)continue;
            if(!groups.TryGetValue(renderer.sharedMaterial,out var list))groups[renderer.sharedMaterial]=list=new List<CombineInstance>();
            list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=section.worldToLocalMatrix*filter.transform.localToWorldMatrix});objects.Add(filter.gameObject);
        }
        int n=0;
        foreach(var pair in groups)
        {
            var mesh=new Mesh{name=section.name+" geometry",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray());
            string path=Folder+section.name.Substring(0,2)+"-"+n+++".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);}
            var o=new GameObject(pair.Key.name);o.transform.SetParent(section,false);o.AddComponent<MeshFilter>().sharedMesh=saved;
            var renderer=o.AddComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key;renderer.allowOcclusionWhenDynamic=false;
        }
        foreach(var o in objects)UnityEngine.Object.DestroyImmediate(o);
    }
    static void Review()
    {
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();var field=game.transform.Find("Spaceport exterior/Rocket launch field");
        if(field==null||field.childCount!=4)throw new Exception("Launch field zones missing");
        if(field.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Scenery blocks gameplay routes");
        foreach(var r in field.GetComponentsInChildren<MeshRenderer>())if(r.sharedMaterial==null||ShaderUtil.ShaderHasError(r.sharedMaterial.shader))throw new Exception("Launch field shader error");
        Capture(new Vector3(0,2.2f,25),new Vector3(-13,15,102),"LaunchField-Terminal");
        var light=new GameObject("Temporary launch field inspection light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.transform.rotation=Quaternion.Euler(55,-30,0);
        bool fog=RenderSettings.fog;
        try
        {
            RenderSettings.fog=false;
            Capture(new Vector3(-125,110,10),new Vector3(0,0,115),"LaunchField-Overview");
            Capture(new Vector3(8,12,75),new Vector3(-13,12,107),"LaunchField-Pad");
            Capture(new Vector3(61,16,43),new Vector3(90,6,80),"LaunchField-Fuel");
            Capture(new Vector3(-135,25,122),new Vector3(-105,0,157),"LaunchField-Recovery");
        }
        finally{RenderSettings.fog=fog;UnityEngine.Object.DestroyImmediate(light.gameObject);}
        File.WriteAllText("Assets/Verification~/LaunchFieldReview.txt","PASS four exterior zones loaded: launch complex, booster recovery, propellant storage, transport routes.\nPASS materials compile; no new gameplay colliders.\nRenderers: "+field.GetComponentsInChildren<MeshRenderer>().Length+"; lights: "+field.GetComponentsInChildren<Light>().Length);
    }
    static void Capture(Vector3 p,Vector3 target,string name)
    {
        var camera=new GameObject("Launch field inspection").AddComponent<Camera>();camera.transform.position=p;camera.transform.LookAt(target);camera.fieldOfView=65;camera.farClipPlane=800;camera.useOcclusionCulling=false;camera.cullingMask=~(1<<30);camera.clearFlags=CameraClearFlags.Skybox;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.FastApproximateAntialiasing;
        var rt=new RenderTexture(1600,1000,24);var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);var prev=RenderTexture.active;
        try{rt.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();File.WriteAllBytes("Assets/Verification~/"+name+".png",pixels.EncodeToPNG());}
        finally{RenderTexture.active=prev;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(camera.gameObject);}
    }
}
