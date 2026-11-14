using UnityEngine;

namespace YardSurvivalPrototype
{
    public static partial class DepotModels
    {
        public sealed class Rig
        {
            public Transform root, leftLeg, rightLeg, leftArm, rightArm, head, gun;
            public void Animate(float time, float movement, bool ghoul)
            {
                float stride = Mathf.Sin(time * (ghoul ? 5 : 10)) * movement;
                // The hidden first-person player rig has no legs; enemy rigs do.
                if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(stride * 22, 0, 0);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-stride * 22, 0, 0);
                if (ghoul)
                {
                    leftArm.localRotation = Quaternion.Euler(-58 + stride * 8, 0, -12);
                    rightArm.localRotation = Quaternion.Euler(-66 - stride * 8, 0, 15);
                    head.localRotation = Quaternion.Euler(14, Mathf.Sin(time * 2) * 9, 9);
                }
            }
        }

        static Transform Part(string name, PrimitiveType shape, Transform parent, Vector3 position, Vector3 scale, Material material, Vector3 angles = default)
        {
            var obj = GameObject.CreatePrimitive(shape);
            obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            obj.transform.localRotation = Quaternion.Euler(angles);
            obj.GetComponent<Collider>().enabled = false;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj.transform;
        }
        static Transform Box(string name, Transform p, Vector3 pos, Vector3 size, Material mat, Vector3 angles = default) => Part(name, PrimitiveType.Cube, p, pos, size, mat, angles);
        static Transform Round(string name, Transform p, Vector3 pos, Vector3 size, Material mat, Vector3 angles = default) => Part(name, PrimitiveType.Cylinder, p, pos, size, mat, angles);
        static Transform Group(string name, Transform parent, Vector3 position)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.transform.localPosition = position; return obj.transform;
        }
        static void Label(YardSurvival game, Transform parent, string words, Vector3 pos, float size, Color color, bool flat = true)
        {
            var obj = new GameObject(words); obj.transform.SetParent(parent, false); obj.transform.localPosition = pos;
            obj.transform.localRotation = flat ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
            var text = obj.AddComponent<TextMesh>(); text.text = words; text.fontSize = 64;
            text.characterSize = size; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            text.font = game.signFont; text.fontStyle = FontStyle.Bold; text.color = color;
            if (game.signFont != null) obj.GetComponent<MeshRenderer>().sharedMaterial = game.signFont.material;
        }

        public static void Environment(YardSurvival g, Transform yard)
        {
            Box("Wasteland beyond depot", yard, new Vector3(0, -.36f, 0), new Vector3(380, .25f, 380), g.groundMaterial);
            Wasteland(g, yard);
            ExteriorVariation(g,yard);
            Box("Cracked depot concrete", yard, new Vector3(0, -.18f, 0), new Vector3(36, .3f, 36), g.groundMaterial);
            Round("Faded depot seal", yard, new Vector3(0, .002f, 0), new Vector3(6, .012f, 6), g.wallMaterial);
            Round("Seal inner circle", yard, new Vector3(0, .025f, 0), new Vector3(5.7f, .006f, 5.7f), g.groundMaterial);
            Label(g, yard, "D / 27", new Vector3(0, .05f, 0), .12f, new Color(.5f, .47f, .3f));
            int building = 0;
            foreach (int x in new[] { -10, 10 }) foreach (int z in new[] { -9, 9 })
            {
                var shed = Group("Retrofuture depot module " + ++building, yard, new Vector3(x, 0, z));
                Material paint = x < 0 ? g.blueMaterial : g.redMaterial;
                Box("Riveted body", shed, new Vector3(0, 1.2f, 0), new Vector3(6, 2.4f, 4), paint);
                Box("Concrete plinth", shed, new Vector3(0, .22f, 0), new Vector3(6.05f, .4f, 4.05f), g.wallMaterial);
                Box("Roof cornice", shed, new Vector3(0, 2.45f, 0), new Vector3(6.25f, .16f, 4.2f), g.creamMaterial);
                Box("Oxidized roof", shed, new Vector3(0, 2.54f, 0), new Vector3(6.08f, .1f, 4.05f), g.woodMaterial);
                for (int i = 0; i < 9; i++)
                {
                    float px = -2.8f + i * .7f;
                    Box("Steel rib", shed, new Vector3(px, 1.4f, -2.035f), new Vector3(.06f, 1.6f, .07f), g.wallMaterial);
                    foreach (float y in new[] { .66f, 2.12f })
                        Part("Rivet", PrimitiveType.Sphere, shed, new Vector3(px, y, -2.085f), Vector3.one * .1f, g.creamMaterial);
                }
                Box("Cream atomic stripe", shed, new Vector3(0, 1.65f, -2.08f), new Vector3(5.85f, .2f, .08f), g.creamMaterial);
                var facade = Group("Inner service facade", shed, new Vector3(x < 0 ? 3 : -3, 0, 0));
                facade.localRotation = Quaternion.Euler(0, x < 0 ? -90 : 90, 0);
                Box("Inner door surround", facade, new Vector3(-.8f, 1.05f, -.08f), new Vector3(1.35f, 2.1f, .14f), g.creamMaterial);
                Box("Sealed inner door", facade, new Vector3(-.8f, 1, -.18f), new Vector3(1.15f, 1.9f, .12f), g.wallMaterial);
                Box("Inner door window", facade, new Vector3(-.8f, 1.55f, -.255f), new Vector3(.8f, .48f, .05f), g.accentMaterial);
                Box("Inner door latch", facade, new Vector3(-.37f, 1, -.27f), new Vector3(.055f, .3f, .08f), g.yellowMaterial);
                Box("Inner instrument panel", facade, new Vector3(1, 1.2f, -.1f), new Vector3(1.15f, 1.1f, .18f), g.creamMaterial);
                for (int i = 0; i < 2; i++)
                {
                    Round("Instrument dial rim", facade, new Vector3(.72f + i * .52f, 1.5f, -.22f), new Vector3(.35f, .04f, .35f), g.wallMaterial, new Vector3(90, 0, 0));
                    Box("Instrument needle", facade, new Vector3(.72f + i * .52f, 1.5f, -.28f), new Vector3(.025f, .21f, .02f), g.yellowMaterial, new Vector3(0, 0, -35 + i * 55));
                    Box("Instrument switch", facade, new Vector3(.72f + i * .52f, 1.05f, -.22f), new Vector3(.1f, .18f, .05f), g.redMaterial);
                }
                Box("Inner depot name plate", facade, new Vector3(0, 2.18f, -.08f), new Vector3(3.65f, .31f, .12f), g.creamMaterial);
                Label(g, facade, x < 0 ? "D27  /  POWER" : "D07  /  FUEL", new Vector3(0, 2.19f, -.16f), .032f, new Color(.16f, .22f, .18f), false);
                Box("Service door", shed, new Vector3(-1.7f, 1.15f, -2.13f), new Vector3(1.3f, 1.9f, .15f), g.wallMaterial);
                Box("Door glazing", shed, new Vector3(-1.7f, 1.65f, -2.22f), new Vector3(.9f, .6f, .04f), g.accentMaterial);
                Box("Door handle", shed, new Vector3(-1.23f, 1.05f, -2.26f), new Vector3(.07f, .3f, .09f), g.creamMaterial);
                Box("Control panel", shed, new Vector3(1.5f, 1.1f, -2.15f), new Vector3(1.35f, 1.25f, .15f), g.creamMaterial);
                for (int i = 0; i < 3; i++)
                {
                    Round("Analog dial", shed, new Vector3(1.12f + i * .36f, 1.43f, -2.26f), new Vector3(.25f, .025f, .25f), g.wallMaterial, new Vector3(90, 0, 0));
                    Box("Dial needle", shed, new Vector3(1.12f + i * .36f, 1.44f, -2.295f), new Vector3(.025f, .14f, .015f), g.yellowMaterial, new Vector3(0, 0, -25 + i * 20));
                }
                for (int i = 0; i < 5; i++)
                    Box("Cooling vent", shed, new Vector3(1.5f, .8f + i * .075f, -2.245f), new Vector3(.9f, .022f, .03f), g.wallMaterial);
                Round("Storage tank", shed, new Vector3(1.65f, 3.03f, .4f), new Vector3(1.25f, .45f, 1.25f), g.blueMaterial);
                foreach (float y in new[] { 2.67f, 3.35f }) Round("Tank steel band", shed, new Vector3(1.65f, y, .4f), new Vector3(1.3f, .045f, 1.3f), g.creamMaterial);
                Round("Tank filler cap", shed, new Vector3(1.65f, 3.52f, .4f), new Vector3(.3f, .1f, .3f), g.wallMaterial);
                Round("Steam riser", shed, new Vector3(-2.2f, 3, 1), new Vector3(.3f, .48f, .3f), g.wallMaterial);
                Round("Riser elbow", shed, new Vector3(-2.05f, 3.45f, 1), new Vector3(.3f, .2f, .3f), g.wallMaterial, new Vector3(0, 0, 90));
                Box("Roof access box", shed, new Vector3(-.7f, 2.88f, .9f), new Vector3(1.4f, .65f, 1), g.wallMaterial);
                for (int i = 0; i < 5; i++) Box("Access box louvers", shed, new Vector3(-.7f, 2.65f + .1f * i, .37f), new Vector3(1.1f, .025f, .035f), g.creamMaterial);
                Box("Roof sign plate", shed, new Vector3(-.4f, 2.64f, -.85f), new Vector3(4.4f, .04f, 1.2f), g.creamMaterial);
                Label(g, shed, x < 0 ? "POWER / 27" : "FUEL / 07", new Vector3(-.4f, 2.69f, -.85f), .09f, new Color(.16f, .22f, .18f));
                // Roof warning symbol made from three separate sectors, not a franchise logo.
                Round("Warning disk", shed, new Vector3(2.2f, 2.66f, -1.1f), new Vector3(.8f, .018f, .8f), g.yellowMaterial);
                for (int i = 0; i < 3; i++) Box("Warning ray", shed, new Vector3(2.2f, 2.7f, -1.1f), new Vector3(.09f, .022f, .7f), g.wallMaterial, new Vector3(0, i * 60, 0));
            }
            for (int side = 0; side < 4; side++)
            {
                bool horizontal = side % 2 == 0; float edge = side < 2 ? 17.6f : -17.6f;
                foreach (int sign in new[] { -1, 1 })
                {
                    var position = horizontal ? new Vector3(sign * 10, 1.5f, edge) : new Vector3(edge, 1.5f, sign * 10);
                    Box("Solid perimeter wall", yard, position, horizontal ? new Vector3(15, 3, .6f) : new Vector3(.6f, 3, 15), g.wallMaterial);
                    Box("Perimeter cap", yard, position + Vector3.up * 1.55f, horizontal ? new Vector3(15, .1f, .75f) : new Vector3(.75f, .1f, 15), g.creamMaterial);
                    for (int i = 0; i < 7; i++)
                    {
                        float at = sign * (3 + i * 2.25f);
                        Box("Perimeter buttress", yard, horizontal ? new Vector3(at, 1.6f, edge) : new Vector3(edge, 1.6f, at), new Vector3(.8f, 3.2f, .8f), g.wallMaterial);
                    }
                }
                Vector3 gate = side == 0 ? new Vector3(0, 0, 16) : side == 1 ? new Vector3(16, 0, 0) : side == 2 ? new Vector3(0, 0, -16) : new Vector3(-16, 0, 0);
                for (int i = 0; i < 8; i++)
                    Box("Entry hazard stripe", yard, gate + (horizontal ? Vector3.right : Vector3.forward) * (-1.75f + i * .5f) + Vector3.up * .02f,
                        horizontal ? new Vector3(.5f, .04f, 1.5f) : new Vector3(1.5f, .04f, .5f), i % 2 == 0 ? g.yellowMaterial : g.wallMaterial);
                foreach (int sign in new[] { -1, 1 })
                {
                    Vector3 post = gate + (horizontal ? Vector3.right : Vector3.forward) * sign * 2.3f;
                    Box("Gate post", yard, post + Vector3.up, new Vector3(.4f, 2, .4f), g.redMaterial);
                    Part("Warning beacon", PrimitiveType.Sphere, yard, post + Vector3.up * 2.2f, new Vector3(.38f, .3f, .38f), g.accentMaterial);
                }
            }
            for (int z = -14; z <= 14; z += 4)
                if (Mathf.Abs(z) > 3) Box("Weathered lane paint", yard, new Vector3(0, .024f, z), new Vector3(.14f, .03f, 1.3f), g.creamMaterial);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI / 6;
                Vector3 location = new Vector3(Mathf.Cos(angle) * 35, 0, Mathf.Sin(angle) * 35);
                float height = 5 + i % 4 * 2;
                Box("Distant ruined block", yard, location + Vector3.up * height / 2, new Vector3(3 + i % 3, height, 3), g.wallMaterial);
                Box("Broken roof beam", yard, location + Vector3.up * height, new Vector3(4, .4f, 1.4f), g.woodMaterial, new Vector3(0, 20, i % 2 == 0 ? 12 : -16));
                for (int level = 1; level < height - 1; level += 2)
                    Box("Ruined window band", yard, location + new Vector3(0, level, -1.52f), new Vector3(2.3f, .55f, .025f), g.woodMaterial);
            }
        }

        static void Wasteland(YardSurvival g, Transform yard)
        {
            // Broken service roads continue through all four gates into the landscape.
            for (int side = 0; side < 4; side++)
            {
                var road = Group("Abandoned highway " + side, yard, Vector3.zero); road.localRotation = Quaternion.Euler(0, side * 90, 0);
                Box("Broken asphalt", road, new Vector3(0, -.19f, 61), new Vector3(7, .14f, 88), g.woodMaterial);
                for (int i = 0; i < 16; i++) Box("Worn highway stripe", road, new Vector3(.05f, -.11f, 22 + i * 5), new Vector3(.15f, .025f, 2.2f), g.creamMaterial);
                for (int i = 0; i < 4; i++)
                {
                    var pole = Group("Dead utility pole", road, new Vector3(6, 0, 25 + i * 15));
                    Round("Timber mast", pole, new Vector3(0, 4.5f, 0), new Vector3(.3f, 4.5f, .3f), g.woodMaterial);
                    Box("Crossarm", pole, new Vector3(0, 8.4f, 0), new Vector3(2.3f, .16f, .2f), g.wallMaterial);
                    foreach (int sign in new[] { -1, 1 })
                    {
                        Round("Ceramic insulator", pole, new Vector3(sign * .85f, 8.6f, 0), new Vector3(.2f, .15f, .2f), g.creamMaterial);
                        Box("Sagging cable", pole, new Vector3(sign * .85f, 7.8f, 7.5f), new Vector3(.035f, .035f, 15.1f), g.wallMaterial, new Vector3(-6, 0, 0));
                    }
                }
                for (int i = 0; i < 8; i++)
                {
                    float x = (i % 2 == 0 ? -1 : 1) * (11 + i % 3 * 6);
                    var rubble = Group("Rubble / burned scrub", road, new Vector3(x, 0, 23 + i * 5));
                    Part("Broken concrete", PrimitiveType.Sphere, rubble, new Vector3(0, .3f, 0), new Vector3(2 + i % 3, .9f, 1.7f), g.wallMaterial, new Vector3(13, i * 29, 17));
                    Box("Burned tree trunk", rubble, new Vector3(2, 1.7f, 0), new Vector3(.2f, 3.7f, .24f), g.woodMaterial, new Vector3(0, 0, -13));
                    Box("Bare branch", rubble, new Vector3(2.55f, 2.65f, 0), new Vector3(.13f, 1.5f, .14f), g.woodMaterial, new Vector3(0, 0, -50));
                }
                var wreck = Group("Burned service vehicle", road, new Vector3(-5.5f, .1f, 25 + side * 3)); wreck.localRotation = Quaternion.Euler(0, -25, 0);
                Box("Vehicle chassis", wreck, new Vector3(0, .5f, 0), new Vector3(1.9f, .5f, 4.2f), g.redMaterial);
                Box("Crumpled bonnet", wreck, new Vector3(0, .9f, 1.3f), new Vector3(1.8f, .5f, 1.6f), g.woodMaterial, new Vector3(10, 0, 0));
                Box("Cab roof", wreck, new Vector3(0, 1.7f, -.1f), new Vector3(1.8f, .16f, 1.5f), g.wallMaterial, new Vector3(0, 0, 7));
                foreach (int sign in new[] { -1, 1 })
                {
                    Box("Cab pillar", wreck, new Vector3(sign * .8f, 1.25f, .6f), new Vector3(.12f, 1, .13f), g.wallMaterial, new Vector3(-20, 0, 0));
                    foreach (float z in new[] { -1.3f, 1.3f }) Round("Wreck wheel", wreck, new Vector3(sign, .4f, z), new Vector3(.8f, .15f, .8f), g.woodMaterial, new Vector3(0, 0, 90));
                }
            }
            // Irregular continuous terrain ridges close the horizon; mesh assets persist in the scene.
            for (int ring = 0; ring < 2; ring++)
            {
                const int segments = 96; var vertices = new Vector3[segments * 3]; var triangles = new int[segments * 12];
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * Mathf.PI * 2 / segments;
                    Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    float radius = 88 + ring * 65;
                    float height = 12 + ring * 15 + Mathf.PerlinNoise(i * .22f, ring * 2.7f) * 35;
                    vertices[i * 3] = direction * (radius - 22) + Vector3.down;
                    vertices[i * 3 + 1] = direction * radius + Vector3.up * height;
                    vertices[i * 3 + 2] = direction * (radius + 35) + Vector3.down;
                    int next = (i + 1) % segments * 3, at = i * 3, t = i * 12;
                    int[] indices = { at, at + 1, next, next, at + 1, next + 1, at + 1, at + 2, next + 1, next + 1, at + 2, next + 2 };
                    for (int k = 0; k < 12; k++) triangles[t + k] = indices[k];
                }
                for (int t = 0; t < triangles.Length; t += 3) { int a = triangles[t]; triangles[t] = triangles[t + 2]; triangles[t + 2] = a; }
                var mesh = new Mesh { name = "Ashfall terrain ridge " + ring }; mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    string path = "Assets/BoxheadPrototype/ThirdParty/TerrainRidge" + ring + ".asset";
                    var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (existing == null) UnityEditor.AssetDatabase.CreateAsset(mesh, path);
                    else { UnityEditor.EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
                }
#endif
                var obj = new GameObject(mesh.name); obj.transform.SetParent(yard, false); obj.AddComponent<MeshFilter>().sharedMesh = mesh; obj.AddComponent<MeshRenderer>().sharedMaterial = ring == 0 ? g.groundMaterial : g.wallMaterial;
            }
            var tower = Group("Derelict water tower", yard, new Vector3(-23, 0, 30));
            Round("Weathered reservoir", tower, new Vector3(0, 12, 0), new Vector3(5, 2.2f, 5), g.blueMaterial);
            Round("Reservoir lid", tower, new Vector3(0, 14.3f, 0), new Vector3(5.3f, .16f, 5.3f), g.creamMaterial);
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -1.7f : 1.7f, z = i % 2 == 0 ? -1.7f : 1.7f;
                Box("Tower support", tower, new Vector3(x, 5, z), new Vector3(.3f, 10, .3f), g.wallMaterial);
                Box("Tower cross brace", tower, new Vector3(0, 4 + i * 1.2f, z), new Vector3(4.3f, .15f, .15f), g.woodMaterial, new Vector3(0, 0, i % 2 == 0 ? 45 : -45));
            }
            var billboard = Group("Abandoned highway billboard", yard, new Vector3(17, 0, 31));
            Box("Billboard plate", billboard, new Vector3(0, 6, 0), new Vector3(8, 3.2f, .25f), g.creamMaterial, new Vector3(0, 0, -4));
            foreach (int side in new[] { -1, 1 }) Box("Billboard steel leg", billboard, new Vector3(side * 2.6f, 2.5f, 0), new Vector3(.25f, 5, .25f), g.wallMaterial);
            HighwaySign(g, billboard);
        }

        public static Rig Player(YardSurvival g, Transform parent, Vector3 position)
        {
            var rig = new Rig { root = Group("Wasteland ranger", parent, position) };
            Box("Field coat", rig.root, new Vector3(0, .08f, 0), new Vector3(.7f, .72f, .45f), g.playerMaterial);
            Box("Utility belt", rig.root, new Vector3(0, -.19f, .02f), new Vector3(.73f, .12f, .48f), g.woodMaterial);
            Box("Belt buckle", rig.root, new Vector3(0, -.19f, .28f), new Vector3(.15f, .1f, .04f), g.creamMaterial);
            rig.head = Group("Respirator head", rig.root, new Vector3(0, .63f, .04f));
            Part("Head", PrimitiveType.Sphere, rig.head, Vector3.zero, new Vector3(.52f, .55f, .48f), g.skinMaterial);
            Part("Ranger helmet", PrimitiveType.Sphere, rig.head, new Vector3(0, .19f, -.04f), new Vector3(.64f, .34f, .55f), g.playerMaterial);
            Round("Helmet rim", rig.head, new Vector3(0, .1f, -.01f), new Vector3(.69f, .025f, .59f), g.woodMaterial);
            Box("Gas mask", rig.head, new Vector3(0, -.06f, .21f), new Vector3(.44f, .32f, .17f), g.wallMaterial);
            foreach (int sign in new[] { -1, 1 })
            {
                Round("Goggle brass rim", rig.head, new Vector3(sign * .14f, .035f, .31f), new Vector3(.19f, .045f, .19f), g.yellowMaterial, new Vector3(90, 0, 0));
                Round("Goggle lens", rig.head, new Vector3(sign * .14f, .035f, .36f), new Vector3(.135f, .02f, .135f), g.accentMaterial, new Vector3(90, 0, 0));
            }
            Round("Mask filter", rig.head, new Vector3(0, -.15f, .34f), new Vector3(.18f, .1f, .18f), g.woodMaterial, new Vector3(90, 0, 0));
            Box("Rucksack", rig.root, new Vector3(0, .13f, -.35f), new Vector3(.59f, .68f, .25f), g.woodMaterial);
            Round("Air canister", rig.root, new Vector3(-.22f, .2f, -.53f), new Vector3(.18f, .35f, .18f), g.creamMaterial);
            foreach (int sign in new[] { -1, 1 })
            {
                Box("Shoulder strap", rig.root, new Vector3(sign * .2f, .13f, .24f), new Vector3(.09f, .57f, .04f), g.woodMaterial);
                var leg = Group(sign < 0 ? "Left leg" : "Right leg", rig.root, new Vector3(sign * .19f, -.26f, 0));
                Box("Trouser", leg, new Vector3(0, -.18f, 0), new Vector3(.24f, .4f, .28f), g.playerMaterial);
                Box("Boot", leg, new Vector3(0, -.35f, .08f), new Vector3(.28f, .22f, .42f), g.wallMaterial);
                if (sign < 0) rig.leftLeg = leg; else rig.rightLeg = leg;
                Box("Sleeve", rig.root, new Vector3(sign * .43f, .15f, .05f), new Vector3(.23f, .4f, .26f), g.playerMaterial, new Vector3(-25, 0, sign * -12));
                Box("Glove", rig.root, new Vector3(sign * .4f, .02f, .29f), new Vector3(.2f, .22f, .2f), g.woodMaterial);
            }
            rig.gun = Group("Riveted carbine", rig.root, new Vector3(.35f, .12f, .48f));
            Box("Wood stock", rig.gun, new Vector3(0, 0, -.16f), new Vector3(.17f, .17f, .42f), g.woodMaterial);
            Box("Receiver", rig.gun, new Vector3(0, .02f, .13f), new Vector3(.2f, .2f, .36f), g.wallMaterial);
            Round("Barrel", rig.gun, new Vector3(0, .04f, .47f), new Vector3(.11f, .25f, .11f), g.wallMaterial, new Vector3(90, 0, 0));
            for (int i = 0; i < 5; i++) Round("Barrel cooling ring", rig.gun, new Vector3(0, .04f, .34f + i * .065f), new Vector3(.14f, .014f, .14f), g.woodMaterial, new Vector3(90, 0, 0));
            Box("Receiver brass plate", rig.gun, new Vector3(.105f, .015f, .13f), new Vector3(.025f, .12f, .2f), g.yellowMaterial);
            Box("Charging handle", rig.gun, new Vector3(.145f, .07f, .07f), new Vector3(.08f, .05f, .055f), g.creamMaterial);
            Box("Rear iron sight", rig.gun, new Vector3(0, .155f, -.01f), new Vector3(.14f, .055f, .055f), g.woodMaterial);
            for (int i = 0; i < 3; i++) Part("Receiver fastener", PrimitiveType.Sphere, rig.gun, new Vector3(.124f, .05f, .06f + i * .065f), Vector3.one * .035f, g.creamMaterial);
            Box("Magazine", rig.gun, new Vector3(0, -.17f, .1f), new Vector3(.13f, .24f, .17f), g.creamMaterial);
            Box("Front sight", rig.gun, new Vector3(0, .13f, .65f), new Vector3(.04f, .08f, .06f), g.yellowMaterial);
            return rig;
        }

        public static Rig Ghoul(YardSurvival g, Transform parent, Vector3 position, int variation)
        {
            var rig = new Rig { root = Group("Ghoul scavenger", parent, position) };
            Material coat = variation % 3 == 0 ? g.enemyMaterial : variation % 3 == 1 ? g.redMaterial : g.blueMaterial;
            Box("Torn work jacket", rig.root, new Vector3(0, .03f, -.05f), new Vector3(.67f, .74f, .38f), coat, new Vector3(14, 0, 0));
            Box("Exposed ribs", rig.root, new Vector3(.13f, .05f, .18f), new Vector3(.27f, .36f, .05f), g.skinMaterial);
            for (int i = 0; i < 3; i++) Box("Rib shadow", rig.root, new Vector3(.13f, -.04f + i * .08f, .215f), new Vector3(.22f, .018f, .025f), g.woodMaterial);
            foreach (int sign in new[] { -1, 1 })
            {
                var leg = Group(sign < 0 ? "Left leg" : "Right leg", rig.root, new Vector3(sign * .2f, -.28f, -.05f));
                Box("Ragged trousers", leg, new Vector3(0, -.15f, 0), new Vector3(.24f, .35f, .26f), g.woodMaterial);
                Box("Exposed shin", leg, new Vector3(0, -.33f, .02f), new Vector3(.19f, .2f, .19f), g.skinMaterial);
                Box("Worn boot", leg, new Vector3(0, -.39f, .09f), new Vector3(.28f, .13f, .38f), g.wallMaterial);
                if (sign < 0) rig.leftLeg = leg; else rig.rightLeg = leg;
                var arm = Group(sign < 0 ? "Left reaching arm" : "Right reaching arm", rig.root, new Vector3(sign * .42f, .31f, -.04f));
                Box("Torn sleeve", arm, new Vector3(0, -.13f, 0), new Vector3(.23f, .3f, .22f), coat);
                Box("Decayed forearm", arm, new Vector3(0, -.37f, 0), new Vector3(.16f, .29f, .15f), g.skinMaterial);
                Box("Long hand", arm, new Vector3(0, -.56f, .025f), new Vector3(.19f, .14f, .2f), g.skinMaterial);
                for (int i = 0; i < 3; i++) Box("Finger", arm, new Vector3(-.065f + i * .065f, -.66f, .045f), new Vector3(.035f, .14f, .055f), g.skinMaterial);
                if (sign < 0) rig.leftArm = arm; else rig.rightArm = arm;
            }
            rig.head = Group("Sunken ghoul face", rig.root, new Vector3(0, .65f, .21f));
            Part("Decayed skull", PrimitiveType.Sphere, rig.head, Vector3.zero, new Vector3(.61f, .62f, .52f), g.skinMaterial);
            Box("Scalp scar", rig.head, new Vector3(.18f, .24f, .03f), new Vector3(.15f, .1f, .24f), g.woodMaterial, new Vector3(0, 0, 20));
            foreach (int sign in new[] { -1, 1 })
            {
                Part("Sunken eye socket", PrimitiveType.Sphere, rig.head, new Vector3(sign * .15f, .06f, .225f), new Vector3(.18f, .15f, .09f), g.wallMaterial);
                Box("Clouded eye", rig.head, new Vector3(sign * .15f, .06f, .28f), new Vector3(.07f, .045f, .025f), g.creamMaterial);
                Part("Ear", PrimitiveType.Sphere, rig.head, new Vector3(sign * .31f, -.02f, -.02f), new Vector3(.1f, .19f, .11f), g.skinMaterial);
            }
            Box("Hollow nose", rig.head, new Vector3(0, -.04f, .27f), new Vector3(.08f, .1f, .045f), g.woodMaterial);
            Box("Grimacing mouth", rig.head, new Vector3(0, -.19f, .235f), new Vector3(.27f, .14f, .06f), g.wallMaterial);
            for (int i = 0; i < 4; i++) Box("Broken tooth", rig.head, new Vector3(-.09f + .06f * i, -.16f, .28f), new Vector3(.04f, .065f, .025f), g.creamMaterial);
            rig.Animate(variation, 1, true);
            return rig;
        }

        public static Transform Barricade(YardSurvival g, Transform parent, Vector3 position, bool vertical)
        {
            if (EntranceBoards.IsDoorway(position))
                return EntranceBoards.Create(g, parent, position, vertical);
            var meshy = Resources.Load<GameObject>("Construction/Barricade");
            if (meshy != null)
            {
                var instance = Object.Instantiate(meshy, parent);
                instance.transform.localPosition = position;
                instance.transform.localRotation = Quaternion.Euler(0, vertical ? 90 : 0, 0);
                EntranceBoards.TrackImported(instance, g);
                return instance.transform;
            }
            if (Mathf.Max(Mathf.Abs(position.x), Mathf.Abs(position.z)) > 20)
                return EntranceBoards.Create(g, parent, position, vertical);
            if (g.barricadeModel != null)
            {
                var instance = Object.Instantiate(g.barricadeModel, parent);
                instance.transform.localPosition = position;
                instance.transform.localRotation = Quaternion.Euler(0, vertical ? 90 : 0, 0);
                foreach (var collider in instance.GetComponentsInChildren<Collider>()) collider.enabled = false;
                return instance.transform;
            }
            var root = Group("Salvaged steel barricade", parent, position);
            root.localRotation = Quaternion.Euler(0, vertical ? 90 : 0, 0);
            foreach (int sign in new[] { -1, 1 })
            {
                Box("A-frame foot", root, new Vector3(sign * 1.1f, -.42f, 0), new Vector3(.17f, .22f, .64f), g.wallMaterial);
                Box("Steel upright", root, new Vector3(sign * 1.1f, .03f, 0), new Vector3(.14f, 1.1f, .16f), g.wallMaterial);
            }
            for (int i = 0; i < 3; i++)
            {
                Box("Salvaged plank", root, new Vector3(0, -.27f + i * .35f, 0), new Vector3(2.8f, .27f, .22f), i == 1 ? g.yellowMaterial : g.woodMaterial);
                foreach (int sign in new[] { -1, 1 }) Part("Bolt", PrimitiveType.Sphere, root, new Vector3(sign * 1.1f, -.27f + i * .35f, -.13f), Vector3.one * .09f, g.creamMaterial);
            }
            return root;
        }

        public static Transform Turret(YardSurvival g, Transform parent, Vector3 position, out Transform head)
        {
            if (g.turretModel != null)
            {
                Transform pivot = g.turretModel.transform.Find("TurretHead");
                if (pivot != null)
                {
                    var instance = Object.Instantiate(g.turretModel, parent);
                    instance.transform.localPosition = position;
                    instance.transform.localRotation = Quaternion.identity;
                    head = instance.transform.Find("TurretHead");
                    // Gameplay uses simulation bounds rather than prefab physics colliders.
                    foreach (var collider in instance.GetComponentsInChildren<Collider>()) collider.enabled = false;
                    return instance.transform;
                }
                Debug.LogWarning("Sentry prefab needs a direct child named TurretHead. Using the built-in sentry.", g);
            }
            var root = Group("Field sentry - tripod minigun", parent, position);
            // Compact base stays inside the existing one-metre gameplay footprint.
            Round("Tripod hub", root, new Vector3(0, .42f, 0), new Vector3(.28f, .09f, .28f), g.wallMaterial);
            for (int i = 0; i < 3; i++)
            {
                var support = Group("Tripod leg", root, Vector3.zero);
                support.localRotation = Quaternion.Euler(0, i * 120 + 60, 0);
                Box("Splayed steel leg", support, new Vector3(0, .23f, .22f), new Vector3(.09f, .48f, .09f), g.wallMaterial, new Vector3(52, 0, 0));
                Box("Ground shoe", support, new Vector3(0, .04f, .4f), new Vector3(.18f, .07f, .18f), g.woodMaterial);
            }
            Round("Elevation column", root, new Vector3(0, .75f, 0), new Vector3(.16f, .3f, .16f), g.wallMaterial);
            Round("Traverse collar", root, new Vector3(0, 1.06f, 0), new Vector3(.34f, .07f, .34f), g.woodMaterial);
            head = Group("TurretHead", root, new Vector3(0, 1.2f, 0));
            Box("Olive receiver", head, new Vector3(0, 0, -.06f), new Vector3(.4f, .27f, .48f), g.wallMaterial);
            Box("Receiver top cover", head, new Vector3(0, .155f, -.08f), new Vector3(.42f, .04f, .44f), g.woodMaterial);
            Box("Ammunition can", head, new Vector3(-.31f, -.05f, -.12f), new Vector3(.22f, .3f, .34f), g.wallMaterial);
            Box("Ammo can lid", head, new Vector3(-.31f, .115f, -.12f), new Vector3(.25f, .04f, .36f), g.woodMaterial);
            for (int i = 0; i < 5; i++)
                Box("Feed belt link", head, new Vector3(-.23f + i * .028f, .015f, .02f), new Vector3(.025f, .08f, .045f), g.brassMaterial != null ? g.brassMaterial : g.woodMaterial);
            Round("Barrel drive", head, new Vector3(0, 0, .23f), new Vector3(.27f, .09f, .27f), g.woodMaterial, new Vector3(90, 0, 0));
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * .082f, Mathf.Sin(angle) * .082f, .55f);
                Round("Steel barrel", head, offset, new Vector3(.045f, .31f, .045f), g.wallMaterial, new Vector3(90, 0, 0));
                Round("Dark bore", head, new Vector3(offset.x, offset.y, .866f), new Vector3(.031f, .003f, .031f), g.woodMaterial, new Vector3(90, 0, 0));
            }
            Round("Barrel retaining collar", head, new Vector3(0, 0, .74f), new Vector3(.23f, .025f, .23f), g.woodMaterial, new Vector3(90, 0, 0));
            Box("Optic housing", head, new Vector3(.145f, .19f, .1f), new Vector3(.12f, .1f, .2f), g.woodMaterial);
            Box("Optic glass", head, new Vector3(.145f, .19f, .205f), new Vector3(.07f, .045f, .009f), g.accentMaterial);
            Box("Rear carry handle", head, new Vector3(0, .22f, -.22f), new Vector3(.23f, .045f, .07f), g.wallMaterial);
            return root;
        }

        public static Transform SlowTrap(YardSurvival g, Transform parent, Vector3 position)
        {
            if (g.trapModel != null)
            {
                var instance = Object.Instantiate(g.trapModel, parent);
                instance.transform.localPosition = position;
                instance.transform.localRotation = Quaternion.identity;
                foreach (var collider in instance.GetComponentsInChildren<Collider>()) collider.enabled = false;
                return instance.transform;
            }
            var root = Group("Induction snare - slows ghouls", parent, position);
            Round("Induction base", root, new Vector3(0, .025f, 0), new Vector3(2.6f, .025f, 2.6f), g.wallMaterial);
            Round("Copper coil", root, new Vector3(0, .065f, 0), new Vector3(2.2f, .012f, 2.2f), g.yellowMaterial);
            Round("Insulated center", root, new Vector3(0, .09f, 0), new Vector3(1.95f, .012f, 1.95f), g.blueMaterial);
            Round("Coil inner ring", root, new Vector3(0, .12f, 0), new Vector3(1.4f, .008f, 1.4f), g.accentMaterial);
            Round("Center cap", root, new Vector3(0, .14f, 0), new Vector3(1.1f, .01f, 1.1f), g.wallMaterial);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                Round("Coil emitter", root, new Vector3(Mathf.Cos(angle), .17f, Mathf.Sin(angle)), new Vector3(.12f, .12f, .12f), g.creamMaterial);
            }
            return root;
        }
    }
}
