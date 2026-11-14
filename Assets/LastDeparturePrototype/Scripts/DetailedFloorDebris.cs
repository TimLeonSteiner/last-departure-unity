using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace YardSurvivalPrototype
{
    // Small static props share material batches; no extra rigidbodies or collision in the aisles.
    public sealed class DetailedFloorDebris : MonoBehaviour
    {
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<Material> materials=new List<Material>();
        readonly Dictionary<Material,List<CombineInstance>> batches=new Dictionary<Material,List<CombineInstance>>();
        Mesh cube;Material steel,card,paper,rubber,board,copper,paint;
        Matrix4x4 placement;
        public int PropCount {get;private set;}
        public static void Build(Transform hall)
        {
            if(hall.Find("Detailed server floor debris")!=null)return;
            var obj=new GameObject("Detailed server floor debris");obj.transform.SetParent(hall,false);obj.AddComponent<DetailedFloorDebris>().Create();
        }
        Material Surface(string name,Color color,float metal=0)
        {
            var mat=new Material(RuntimeShaders.Find("Universal Render Pipeline/Lit")){name=name};mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metal);mat.SetFloat("_Smoothness",metal>0?.28f:.12f);
            mat.SetTexture("_BaseMap",Resources.Load<Texture2D>("Spaceport/IndustrialRoof/GalvanizedColor"));mat.SetFloat("_Cull",0);materials.Add(mat);return mat;
        }
        void Add(Mesh mesh,Material mat,Matrix4x4 local)
        {if(!batches.TryGetValue(mat,out var list))batches[mat]=list=new List<CombineInstance>();list.Add(new CombineInstance{mesh=mesh,transform=placement*local});}
        void Box(Vector3 p,Vector3 size,Material mat,Quaternion? rotation=null)
        {Add(cube,mat,Matrix4x4.TRS(p,rotation??Quaternion.identity,size));}
        Mesh Mesh(List<Vector3> vertices,List<int> triangles)
        {
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
            var uv=new List<Vector2>();foreach(var v in vertices)uv.Add(new Vector2(v.x*3+v.y,v.z*3+v.y));mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);return mesh;
        }
        void Tube(List<Vector3> path,float radius,Material mat,bool closed=false)
        {
            var v=new List<Vector3>();var t=new List<int>();const int sides=8;
            for(int i=0;i<path.Count;i++)
            {
                var tangent=(path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(0,i-1)]).normalized;
                var right=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.right:Vector3.up).normalized;var up=Vector3.Cross(tangent,right);
                for(int j=0;j<sides;j++){float a=j*Mathf.PI*2/sides;v.Add(path[i]+radius*(right*Mathf.Cos(a)+up*Mathf.Sin(a)));}
                if(i==0)continue;for(int j=0;j<sides;j++){int a=(i-1)*sides+j,b=(i-1)*sides+(j+1)%sides;t.AddRange(new[]{a,b,a+sides,b,b+sides,a+sides});}
            }
            Add(Mesh(v,t),mat,Matrix4x4.identity);
        }
        void Sheet(float width,float depth,float fold,Material mat)
        {
            var v=new List<Vector3>();var t=new List<int>();const int n=5;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                float px=(x/(float)(n-1)-.5f)*width,pz=(z/(float)(n-1)-.5f)*depth;
                float lift=.012f+Mathf.Abs(px/width)*fold+Mathf.Sin(x*2.2f+z*1.8f)*.008f;
                if(x==0||x==n-1)px+=Mathf.Sin(z*4.3f+x)*.018f;
                v.Add(new Vector3(px,lift,pz));
                if(x>0&&z>0){int a=(z-1)*n+x-1;t.AddRange(new[]{a,a+n,a+1,a+1,a+n,a+n+1});}
            }
            Add(Mesh(v,t),mat,Matrix4x4.identity);
        }
        void Can()
        {
            var v=new List<Vector3>();var t=new List<int>();const int rings=9,sides=24;
            for(int i=0;i<rings;i++)for(int j=0;j<sides;j++)
            {
                float a=j*Mathf.PI*2/sides,x=-.13f+i*.0325f;
                float dent=1-.32f*Mathf.Sin(i*Mathf.PI/(rings-1))*Mathf.Pow(.5f+.5f*Mathf.Cos(a-1),5);
                float r=.068f*dent*(i==0||i==rings-1?.90f:1);
                v.Add(new Vector3(x,.075f+Mathf.Cos(a)*r,Mathf.Sin(a)*r));
                if(i>0){int b=(i-1)*sides+j,c=(i-1)*sides+(j+1)%sides;t.AddRange(new[]{b,c,b+sides,c,c+sides,b+sides});}
            }
            Add(Mesh(v,t),paint,Matrix4x4.identity);
            foreach(int sign in new[]{-1,1})
            {
                var ring=new List<Vector3>();for(int j=0;j<=24;j++){float a=j*Mathf.PI/12;ring.Add(new Vector3(sign*.13f,.075f+Mathf.Cos(a)*.062f,Mathf.Sin(a)*.062f));}Tube(ring,.004f,steel);
                var cap=new List<Vector3>{new Vector3(sign*.13f,.075f,0)};cap.AddRange(ring);var face=new List<int>();for(int j=1;j<25;j++)face.AddRange(new[]{0,j,j+1});Add(Mesh(cap,face),steel,Matrix4x4.identity);
            }
            Box(new Vector3(.135f,.085f,0),new Vector3(.004f,.036f,.022f),rubber);
            Box(new Vector3(.139f,.064f,0),new Vector3(.006f,.027f,.017f),steel,Quaternion.Euler(12,0,0));
            for(int i=0;i<3;i++)Box(new Vector3(-.045f+i*.027f,.139f,-.006f),new Vector3(.014f,.002f,.065f),paper);
        }
        void CircuitBoard()
        {
            var v=new List<Vector3>{new Vector3(-.18f,.015f,-.12f),new Vector3(-.18f,.015f,.12f),new Vector3(.16f,.015f,.12f),new Vector3(.18f,.015f,.02f),new Vector3(.10f,.015f,-.035f),new Vector3(.13f,.015f,-.12f)};
            Add(Mesh(v,new List<int>{0,1,2,0,2,3,0,3,4,0,4,5}),board,Matrix4x4.identity);
            for(int row=0;row<3;row++)for(int col=0;col<3;col++)
            {
                float x=-.12f+col*.08f,z=-.075f+row*.07f;
                Box(new Vector3(x,.025f,z),new Vector3(.05f,.018f,.036f),rubber);
                for(int pin=-2;pin<=2;pin++)foreach(int side in new[]{-1,1})Box(new Vector3(x+pin*.009f,.020f,z+side*.023f),new Vector3(.003f,.004f,.014f),steel);
                Box(new Vector3(x+.034f,.017f,z+.02f),new Vector3(.003f,.002f,.09f),copper);
            }
            for(int i=0;i<12;i++)Box(new Vector3(-.14f+i*.021f,.017f,.11f),new Vector3(.012f,.003f,.025f),copper);
        }
        void Cable()
        {
            var path=new List<Vector3>();for(int i=0;i<=24;i++){float t=i/24f;path.Add(new Vector3((t-.5f)*.7f,.018f+Mathf.Sin(t*Mathf.PI)*.015f,Mathf.Sin(t*7)*.13f));}Tube(path,.009f,rubber);
            Box(path[0],new Vector3(.045f,.025f,.030f),steel);Box(path[0]+Vector3.right*.03f,new Vector3(.035f,.024f,.035f),rubber);
            for(int i=-1;i<=1;i++){var end=path[path.Count-1];Tube(new List<Vector3>{end,end+new Vector3(.045f,.006f,i*.012f)},.002f,copper);}
        }
        void Packaging(int variant)
        {
            Sheet(.36f,.44f,variant==0?.08f:.025f,variant==0?card:paper);
            if(variant==0)
            {
                Box(new Vector3(-.16f,.048f,0),new Vector3(.015f,.075f,.40f),card,Quaternion.Euler(0,0,-14));
                Box(new Vector3(.10f,.023f,.04f),new Vector3(.10f,.003f,.16f),paper);
                for(int i=0;i<7;i++)Box(new Vector3(.07f+i*.01f,.027f,.045f),new Vector3(i%3==0?.005f:.002f,.001f,.075f),rubber);
            }
            else for(int i=0;i<6;i++)Box(new Vector3(-.025f,.032f,-.12f+i*.035f),new Vector3(.17f-i%3*.025f,.001f,.003f),rubber);
        }
        void FanGrille()
        {
            foreach(int sign in new[]{-1,1})
            {Box(new Vector3(sign*.15f,.025f,0),new Vector3(.025f,.018f,.32f),steel);Box(new Vector3(0,.025f,sign*.15f),new Vector3(.32f,.018f,.025f),steel);}
            for(int i=-4;i<=4;i++)if(i!=2)Box(new Vector3(i*.028f,.028f,0),new Vector3(.008f,.014f,.27f),steel,Quaternion.Euler(i==1?9:0,0,0));
            var ring=new List<Vector3>();for(int j=0;j<=24;j++){float a=j*Mathf.PI/12;ring.Add(new Vector3(Mathf.Cos(a)*.11f,.045f,Mathf.Sin(a)*.11f));}Tube(ring,.009f,rubber);
            for(int i=0;i<5;i++)Box(Vector3.up*.04f,new Vector3(.18f,.012f,.04f),rubber,Quaternion.Euler(0,i*72,0));
        }
        void Create()
        {
            steel=Surface("Scratched scrap metal",new Color(.32f,.34f,.34f),.65f);card=Surface("Torn stained cardboard",new Color(.30f,.22f,.12f));paper=Surface("Dirty packing labels",new Color(.55f,.52f,.42f));rubber=Surface("Cable insulation and chips",new Color(.025f,.028f,.026f));board=Surface("Broken circuit board",new Color(.065f,.15f,.085f));copper=Surface("Exposed copper and contacts",new Color(.45f,.28f,.10f),.65f);paint=Surface("Crushed faded red cans",new Color(.28f,.055f,.025f),.35f);
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);primitive.SetActive(false);cube=primitive.GetComponent<MeshFilter>().sharedMesh;Destroy(primitive);
            var random=new System.Random(271113);var spots=new List<Vector3>{new Vector3(7.2f,0,20.3f),new Vector3(9.1f,0,20.5f)};
            for(int side=0;side<4;side++)foreach(float x in new[]{-16f,-10f,12f,17f})spots.Add(Quaternion.Euler(0,side*90,0)*new Vector3(x,0,20.2f));
            foreach(var spot in spots)for(int prop=0;prop<6;prop++)
            {
                float x=(float)random.NextDouble()*1.3f-.65f,z=(float)random.NextDouble()*.8f-.4f;
                placement=Matrix4x4.TRS(spot+new Vector3(x,.012f,z),Quaternion.Euler(0,random.Next(360),0),Vector3.one*Mathf.Lerp(.8f,1.15f,(float)random.NextDouble()));
                switch(prop){case 0:Can();break;case 1:CircuitBoard();break;case 2:Cable();break;case 3:Packaging(0);break;case 4:Packaging(1);break;default:FanGrille();break;}PropCount++;
            }
            foreach(var pair in batches)
            {
                var mesh=new Mesh{name=pair.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray());meshes.Add(mesh);
                var obj=new GameObject(pair.Key.name);obj.transform.SetParent(transform,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key;renderer.allowOcclusionWhenDynamic=false;
            }
            batches.Clear();
        }
        void OnDestroy(){foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);foreach(var mat in materials)if(mat!=null)Destroy(mat);}
    }
}
