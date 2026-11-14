using UnityEngine;

namespace YardSurvivalPrototype
{
    public static partial class DepotModels
    {
        // Legacy arena coordinates; also used for rain and shot/physics collision.
        public static readonly Vector2[] RoofLeaks = {
            new Vector2(-3,-9), new Vector2(9,3), new Vector2(-9,15)
        };
        public static bool IsCollapsedRoofTile(float x,float z) => false;
        public static System.Collections.Generic.IEnumerable<Bounds> WarehouseRoofPanels()
        {
            if (FullRoofLayout.Load() != null) yield break;
            for(int x=-15;x<=15;x+=6) for(int z=-15;z<=15;z+=6)
            {
                if(IsCollapsedRoofTile(x,z))continue;
                bool broken=false;
                foreach(var leak in RoofLeaks) if(leak==new Vector2(x,z))broken=true;
                if(!broken)yield return new Bounds(new Vector3(x,8.1f,z),new Vector3(6,.25f,6));
                else
                {
                    foreach(int side in new[]{-1,1})
                    {
                        yield return new Bounds(new Vector3(x+side*2.5f,8.1f,z),new Vector3(1,.25f,6));
                        yield return new Bounds(new Vector3(x,8.1f,z+side*2.5f),new Vector3(4,.25f,1));
                    }
                }
            }
        }
        public static void EnsureDamagedRoof(YardSurvival g,Transform hall)
        {
            if(hall==null)return;
            if(FullRoofLayout.Load()!=null)
            {
                foreach(string roofName in new[]{"Continuous steel roof","Weathered roof","Superseded roof"})
                {
                    var legacy=hall.Find(roofName);
                    if(legacy!=null)legacy.gameObject.SetActive(false);
                }
                return;
            }
            var old=hall.Find("Continuous steel roof");
            if(old!=null)old.gameObject.SetActive(false);
            var previous=hall.Find("Weathered roof");
            if(previous!=null)
            {
                if(previous.Find("Collapsed north roof")!=null)return;
                previous.gameObject.SetActive(false);
                previous.name="Superseded roof";
                Object.Destroy(previous.gameObject);
            }
            var roof=Group("Weathered roof",hall,Vector3.zero);
            Group("Collapsed north roof",roof,Vector3.zero);
            foreach(var panel in WarehouseRoofPanels())
                Box("Roof panel",roof,panel.center,panel.size,g.wallMaterial).GetComponent<Collider>().enabled=true;
            foreach(var leak in RoofLeaks)
            {
                if(IsCollapsedRoofTile(leak.x,leak.y))continue;
                for(int i=0;i<5;i++)
                {
                    var lip=Box("Torn roof edge",roof,new Vector3(leak.x-1.6f+i*.8f,7.98f,leak.y+2.12f),new Vector3(.68f,.055f,.65f),g.wallMaterial);
                    lip.transform.localRotation=Quaternion.Euler(15+i%3*12,0,i%2==0?8:-8);
                }
            }
        }
        public static void WarehouseEnvironment(YardSurvival g, Transform yard)
        {
            Box("Exterior terrain", yard, new Vector3(0, -.36f, 0), new Vector3(380, .25f, 380), g.groundMaterial);
            Wasteland(g, yard);
            ExteriorVariation(g,yard);
            var hall = Group("Warehouse hall", yard, Vector3.zero);
            var floor = Box("Concrete warehouse floor", hall, new Vector3(0, -.16f, 0), new Vector3(36, .3f, 36), g.groundMaterial);
            floor.GetComponent<Collider>().enabled = true;
            EnsureDamagedRoof(g,hall);
            for (int side = 0; side < 4; side++)
            {
                var wall = Group("Window wall " + side, hall, Vector3.zero);
                wall.localRotation = Quaternion.Euler(0, side * 90, 0);
                foreach (int sign in new[] { -1, 1 })
                {
                    Box("Concrete window sill", wall, new Vector3(sign * 10, .55f, 17.6f), new Vector3(15, 1.1f, .6f), g.wallMaterial).GetComponent<Collider>().enabled = true;
                    Box("Upper wall cladding", wall, new Vector3(sign * 10, 6.15f, 17.6f), new Vector3(15, 3.7f, .6f), g.blueMaterial).GetComponent<Collider>().enabled = true;
                    foreach (float y in new[] { 1.14f, 4.24f })
                        Box("Window horizontal frame", wall, new Vector3(sign * 10, y, 17.6f), new Vector3(15, .12f, .72f), g.creamMaterial);
                    for (int i = 0; i < 6; i++)
                    {
                        float x = sign * (2.6f + i * 2.96f);
                        Box("Window steel mullion", wall, new Vector3(x, 2.7f, 17.6f), new Vector3(.16f, 3.2f, .65f), g.wallMaterial).GetComponent<Collider>().enabled = true;
                    }
                    Box("Loading door upright", wall, new Vector3(sign * 2.3f, 2, 16), new Vector3(.4f, 4, .4f), g.yellowMaterial).GetComponent<Collider>().enabled = true;
                }
                Box("Open loading door lintel", wall, new Vector3(0, 6.1f, 17.6f), new Vector3(5, 3.8f, .6f), g.wallMaterial).GetComponent<Collider>().enabled = true;
                LoadingSign(g, wall, side + 1);
                for (int i = 0; i < 8; i++)
                    Box("Loading lane hazard stripe", wall, new Vector3(-1.75f + i * .5f, .014f, 16), new Vector3(.45f, .025f, 1.6f), i % 2 == 0 ? g.yellowMaterial : g.wallMaterial);
            }
            for (int z = -12; z <= 12; z += 6)
            {
                Box("Roof transverse girder", hall, new Vector3(0, 7.7f, z), new Vector3(35.5f, .45f, .22f), g.creamMaterial);
                foreach (int x in new[] { -8, 8 })
                {
                    Box("Suspended fluorescent fixture", hall, new Vector3(x, 6.9f, z), new Vector3(2.3f, .12f, .32f), g.creamMaterial);
                    var lamp = new GameObject("Warehouse working light"); lamp.transform.SetParent(hall, false);
                    lamp.transform.localPosition = new Vector3(x, 6.7f, z);
                    lamp.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    var light = lamp.AddComponent<Light>(); light.type = LightType.Spot;
                    light.color = new Color(.72f, .83f, .88f); light.intensity = 3; light.range = 15;
                    light.spotAngle = 105; light.innerSpotAngle = 70; light.shadows = LightShadows.None;
                }
            }
            // Preserve the four navigation footprints using solid freight stacks.
            foreach (int x in new[] { -10, 10 }) foreach (int z in new[] { -9, 9 })
            {
                Box("Freight stack collision", hall, new Vector3(x, 1.325f, z), new Vector3(6, 2.65f, 4), g.woodMaterial).GetComponent<Collider>().enabled = true;
                for (int row = 0; row < 3; row++) for (int column = 0; column < 4; column++)
                {
                    Box("Individual freight crate", hall, new Vector3(x - 2.24f + column * 1.49f, .45f + row * .88f, z), new Vector3(1.43f, .82f, 4.04f), column % 2 == 0 ? g.blueMaterial : g.redMaterial);
                }
                for (int row = 0; row < 3; row++)
                    Box("Rack shelf edge", hall, new Vector3(x, .05f + row * .9f, z), new Vector3(6.04f, .07f, 4.12f), g.yellowMaterial);
            }
            for (int z = -14; z <= 14; z += 4)
                Box("Central aisle marking", hall, new Vector3(0, .01f, z), new Vector3(.12f, .02f, 1.5f), g.creamMaterial);
            DepotNightLighting.Warehouse(g, hall);
        }
    }

