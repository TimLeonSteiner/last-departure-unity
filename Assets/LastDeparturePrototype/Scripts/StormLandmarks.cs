using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace YardSurvivalPrototype
{
    // Decorative world-space sets beyond the playable perimeter. Each set is batched by material.
    public sealed partial class StormLandmarks : MonoBehaviour
    {
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> labelMaterials = new List<Material>();
        readonly Dictionary<string,Material> authoredSurfaces=new Dictionary<string,Material>();
        Material steel, hull, dark, rust, amber, teal, red;
        Transform section;

        public static StormLandmarks Build(Transform parent)
        {
            var existing = parent.Find("Four storm outlooks");
            if (existing != null) return existing.GetComponent<StormLandmarks>();
            var root = new GameObject("Four storm outlooks"); root.transform.SetParent(parent, false);
            var set = root.AddComponent<StormLandmarks>(); set.Create(); return set;
        }
        void Create()
        {
            steel = Resources.Load<Material>("Spaceport/Steel");
            hull = Resources.Load<Material>("Spaceport/Hull");
            dark = Resources.Load<Material>("Spaceport/Ink");
            rust = Resources.Load<Material>("Spaceport/Rust");
            amber = Resources.Load<Material>("Spaceport/AmberGlow");
            teal = new Material(Resources.Load<Material>("Spaceport/TealGlow")){name="Muted exterior instrument lenses"};
            teal.SetColor("_BaseColor",new Color(.48f,.31f,.14f));teal.SetFloat("_Glow",.35f);labelMaterials.Add(teal);
            red = Resources.Load<Material>("Spaceport/RedGlow");
            Begin("NORTH / launch control", new Vector3(-23, 0, 55), 0); ControlTower(); Batch();
            Begin("EAST / coolant works", new Vector3(62, 0, 0), 90); CoolantWorks(); Batch();
            Begin("SOUTH / abandoned freight", new Vector3(0, 0, -62), 180); FreightYard(); Batch();
            Begin("WEST / broken relay", new Vector3(-62, 0, 0), 270); RelayArray(); Batch();
            for (int side = 0; side < 4; side++)
            {
                Begin("Gate service details " + side, Vector3.zero, side * 90);
                GateDetails(side); Batch();
                PlaceEntranceTerminal(-5);
                PlaceEntranceTerminal(5);
            }
        }
        void Begin(string name, Vector3 position, float yaw)
        {
            section = new GameObject(name).transform; section.SetParent(transform, false);
            section.localPosition = position; section.localRotation = Quaternion.Euler(0, yaw, 0);
        }
        Transform Shape(string name, PrimitiveType type, Vector3 p, Vector3 scale, Material material, Vector3 angles = default)
        {
            var obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.SetParent(section, false);
            obj.transform.localPosition = p; obj.transform.localScale = scale; obj.transform.localRotation = Quaternion.Euler(angles);
            obj.GetComponent<Collider>().enabled = false;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj.transform;
        }
        void Box(string name, Vector3 p, Vector3 s, Material m, Vector3 a = default) => Shape(name, PrimitiveType.Cube, p, s, m, a);
        void Cylinder(string name, Vector3 p, float radius, float height, Material m) => Shape(name, PrimitiveType.Cylinder, p, new Vector3(radius * 2, height / 2, radius * 2), m);
        void Beam(string name, Vector3 a, Vector3 b, float width, Material m)
        {
            var t = Shape(name, PrimitiveType.Cube, (a + b) / 2, new Vector3(width, (b - a).magnitude, width), m);
            t.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        }
        void Rail(Vector3 a, Vector3 b)
        {
            Beam("Handrail", a + Vector3.up, b + Vector3.up, .08f, hull);
            for (int i = 0; i <= 4; i++) { var p = Vector3.Lerp(a, b, i / 4f); Beam("Railing upright", p, p + Vector3.up, .07f, steel); }
        }
        void Sign(string words, Vector3 p, float size, Color color)
        {
            var obj = new GameObject(words); obj.transform.SetParent(section, false); obj.transform.localPosition = p;
            obj.transform.localRotation = Quaternion.identity;
            var text = obj.AddComponent<TextMesh>(); text.text = words; text.fontSize = 64;
            text.characterSize = size; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = color;
            // Default legacy text ignores depth and bleeds through the interior sign cabinets.
            var renderer=obj.GetComponent<MeshRenderer>();
            var material=new Material(RuntimeShaders.Find("Universal Render Pipeline/Unlit")){name="Depth-tested exterior label"};
            material.SetTexture("_BaseMap",renderer.sharedMaterial.mainTexture);material.SetColor("_BaseColor",color);
            material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.3f);material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType","TransparentCutout");material.renderQueue=2450;
            renderer.sharedMaterial=material;labelMaterials.Add(material);
        }
        Light Accent(Vector3 p, Vector3 target, Color color, float intensity, float range)
        {
            var obj = new GameObject("Shielded landmark light"); obj.transform.SetParent(section, false);
            obj.transform.localPosition = p; obj.transform.localRotation = Quaternion.LookRotation(target - p);
            var lamp = obj.AddComponent<Light>(); lamp.type = LightType.Spot; lamp.color = color;
            lamp.intensity = intensity; lamp.range = range; lamp.spotAngle = 65; lamp.innerSpotAngle = 30;
            lamp.cullingMask = ~(1 << 30); lamp.shadows = LightShadows.Soft;
            lamp.shadowBias=.02f;lamp.shadowNormalBias=.12f;lamp.shadowCustomResolution=256;
            return lamp;
        }
        void ControlTower()
        {
            Box("Tower foundation below paving",new Vector3(0,-.16f,0),new Vector3(10,.34f,10),dark);
            Box("Tower plinth", new Vector3(0, .4f, 0), new Vector3(10, .8f, 10), dark);
            Box("Concrete lift shaft", new Vector3(0, 8, 0), new Vector3(4, 16, 4), steel);
            Box("Observation platform", new Vector3(0, 16, 0), new Vector3(11, .5f, 9), hull);
            Box("Control room", new Vector3(0, 18, 0), new Vector3(9, 3.5f, 7), dark);
            Box("Cantilevered roof", new Vector3(0, 20, 0), new Vector3(12, .4f, 10), steel);
            for (int i = -3; i <= 3; i++)
            {
                Box("Dark observation glass", new Vector3(i * 1.2f, 18, -3.55f), new Vector3(1, 1.6f, .08f), i == -2 || i == 1 ? teal : steel);
                Box("Window sun shade", new Vector3(i * 1.2f, 19, -3.8f), new Vector3(1.1f, .1f, .7f), hull);
            }
            Rail(new Vector3(-5, 16.24f, -4), new Vector3(5, 16.24f, -4));
            foreach(float x in new[]{-.5f,.5f})
            {
                Beam("Tower ladder side rail",new Vector3(x,.6f,-2.1f),new Vector3(x,16.4f,-2.1f),.09f,steel);
                for(int y=1;y<=15;y+=2)Beam("Tower ladder wall bracket",new Vector3(x,y,-2.1f),new Vector3(x,y,-1.9f),.08f,steel);
            }
            for (int y = 1; y < 15; y++) Beam("Access ladder rung", new Vector3(-.5f, y, -2.1f), new Vector3(.5f, y, -2.1f), .07f, hull);
            Beam("Antenna", new Vector3(0, 20, 0), new Vector3(0, 28, 0), .15f, steel);
            Beam("Antenna crossbar", new Vector3(-2, 25, 0), new Vector3(2, 25, 0), .12f, hull);
            Box("Red aviation beacon", new Vector3(0, 28, 0), Vector3.one * .35f, red);
            for (int i = 0; i < 3; i++) Box("Service cabinet", new Vector3(6 + i * 2, 1.2f, 2), new Vector3(1.6f, 2.4f, 2), steel);
            Box("Tower identification backing",new Vector3(0,5,-2.025f),new Vector3(3.8f,.55f,.08f),dark);
            Sign("01 / FLIGHT CONTROL", new Vector3(0, 5, -2.08f), .043f, new Color(.4f, .65f, .68f));
            Accent(new Vector3(0, 6, -7), new Vector3(0, 14, 0), new Color(.35f, .62f, .8f), 90, 24);
        }
        void CoolantWorks()
        {
            Box("Coolant foundation below paving",new Vector3(0,-.16f,0),new Vector3(25,.34f,18),dark);
            if(Authored("CoolantWorksDetailed",Vector3.zero))
            {
                Accent(new Vector3(-7,9,-10),new Vector3(0,6,0),new Color(.70f,.78f,.82f),480,29);
                Accent(new Vector3(9,7,-8),new Vector3(4,3,-4),new Color(1,.86f,.69f),260,22);
                return;
            }
            Box("Plant foundation", new Vector3(0, .3f, 0), new Vector3(25, .6f, 18), dark);
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 7; float height = i == 0 ? 15 : 10;
                Cylinder("Pressure vessel", new Vector3(x, height / 2, 2), 2.6f, height, steel);
                for (int band = 1; band <= 3; band++) Cylinder("Tank reinforcement band", new Vector3(x, height * band / 4, 2), 2.7f, .16f, hull);
                Shape("Domed tank cap", PrimitiveType.Sphere, new Vector3(x, height, 2), new Vector3(5.2f, 1.6f, 5.2f), steel);
                Beam("Coolant delivery pipe", new Vector3(x, 3, -.6f), new Vector3(x, 3, -6), .5f, rust);
                Beam("Pipe riser", new Vector3(x, 0, -6), new Vector3(x, 3, -6), .5f, rust);
                Box("Tank status lamp", new Vector3(x, 5, -.7f), new Vector3(.15f, 1.5f, .12f), teal);
            }
            Box("Maintenance catwalk", new Vector3(0, 6, -2), new Vector3(22, .2f, 2), hull);
            Rail(new Vector3(-11, 6, -3), new Vector3(11, 6, -3));
            Beam("Main pipe manifold", new Vector3(-10, 3, -6), new Vector3(10, 3, -6), .65f, rust);
            for (int i = 0; i < 6; i++) Box("Heat exchanger fins", new Vector3(12, 1.5f, -3 + i * .6f), new Vector3(2.5f, 3, .15f), steel);
            Sign("02 / COOLANT WORKS", new Vector3(0, 6.7f, -3.1f), .13f, new Color(.32f, .65f, .62f));
            Accent(new Vector3(-7, 7, -9), new Vector3(0, 7, 0), new Color(.27f, .7f, .64f), 110, 27);
        }
        void FreightYard()
        {
            Box("Freight equipment foundation",new Vector3(0,-.16f,2),new Vector3(65,.34f,24),dark);
            for (int track = -1; track <= 1; track += 2)
            {
                for (int rail = -1; rail <= 1; rail += 2) Box("Rail line", new Vector3(0, .16f, track * 7 + rail), new Vector3(64, .18f, .12f), steel);
                for (int x = -30; x <= 30; x += 3) Box("Rail sleeper", new Vector3(x, .02f, track * 7), new Vector3(.35f, .12f, 3), dark);
            }
            for (int wagon = -1; wagon <= 1; wagon++)
            {
                float x = wagon * 12;
                Box("Freight wagon deck", new Vector3(x, 1, 7), new Vector3(11, .5f, 3.4f), dark);
                Box("Sealed freight container", new Vector3(x, 3.2f, 7), new Vector3(10, 4, 3), wagon == 0 ? rust : steel);
                for (int rib = -4; rib <= 4; rib++) Box("Corrugated freight rib", new Vector3(x + rib, 3.2f, 5.44f), new Vector3(.09f, 3.8f, .12f), hull);
                for (int wheel = -1; wheel <= 1; wheel += 2) Shape("Wagon axle", PrimitiveType.Cylinder, new Vector3(x + wheel * 3.6f, .80f, 7), new Vector3(1.1f, 1.8f, 1.1f), steel, new Vector3(90, 0, 0));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Beam("Crane leg", new Vector3(side * 17, 0, -2), new Vector3(side * 17, 18, -2), .8f, rust);
                Beam("Crane brace", new Vector3(side * 21, 0, -2), new Vector3(side * 17, 9, -2), .4f, steel);
                Box("Crane obstruction lamp", new Vector3(side * 17, 18.7f, -2), Vector3.one * .3f, amber);
            }
            Box("Gantry crossbeam", new Vector3(0, 18, -2), new Vector3(36, 1.2f, 1.6f), rust);
            Box("Abandoned hoist trolley", new Vector3(-6, 17, -2), new Vector3(3, 1.5f, 2), steel);
            Beam("Hanging hoist cable", new Vector3(-6, 17, -2), new Vector3(-6, 7, -2), .07f, hull);
            Box("Empty cargo spreader", new Vector3(-6, 7, -2), new Vector3(8, .25f, 2.5f), rust);
            Box("Gantry sign backing",new Vector3(0,16.5f,-2.83f),new Vector3(11,1,.12f),dark);
            foreach(float x in new[]{-4f,4f})Beam("Gantry sign hanger",new Vector3(x,17.5f,-2.7f),new Vector3(x,16.8f,-2.83f),.1f,steel);
            Sign("03 / FREIGHT TERMINUS", new Vector3(0, 16.5f, -2.91f), .17f, new Color(.7f, .47f, .19f));
            Accent(new Vector3(8, 11, -7), new Vector3(0, 2, 7), new Color(1, .48f, .16f), 110, 29);
        }
        void RelayArray()
        {
            Box("Relay equipment foundation",new Vector3(0,-.16f,3),new Vector3(25,.34f,16),dark);
            Box("Relay bunker", new Vector3(0, 2, 3), new Vector3(14, 4, 10), dark);
            Box("Bunker roof", new Vector3(0, 4.2f, 3), new Vector3(15, .4f, 11), steel);
            for (int side = -1; side <= 1; side += 2)
                Beam("Lattice mast leg", new Vector3(side * 3, 4, 3), new Vector3(side, 25, 3), .25f, steel);
            for (int y = 5; y < 23; y += 3)
            {
                float w = Mathf.Lerp(3, 1, (y - 4) / 21f);
                float upperW=Mathf.Lerp(3,1,(y-1)/21f);
                Beam("Lattice diagonal", new Vector3(-w, y, 3), new Vector3(upperW, y + 3, 3), .12f, hull);
                Beam("Lattice crossbar", new Vector3(-w, y, 3), new Vector3(w, y, 3), .15f, steel);
            }
            // Open skeletal dish: concentric polygon rings and spokes read clearly in a flash.
            Vector3 centre = new Vector3(0, 19, 1);
            Beam("Dish central mounting arm",new Vector3(0,19,3),centre,.32f,steel);
            foreach(float x in new[]{-1.57f,1.57f})Beam("Dish mounting yoke",new Vector3(x,19,3),centre,.18f,steel);
            Beam("Beacon mounting bridge",new Vector3(-1,25,3),new Vector3(1,25,3),.18f,steel);
            Beam("Beacon mounting stalk",new Vector3(0,25,3),new Vector3(0,25.5f,3),.12f,steel);
            for (int ring = 1; ring <= 3; ring++)
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI / 8, b = (i + 1) * Mathf.PI / 8, r = ring * 1.5f;
                    Beam("Dish rim", centre + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, ring * .3f), centre + new Vector3(Mathf.Cos(b) * r, Mathf.Sin(b) * r, ring * .3f), .1f, hull);
                    if (ring == 3 && i % 2 == 0) Beam("Dish spoke", centre, centre + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, .9f), .1f, steel);
                }
            Beam("Dish receiver", centre, centre + new Vector3(0, 0, -2.5f), .16f, rust);
            Box("Relay warning beacon", new Vector3(0, 25.5f, 3), Vector3.one * .35f, red);
            Beam("Collapsed secondary mast", new Vector3(12, .06f, -1), new Vector3(23, .06f, 8), .55f, rust);
            for (int i = 0; i < 4; i++) Box("Battery cabinet", new Vector3(-10, 1.2f, i * 2.2f), new Vector3(2, 2.4f, 1.7f), steel);
            Box("Sealed bunker door", new Vector3(0, 1.6f, -2.05f), new Vector3(2.5f, 3.2f, .15f), rust);
            Sign("04 / RELAY OFFLINE", new Vector3(0, 3.6f, -2.14f), .12f, new Color(.65f, .25f, .2f));
            Accent(new Vector3(-8, 8, -7), new Vector3(0, 16, 2), new Color(.45f, .58f, .8f), 95, 29);
        }
        void GateDetails(int side)
        {
            ApproachDetails(side);
            StormDamage(side);
            string[] signs = { "01  FLIGHT CONTROL", "02  COOLANT WORKS", "03  FREIGHT TERMINUS", "04  RELAY ARRAY" };
            // Keep the full central gate and all player/enemy lanes unobstructed.
            for (int edge = -1; edge <= 1; edge += 2)
            {
                float x = edge * 5;
                if (Resources.Load<GameObject>("Spaceport/EntranceTerminal/Terminal") == null)
                    DetailedServiceTerminal(x, side);
                Box("Drain surround", new Vector3(edge * 8, .015f, 22), new Vector3(3, .03f, .6f), dark);
                for (int grate = 0; grate < 10; grate++) Box("Drain grate", new Vector3(edge * 8 - 1.3f + grate * .28f, .035f, 22), new Vector3(.07f, .025f, .5f), steel);
            }
            // TerminalWear supplies the shared destination signage.

        }
        void Batch()
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var originals = new List<GameObject>();
            foreach (var filter in section.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.GetComponent<ExteriorFixture>()!=null)continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || renderer.sharedMaterial == null) continue;
                if (!groups.TryGetValue(renderer.sharedMaterial, out var items)) groups[renderer.sharedMaterial] = items = new List<CombineInstance>();
                items.Add(new CombineInstance { mesh = filter.sharedMesh, transform = section.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                originals.Add(filter.gameObject);
            }
            foreach (var pair in groups)
            {
                var mesh = new Mesh { name = section.name + " / " + pair.Key.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(pair.Value.ToArray(), true, true); meshes.Add(mesh);
                var obj = new GameObject(pair.Key.name + " detail batch"); obj.transform.SetParent(section, false);
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = pair.Key;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            }
            foreach (var obj in originals) { obj.SetActive(false); if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
        }
        void OnDestroy()
        {
            foreach(var material in labelMaterials)if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
            foreach (var mesh in meshes) if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}
