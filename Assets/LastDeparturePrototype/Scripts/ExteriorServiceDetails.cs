using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace YardSurvivalPrototype
{
    // Decorative service bays on existing apron pads, outside the approach lanes.
    public sealed class ExteriorServiceDetails : MonoBehaviour
    {
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<Light> lamps=new List<Light>();
        readonly Dictionary<PrimitiveType,Mesh> primitives=new Dictionary<PrimitiveType,Mesh>();
        readonly Dictionary<Material,List<CombineInstance>> batches=new Dictionary<Material,List<CombineInstance>>();
        Material steel,dark,rust,hull,amber;
        Transform sector;
        public static ExteriorServiceDetails Build(Transform parent)
        {
            var old=parent.Find("Exterior maintenance stories");
            if(old!=null)return old.GetComponent<ExteriorServiceDetails>();
            var root=new GameObject("Exterior maintenance stories");root.transform.SetParent(parent,false);
            var detail=root.AddComponent<ExteriorServiceDetails>();detail.Create();return detail;
        }
        void Shape(PrimitiveType type,Vector3 p,Vector3 scale,Material material,Quaternion rotation)
        {
            if(material==null)return;
            if(!primitives.TryGetValue(type,out var mesh))
            {
                var temp=GameObject.CreatePrimitive(type);temp.SetActive(false);
                mesh=temp.GetComponent<MeshFilter>().sharedMesh;primitives[type]=mesh;
                Destroy(temp);
            }
            if(!batches.TryGetValue(material,out var list))batches[material]=list=new List<CombineInstance>();
            list.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(p,rotation,scale)});
        }
        void Box(Vector3 p,Vector3 s,Material m, float yaw=0)=>Shape(PrimitiveType.Cube,p,s,m,Quaternion.Euler(0,yaw,0));
        void Pipe(Vector3 a,Vector3 b,float r,Material m)=>Shape(PrimitiveType.Cylinder,(a+b)*.5f,new Vector3(r*2,(b-a).magnitude*.5f,r*2),m,Quaternion.FromToRotation(Vector3.up,b-a));
        void Cable(Vector3 a,Vector3 b,float sag,float r,Material m)
        {
            for(int i=0;i<16;i++)
            {
                Vector3 Point(float t)=>Vector3.Lerp(a,b,t)-Vector3.up*(Mathf.Sin(t*Mathf.PI)*sag);
                Pipe(Point(i/16f),Point((i+1)/16f),r,m);
            }
        }
        void Ring(Vector3 center,float radius,Material m,bool vertical=false)
        {
            for(int i=0;i<20;i++)
            {
                Vector3 Point(float a)=>center+(vertical?new Vector3(Mathf.Cos(a),Mathf.Sin(a),0):new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)))*radius;
                Pipe(Point(i*Mathf.PI/10),Point((i+1)*Mathf.PI/10),.045f,m);
            }
        }
        void Drum(Vector3 p)
        {
            Pipe(p,p+Vector3.up*1.2f,.38f,rust);
            foreach(float y in new[]{.10f,.32f,.94f,1.16f})Ring(p+Vector3.up*y,.39f,steel);
            Pipe(p+new Vector3(.16f,1.2f,0),p+new Vector3(.16f,1.24f,0),.055f,dark);
        }
        void Crate(Vector3 p,Vector3 size)
        {
            Box(p+Vector3.up*(size.y*.5f),size,hull);
            foreach(float x in new[]{-.42f,.42f})Box(p+new Vector3(size.x*x,size.y*.5f,0),new Vector3(.08f,size.y+.05f,size.z+.07f),steel);
            foreach(float y in new[]{.12f,size.y-.12f})Box(p+Vector3.up*y,new Vector3(size.x+.05f,.06f,size.z+.07f),dark);
            Box(p+new Vector3(0,size.y*.55f,-size.z*.5f-.015f),new Vector3(size.x*.3f,.18f,.025f),dark);
        }
        void Create()
        {
            steel=Resources.Load<Material>("Spaceport/Steel");dark=Resources.Load<Material>("Spaceport/Ink");
            rust=Resources.Load<Material>("Spaceport/Rust");hull=Resources.Load<Material>("Spaceport/Hull");amber=Resources.Load<Material>("Spaceport/AmberGlow");
            for(int side=0;side<4;side++)
            {
                sector=new GameObject(new[]{"Launch servicing","Coolant maintenance","Freight handling","Relay power"}[side]).transform;
                sector.SetParent(transform,false);sector.localRotation=Quaternion.Euler(0,side*90,0);
                // Bridge columns remain on the solid side pads; the open road stays clear.
                foreach(float x in new[]{-11f,11f})
                {
                    Box(new Vector3(x,.12f,48),new Vector3(1,.24f,1),dark);
                    Box(new Vector3(x,3.05f,48),new Vector3(.24f,5.9f,.24f),steel);
                    for(int i=0;i<7;i++)Box(new Vector3(x, .4f+i*.33f,47.86f),new Vector3(.26f,.12f,.035f),i%2==0?hull:rust);
                    Pipe(new Vector3(x,4.5f,48),new Vector3(x-Mathf.Sign(x)*1.5f,6,48),.075f,steel);
                }
                Box(new Vector3(0,6,48),new Vector3(22,.25f,.45f),steel);
                for(int cable=0;cable<3;cable++)Cable(new Vector3(-10.8f,5.8f,48+(.18f*cable)),new Vector3(10.8f,5.8f,48+(.18f*cable)),.5f+side*.12f,.035f,dark);
                // Long supply pipes follow the left pad into the utility bridge.
                for(int line=0;line<2;line++)
                {
                    float x=-12+line*.45f;
                    Pipe(new Vector3(x,.35f,39),new Vector3(x,.35f,49),.13f,line==0?rust:steel);
                    Pipe(new Vector3(x,.35f,49),new Vector3(x,4.7f,49),.13f,steel);
                    foreach(float z in new[]{40f,44f,48f})
                    {
                        Box(new Vector3(x,.12f,z),new Vector3(.55f,.24f,.5f),dark);
                        Pipe(new Vector3(x,.35f,z-.06f),new Vector3(x,.35f,z+.06f),.2f,steel);
                    }
                }
                if(side==1)
                {
                    foreach(float x in new[]{9f,10.3f,11.6f})
                    {
                        Pipe(new Vector3(x,0,46),new Vector3(x,.14f,46),.52f,dark);
                        Pipe(new Vector3(x,.1f,46),new Vector3(x,2.9f,46),.45f,hull);
                        foreach(float y in new[]{.22f,1.85f,2.8f})Pipe(new Vector3(x,y-.04f,46),new Vector3(x,y+.04f,46),.475f,steel);
                        Pipe(new Vector3(x,2.9f,46),new Vector3(x,3.03f,46),.16f,steel);
                        for(int bolt=0;bolt<8;bolt++)
                        {
                            float angle=bolt*Mathf.PI/4;
                            Box(new Vector3(x+Mathf.Cos(angle)*.39f,2.87f,46+Mathf.Sin(angle)*.39f),new Vector3(.06f,.05f,.06f),rust);
                        }
                        Pipe(new Vector3(x,.18f,46),new Vector3(x,.18f,44.5f),.12f,steel);
                        Pipe(new Vector3(x,1.4f,45.55f),new Vector3(x,1.4f,45.2f),.055f,steel);
                        Ring(new Vector3(x,1.4f,45.18f),.25f,rust,true);
                        Pipe(new Vector3(x-.25f,1.4f,45.18f),new Vector3(x+.25f,1.4f,45.18f),.035f,rust);
                    }
                }
                else if(side==3)
                {
                    for(int j=0;j<3;j++)
                    {
                        var c=new Vector3(9+j*1.4f,.78f,46);
                        Pipe(c+Vector3.back*.55f,c+Vector3.forward*.55f,.6f,dark);
                        foreach(float z in new[]{-.58f,.58f})Pipe(c+Vector3.forward*(z-.04f),c+Vector3.forward*(z+.04f),.78f,hull);
                        for(int i=0;i<6;i++)Ring(c+Vector3.forward*(-.4f+i*.16f),.60f,steel,true);
                        Box(c+Vector3.down*.66f,new Vector3(1.6f,.22f,1.45f),dark);
                    }
                }
                else
                {
                    Crate(new Vector3(9,.08f,45),new Vector3(1.8f,1.1f,1.3f));
                    Crate(new Vector3(11.2f,.08f,46.5f),new Vector3(1.6f,1.5f,1.5f));
                    if(side==2)Crate(new Vector3(11.2f,1.58f,46.5f),new Vector3(1.4f,.75f,1.2f));
                    Drum(new Vector3(9.2f,0,48));Drum(new Vector3(10.15f,0,48.5f));
                }
                // Open service cabinet, a detached cover, and windblown sheet fragments.
                Box(new Vector3(-9.1f,1.15f,45.5f),new Vector3(1.5f,2.3f,.8f),steel);
                Box(new Vector3(-9.1f,1.15f,45.07f),new Vector3(1.28f,2.05f,.06f),dark);
                for(int i=0;i<6;i++)Box(new Vector3(-9.1f,.45f+i*.27f,45.02f),new Vector3(1.1f,.12f,.06f),rust);
                Box(new Vector3(-10.2f,.04f,44),new Vector3(1.3f,.07f,2.05f),steel,18);
                var random=new System.Random(117+side);
                for(int i=0;i<15;i++)
                {
                    float x=(i%2==0?-1:1)*(8.2f+(float)random.NextDouble()*4.5f),z=40+(float)random.NextDouble()*10;
                    Box(new Vector3(x,.026f,z),new Vector3(.13f+(float)random.NextDouble()*.35f,.045f,.25f+(float)random.NextDouble()*.45f),i%3==0?rust:hull,(float)random.NextDouble()*180);
                }
                Box(new Vector3(-9,3.5f,45.5f),new Vector3(.65f,.16f,.45f),dark);
                Box(new Vector3(-9,3.40f,45.5f),new Vector3(.48f,.025f,.32f),amber);
                Pipe(new Vector3(-9,2.3f,45.8f),new Vector3(-9,3.5f,45.8f),.055f,steel);
                var lampObject=new GameObject("Shielded maintenance lamp");lampObject.transform.SetParent(sector,false);
                lampObject.transform.localPosition=new Vector3(-9,3.36f,45.5f);lampObject.transform.localRotation=Quaternion.LookRotation(new Vector3(0,-3,-.8f));
                var lamp=lampObject.AddComponent<Light>();lamp.type=LightType.Spot;lamp.color=new Color(1,.61f,.30f);lamp.intensity=65;lamp.range=7;lamp.spotAngle=90;lamp.innerSpotAngle=45;lamp.shadows=LightShadows.None;lamp.cullingMask=~(1<<30);lamps.Add(lamp);
                // A small hooded lamp reveals the equipment without lifting the outdoor exposure.
                Box(new Vector3(11,4.5f,47.7f),new Vector3(.5f,.16f,.4f),dark);
                Box(new Vector3(11,4.4f,47.7f),new Vector3(.38f,.025f,.28f),amber);
                var equipmentLamp=new GameObject("Equipment inspection lamp");equipmentLamp.transform.SetParent(sector,false);
                equipmentLamp.transform.localPosition=new Vector3(11,4.35f,47.7f);
                equipmentLamp.transform.localRotation=Quaternion.LookRotation(new Vector3(-1,-4,-1.7f));
                var equipmentLight=equipmentLamp.AddComponent<Light>();equipmentLight.type=LightType.Spot;
                equipmentLight.color=new Color(1,.65f,.38f);equipmentLight.intensity=65;equipmentLight.range=7;
                equipmentLight.spotAngle=90;equipmentLight.innerSpotAngle=48;equipmentLight.shadows=LightShadows.None;equipmentLight.cullingMask=~(1<<30);lamps.Add(equipmentLight);
                Batch();
            }
        }
        void Batch()
        {
            foreach(var pair in batches)
            {
                var mesh=new Mesh{name=sector.name+" / "+pair.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray());meshes.Add(mesh);
                var obj=new GameObject(pair.Key.name+" detail batch");obj.transform.SetParent(sector,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=pair.Key;
                var block=new MaterialPropertyBlock();block.SetFloat("_ExteriorWear",1);block.SetFloat("_Smoothness",.19f);r.SetPropertyBlock(block);
            }
            batches.Clear();
        }
        void Update(){foreach(var lamp in lamps)if(lamp!=null)lamp.enabled=!RuntimeVisualBudget.Minimum;}
        void OnDestroy(){foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);}
    }
}