    public sealed partial class YardSurvival
    {
        void RegisterWarehouseWalls()
        {
            for (int side = 0; side < 4; side++)
            {
                bool horizontal = side % 2 == 0;
                float edge = side < 2 ? 17.6f : -17.6f;
                foreach (int sign in new[] { -1, 1 })
                {
                    AddPerimeter(horizontal ? new Rect(sign * 10 - 7.5f, edge - .3f, 15, .6f) : new Rect(edge - .3f, sign * 10 - 7.5f, .6f, 15), 1.1f);
                    wallVolumes.Add(new Bounds(horizontal ? new Vector3(sign * 10, 6.15f, edge) : new Vector3(edge, 6.15f, sign * 10), horizontal ? new Vector3(15, 3.7f, .6f) : new Vector3(.6f, 3.7f, 15)));
                    for (int i = 0; i < 6; i++)
                    {
                        float at = sign * (2.6f + i * 2.96f);
                        wallVolumes.Add(new Bounds(horizontal ? new Vector3(at, 2.7f, edge) : new Vector3(edge, 2.7f, at), horizontal ? new Vector3(.16f, 3.2f, .65f) : new Vector3(.65f, 3.2f, .16f)));
                    }
                    Vector3 post = entrances[side] + (horizontal ? Vector3.right : Vector3.forward) * sign * 2.3f;
                    var terminal=Resources.Load<GameObject>("Spaceport/ModularHallTerminalSet");
                    if(terminal==null||terminal.transform.Find("Open entrances without gate posts")==null)
                        AddPerimeter(new Rect(post.x - .2f, post.z - .2f, .4f, .4f), 4);
                }
                wallVolumes.Add(new Bounds(horizontal ? new Vector3(0, 6.1f, edge) : new Vector3(edge, 6.1f, 0), horizontal ? new Vector3(5, 3.8f, .6f) : new Vector3(.6f, 3.8f, 5)));
            }
            foreach(var panel in DepotModels.WarehouseRoofPanels())wallVolumes.Add(panel);
            RegisterModularHallWalls();
        }
    }
}
