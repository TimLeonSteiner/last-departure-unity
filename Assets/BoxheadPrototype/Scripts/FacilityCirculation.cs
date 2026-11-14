using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace YardSurvivalPrototype
{
    public sealed class FacilityCirculation : MonoBehaviour
    {
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Vector2> uv=new List<Vector2>();
        readonly List<int> triangles=new List<int>();
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<Material> materials=new List<Material>();
        readonly List<Vector3[]> surfaceVeins=new List<Vector3[]>();
        int builtPreset;
        SuspendedCore core;
        Quaternion rotation;
        public int BranchCount {get;private set;}
        public int MembraneCount {get;private set;}
        public int JunctionCount {get;private set;}
        public static void Build(SuspendedCore source)
        {
            if(source.transform.Find("Facility circulation")!=null)return;
            var obj=new GameObject("Facility circulation");obj.transform.SetParent(source.transform,false);
            var network=obj.AddComponent<FacilityCirculation>();network.core=source;network.Create();
        }
        void Create()
        {
            builtPreset=RuntimeVisualBudget.Preset;
            var artery=new Material(Resources.Load<Shader>("FacilityVessel")){name="Facility / arterial tissue"};
            artery.SetColor("_BaseColor",new Color(.32f,.065f,.075f));artery.SetFloat("_Smoothness",.63f);materials.Add(artery);

            // The authored cabinets are centred at (+/-10, +/-9) * 23/17 metres.
            // Their roof is 2.64m high and their front fascia is 2.71m from centre.
            rotation=Quaternion.identity;
            for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)
            {
                float x=sx*10*23f/17,z=sz*9*23f/17;
                // Keep growth local to the cabinets; no overhead or wall feeders.
                var roof=new Vector3(x-sx*2.3f,2.76f,z-sz*.7f);
                Junction(roof,new Vector3(.55f,.19f,.43f));
                for(int face=-1;face<=1;face+=2)
                {
                    var contact=new Vector3(x-sx*2.3f,2.52f,z+face*2.77f);
                    Tube(new[]{roof,new Vector3(roof.x+.3f,2.79f,z+face*1.7f),contact},.14f,.10f);
                    var trunk=new[]{contact,contact+new Vector3(.12f,-.36f,face*.07f),contact+new Vector3(-.16f,-.88f,face*.035f),contact+new Vector3(.08f,-1.5f,face*.02f),contact+new Vector3(-.12f,-2.12f,0)};
                    Tube(trunk,.16f,.012f);
                    for(int branch=0;branch<6;branch++)
                    {
                        int side=branch%2==0?-1:1;
                        var start=trunk[1+branch/2];
                        var elbow=start+new Vector3(side*(.38f+.12f*(branch%3)),-.18f,face*.04f);
                        var end=elbow+new Vector3(side*.45f,-.45f-.12f*(branch%2),-face*.025f);
                        Tube(new[]{start,elbow,end},.085f,.009f);
                        for(int twig=0;twig<3;twig++)
                        {
                            var twigRoot=Vector3.Lerp(elbow,end,twig*.27f);
                            Tube(new[]{twigRoot,twigRoot+new Vector3(-side*.08f,-.16f,0),twigRoot+new Vector3(-side*.24f,-.42f,0)},.024f,.002f);
                        }
                    }

                }
            }
            Flush("Server cabinet vascular growth",artery);
            var vein=new Material(artery){name="Facility / raised subdermal veins"};
            vein.SetColor("_BaseColor",new Color(.095f,.035f,.05f));materials.Add(vein);
            foreach(var path in surfaceVeins)Tube(path,.014f,.003f);
            surfaceVeins.Clear();
            Flush("Raised embedded vascular detail",vein);
            for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)
            {
                float x=sx*10*23f/17,z=sz*9*23f/17;
                for(int face=-1;face<=1;face+=2)
                {
                    var contact=new Vector3(x-sx*2.3f,2.38f,z+face*2.74f);
                    for(int pad=0;pad<5;pad++)
                    {
                        var centre=contact+new Vector3(.13f*Mathf.Sin(pad*5),-.28f*pad,0);
                        Junction(centre,new Vector3(.26f-.027f*pad,.31f,.065f));
                    }
                }
            }
            var fascia=new Material(artery){name="Facility / adherent fascia"};
            fascia.SetColor("_BaseColor",new Color(.24f,.09f,.085f));fascia.SetFloat("_Smoothness",.38f);materials.Add(fascia);
            Flush("Adherent tissue at cabinet connections",fascia);
        }
        void Junction(Vector3 centre,Vector3 scale)
        {
            JunctionCount++;int start=vertices.Count;int rows=RuntimeVisualBudget.Cheap?8:14,sides=RuntimeVisualBudget.Cheap?12:24;
            for(int i=0;i<=rows;i++)for(int j=0;j<=sides;j++)
            {
                float a=j*Mathf.PI*2/sides,t=i*Mathf.PI/rows;
                float lobe=1+.22f*Mathf.Sin(a*3+t*4)*Mathf.Sin(t)+.065f*Mathf.Sin(a*9-t*7)*Mathf.Sin(t);
                var p=new Vector3(Mathf.Sin(t)*Mathf.Cos(a),Mathf.Cos(t),Mathf.Sin(t)*Mathf.Sin(a));
                vertices.Add(rotation*(centre+Vector3.Scale(p,scale)*lobe));uv.Add(new Vector2(j/(float)sides,i*.12f));
                if(i==rows||j==sides)continue;int b=start+i*(sides+1)+j;
                triangles.AddRange(new[]{b,b+1,b+sides+1,b+1,b+sides+2,b+sides+1});
            }
        }
        void Tube(Vector3[] anchors,float radius,float tip)
        {
            BranchCount++;var points=new List<Vector3>();
            int samples=anchors.Length>20?1:radius>.09f?(RuntimeVisualBudget.Cheap?8:24):3;
            for(int k=0;k<anchors.Length-1;k++)for(int j=0;j<samples;j++)
            {
                float t=j/(float)samples;Vector3 a=anchors[Mathf.Max(k-1,0)],b=anchors[k],c=anchors[k+1],d=anchors[Mathf.Min(k+2,anchors.Length-1)];
                points.Add(rotation*(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t)));
            }
            points.Add(rotation*anchors[anchors.Length-1]);
            int start=vertices.Count;int sides=RuntimeVisualBudget.Cheap?(radius>.09f?12:6):(radius>.12f?48:radius>.04f?20:7);Vector3 right=Vector3.zero;float distance=0;
            var tracks=new List<Vector3>[] {new List<Vector3>(),new List<Vector3>(),new List<Vector3>()};
            for(int i=0;i<points.Count;i++)
            {
                Vector3 tangent=(points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(i-1,0)]).normalized;
                right=i==0?Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.forward:Vector3.up).normalized:Vector3.ProjectOnPlane(right,tangent).normalized;
                Vector3 up=Vector3.Cross(tangent,right);if(i>0)distance+=Vector3.Distance(points[i],points[i-1]);
                float r=Mathf.Lerp(radius,tip,i/(float)(points.Count-1))*(1+.065f*Mathf.Sin(distance*3.7f)+.025f*Mathf.Sin(distance*17));
                for(int j=0;j<sides;j++)
                {
                    float a=j*Mathf.PI*2/sides;float relief=radius>.09f?OrganicRadius(a,distance):1;vertices.Add(points[i]+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*r*relief);uv.Add(new Vector2(j/(float)sides,distance));
                    if(i==0)continue;int b=start+(i-1)*sides+j,c=start+(i-1)*sides+(j+1)%sides;
                    triangles.AddRange(new[]{b,c,c+sides,b,c+sides,b+sides});
                }
                if(radius>.09f&&!RuntimeVisualBudget.Cheap)for(int v=0;v<3;v++)
                {
                    float angle=v*Mathf.PI*2/3+.28f*Mathf.Sin(distance*1.6f+v)+distance*.13f;
                    tracks[v].Add(points[i]+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*r*OrganicRadius(angle,distance)*.99f);
                }
            }
            if(radius>.09f&&!RuntimeVisualBudget.Cheap)foreach(var track in tracks)surfaceVeins.Add(track.ToArray());
        }
        static float OrganicRadius(float angle,float length)
        {
            float bundle=.14f*Mathf.Sin(angle*3+length*.9f)+.065f*Mathf.Sin(angle*7-length*1.7f);
            float groove=.09f*Mathf.Pow(.5f+.5f*Mathf.Sin(angle*5+length*.8f),12);
            float knot=.14f*Mathf.Sin(length*2.3f)+.06f*Mathf.Sin(length*5.7f);
            return 1+bundle-groove+knot;
        }
        void Flush(string name,Material material)
        {
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var bounds=mesh.bounds;bounds.Expand(.05f);mesh.bounds=bounds;meshes.Add(mesh);
            var obj=new GameObject(name);obj.transform.SetParent(transform,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
            vertices.Clear();triangles.Clear();uv.Clear();
        }
        void Update()
        {
            if(builtPreset!=RuntimeVisualBudget.Preset)
            {
                foreach(Transform child in transform){child.gameObject.SetActive(false);Destroy(child.gameObject);}
                foreach(var mesh in meshes)Destroy(mesh);foreach(var material in materials)Destroy(material);
                meshes.Clear();materials.Clear();surfaceVeins.Clear();vertices.Clear();uv.Clear();triangles.Clear();
                BranchCount=MembraneCount=JunctionCount=0;Create();
            }
            if(core!=null)foreach(var material in materials)material.SetFloat("_Pulse",core.EvaluatePulse(core.PulseSampleTime)*.65f);
        }
        void OnDestroy(){foreach(var mesh in meshes)Destroy(mesh);foreach(var material in materials)Destroy(material);}
    }
}
