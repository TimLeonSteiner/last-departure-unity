using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class StormLandmarks
    {
        void StormDamage(int side)
        {
            PaintedMark("Service",new Vector3(9,.048f,30),new Vector2(3.8f,.65f),Quaternion.Euler(90,0,0));
            if(side==0)
            {
                PaintedMark("Hangar01",new Vector3(-29.65f,4.8f,78),new Vector2(3.4f,1.6f),Quaternion.Euler(0,-90,0));
                PaintedMark("Hangar02",new Vector3(29.65f,4.8f,78),new Vector2(3.4f,1.6f),Quaternion.Euler(0,90,0));
            }
            var rng = new System.Random(2740+side);
            // Everything bulky stays outside the clear approach corridor.
            float edge=side%2==0 ? 1 : -1;
            Vector3 cart=new Vector3(edge*10,0,39);
            Box("Abandoned cargo trolley chassis",cart+Vector3.up*.52f,new Vector3(2.8f,.22f,1.6f),rust);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Shape("Trolley rubber wheel",PrimitiveType.Cylinder,cart+new Vector3(x*1.05f,.24f,z*.72f),new Vector3(.48f,.12f,.48f),dark,new Vector3(90,0,0));
            for(int i=-3;i<=3;i++)Box("Trolley deck slat",cart+new Vector3(i*.37f,.66f,0),new Vector3(.31f,.09f,1.55f),steel);
            Beam("Bent trolley handle",cart+new Vector3(-1.3f,.6f,-.6f),cart+new Vector3(-1.6f,1.5f,-.6f),.055f,steel);
            Beam("Trolley handle grip",cart+new Vector3(-1.6f,1.5f,-.6f),cart+new Vector3(-1.6f,1.5f,.6f),.055f,steel);
            // Sagging perimeter mesh, with one storm-flattened section.
            Vector3 FenceBase(int bay)=>new Vector3(-edge*15,0,29+bay*4);
            Vector3 FenceTop(int bay)=>FenceBase(bay)+new Vector3(bay==2?1.3f:0,bay==2?1.2f:2.6f,.25f);
            Vector3 FencePoint(int bay,float t,float height)
            {
                var a=Vector3.Lerp(FenceBase(bay),FenceTop(bay),height);
                var b=Vector3.Lerp(FenceBase(bay+1),FenceTop(bay+1),height);
                return Vector3.Lerp(a,b,t)-Vector3.up*(Mathf.Sin(t*Mathf.PI)*.12f*height);
            }
            for(int bay=0;bay<4;bay++)
            {
                Vector3 a=FenceBase(bay),b=FenceTop(bay);
                Beam("Leaning fence post",a,b,.09f,steel);
                if(bay==3)continue;
                for(int wire=0;wire<7;wire++)
                {
                    float h=.08f+wire*.14f;
                    for(int span=0;span<8;span++)Beam("Sagging fence wire",FencePoint(bay,span/8f,h),FencePoint(bay,(span+1)/8f,h),.012f,rust);
                }
                for(int wire=0;wire<=8;wire++)Beam("Fence vertical strand",FencePoint(bay,wire/8f,.08f),FencePoint(bay,wire/8f,.92f),.012f,steel);
            }
            Vector3 fallen=new Vector3(edge*14,.07f,44);
            Beam("Storm-felled floodlight mast",fallen,fallen+new Vector3(-edge*4,.08f,5),.13f,steel);
            Box("Broken floodlight head",fallen+new Vector3(-edge*4,.08f,5),new Vector3(1.1f,.3f,.65f),dark,new Vector3(0,34,0));
            for(int i=0;i<22;i++)
            {
                float x=edge*(12+(float)rng.NextDouble()*3),z=29+(float)rng.NextDouble()*18;
                float width=.15f+(float)rng.NextDouble()*.45f,depth=.22f+(float)rng.NextDouble()*.65f,tilt=i%4*3;
                float restHeight=(width*Mathf.Sin(tilt*Mathf.Deg2Rad)+.025f*Mathf.Cos(tilt*Mathf.Deg2Rad))*.5f;
                Box("Windblown sheet fragment",new Vector3(x,restHeight,z),new Vector3(width,.025f,depth),i%3==0?rust:steel,new Vector3(0,(float)rng.NextDouble()*180,tilt));
            }
            // Looped service hose with a loose end, safely beside the route.
            for(int i=0;i<28;i++)
            {
                float a=i*Mathf.PI/14,b=(i+1)*Mathf.PI/14;
                Beam("Discarded service hose",cart+new Vector3(Mathf.Cos(a)*1.1f,.06f,3+Mathf.Sin(a)*.8f),cart+new Vector3(Mathf.Cos(b)*1.1f,.06f,3+Mathf.Sin(b)*.8f),.045f,dark);
            }
            Beam("Loose hose end",cart+new Vector3(1.1f,.06f,3),cart+new Vector3(2.1f,.06f,4),.045f,dark);
            // Broken lengths expose the old paint instead of pristine continuous stripes.
            for(int i=0;i<9;i++)
                if(i!=3&&i!=6)Box("Faded stop line",new Vector3(-3.6f+i*.9f,.032f,30),new Vector3(.64f,.012f,.22f),hull);
        }
        void PaintedMark(string key,Vector3 position,Vector2 size,Quaternion rotation)
        {
            var material=new Material(RuntimeShaders.Find("Universal Render Pipeline/Lit")){name="Faded exterior stencil / "+key};
            material.SetTexture("_BaseMap",Resources.Load<Texture2D>("ExteriorStencils/"+key));
            material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.4f);material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Smoothness",.1f);material.renderQueue=2450;material.SetOverrideTag("RenderType","TransparentCutout");labelMaterials.Add(material);
            var mesh=new Mesh{name="Painted exterior identification"};
            mesh.vertices=new[]{new Vector3(-size.x/2,-size.y/2,0),new Vector3(size.x/2,-size.y/2,0),new Vector3(-size.x/2,size.y/2,0),new Vector3(size.x/2,size.y/2,0)};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};mesh.triangles=new[]{0,2,1,2,3,1};mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var obj=new GameObject("Painted exterior / "+key);obj.transform.SetParent(section,false);obj.transform.localPosition=position;obj.transform.localRotation=rotation;
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
    }
}
