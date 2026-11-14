using UnityEngine;
using System.Collections.Generic;

namespace YardSurvivalPrototype
{
    public sealed partial class TerminalWear
    {
        void BuildModularHallDetails()
        {
            float span = 36f * YardSurvival.ArenaScale;
            float tile = span / 4f;
            var frame = Resources.Load<Material>("Spaceport/IndustrialRoof/Painted steel frame");
            var panel = Resources.Load<Material>("Spaceport/ModularHall/WallCladding");
            var amber = Resources.Load<Material>("Spaceport/AmberGlow");
            var roof = transform.parent.Find("Meshy roof");
            var cassette = LoadHallDetail("RoofCassette");
            var tray = LoadHallDetail("ServiceTray");
            var vent = LoadHallDetail("VentBay");
            var cladding = LoadHallDetail("UpperCladding");
            foreach (Transform section in roof)
            {
                if (!section.name.StartsWith("Intact Meshy section ")) continue;
                Vector3 center = section.localPosition;
                Quaternion rotation = section.localRotation;
                if (cassette != null)
                    PlaceHallDetail(cassette, center + Vector3.down * .21f, rotation * Quaternion.Euler(180, 0, 0),
                        new Vector3(tile * .88f, .35f, tile * .88f));
                if (tray != null)
                {
                    PlaceHallDetail(tray, center + rotation * new Vector3(0, -.55f, tile * .22f), rotation,
                        new Vector3(tile * .71f, .45f, .62f));
                    continue;
                }
                void Detail(Vector3 offset, Vector3 size, Material material) =>
                    Box(center + rotation * offset, size, material, rotation);

                // Each intact bay gets two support rails and a suspended utility tray.
                Detail(new Vector3(-tile * .32f, -.20f, 0), new Vector3(.20f, .26f, tile * .92f), frame);
                Detail(new Vector3(tile * .32f, -.20f, 0), new Vector3(.20f, .26f, tile * .92f), frame);
                Detail(new Vector3(0, -.40f, tile * .22f), new Vector3(tile * .71f, .19f, .64f), ink);
                Detail(new Vector3(0, -.50f, tile * .22f), new Vector3(tile * .69f, .06f, .49f), panel);
                for (int rib = -3; rib <= 3; rib++)
                    Detail(new Vector3(rib * tile * .095f, -.54f, tile * .22f), new Vector3(.055f, .035f, .53f), frame);
                Detail(new Vector3(0, -.55f, tile * .22f - .23f), new Vector3(.55f, .022f, .045f), amber);
            }

            float wallEdge = span * .5f - .4f;
            const float gate = 3.5f;
            float bayWidth = (wallEdge - gate) / 5f;
            for (int side = 0; side < 4; side++)
            {
                Quaternion rotation = Quaternion.Euler(0, side * 90f, 0);
                void Detail(Vector3 position, Vector3 size, Material material) =>
                    Box(rotation * position, size, material, rotation);

                foreach (int sign in new[] { -1, 1 })
                for (int bay = 0; bay < 5; bay++)
                {
                    float x = sign * (gate + (bay + .5f) * bayWidth);
                    if (Mathf.Abs(x) < 5f) continue;
                    float width = bayWidth * .72f;
                    if (vent != null)
                        PlaceHallDetail(vent, rotation * new Vector3(x, 1.75f, wallEdge - .20f), rotation,
                            new Vector3(width * .51f, 2.65f, .26f));
                    if (cladding != null)
                        PlaceHallDetail(cladding, rotation * new Vector3(x, 6.55f, wallEdge - .22f), rotation,
                            new Vector3(width, 1.58f, .26f));
                    if (vent != null && cladding != null) continue;
                    Detail(new Vector3(x, 1.75f, wallEdge - .18f), new Vector3(width, 2.65f, .15f), frame);
                    Detail(new Vector3(x, 1.75f, wallEdge - .28f), new Vector3(width - .22f, 2.40f, .10f), panel);
                    Detail(new Vector3(x, 2.34f, wallEdge - .35f), new Vector3(width * .66f, .66f, .06f), ink);
                    for (int slat = 0; slat < 5; slat++)
                        Detail(new Vector3(x, 2.10f + slat * .12f, wallEdge - .40f), new Vector3(width * .59f, .035f, .065f), frame);
                    Detail(new Vector3(x, 6.55f, wallEdge - .20f), new Vector3(width, 1.64f, .19f), frame);
                    Detail(new Vector3(x, 6.55f, wallEdge - .32f), new Vector3(width - .23f, 1.41f, .07f), panel);
                    Detail(new Vector3(x, 6.55f, wallEdge - .37f), new Vector3(.07f, 1.39f, .045f), steel);
                    if (bay % 2 == 0)
                        Detail(new Vector3(x + width * .32f, 1.28f, wallEdge - .35f), new Vector3(.095f, .20f, .035f), amber);
                }
            }
        }

        GameObject LoadHallDetail(string name)
        {
            string path = "Spaceport/HallDetails/" + name;
            var model = Resources.Load<GameObject>(path);
            var color = Resources.Load<Texture2D>(path + "Color");
            if (model == null || color == null) return null;
            var material = new Material(RuntimeShaders.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetTexture("_BaseMap", color);
            var normal = Resources.Load<Texture2D>(path + "Normal");
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            var metallic = Resources.Load<Texture2D>(path + "MetallicSmoothness");
            if (metallic != null)
            {
                material.SetTexture("_MetallicGlossMap", metallic);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            material.SetFloat("_Smoothness", .65f);
            ownedMaterials.Add(material);
            hallDetailMaterials.Add(model, material);
            return model;
        }

        readonly Dictionary<GameObject, Material> hallDetailMaterials = new Dictionary<GameObject, Material>();

        void PlaceHallDetail(GameObject model, Vector3 center, Quaternion rotation, Vector3 dimensions)
        {
            var mount = new GameObject(model.name);
            mount.transform.SetParent(transform, false);
            var instance = Instantiate(model, mount.transform, false);
            var bounds = new Bounds();
            bool first = true;
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Matrix4x4 local = mount.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds meshBounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var direction = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                    Vector3 point = local.MultiplyPoint3x4(meshBounds.center + Vector3.Scale(direction, meshBounds.extents));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            if (first) { Destroy(mount); return; }
            Vector3 size = bounds.size;
            var scale = new Vector3(dimensions.x / Mathf.Max(size.x, .001f),
                dimensions.y / Mathf.Max(size.y, .001f), dimensions.z / Mathf.Max(size.z, .001f));
            // Keep instances on the imported mesh so repeated parts can use GPU instancing.
            mount.transform.localPosition = center;
            mount.transform.localRotation = rotation;
            mount.transform.localScale = scale;
            instance.transform.localPosition -= bounds.center;
            Material material = hallDetailMaterials[model];
            material.enableInstancing = true;
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
                renderer.allowOcclusionWhenDynamic = false;
            }
        }
    }
}
