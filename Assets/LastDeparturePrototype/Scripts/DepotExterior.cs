using UnityEngine;
namespace YardSurvivalPrototype
{
    public static partial class DepotModels
    {
        public static void RunwayExtension(YardSurvival g, Transform exterior)
        {
            var crafted=Resources.Load<GameObject>("Spaceport/CraftedRunwaySet");
            if(crafted!=null)
            {
                var instance=Object.Instantiate(crafted,exterior,false);
                instance.name="Distant runway and service buildings";
                return;
            }
            var root = Group("Distant runway and service buildings", exterior, Vector3.zero);
            var asphalt = Resources.Load<Material>("Spaceport/Asphalt") ?? g.groundMaterial;
            var steel = Resources.Load<Material>("Spaceport/Steel") ?? g.wallMaterial;
            var dark = Resources.Load<Material>("Spaceport/Ink") ?? g.wallMaterial;
            var hull = Resources.Load<Material>("Spaceport/Hull") ?? g.wallMaterial;
            var paint = Resources.Load<Material>("Spaceport/Paint") ?? g.accentMaterial;
            var amber = Resources.Load<Material>("Spaceport/AmberGlow") ?? g.accentMaterial;
            var teal = Resources.Load<Material>("Spaceport/TealGlow") ?? g.accentMaterial;
            var red = Resources.Load<Material>("Spaceport/RedGlow") ?? g.redMaterial;

            Box("Extended landing strip", root, new Vector3(0, -.06f, 182.25f), new Vector3(12, .1f, 83.5f), asphalt);
            Box("Distant service apron", root, new Vector3(0, -.15f, 183), new Vector3(124, .12f, 90), asphalt);
            for (int z = 142; z <= 222; z += 5)
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Runway edge housing", root, new Vector3(side * 5.7f, .09f, z), new Vector3(.42f, .18f, .55f), dark);
                    Box("Runway edge beacon", root, new Vector3(side * 5.7f, .19f, z), new Vector3(.25f, .06f, .32f), z > 192 ? amber : teal);
                }
            for (int z = 146; z < 218; z += 9)
                Box("Extended centerline", root, new Vector3(0, .005f, z), new Vector3(.18f, .025f, 3), hull);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Continuous runway boundary", root, new Vector3(side * 5.2f, .008f, 124), new Vector3(.12f, .02f, 196), hull);
                for (int stripe = 0; stripe < 4; stripe++)
                    Box("Far threshold marking", root, new Vector3(side * (1 + stripe * .9f), .012f, 216), new Vector3(.5f, .025f, 6), hull);
                for (int row = 0; row < 4; row++)
                {
                    float x = side * (39 + row % 2 * 13), z = 78 + row * 39;
                    var hangar = Group("Remote hangar " + side + " " + row, root, new Vector3(x, 0, z));
                    Box("Hangar shell", hangar, new Vector3(0, 4.5f, 0), new Vector3(18, 9, 22), steel);
                    Box("Overhanging roof", hangar, new Vector3(0, 9.2f, 0), new Vector3(20, .45f, 24), dark);
                    Box("Recessed roller door", hangar, new Vector3(-side * 9.02f, 3.3f, 0), new Vector3(.08f, 6.6f, 14), dark);
                    for (int rib = -3; rib <= 3; rib++)
                        Box("Door reinforcement", hangar, new Vector3(-side * 9.08f, 3.3f, rib * 2), new Vector3(.12f, 6.6f, .1f), hull);
                    Box("Hangar amber header", hangar, new Vector3(-side * 9.15f, 7, 0), new Vector3(.15f, .13f, 10), amber);
                    Box("Hangar roof vent", hangar, new Vector3(3, 9.8f, 3), new Vector3(3, 1, 4), steel);
                    Box("Service taxiway", root, new Vector3(side * 22, -.04f, z), new Vector3(30, .06f, 6), asphalt);
                    Box("Taxiway guide stripe", root, new Vector3(side * 22, .005f, z), new Vector3(30, .02f, .12f), paint);
                    for (int cargo = 0; cargo < 3; cargo++)
                    {
                        var container = Group("Distant freight module", root, new Vector3(side * (19 + cargo * 5), 0, z + 16));
                        Box("Freight shell", container, new Vector3(0, 1.3f, 0), new Vector3(3.6f, 2.6f, 7), cargo % 2 == 0 ? steel : paint);
                        Box("Freight roof", container, new Vector3(0, 2.65f, 0), new Vector3(3.8f, .16f, 7.2f), dark);
                    }
                    Round("Apron light mast", root, new Vector3(side * 17, 6, z - 10), new Vector3(.18f, 6, .18f), steel);
                    Box("Apron mast crossbar", root, new Vector3(side * 17, 12, z - 10), new Vector3(3, .22f, .3f), dark);
                    Box("Distant amber floodlight", root, new Vector3(side * 17, 11.85f, z - 10), new Vector3(2.6f, .12f, .45f), amber);
                }
            }
            for (int x = -5; x <= 5; x++)
                Box("Runway end red light", root, new Vector3(x, .18f, 224), new Vector3(.3f, .12f, .3f), red);
            // Distant fixtures use emission rather than dozens of realtime lights.
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public static void ExteriorVariation(YardSurvival g,Transform yard)
        {
            if(yard.Find("Exterior landmarks")!=null)return;
            var root=Group("Exterior landmarks",yard,Vector3.zero);
            for(int i=0;i<7;i++)
            {
                float angle=(i+.37f)*Mathf.PI*2/7;
                float radius=36+(i%3)*13;
                var cluster=Group("Abandoned industrial site "+i,root,new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius));
                cluster.localRotation=Quaternion.Euler(0,i*47+13,0);
                if(i%3==0)
                {
                    Round("Corroded storage tank",cluster,new Vector3(0,3.3f,0),new Vector3(5.5f,3.3f,5.5f),g.redMaterial);
                    Round("Tank rim",cluster,new Vector3(0,6.6f,0),new Vector3(5.8f,.12f,5.8f),g.wallMaterial);
                    Box("Service ladder",cluster,new Vector3(2.9f,3.1f,0),new Vector3(.1f,6.2f,.75f),g.wallMaterial);
                }
                else if(i%3==1)
                {
                    for(int wall=0;wall<3;wall++)Box("Collapsed factory wall",cluster,new Vector3(wall*3-3,2.1f,0),new Vector3(2.7f,3.4f+wall*.5f,.5f),g.wallMaterial,new Vector3(0,wall*9,wall*4));
                    Box("Broken roof beam",cluster,new Vector3(0,3,1.4f),new Vector3(9,.3f,.35f),g.woodMaterial,new Vector3(0,10,-14));
                    Box("Empty window opening frame",cluster,new Vector3(0,2.3f,.4f),new Vector3(1.2f,.12f,.2f),g.blueMaterial);
                }
                else
                {
                    for(int leg=0;leg<4;leg++)Round("Water tower support",cluster,new Vector3((leg%2==0?-1:1)*2,4,(leg<2?-1:1)*2),new Vector3(.22f,4,.22f),g.wallMaterial);
                    Round("Water reservoir",cluster,new Vector3(0,8.8f,0),new Vector3(5.4f,1.7f,5.4f),g.blueMaterial);
                    Box("Tower cross brace",cluster,new Vector3(0,3,2),new Vector3(5,.18f,.18f),g.wallMaterial,new Vector3(0,0,38));
                }
                for(int rubble=0;rubble<6;rubble++)Part("Weathered debris",PrimitiveType.Sphere,cluster,new Vector3(-5+rubble*1.7f,.2f,3+(rubble%2)*1.4f),new Vector3(.7f+rubble*.16f,.4f+(rubble%3)*.2f,1.1f),g.groundMaterial,new Vector3(rubble*17,rubble*37,13));
            }
        }
    }
}
