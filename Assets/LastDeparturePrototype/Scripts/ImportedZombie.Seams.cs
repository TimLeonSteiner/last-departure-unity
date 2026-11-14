using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class ImportedZombie
    {
        static Mesh seamMesh;
        static Material seamMaterial;
        void SeverSeams(Transform joint,Transform detached,int part)
        {
            if(seamMesh==null)
            {
                const int n=40,rings=5;var vertices=new Vector3[1+n*rings];var uv=new Vector2[vertices.Length];var triangles=new int[n*3+(rings-1)*n*6];
                vertices[0]=Vector3.back*.16f;uv[0]=Vector2.one*.5f;
                for(int ring=0;ring<rings;ring++)for(int i=0;i<n;i++)
                {
                    float angle=i*Mathf.PI*2/n, ringRadius=(ring+1)/(float)rings*(1+.035f*Mathf.Sin(i*2.7f));int at=1+ring*n+i;
                    vertices[at]=new Vector3(Mathf.Cos(angle)*ringRadius,Mathf.Sin(angle)*ringRadius,ring==rings-1?-.12f:ring==rings-2?.18f:0);
                    uv[at]=new Vector2(vertices[at].x,vertices[at].y)*.5f+Vector2.one*.5f;
                }
                int t=0;for(int i=0;i<n;i++){triangles[t++]=0;triangles[t++]=1+i;triangles[t++]=1+(i+1)%n;}
                for(int ring=0;ring<rings-1;ring++)for(int i=0;i<n;i++)
                {int a=1+ring*n+i,b=1+ring*n+(i+1)%n,c=a+n,d=b+n;triangles[t++]=a;triangles[t++]=c;triangles[t++]=b;triangles[t++]=b;triangles[t++]=c;triangles[t++]=d;}
                seamMesh=new Mesh{name="Rounded irregular wound rim"};seamMesh.vertices=vertices;seamMesh.uv=uv;seamMesh.triangles=triangles;seamMesh.RecalculateNormals();seamMesh.RecalculateBounds();
            }
            if(seamMaterial==null)
            {
                seamMaterial=new Material(woundMaterial);seamMaterial.name="Detailed severed tissue";
                var texture=new Texture2D(128,128,TextureFormat.RGBA32,false);texture.name="Mottled wound detail";var pixels=new Color[128*128];
                for(int y=0;y<128;y++)for(int x=0;x<128;x++){float u=x/127f,v=y/127f;float grain=Mathf.PerlinNoise(u*27,v*27),veins=Mathf.Pow(Mathf.Abs(Mathf.Sin(u*37+Mathf.PerlinNoise(u*7,v*9)*8)),8);float rim=Mathf.Clamp01((new Vector2(u-.5f,v-.5f).magnitude-.36f)*10);pixels[y*128+x]=Color.Lerp(new Color(.16f+.18f*grain,.025f+.045f*grain,.035f+.03f*grain),new Color(.32f,.095f,.08f),Mathf.Max(rim*.7f,veins*.18f));}
                // Offset bone cross-section and a darker cavity give the cut depth
                // while the outer mottled ring reads as torn skin and clothing.
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                {
                    float u=x/127f,v=y/127f;
                    float bone=new Vector2((u-.48f)*1.15f,v-.53f).magnitude;
                    float edge=.115f+.008f*Mathf.Sin(Mathf.Atan2(v-.53f,u-.48f)*7);
                    if(bone<edge)
                    {
                        float marrow=Mathf.SmoothStep(.035f,.065f,bone);
                        pixels[y*128+x]=Color.Lerp(new Color(.18f,.045f,.035f),new Color(.64f,.52f,.37f),marrow);
                    }
                    else if(bone<edge+.025f)pixels[y*128+x]*=.48f;
                }
                texture.SetPixels(pixels);texture.Apply(false,true);seamMaterial.SetTexture("_BaseMap",texture);seamMaterial.SetColor("_BaseColor",Color.white);seamMaterial.SetFloat("_Smoothness",.38f);
                if(seamMaterial.HasProperty("_Cull"))seamMaterial.SetFloat("_Cull",0);
            }
            Vector3 axis=joint.childCount>0?joint.GetChild(0).position-joint.position:joint.up;
            if(axis.sqrMagnitude<.0001f)axis=joint.up;
            float radius=part==0?.096f:part<3?.068f:.09f;
            // The stump follows the surviving torso/hip, not the now-hidden limb's
            // animated bone. The matching cut on the loose limb follows its physics.
            AddSeam(joint.parent!=null?joint.parent:joint,joint.position,axis,radius);
            AddSeam(detached,joint.position,-axis,radius);
        }
        void AddSeam(Transform parent,Vector3 position,Vector3 normal,float radius)
        {
            var obj=new GameObject("Detailed cut surface");obj.transform.position=position;obj.transform.rotation=Quaternion.LookRotation(normal);obj.transform.localScale=new Vector3(radius,radius*.88f,.025f);obj.transform.SetParent(parent,true);
            obj.AddComponent<MeshFilter>().sharedMesh=seamMesh;obj.AddComponent<MeshRenderer>().sharedMaterial=seamMaterial;
        }
    }
}
