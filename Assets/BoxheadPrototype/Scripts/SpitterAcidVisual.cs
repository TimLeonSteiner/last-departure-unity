using UnityEngine;
using UnityEngine.Rendering;

namespace YardSurvivalPrototype
{
    // Cosmetic only. YardSurvival continues to own collision, damage and lifetime.
    public sealed class SpitterAcidVisual : MonoBehaviour
    {
        const int Rings=25, Sides=10;
        static Material liquid, poolMaterial;
        static Mesh dropMesh;
        Mesh surface;
        MeshRenderer surfaceRenderer;
        Vector3[] vertices;
        Vector2[] uv;
        Color[] colors;
        readonly Vector3[] history=new Vector3[Rings];
        readonly Transform[] drops=new Transform[24];
        MaterialPropertyBlock properties;
        bool pool, splash, initialized;
        float age, remaining=3.6f, radius, sampleClock;
        Vector3 previous, velocity;
        int seed;
        public bool IsPool=>pool;
        public float Age=>age;
        public Mesh Surface=>surface;

        static void EnsureResources()
        {
            if(liquid!=null)return;
            var shader=Resources.Load<Shader>("SpitterBile");
            if(shader==null)throw new System.InvalidOperationException("SpitterBile shader is missing");
            liquid=new Material(shader){name="Wet olive bile",hideFlags=HideFlags.HideAndDontSave};
            poolMaterial=new Material(liquid){name="Bubbling bile surface",hideFlags=HideFlags.HideAndDontSave};
            poolMaterial.SetFloat("_Pool",1);
            // Shared low-poly droplet mesh; no primitive colliders or per-drop materials.
            dropMesh=new Mesh{name="Bile droplet",hideFlags=HideFlags.HideAndDontSave};
            var v=new Vector3[9*13];var u=new Vector2[v.Length];var c=new Color[v.Length];
            for(int y=0;y<=8;y++)for(int x=0;x<=12;x++)
            {int k=y*13+x;float a=x*Mathf.PI*2/12,b=y*Mathf.PI/8;v[k]=new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a))*.5f;u[k]=new Vector2(x/12f,y/8f);c[k]=Color.white;}
            dropMesh.vertices=v;dropMesh.uv=u;dropMesh.colors=c;dropMesh.triangles=GridTriangles(9,13);dropMesh.RecalculateNormals();
        }
        public void Initialize(bool isPool,float poolRadius,int visualSeed=0,float life=3.6f)
        {
            EnsureResources();pool=isPool;radius=poolRadius;seed=visualSeed;remaining=life;
            age=isPool?Mathf.Max(0,3.6f-life):0;previous=transform.position;
            for(int i=0;i<Rings;i++)history[i]=previous;
            surface=new Mesh{name=isPool?"Irregular bile puddle":"Broken bile stream"};surface.MarkDynamic();
            var filter=gameObject.AddComponent<MeshFilter>();filter.sharedMesh=surface;
            surfaceRenderer=gameObject.AddComponent<MeshRenderer>();surfaceRenderer.sharedMaterial=isPool?poolMaterial:liquid;
            surfaceRenderer.shadowCastingMode=ShadowCastingMode.Off;
            properties=new MaterialPropertyBlock();
            int rows=isPool?7:Rings,cols=isPool?65:Sides+1;
            vertices=new Vector3[rows*cols];uv=new Vector2[vertices.Length];colors=new Color[vertices.Length];
            for(int row=0;row<rows;row++)for(int col=0;col<cols;col++)
            {int k=row*cols+col;uv[k]=new Vector2(col/(float)(cols-1),row/(float)(rows-1));colors[k]=Color.white;}
            surface.vertices=vertices;surface.uv=uv;surface.colors=colors;surface.triangles=GridTriangles(rows,cols);
            for(int i=0;i<drops.Length;i++)
            {
                var child=new GameObject(isPool?"Surface bubble":"Bile droplet");child.transform.SetParent(transform,false);
                child.AddComponent<MeshFilter>().sharedMesh=dropMesh;
                var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=liquid;renderer.shadowCastingMode=ShadowCastingMode.Off;
                drops[i]=child.transform;
            }
            initialized=true;TickVisual(0);
            if(pool&&age<.3f)CreateSplash(transform.position,transform.parent,seed);
        }
        static int[] GridTriangles(int rows,int cols)
        {
            var indices=new int[(rows-1)*(cols-1)*6];int k=0;
            for(int y=0;y<rows-1;y++)for(int x=0;x<cols-1;x++)
            {int a=y*cols+x;indices[k++]=a;indices[k++]=a+cols;indices[k++]=a+1;indices[k++]=a+1;indices[k++]=a+cols;indices[k++]=a+cols+1;}
            return indices;
        }
        float Rand(int i)=>Mathf.Repeat(Mathf.Sin((i+seed*17)*127.13f)*43758.545f,1);
        public void SyncLifetime(float life) { remaining=life;if(pool)age=Mathf.Max(age,3.6f-life); }
        void Update(){if(initialized)TickVisual(Time.deltaTime);}
        public void TickVisual(float dt)
        {
            age+=dt;remaining-=dt;
            if(splash){AnimateSplash();return;}
            if(pool)AnimatePool();else AnimateStream(dt);
            surface.vertices=vertices;surface.colors=colors;surface.RecalculateNormals();surface.RecalculateBounds();
            properties.SetFloat("_Fade",pool?Mathf.Clamp01(remaining/.65f):1);surfaceRenderer.SetPropertyBlock(properties);
        }
        void AnimateStream(float dt)
        {
            Vector3 current=transform.position;
            if(dt>0)velocity=Vector3.Lerp(velocity,(current-previous)/dt,Mathf.Clamp01(dt*25));
            sampleClock+=dt;
            // Sample at a fixed cadence; long frames interpolate rather than adding identical points.
            int count=Mathf.Min(Rings,Mathf.FloorToInt(sampleClock/.012f));
            for(int n=0;n<count;n++)
            {for(int i=Rings-1;i>0;i--)history[i]=history[i-1];history[0]=Vector3.Lerp(previous,current,(n+1f)/count);}
            if(count>0)sampleClock%=.012f;
            history[0]=current;previous=current;
            Vector3 direction=velocity.sqrMagnitude>.001f?velocity.normalized:Vector3.forward;
            Vector3 side=Vector3.Cross(direction,Vector3.up).normalized;if(side.sqrMagnitude<.01f)side=Vector3.right;
            Vector3 up=Vector3.Cross(side,direction);
            for(int i=0;i<Rings;i++)
            {
                float along=i/(float)(Rings-1);
                float width=(.105f+.035f*Mathf.Sin(i*1.9f-age*22))*(1-along*.88f);
                width*=Mathf.Lerp(.4f,1,Mathf.SmoothStep(0,1,Mathf.Sin(i*.85f-age*15)*.5f+.5f));
                for(int j=0;j<=Sides;j++)
                {
                    float a=j*Mathf.PI*2/Sides;int k=i*(Sides+1)+j;
                    vertices[k]=history[i]-current+(side*Mathf.Cos(a)+up*Mathf.Sin(a))*(width*(1+.16f*Mathf.Sin(a*3+i)));
                    colors[k]=new Color(1,1,1,Mathf.Clamp01((1-along)*4));
                }
            }
            for(int i=0;i<drops.Length;i++)
            {
                float f=Mathf.Repeat(age*(1.5f+Rand(i)) + Rand(i+32),1);
                int h=Mathf.Clamp(2+i%21,0,Rings-1);
                Vector3 offset=side*((Rand(i+75)-.5f)*f*.65f)+up*((Rand(i+90)-.5f)*f*.4f)-Vector3.up*f*f*.2f;
                drops[i].position=history[h]+offset;
                drops[i].rotation=Quaternion.FromToRotation(Vector3.up,direction);
                float size=(.018f+Rand(i+4)*.055f)*(1-f*.5f);
                drops[i].localScale=new Vector3(size,size*(1.8f+Rand(i+50)*2),size);
            }
        }
        void AnimatePool()
        {
            float spread=Mathf.Lerp(.45f,1,Mathf.SmoothStep(0,1,age/.25f));
            for(int r=0;r<7;r++)for(int j=0;j<=64;j++)
            {
                float a=j*Mathf.PI*2/64,f=r/6f;
                float edge=.94f+.06f*Mathf.Sin(a*5+seed)+.045f*Mathf.Cos(a*9-seed)+.025f*Mathf.Sin(a*17);
                int k=r*65+j;float distance=radius*f*edge*spread;
                vertices[k]=new Vector3(Mathf.Cos(a)*distance,.008f+(.012f*Mathf.Sin(a*8+age*3)+.008f*Mathf.Cos(a*13-age*2))*f*(1-f),Mathf.Sin(a)*distance);
                colors[k]=new Color(1,1,1,r==6?.18f:1);
            }
            float fade=Mathf.Clamp01(remaining/.65f);
            for(int i=0;i<drops.Length;i++)
            {
                float a=Rand(i+3)*Mathf.PI*2,d=Mathf.Sqrt(Rand(i+10))*radius*.85f;
                float phase=Mathf.Repeat(age*(.65f+Rand(i+40)) + Rand(i+70),1);
                float size=(.065f+Rand(i+22)*.17f)*Mathf.Sin(phase*Mathf.PI)*fade;
                drops[i].localPosition=new Vector3(Mathf.Cos(a)*d*spread,.007f,Mathf.Sin(a)*d*spread);
                drops[i].localScale=new Vector3(size,size*.58f,size);
            }
        }
        public static SpitterAcidVisual CreateSplash(Vector3 point,Transform parent,int seed=0)
        {
            var obj=new GameObject("Bile impact splash");obj.transform.SetParent(parent,false);obj.transform.position=point;
            var v=obj.AddComponent<SpitterAcidVisual>();v.Initialize(false,1,seed);v.splash=true;
            // The same strip topology becomes an expanding corrugated splash crown.
            v.TickVisual(0);if(Application.isPlaying)Destroy(obj,1.1f);return v;
        }
        void AnimateSplash()
        {
            float t=age,fade=1-Mathf.Clamp01((t-.25f)/.65f);
            for(int r=0;r<Rings;r++)for(int j=0;j<=Sides;j++)
            {
                float a=r*Mathf.PI*2/(Rings-1),f=j/(float)Sides;
                float d=(.16f+t*2.8f)*f;
                float height=Mathf.Max(0,(.32f+Mathf.Sin(a*7)*.24f)*Mathf.Sin(f*Mathf.PI*.8f)+t*.65f-t*t*2)*f;
                int k=r*(Sides+1)+j;vertices[k]=new Vector3(Mathf.Cos(a)*d,height,Mathf.Sin(a)*d);
                colors[k]=new Color(1,1,1,fade*(1-f*.6f));
            }
            surface.vertices=vertices;surface.colors=colors;surface.RecalculateNormals();surface.RecalculateBounds();
            for(int i=0;i<drops.Length;i++)
            {
                float a=Rand(i+1)*Mathf.PI*2,speed=.8f+Rand(i+2)*2;
                float y=Mathf.Max(.01f,(1+Rand(i+3)*1.6f)*t-4.9f*t*t);
                drops[i].localPosition=new Vector3(Mathf.Cos(a)*speed*t,y,Mathf.Sin(a)*speed*t);
                float size=(.025f+Rand(i+4)*.06f)*fade;
                drops[i].localScale=new Vector3(size,size*(y>.02f?1.8f:.25f),size);
            }
        }
        void OnDestroy(){if(surface!=null){if(Application.isPlaying)Destroy(surface);else DestroyImmediate(surface);}}
    }
}


