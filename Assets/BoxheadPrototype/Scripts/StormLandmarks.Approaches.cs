using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class StormLandmarks
    {
        void ApproachDetails(int side)
        {
            void Crate(Vector3 p,Vector3 size)
            {
                Box("Ribbed transit case",p+Vector3.up*size.y/2,size,steel);
                foreach(float x in new[]{-.5f,.5f})foreach(float z in new[]{-.5f,.5f})Box("Case corner protector",p+new Vector3(x*size.x,size.y/2,z*size.z),new Vector3(.09f,size.y+.04f,.09f),dark);
                foreach(float y in new[]{.15f,size.y-.15f})Box("Case retaining band",p+Vector3.up*y,new Vector3(size.x+.035f,.065f,size.z+.035f),dark);
                Box("Case shipping plate",p+new Vector3(0,size.y*.65f,-size.z*.5f-.012f),new Vector3(.35f,.18f,.018f),hull);
                for(int j=-2;j<=2;j++)Box("Case ventilation slot",p+new Vector3(j*.13f,size.y*.33f,-size.z*.5f-.025f),new Vector3(.055f,.14f,.018f),dark);
            }
            void Pallet(Vector3 p)
            {
                for(int i=-2;i<=2;i++)Box("Loading pallet slat",p+new Vector3(i*.29f,.15f,0),new Vector3(.24f,.075f,1.5f),hull);
                foreach(float x in new[]{-.52f,.52f})Box("Pallet runner",p+new Vector3(x,.06f,0),new Vector3(.17f,.12f,1.5f),dark);
            }
            // Equipment sits outside the original four-metre approach lanes.
            for(int edge=-1;edge<=1;edge+=2)
            {
                // The imported apron is below zero; equipment needs a solid pad,
                // not an assumed floor at the model origin.
                Box("Grounded service equipment pad",new Vector3(edge*10.75f,-.18f,38),new Vector3(10.5f,.36f,29),dark);
                for(int i=0;i<3;i++)
                {
                    float z=28+i*8;
                    Box("Concrete vehicle barrier",new Vector3(edge*6.3f,.46f,z),new Vector3(.62f,.92f,3),steel);
                    Box("Barrier broad foot",new Vector3(edge*6.3f,.12f,z),new Vector3(.9f,.24f,3.15f),dark);
                    for(int k=-2;k<=2;k++)Box("Barrier faded safety stripe",new Vector3(edge*5.975f,.53f,z+k*.47f),new Vector3(.018f,.17f,.23f),hull,new Vector3(20,0,0));
                    Cylinder("Guard bollard foot",new Vector3(edge*5.1f,.05f,z-1.9f),.24f,.1f,dark);
                    Cylinder("Guard bollard",new Vector3(edge*5.1f,.51f,z-1.9f),.11f,.94f,steel);
                    Cylinder("Bollard reflector",new Vector3(edge*5.1f,.82f,z-1.9f),.115f,.09f,hull);
                }
                // Trench drains and removable access covers give the paving a physical scale.
                Box("Drain channel",new Vector3(edge*7.6f,.015f,35),new Vector3(.65f,.025f,19),dark);
                for(int i=0;i<70;i++)Box("Drain cross grate",new Vector3(edge*7.6f,.04f,25.6f+i*.27f),new Vector3(.61f,.025f,.055f),steel);
                Box("Maintenance hatch",new Vector3(edge*9.3f,.015f,29),new Vector3(1.6f,.055f,1.6f),steel);
                foreach(float x in new[]{-.6f,.6f})Box("Recessed hatch handle",new Vector3(edge*9.3f+x,.043f,29),new Vector3(.25f,.012f,.07f),dark);
                for(int i=0;i<3;i++)Box("Faded service bay marking",new Vector3(edge*10,.002f,32+i*2.1f),new Vector3(5,.004f,.075f),hull);
                Pallet(new Vector3(edge*9,0,33));Crate(new Vector3(edge*9,.1875f,33),new Vector3(1.25f,.85f,1.25f));
                Crate(new Vector3(edge*11,0,35),new Vector3(1.8f,1.2f,1.35f));
                if(edge==1){Pallet(new Vector3(9,0,37));Pallet(new Vector3(9,.1875f,37));}
                // Real lamp heads on arms, with a restrained warm pool over the equipment.
                Beam("Yard lamp post",new Vector3(edge*10,0,32),new Vector3(edge*10,5.4f,32),.13f,steel);
                Beam("Yard lamp outreach",new Vector3(edge*10,5.4f,32),new Vector3(edge*8.3f,5.4f,32),.10f,steel);
                Box("Hooded yard floodlight",new Vector3(edge*8.3f,5.3f,32),new Vector3(.8f,.22f,.5f),dark);
                var lens=Shape("Recessed yard lens",PrimitiveType.Cube,new Vector3(edge*8.3f,5.18f,32),new Vector3(.62f,.025f,.33f),amber);
                var practical=Accent(new Vector3(edge*8.3f,5.1f,32),new Vector3(edge*8,0,35),new Color(1,.65f,.32f),75,17);
                lens.gameObject.AddComponent<ExteriorFixture>().Configure(practical,edge==1&&side%2==0,side*4.7f);
            }
            Accent(new Vector3(0,5.8f,24),new Vector3(0,0,35),new Color(.68f,.75f,.8f),95,23);
            // Offset pump/generator skid: individual fins, access door, exhaust and conduit supports.
            Vector3 skid=new Vector3(-11,0,42);
            Accent(new Vector3(-10,4,27),new Vector3(-9,1,31),new Color(.8f,.86f,.9f),150,12);
            if(!Authored("ServiceSkidDetailed",new Vector3(-9,0,31)))
            {
            Box("Service machinery skid",skid+Vector3.up*.18f,new Vector3(4,.36f,2.5f),dark);
            Box("Service machinery housing",skid+Vector3.up*1.1f,new Vector3(3.5f,1.65f,2.1f),steel);
            for(int i=0;i<12;i++)Box("Radiator grille",skid+new Vector3(-1.4f+i*.24f,1.15f,-1.07f),new Vector3(.10f,1.1f,.04f),dark);
            Box("Maintenance door",skid+new Vector3(1.77f,1.1f,0),new Vector3(.035f,1.35f,1.55f),hull);
            Cylinder("Exhaust stack",skid+new Vector3(-1.2f,2.5f,.5f),.12f,1.5f,steel);
            Cylinder("Exhaust rain cap",skid+new Vector3(-1.2f,3.28f,.5f),.21f,.08f,dark);
            }
            for(int i=0;i<3;i++)
            {
                float z=41+i*.6f;Beam("Service pipe",new Vector3(9,.7f,z),new Vector3(15,.7f,z),.18f,steel);
                foreach(float x in new[]{9.8f,13.8f}){Beam("Pipe saddle",new Vector3(x,0,z),new Vector3(x,.7f,z),.09f,dark);Box("Pipe collar",new Vector3(x,.7f,z),Vector3.one*.26f,hull);}
            }
            // A corrugated utility container and its panel seams occupy the middle distance.
            Vector3 cabin=new Vector3(side%2==0?12:-12,1.5f,49);
            Box("Weathered utility container",cabin,new Vector3(6,3,3),steel);
            for(int i=-11;i<=11;i++)Box("Container corrugation",cabin+new Vector3(i*.25f,0,-1.53f),new Vector3(.045f,2.8f,.08f),dark);
            foreach(float x in new[]{-2.9f,2.9f})Box("Container corner post",cabin+new Vector3(x,0,-1.55f),new Vector3(.12f,3.05f,.13f),hull);
            Box("Container repair patch",cabin+new Vector3(-1,-.4f,-1.59f),new Vector3(1.1f,.7f,.04f),dark);
            Box("Container identification plate",cabin+new Vector3(1.5f,.7f,-1.60f),new Vector3(.9f,.3f,.02f),hull);
        }
    }
}
