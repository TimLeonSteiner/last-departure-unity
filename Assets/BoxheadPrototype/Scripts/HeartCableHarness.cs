using System;
using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    // Ceiling ends are fixed. Heart ends follow the actual animated heart transform.
    public sealed class HeartCableHarness : MonoBehaviour
    {
        [Serializable] public sealed class CableSpec
        {
            public string name;
            public Vector3 heartAnchor,roofAnchor,bend,vesselAxis,vesselMouth;
            public float radius, outerRadius, lumenRadius;
        }
        [Serializable] public sealed class Layout
        {
            public float ceilingHeight;
            public CableSpec[] cables;
        }
        sealed class Cable
        {
            public CableSpec spec;
            public Mesh mesh;
            public Vector3[] vertices,normals,points;
            public float[] radiusScales;
            public bool organic;
            public Renderer renderer;
            public readonly List<Cable> veins=new List<Cable>();
        }
        sealed class HardwareBatch
        {
            public readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            public readonly List<int> triangles=new List<int>();
        }
        const int Rings=121,Sides=16;
        readonly List<Cable> cables=new List<Cable>();
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        readonly Dictionary<Transform,Dictionary<Material,HardwareBatch>> hardware=new Dictionary<Transform,Dictionary<Material,HardwareBatch>>();
        static readonly Vector2[] Circle=MakeCircle();
        float nextVisualUpdate;
        Renderer[] visualRenderers;
        Transform heart;
        SuspendedCore pulseSource;
        Material rubber, living, veinMaterial;
        static Vector2[] MakeCircle()
        {
            var result=new Vector2[Sides];for(int j=0;j<Sides;j++){float a=j*Mathf.PI*2/Sides;result[j]=new Vector2(Mathf.Cos(a),Mathf.Sin(a));}return result;
        }
        static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
        {
            float s=1-t;return s*s*s*a+3*s*s*t*b+3*s*t*t*c+t*t*t*d;
        }
        static void FillTube(Vector3[] points,float radius,Vector3[] vertices,Vector3[] normals,bool corrugated=false,float[] radiusScales=null,bool organic=false)
        {
            Vector3 right=Vector3.zero;
            for(int i=0;i<points.Length;i++)
            {
                Vector3 tangent=(points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(0,i-1)]).normalized;
                right=i==0?Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.forward:Vector3.up).normalized:Vector3.ProjectOnPlane(right,tangent).normalized;
                Vector3 up=Vector3.Cross(tangent,right);
                for(int j=0;j<Sides;j++)
                {
                    Vector3 n=right*Circle[j].x+up*Circle[j].y;int index=i*Sides+j;
                    float t=i/(float)(points.Length-1),angle=j*Mathf.PI*2/Sides;
                    float rib=organic?TissueProfile(t,angle):corrugated?(i%2==0?1.08f:.97f):1;
                    vertices[index]=points[i]+n*radius*rib*(radiusScales==null?1:radiusScales[i]);normals[index]=n;
                }
            }
        }
        static float TissueProfile(float t,float angle)
        {
            float envelope=Mathf.Sin(t*Mathf.PI);
            return 1+envelope*(.17f*Mathf.Sin(angle*3+t*9)+.09f*Mathf.Sin(angle*7-t*13)+.16f*Mathf.Sin(t*17)+.08f*Mathf.Sin(t*31));
        }
        static int[] Indices(int rings)
        {
            var triangles=new int[(rings-1)*Sides*6];int k=0;
            for(int i=1;i<rings;i++)for(int j=0;j<Sides;j++)
            {
                int a=(i-1)*Sides+j,b=(i-1)*Sides+(j+1)%Sides,c=i*Sides+j,d=i*Sides+(j+1)%Sides;
                triangles[k++]=a;triangles[k++]=b;triangles[k++]=d;triangles[k++]=a;triangles[k++]=d;triangles[k++]=c;
            }
            return triangles;
        }
        Mesh MeshObject(string name,Transform parent,Material material)
        {
            var obj=new GameObject(name);obj.transform.SetParent(parent,false);
            var mesh=new Mesh{name=name};obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
            ownedMeshes.Add(mesh);return mesh;
        }
        void StaticTube(string name,Vector3[] points,float radius,Material material,Transform parent)
        {
            var vertices=new Vector3[points.Length*Sides];var normals=new Vector3[vertices.Length];FillTube(points,radius,vertices,normals);
            AddHardware(parent,material,vertices,normals,Indices(points.Length));
        }
        void AddHardware(Transform parent,Material material,Vector3[] vertices,Vector3[] normals,int[] triangles)
        {
            if(!hardware.TryGetValue(parent,out var materials)){materials=new Dictionary<Material,HardwareBatch>();hardware.Add(parent,materials);}
            if(!materials.TryGetValue(material,out var batch)){batch=new HardwareBatch();materials.Add(material,batch);}
            int offset=batch.vertices.Count;batch.vertices.AddRange(vertices);batch.normals.AddRange(normals);
            foreach(int index in triangles)batch.triangles.Add(offset+index);
        }
        void FlushHardware()
        {
            foreach(var parent in hardware)foreach(var item in parent.Value)
            {
                var mesh=MeshObject(parent.Key==heart?"Heart connector hardware":"Ceiling connector hardware",parent.Key,item.Key);
                mesh.SetVertices(item.Value.vertices);mesh.SetNormals(item.Value.normals);mesh.SetTriangles(item.Value.triangles,0);mesh.RecalculateBounds();
            }
            hardware.Clear();
        }
        void Collar(string name,Vector3 p,float radius,Material material,Transform parent,Vector3 axis=default)
        {
            if(axis.sqrMagnitude<.01f)axis=Vector3.up;
            var rotation=Quaternion.FromToRotation(Vector3.up,axis.normalized);
            var points=new Vector3[25];for(int i=0;i<points.Length;i++){float a=i*Mathf.PI*2/24;points[i]=p+rotation*new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;}
            StaticTube(name,points,.012f,material,parent);
        }
        void Plate(string name,Vector3 p,Vector3 size,Material material)
        {
            // A mesh-only plate avoids even a transient gameplay collider.
            var v=new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
            var normals=new Vector3[v.Length];for(int i=0;i<v.Length;i++){normals[i]=v[i].normalized;v[i]=p+Vector3.Scale(v[i],size)*.5f;}
            AddHardware(transform,material,v,normals,new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5});
        }
        public void Build(Transform animatedHeart,Material metal)
        {
            heart=animatedHeart;
            pulseSource=heart.GetComponentInParent<SuspendedCore>();
            var asset=Resources.Load<TextAsset>("Spaceport/HeartCableLayout");
            if(asset==null){Debug.LogWarning("Heart cable layout is missing");return;}
            var layout=JsonUtility.FromJson<Layout>(asset.text);
            // The existing Blender FBX pipeline mirrors source X on Unity import.
            // Match the imported organ while keeping the shared layout in Blender source coordinates.
            if(heart.Find("Blender anatomical heart")!=null)
                foreach(var spec in layout.cables){spec.heartAnchor.x=-spec.heartAnchor.x;spec.roofAnchor.x=-spec.roofAnchor.x;spec.bend.x=-spec.bend.x;spec.vesselAxis.x=-spec.vesselAxis.x;spec.vesselMouth.x=-spec.vesselMouth.x;}
            rubber=new Material(RuntimeShaders.Find("Universal Render Pipeline/Lit")){name="Core / insulated umbilicals"};rubber.color=new Color(.027f,.035f,.041f);rubber.SetFloat("_Smoothness",.3f);
            living=new Material(Resources.Load<Shader>("FacilityVessel")){name="Core / living vascular umbilicals"};living.SetColor("_BaseColor",new Color(.28f,.062f,.075f));living.SetFloat("_Smoothness",.56f);
            veinMaterial=new Material(living){name="Core / embedded coronary veins"};veinMaterial.SetColor("_BaseColor",new Color(.09f,.032f,.045f));
            Plate("Ceiling umbilical manifold",new Vector3(0,layout.ceilingHeight,0),new Vector3(4,.18f,3.3f),metal);
            Plate("Ceiling manifold inset",new Vector3(0,7.935f,0),new Vector3(3.7f,.045f,3.02f),rubber);
            foreach(var spec in layout.cables)
            {
                // Terminate the hanging umbilicals on the open circular carrier.
                var radial=new Vector3(spec.roofAnchor.x,0,spec.roofAnchor.z).normalized;
                spec.roofAnchor=radial*1.35f+Vector3.up*5.68f;
                var cable=new Cable{spec=spec,points=new Vector3[Rings],vertices=new Vector3[Rings*Sides],normals=new Vector3[Rings*Sides],radiusScales=new float[Rings]};
                cable.organic=cables.Count%2==0;
                cable.mesh=MeshObject(spec.name,transform,cable.organic?living:rubber);
                var uv=new Vector2[Rings*Sides];for(int ring=0;ring<Rings;ring++)for(int j=0;j<Sides;j++)uv[ring*Sides+j]=new Vector2(j/(float)Sides,ring*.04f);
                cable.mesh.vertices=cable.vertices;cable.mesh.uv=uv;cable.mesh.MarkDynamic();cable.mesh.vertices=cable.vertices;cable.mesh.triangles=Indices(Rings);cables.Add(cable);
                if(cable.organic)for(int v=0;v<3;v++)
                {
                    var detail=new Cable{points=new Vector3[Rings],vertices=new Vector3[Rings*Sides],normals=new Vector3[Rings*Sides],radiusScales=new float[Rings]};
                    detail.mesh=MeshObject(spec.name+" / embedded vein "+v,transform,veinMaterial);
                    foreach(var filter in GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh==detail.mesh){detail.renderer=filter.GetComponent<Renderer>();break;}
                    detail.mesh.vertices=detail.vertices;detail.mesh.uv=uv;detail.mesh.triangles=Indices(Rings);detail.mesh.MarkDynamic();cable.veins.Add(detail);
                }
                Vector3 top=spec.roofAnchor;
                StaticTube("Ceiling socket / "+spec.name,new[]{top-Vector3.up*.08f,top+Vector3.up*.23f},spec.radius*1.5f+.018f,metal,transform);
                for(int i=0;i<3;i++)Collar("Ceiling gland rib",top+Vector3.up*(i*.065f-.045f),spec.radius*1.5f+.021f,metal,transform);
                // Embedded sockets follow the same heart-local coordinates as the hose ends.
                Vector3 axis=spec.vesselAxis.sqrMagnitude>.01f?spec.vesselAxis.normalized:Vector3.up;
                // A straight cannula enters the lumen and remains visible beyond the lip.
                // Keep the flexible bend outside the vessel so it cannot cut across the rim.
                StaticTube("Inserted vessel cannula / "+spec.name,new[]{spec.heartAnchor,spec.vesselMouth+axis*.22f},spec.radius*1.12f,metal,heart);
                // A tapered sealed gland fills the entire lumen around the smaller hose.
                float sealRadius=Mathf.Max(spec.lumenRadius*1.06f,spec.radius*1.12f);
                var sealPoints=new[]{spec.vesselMouth-axis*.04f,spec.vesselMouth+axis*.025f,spec.vesselMouth+axis*.14f,spec.vesselMouth+axis*.23f};
                var sealVertices=new Vector3[sealPoints.Length*Sides];var sealNormals=new Vector3[sealVertices.Length];
                FillTube(sealPoints,sealRadius,sealVertices,sealNormals,false,new[]{1f,1f,spec.radius*1.15f/sealRadius,spec.radius*1.12f/sealRadius});
                AddHardware(heart,rubber,sealVertices,sealNormals,Indices(sealPoints.Length));
                Collar("Vessel sealing clamp",spec.vesselMouth+axis*.018f,sealRadius+.006f,metal,heart,axis);
                for(int i=0;i<2;i++)Collar("Exposed coupling collar",spec.vesselMouth+axis*(.09f+i*.075f),spec.radius*1.13f,metal,heart,axis);
            }
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                StaticTube("Manifold anchor bolt",new[]{new Vector3(x*1.86f,7.91f,z*1.49f),new Vector3(x*1.86f,8.08f,z*1.49f)},.055f,metal,transform);
            FlushHardware();RefreshCables();visualRenderers=GetComponentsInChildren<Renderer>();
        }
        public void RefreshCables()
        {
            if(heart==null)return;
            foreach(var cable in cables)
            {
                var spec=cable.spec;
                Vector3 axis=spec.vesselAxis.sqrMagnitude>.01f?spec.vesselAxis.normalized:Vector3.up;
                Vector3 a=transform.InverseTransformPoint(heart.TransformPoint(spec.vesselMouth+axis*.18f));
                Vector3 rise=transform.InverseTransformVector(heart.TransformVector(axis)).normalized;
                Vector3 b=a+rise*.65f,c=spec.roofAnchor-Vector3.up*.65f+spec.bend;
                Vector3 outward=new Vector3(spec.roofAnchor.x,0,spec.roofAnchor.z).normalized;
                Vector3 sideways=Vector3.Cross(Vector3.up,outward);
                for(int i=0;i<Rings;i++)
                {
                    float t=i/(float)(Rings-1);
                    // Smoothly pin both ends and their tangents at the rigid couplings.
                    float envelope=16*t*t*(1-t)*(1-t);
                    float pulse=pulseSource==null?0:pulseSource.EvaluatePulse(pulseSource.PulseSampleTime-t*.28f);
                    Vector3 flex=outward*.065f+sideways*(.025f*Mathf.Sin(t*Mathf.PI*2));
                    cable.points[i]=Bezier(a,b,c,spec.roofAnchor,t)+flex*(envelope*pulse);
                    cable.radiusScales[i]=1+.10f*envelope*pulse;
                }
                FillTube(cable.points,spec.radius,cable.vertices,cable.normals,!cable.organic,cable.radiusScales,cable.organic);
                for(int v=0;v<cable.veins.Count;v++)
                {
                    var detail=cable.veins[v];
                    detail.renderer.enabled=!RuntimeVisualBudget.Cheap;
                    if(RuntimeVisualBudget.Cheap)continue;
                    Vector3 right=Vector3.zero;
                    for(int i=0;i<Rings;i++)
                    {
                        float t=i/(float)(Rings-1),angle=v*Mathf.PI*2/3+.25f*Mathf.Sin(t*11+v)+t*.7f;
                        Vector3 tangent=(cable.points[Mathf.Min(i+1,Rings-1)]-cable.points[Mathf.Max(0,i-1)]).normalized;
                        right=i==0?Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.forward:Vector3.up).normalized:Vector3.ProjectOnPlane(right,tangent).normalized;
                        Vector3 up=Vector3.Cross(tangent,right);
                        detail.points[i]=cable.points[i]+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*spec.radius*cable.radiusScales[i]*TissueProfile(t,angle)*.98f;
                        detail.radiusScales[i]=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.45f)*(.8f+.2f*Mathf.Sin(t*19+v));
                    }
                    FillTube(detail.points,spec.radius*.105f,detail.vertices,detail.normals,false,detail.radiusScales);
                    detail.mesh.vertices=detail.vertices;detail.mesh.normals=detail.normals;detail.mesh.RecalculateBounds();
                }
                cable.mesh.vertices=cable.vertices;cable.mesh.normals=cable.normals;cable.mesh.RecalculateBounds();
            }
        }
        void LateUpdate()
        {
            if(Time.unscaledTime<nextVisualUpdate)return;
            var camera=Camera.main;
            if(camera==null||!camera.enabled)return;
            bool visible=false;
            if(visualRenderers!=null)foreach(var renderer in visualRenderers)if(renderer!=null&&renderer.isVisible){visible=true;break;}
            if(!visible){nextVisualUpdate=Time.unscaledTime+.2f;return;}
            float distance=(camera.transform.position-transform.position).sqrMagnitude;
            float interval=RuntimeVisualBudget.Minimum?.125f:RuntimeVisualBudget.Cheap?.0667f:distance>225?.1f:0;
            nextVisualUpdate=Time.unscaledTime+interval;
            RefreshCables();
            if(living!=null&&pulseSource!=null)living.SetFloat("_Pulse",pulseSource.EvaluatePulse(pulseSource.PulseSampleTime)*.6f);
        }
        void OnDestroy(){foreach(var mesh in ownedMeshes)if(mesh!=null)Destroy(mesh);if(rubber!=null)Destroy(rubber);if(living!=null)Destroy(living);if(veinMaterial!=null)Destroy(veinMaterial);}
    }
}
