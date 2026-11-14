using System.Collections.Generic;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class SuspendedCore:MonoBehaviour
    {
        bool menuPresentation; Light menuRim;
        public void SetMenuPresentation(bool active) { menuPresentation=active; if(menuRim!=null)menuRim.enabled=active; }
        Transform heart;Light glow;Material tissue,vein,artery,metal,lightMaterial,harnessSurface,chainedHeartSurface,hoseRepairSurface;Texture2D tissueMap;
        static readonly Vector3 OrganProportions=new Vector3(1.16f,.94f,1.12f);
        readonly List<Mesh> meshes=new List<Mesh>();
        float condition=1,hitAt=-10;bool destroyed;
        public void SetCondition(float value,bool lost){condition=value;destroyed=lost;}
        public void Hit(){hitAt=Time.time;}
        Material Make(string name,Color color,float metallic=0,float smooth=.4f,float emission=0)
        {
            var m=new Material(RuntimeShaders.Find("Universal Render Pipeline/Lit"));m.name=name;m.color=color;m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);
            if(emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*emission);}return m;
        }
        Transform Shape(string name,PrimitiveType type,Vector3 p,Vector3 scale,Material material,Transform parent=null)
        {
            var obj=GameObject.CreatePrimitive(type);obj.name=name;obj.transform.SetParent(parent!=null?parent:transform,false);obj.transform.localPosition=p;obj.transform.localScale=scale;
            var collider=obj.GetComponent<Collider>();collider.enabled=false;Destroy(collider);obj.GetComponent<Renderer>().sharedMaterial=material;return obj.transform;
        }
        void MeshObject(string name,List<Vector3> vertices,List<int> triangles,Material material,Transform parent,List<Vector2> uv=null)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);if(uv!=null)mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        // Rounded tube bends, shared by vessels, conduits and chain links.
        void AppendTube(List<Vector3> vertices,List<int> triangles,Vector3[] path,float radius,int sides=10,float taper=1)
        {
            int offset=vertices.Count;
            for(int i=0;i<path.Length;i++)
            {
                Vector3 tangent=(path[Mathf.Min(i+1,path.Length-1)]-path[Mathf.Max(0,i-1)]).normalized;
                Vector3 right=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.right:Vector3.up).normalized,up=Vector3.Cross(tangent,right);
                float r=radius*Mathf.Lerp(1,taper,i/(float)(path.Length-1));
                for(int j=0;j<sides;j++)
                {
                    float a=j*Mathf.PI*2/sides;vertices.Add(path[i]+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*r);
                    if(i==0)continue;int c=offset+i*sides+j,d=offset+i*sides+(j+1)%sides,b=d-sides,a0=c-sides;
                    triangles.Add(a0);triangles.Add(d);triangles.Add(c);triangles.Add(a0);triangles.Add(b);triangles.Add(d);
                }
            }
        }
        void Tube(string name,Vector3[] points,float radius,Material material,Transform parent,float taper=1)
        {
            var v=new List<Vector3>();var t=new List<int>();AppendTube(v,t,points,radius,12,taper);MeshObject(name,v,t,material,parent);
        }
        Vector3[] Curve(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int count=20)
        {
            var points=new Vector3[count];for(int i=0;i<count;i++){float t=i/(float)(count-1),s=1-t;points[i]=s*s*s*a+3*s*s*t*b+3*s*t*t*c+t*t*t*d;}return points;
        }
        Vector3[] CoronaryPath(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int side)
        {
            var points=Curve(a,b,c,d);
            for(int i=0;i<points.Length;i++)
            {
                var p=points[i];float cosine=Mathf.Clamp(p.y/1.12f,-.98f,.98f),sine=Mathf.Sqrt(1-cosine*cosine);
                float rx=sine*(.72f+.22f*cosine),rz=sine*(.53f+.10f*cosine),center=-.2f*(1-cosine);
                p.x=Mathf.Clamp(p.x,center-rx*.9f,center+rx*.9f);
                float x=(p.x-center)/rx;p.z=side*(rz*Mathf.Sqrt(1-x*x)+.008f);points[i]=p;
            }
            return points;
        }
        void Ring(string name,Vector3 center,float radius,float thickness,Material material,Transform parent)
        {
            var p=new Vector3[49];for(int i=0;i<p.Length;i++){float a=i*Mathf.PI*2/(p.Length-1);p[i]=center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);}Tube(name,p,thickness,material,parent);
        }
        void Chain(Vector3 a,Vector3 b,float bow=0,Material finish=null)
        {
            var v=new List<Vector3>();var t=new List<int>();Vector3 direction=(b-a).normalized,side=Vector3.Cross(direction,Vector3.forward).normalized;
            int links=Mathf.Max(2,Mathf.CeilToInt(Vector3.Distance(a,b)/.18f));
            for(int i=0;i<links;i++)
            {
                float t0=i/(float)(links-1);
                Vector3 outward=new Vector3(b.x,0,b.z).normalized;
                Vector3 center=Vector3.Lerp(a,b,t0)+outward*(Mathf.Sin(t0*Mathf.PI)*bow),across=i%2==0?side:Vector3.Cross(direction,side);
                var path=new Vector3[17];for(int j=0;j<17;j++){float angle=j*Mathf.PI*2/16;path[j]=center+direction*Mathf.Cos(angle)*.125f+across*Mathf.Sin(angle)*.075f;}
                AppendTube(v,t,path,.023f,6);
            }
            MeshObject("Interlocked suspension chain",v,t,finish!=null?finish:metal,transform);
        }
        public void Build()
        {
            if(BuildMeshyPlatform()){BuildCoreLights();return;}
            tissue=Make("Core / mottled cardiac tissue",Color.white,0,.52f);vein=Make("Core / coronary veins",new Color(.12f,.055f,.075f),0,.38f);
            artery=Make("Core / arterial muscle",new Color(.34f,.08f,.075f),0,.46f);metal=Make("Core / worn steel",new Color(.23f,.25f,.24f),.8f,.4f);
            lightMaterial=Make("Core / amber service lamps",new Color(1,.24f,.055f),0,.35f,2);
            var authoredSurface=Resources.Load<Material>("Spaceport/HeartSurface");
            if(authoredSurface!=null){Destroy(tissue);tissue=new Material(authoredSurface);tissue.SetColor("_BaseColor",Color.white);tissue.SetFloat("_BumpScale",.65f);}
            else
            {
            tissueMap=new Texture2D(256,256,TextureFormat.RGB24,true);tissueMap.name="Cardiac mottling";tissueMap.wrapMode=TextureWrapMode.Repeat;
            var pixels=new Color[256*256];for(int y=0;y<256;y++)for(int x=0;x<256;x++)
            {
                float n=Mathf.PerlinNoise(x*.06f,y*.07f)*.65f+Mathf.PerlinNoise(x*.23f,y*.28f)*.35f;
                float fibre=Mathf.Pow(.5f+.5f*Mathf.Sin(x*.7f+y*.11f+Mathf.PerlinNoise(x*.02f,y*.025f)*8),8)*.08f;
                pixels[y*256+x]=Color.Lerp(new Color(.16f,.035f,.045f),new Color(.48f,.19f,.16f),n)*(1-fibre);
            }
            tissueMap.SetPixels(pixels);tissueMap.Apply();tissue.SetTexture("_BaseMap",tissueMap);
            }
            heart=new GameObject("Living anatomical heart").transform;heart.SetParent(transform,false);heart.localPosition=new Vector3(0,2.65f,0);heart.localRotation=Quaternion.Euler(0,-15,4);heart.localScale=OrganProportions;
            var authoredHeart=Resources.Load<GameObject>("Spaceport/AnatomicalHeart");
            bool hasChainedHeart = BuildChainedHeart();
            if(hasChainedHeart)
            {
                heart.gameObject.SetActive(false);
            }
            else if(authoredHeart!=null)
            {
                var organ=Instantiate(authoredHeart,heart,false);organ.name="Blender anatomical heart";
                foreach(var renderer in organ.GetComponentsInChildren<MeshRenderer>())
                    renderer.sharedMaterial=renderer.name.Contains("Vein")?vein:renderer.name.Contains("Artery")?artery:tissue;
            }
            else
            {
            var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();const int rings=40,sides=64;
            for(int r=0;r<=rings;r++)for(int j=0;j<=sides;j++)
            {
                float theta=Mathf.PI*r/rings,phi=Mathf.PI*2*j/sides,c=Mathf.Cos(theta),s=Mathf.Max(.001f,Mathf.Sin(theta));
                float waist=.72f+.22f*c;float asym=1+.09f*Mathf.Sin(phi*2+theta)*s;
                vertices.Add(new Vector3(Mathf.Cos(phi)*s*waist*asym-.20f*(1-c),1.12f*c,Mathf.Sin(phi)*s*(.53f+.10f*c)));
                uv.Add(new Vector2(j/(float)sides,r/(float)rings));
                if(r==rings||j==sides)continue;int a=r*(sides+1)+j,b=a+1,c0=a+sides+1,d=c0+1;
                triangles.Add(a);triangles.Add(b);triangles.Add(c0);triangles.Add(b);triangles.Add(d);triangles.Add(c0);
            }
            MeshObject("Ventricular myocardium",vertices,triangles,tissue,heart,uv);
            Shape("Right atrium",PrimitiveType.Sphere,new Vector3(.39f,.72f,.14f),new Vector3(.9f,.82f,.83f),tissue,heart);
            Shape("Left atrial appendage",PrimitiveType.Sphere,new Vector3(-.46f,.75f,.13f),new Vector3(.66f,.62f,.7f),tissue,heart);
            Tube("Aortic arch",Curve(new Vector3(-.12f,.9f,0),new Vector3(-.45f,1.95f,.02f),new Vector3(.75f,2.1f,.17f),new Vector3(.62f,.48f,.38f)),.18f,artery,heart,.9f);
            Tube("Pulmonary trunk",Curve(new Vector3(.19f,.65f,-.25f),new Vector3(.35f,1.35f,-.3f),new Vector3(-.2f,1.4f,-.22f),new Vector3(-.72f,1.22f,.12f)),.15f,vein,heart,.8f);
            Tube("Superior vena cava",Curve(new Vector3(.55f,.7f,.2f),new Vector3(.78f,1.05f,.2f),new Vector3(.62f,1.7f,.3f),new Vector3(.64f,2,.3f)),.14f,vein,heart,.8f);
            for(int branch=0;branch<3;branch++)
            {
                float x=-.15f+branch*.22f;
                Tube("Aortic branch",Curve(new Vector3(x,1.55f,.1f),new Vector3(x,1.95f,.15f),new Vector3(x-.13f,2.05f,.2f),new Vector3(x-.13f,2.3f,.2f)),.055f,artery,heart,.65f);
            }
            for(int side=-1;side<=1;side+=2)
            {
                float z=side*.51f;
                Tube("Coronary artery",CoronaryPath(new Vector3(.28f,.63f,z*.8f),new Vector3(.1f,.15f,z*1.15f),new Vector3(-.48f,-.35f,z*.9f),new Vector3(-.46f,-.93f,z*.4f),side),.035f,artery,heart,.35f);
                for(int i=0;i<4;i++)
                {
                    float y=.47f-i*.25f;
                    Tube("Branching coronary vein",CoronaryPath(new Vector3(.23f-i*.14f,y,z),new Vector3(.55f,y-.1f,z*.95f),new Vector3(.63f,y-.3f,z*.7f),new Vector3(.3f-i*.12f,y-.43f,z*.7f),side),.023f,vein,heart,.2f);
                }
            }
            }
            Transform armatureMount=null;
            var authoredHarness=Resources.Load<GameObject>("Spaceport/HeartHarness");
            if(!hasChainedHeart && authoredHarness!=null)
            {
                armatureMount=new GameObject("Fitted steel heart harness").transform;armatureMount.SetParent(transform,false);
                armatureMount.localPosition=heart.localPosition;armatureMount.localRotation=heart.localRotation;
                // The rigid cuff must clear the expanded organ at the peak of its beat.
                // Chains use this same mount, keeping their shackles attached after fitting.
                armatureMount.localScale=Vector3.Scale(OrganProportions,new Vector3(1.22f,1,1.30f));
                var frame=Instantiate(authoredHarness,armatureMount,false);
                var surface=Resources.Load<Material>("Spaceport/HeartHarnessSurface");
                if(surface!=null){harnessSurface=new Material(surface);harnessSurface.SetColor("_BaseColor",new Color(.68f,.61f,.52f));surface=harnessSurface;}
                foreach(var renderer in frame.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterial=surface!=null?surface:metal;
            }
            BuildServicePlatform(!hasChainedHeart);
            BuildMaintenanceBay();
            if(!hasChainedHeart) gameObject.AddComponent<HeartCableHarness>().Build(heart,metal);
            if(!hasChainedHeart) BuildSuspensionCrown();
            for(int i=0;!hasChainedHeart && i<4;i++)
            {
                float a=i*Mathf.PI*.5f+Mathf.PI*.25f;Vector3 rim=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Vector3 shackle=armatureMount!=null?transform.InverseTransformPoint(armatureMount.TransformPoint(new Vector3(Mathf.Sign(rim.x)*.72f,1.02f,Mathf.Sign(rim.z)*.60f))):rim+Vector3.up*3.45f;
                Vector3 crown=rim*1.95f+Vector3.up*5.75f;
                Chain(shackle,crown,.35f);
                Chain(crown+Vector3.up*.4f,rim*2.05f+Vector3.up*7.96f);
                Shape("Anchor bolt",PrimitiveType.Cylinder,rim*2.05f+Vector3.up*7.96f,new Vector3(.14f,.08f,.14f),metal);
            }
            BuildCoreLights();
        }
        void BuildCoreLights()
        {
            var lamp=new GameObject("Core inspection light");lamp.transform.SetParent(transform,false);lamp.transform.localPosition=new Vector3(0,3,-2.2f);
            glow=lamp.AddComponent<Light>();glow.type=LightType.Point;glow.color=new Color(1,.92f,.86f);glow.range=HasMeshyPlatform?8:6;glow.intensity=3;
            if(HasMeshyPlatform)lamp.transform.localPosition=new Vector3(0,3.5f,-3.7f);glow.shadows=LightShadows.None;
            var rimObject=new GameObject("Menu red rim light");rimObject.transform.SetParent(transform,false);rimObject.transform.localPosition=new Vector3(1.8f,3.4f,1.1f);
            menuRim=rimObject.AddComponent<Light>();menuRim.type=LightType.Point;menuRim.color=new Color(1,.035f,.012f);menuRim.range=5;menuRim.intensity=4.5f;menuRim.enabled=false;
            var crownLamp=new GameObject("Warm suspension inspection lamp");crownLamp.transform.SetParent(transform,false);crownLamp.transform.localPosition=new Vector3(-1.4f,5.3f,-2.4f);
            var crownLight=crownLamp.AddComponent<Light>();crownLight.type=LightType.Point;crownLight.color=new Color(.77f,.84f,1);crownLight.range=5;crownLight.intensity=1.4f;crownLight.shadows=LightShadows.None;
        }
        bool BuildChainedHeart()
        {
            var asset = Resources.Load<GameObject>("Spaceport/ChainedHeart");
            var color = Resources.Load<Texture2D>("Spaceport/ChainedHeartColor");
            var shader = Resources.Load<Shader>("ChainedHeartSurface");
            if (asset == null || color == null || shader == null || !shader.isSupported) return false;

            var mount = new GameObject("Chained Heart Machine").transform;
            mount.SetParent(transform, false);
            var model = Instantiate(asset, mount, false);
            var renderers = model.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0)
            {
                Destroy(mount.gameObject);
                return false;
            }

            var bounds = new Bounds();
            bool first = true;
            foreach (var renderer in renderers)
            {
                var local = renderer.localBounds;
                var matrix = mount.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var direction = new Vector3((corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                    var point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, direction));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            if (bounds.size.y < .001f)
            {
                Destroy(mount.gameObject);
                return false;
            }

            float scale = Mathf.Min(6f / bounds.size.y, 4.3f / Mathf.Max(.001f, bounds.size.x, bounds.size.z));
            mount.localScale = Vector3.one * scale;
            const float floorClearance = .78f;
            mount.localPosition = new Vector3(-bounds.center.x, floorClearance / scale - bounds.min.y, -bounds.center.z) * scale;
            chainedHeartSurface = new Material(shader) { name = "Core / Chained Heart Machine" };
            chainedHeartSurface.SetTexture("_BaseMap", color);
            var metalMask = Resources.Load<Texture2D>("Spaceport/ChainedHeartMetalMask");
            if (metalMask != null) chainedHeartSurface.SetTexture("_MetalMask", metalMask);
            var normal = Resources.Load<Texture2D>("Spaceport/ChainedHeartNormal");
            if (normal != null)
            {
                chainedHeartSurface.SetTexture("_BumpMap", normal);
            }
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++) materials[slot] = chainedHeartSurface;
                renderer.sharedMaterials = materials;
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetMatrix("_HeartFromObject",
                    Matrix4x4.Scale(new Vector3(1/bounds.size.y, 1/bounds.size.y, 1/bounds.size.y)) *
                    Matrix4x4.Translate(new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z)) *
                    mount.worldToLocalMatrix * renderer.transform.localToWorldMatrix);
                renderer.SetPropertyBlock(properties);
            }
            harnessSurface = Make("Core / bronze chain extensions", new Color(.41f,.28f,.13f), .65f, .35f);
            BuildChainedHeartHoseConnections(bounds, mount.localPosition, scale);
            float top = floorClearance + bounds.size.y * scale;
            // Match the four chain tips on the imported assembly.
            foreach (var tip in new[] {new Vector2(.685f,.021f),new Vector2(.974f,.327f),
                new Vector2(.02f,.687f),new Vector2(.684f,.991f)})
            {
                var anchor = new Vector3((tip.x-.5f)*bounds.size.x*scale, top-.08f,
                    (.5f-tip.y)*bounds.size.z*scale);
                var ceiling = new Vector3(anchor.x, 7.96f, anchor.z);
                Chain(anchor,ceiling,finish:harnessSurface);
                Shape("Ceiling chain anchor",PrimitiveType.Cube,ceiling,new Vector3(.32f,.13f,.32f),metal);
            }
            return true;
        }

        void BuildChainedHeartHoseConnections(Bounds bounds, Vector3 origin, float scale)
        {
            hoseRepairSurface = Make("Core / dark rubber hose", new Color(.085f,.072f,.056f), .12f, .36f);
            // The coordinates are fitted to cross-sections of the imported scan.
            Vector3 Point(float x, float height, float depth) =>
                origin + (bounds.min + Vector3.Scale(bounds.size, new Vector3(x,height,1-depth))) * scale;

            void Connect(string name, Vector3 start, Vector3 startHandle, Vector3 endHandle, Vector3 end, float radius)
            {
                Tube(name, Curve(start, startHandle, endHandle, end, 32), radius, hoseRepairSurface, transform);
                // The terminal collars cover both the cast-in join and the open end of the tube mesh.
                Vector3 direction = (end - endHandle).normalized;
                var collar = Shape(name + " socket", PrimitiveType.Cylinder, end - direction * .08f,
                    new Vector3(radius * 2.6f, .115f, radius * 2.6f), harnessSurface);
                collar.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            }

            Connect("Outer return hose",
                Point(.160f,.710f,.608f), Point(.124f,.653f,.625f),
                Point(.194f,.554f,.638f), Point(.269f,.453f,.649f), .080f);
            Connect("Inner return hose",
                Point(.266f,.578f,.470f), Point(.216f,.654f,.519f),
                Point(.253f,.713f,.507f), Point(.252f,.785f,.491f), .071f);
            Connect("Upper supply hose",
                Point(.380f,.652f,.219f), Point(.360f,.691f,.185f),
                Point(.367f,.724f,.166f), Point(.401f,.775f,.165f), .079f);
        }
        void BuildServicePlatform(bool useLegacySuspension)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Beam(Vector3 p,Vector3 size)
            {
                int offset=vertices.Count;
                foreach(var corner in new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)})vertices.Add(p+Vector3.Scale(corner,size)*.5f);
                foreach(int index in new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5})triangles.Add(offset+index);
            }
            // Broad low steel service deck with open entrances aligned to the arena approaches.
            Beam(new Vector3(0,.045f,0),new Vector3(5.6f,.09f,4.6f));
            for(int i=-13;i<=13;i++)Beam(new Vector3(i*.2f,.10f,0),new Vector3(.035f,.035f,4.5f));
            for(int i=-11;i<=11;i++)Beam(new Vector3(0,.105f,i*.2f),new Vector3(5.5f,.025f,.025f));
            foreach(int side in new[]{-1,1})
            {
                foreach(float x in new[]{-2.7f,-1.8f,-.9f,.9f,1.8f,2.7f})
                {
                    Beam(new Vector3(x,.64f,side*2.2f),new Vector3(.07f,1.05f,.07f));
                    Beam(new Vector3(x,.15f,side*2.2f),new Vector3(.18f,.07f,.18f));
                }
                foreach(float x in new[]{-1.8f,1.8f})foreach(float y in new[]{.58f,1.16f})Beam(new Vector3(x,y,side*2.2f),new Vector3(1.9f,.055f,.055f));
                foreach(float z in new[]{-1.3f,1.3f})
                {
                    Beam(new Vector3(side*2.7f,.64f,z),new Vector3(.07f,1.05f,.07f));
                    foreach(float y in new[]{.58f,1.16f})Beam(new Vector3(side*2.7f,y,z),new Vector3(.055f,.055f,1.40f));
                }
                // Rear inner gantry, visible behind the hanging organ.
                Beam(new Vector3(side*1.6f,.85f,.95f),new Vector3(.09f,1.5f,.09f));
                if(useLegacySuspension)
                    Chain(new Vector3(side*1.6f,1.6f,.95f),new Vector3(side*1.6f,5.75f,.95f));
            }
            foreach(float y in new[]{.85f,1.6f})Beam(new Vector3(0,y,.95f),new Vector3(3.3f,.065f,.065f));
            Beam(new Vector3(0,.19f,0),new Vector3(1.6f,.18f,1.6f));
            for(int i=-6;i<=6;i++)Beam(new Vector3(i*.11f,.34f,0),new Vector3(.045f,.07f,1.45f));
            MeshObject("Industrial grated platform and safety railings",vertices,triangles,metal,transform);
            Shape("Recessed amber maintenance well",PrimitiveType.Cube,new Vector3(0,.26f,0),new Vector3(1.35f,.035f,1.35f),lightMaterial);
            foreach(int side in new[]{-1,1})
            {
                Vector3 p=new Vector3(side*2.6f,.42f,-1.9f);
                Shape("Caged platform service lamp",PrimitiveType.Cylinder,p,new Vector3(.12f,.16f,.12f),lightMaterial);
                Ring("Service lamp lower cage",p-Vector3.up*.15f,.085f,.012f,metal,transform);
                Ring("Service lamp upper cage",p+Vector3.up*.15f,.085f,.012f,metal,transform);
                for(int j=0;j<4;j++){float a=j*Mathf.PI*.5f;var offset=new Vector3(Mathf.Cos(a)*.085f,0,Mathf.Sin(a)*.085f);Tube("Lamp protective bar",new[]{p+offset-Vector3.up*.15f,p+offset+Vector3.up*.15f},.008f,metal,transform);}
            }
            var lamp=new GameObject("Warm light beneath the heart");lamp.transform.SetParent(transform,false);lamp.transform.localPosition=new Vector3(0,.65f,0);
            var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.27f,.09f);light.range=3;light.intensity=.85f;light.shadows=LightShadows.None;
        }
        void BuildSuspensionCrown()
        {
            // Open fabricated-steel drum: the organ and umbilicals remain visible through it.
            var v=new List<Vector3>();var t=new List<int>();const int segments=96;
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments;
                foreach(var p in new[]{new Vector2(1.87f,5.72f),new Vector2(2.03f,5.72f),new Vector2(2.03f,6.10f),new Vector2(1.87f,6.10f)})
                    v.Add(new Vector3(Mathf.Cos(a)*p.x,p.y,Mathf.Sin(a)*p.x));
                for(int j=0;j<4;j++){int b=i*4+j,c=((i+1)%segments)*4+j,d=((i+1)%segments)*4+(j+1)%4,e=i*4+(j+1)%4;t.AddRange(new[]{b,c,d,b,d,e});}
            }
            MeshObject("Riveted overhead suspension drum",v,t,metal,transform);
            Ring("Crown upper rolled flange",Vector3.up*6.12f,1.96f,.065f,metal,transform);
            Ring("Crown lower rolled flange",Vector3.up*5.70f,1.96f,.065f,metal,transform);
            Ring("Inner conduit carrier",Vector3.up*5.68f,1.35f,.04f,metal,transform);
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI*2/16;Vector3 radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                var strap=Shape("Crown joint plate",PrimitiveType.Cube,radial*2.045f+Vector3.up*5.91f,new Vector3(.10f,.40f,.035f),metal);
                strap.localRotation=Quaternion.LookRotation(radial);
                foreach(float y in new[]{5.79f,6.03f})Shape("Crown rivet",PrimitiveType.Sphere,radial*2.074f+Vector3.up*y,Vector3.one*.06f,metal);
                if(i%4==0)Tube("Conduit carrier spoke",new[]{radial*1.35f+Vector3.up*5.68f,radial*1.9f+Vector3.up*5.75f},.035f,metal,transform);
            }
        }
        public float PulseSampleTime { get; private set; }
        public float EvaluatePulse(float time)
        {
            float phase=Mathf.Repeat(time*(condition<.3f?1.8f:1.05f),1);
            return destroyed?0:Mathf.Exp(-Mathf.Pow((phase-.17f)/.06f,2))+.55f*Mathf.Exp(-Mathf.Pow((phase-.36f)/.085f,2));
        }
        public void SamplePulse(float time)
        {
            PulseSampleTime=time;
            if(heart==null)return;
            float pulse=EvaluatePulse(time);
            if(serviceDetails!=null)serviceDetails.SetPulse(pulse,destroyed);
            if (chainedHeartSurface != null)
            {
                chainedHeartSurface.SetFloat("_Pulse", pulse);
                chainedHeartSurface.SetVector("_TubePulse", new Vector4(
                    EvaluatePulse(time-.025f), EvaluatePulse(time-.10f), EvaluatePulse(time-.18f), 0));
            }
            else heart.localScale=Vector3.Scale(OrganProportions,new Vector3(1+pulse*.085f,1-pulse*.035f,1+pulse*.065f));
            float hit=Mathf.Clamp01(1-(Time.time-hitAt)*4);glow.intensity=destroyed?.25f:1.8f+pulse*.25f+hit*.6f;
            if(HasMeshyPlatform)glow.intensity*=3f;
            if(menuPresentation)glow.intensity*=.65f;
            glow.color=menuPresentation?new Color(1,.59f,.43f):condition<.3f?new Color(1,.4f,.25f):new Color(1,.92f,.86f);
        }
        void Update(){SamplePulse(Time.time);}
        void OnDestroy(){foreach(var m in meshes)if(m!=null)Destroy(m);foreach(var m in new[]{tissue,vein,artery,metal,lightMaterial,harnessSurface,chainedHeartSurface,hoseRepairSurface,bayPaint,bayDark,bayDisplay,bayIvory})if(m!=null)Destroy(m);if(tissueMap!=null)Destroy(tissueMap);}
    }
}
