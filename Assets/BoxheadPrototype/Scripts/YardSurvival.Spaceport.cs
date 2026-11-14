using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        const float RackWidth = 1.27f, RackDepth = 1.05f;
        const float RackPitch = 1.29f, RackRowOffset = .55f;
        const float RackBankWidth = 5 * RackPitch + RackWidth;
        const float RackBankDepth = 2 * RackRowOffset + RackDepth;

        static void TightenServerRackBanks(Transform racks, Transform oldHall)
        {
            foreach (Transform cabinet in racks)
            {
                Vector3 position = cabinet.localPosition;
                float centerX = Mathf.Sign(position.x) * 10 * ArenaScale;
                float centerZ = Mathf.Sign(position.z) * 9 * ArenaScale;
                float slot = Mathf.Round((position.x - centerX) / 1.33f + 2.5f) - 2.5f;
                cabinet.localPosition = new Vector3(centerX + slot * RackPitch, position.y,
                    centerZ + Mathf.Sign(position.z - centerZ) * RackRowOffset);
            }

            foreach (var collider in oldHall.GetComponentsInChildren<BoxCollider>())
            {
                if (collider.name != "Freight stack collision") continue;
                Vector3 scale = collider.transform.localScale;
                scale.x = RackBankWidth / ArenaScale;
                scale.z = RackBankDepth / ArenaScale;
                collider.transform.localScale = scale;
            }
        }

        void RefineSpaceportSurfaces(GameObject root)
        {
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if(renderer.name=="Ink contour"){renderer.enabled=false;continue;}
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                string name=renderer.sharedMaterial!=null?renderer.sharedMaterial.name:"";
                var ceiling = root.transform.Find("Meshy roof");
                if (name.StartsWith("SolidHall_") || name.StartsWith("ServerRack_") || name.StartsWith("LaunchField") || (ceiling != null && renderer.transform.IsChildOf(ceiling)))
                {
                    renderer.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
                    continue;
                }
                bool glass=name.Contains("Glass"),ground=name.Contains("Asphalt"),paint=name.Contains("Panel")||name.Contains("Hull")||name.Contains("CraftedTerminal_Steel")||name.Contains("RackBody")||name.Contains("Paint");
                bool lens=name.Contains("Glow")||name.Contains("Lens");
                if(root.name=="Spaceport exterior"&&name.Contains("TealGlow"))
                {
                    block.SetColor("_BaseColor",new Color(.075f,.085f,.085f));
                    block.SetFloat("_Glow",0);
                }
                bool rust=name.Contains("Rust"),rubber=name.Contains("Ink");
                if (root.name == "Spaceport exterior" && ground)
                {
                    block.SetFloat("_Ground", 0);
                    block.SetColor("_BaseColor", new Color(.13f, .145f, .15f));
                }
                block.SetFloat("_Metallic",ground||lens||rust||rubber?0:glass?.15f:paint?.12f:.72f);
                block.SetFloat("_Smoothness",rust?.18f:rubber?.16f:ground?.30f:glass?.82f:paint?.32f:.48f);
                if (root.name == "Spaceport terminal")
                {
                    // Cabinet panels and exposed rails need enough separation to read in the aisles.
                    switch (renderer.name)
                    {
                        case "Spaceport_RackBody":
                            block.SetColor("_BaseColor", new Color(.065f, .077f, .086f));
                            break;
                        case "Spaceport_RackPanel":
                            block.SetColor("_BaseColor", new Color(.12f, .16f, .18f));
                            break;
                        case "Spaceport_RackMetal":
                            block.SetColor("_BaseColor", new Color(.33f, .36f, .38f));
                            block.SetFloat("_Metallic", .48f);
                            block.SetFloat("_Smoothness", .36f);
                            break;
                        case "Spaceport_Steel":
                            block.SetColor("_BaseColor", new Color(.19f, .21f, .22f));
                            break;
                        case "Spaceport_Hull":
                            block.SetColor("_BaseColor", new Color(.39f, .38f, .34f));
                            break;
                    }
                }
                renderer.SetPropertyBlock(block);
                renderer.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
            }
        }
        void SetupSpaceportExterior()
        {
            if (transform.Find("Spaceport exterior") != null) return;
            var model = Resources.Load<GameObject>("Spaceport/SpaceportSet");
            if (model == null) { Debug.LogWarning("Spaceport exterior prefab is missing."); return; }
            var root = Instantiate(model, transform, false);
            root.name = "Spaceport exterior";
            DepotModels.RunwayExtension(this, root.transform);
            var launchField = Resources.Load<GameObject>("Spaceport/LaunchField/LaunchFieldSet");
            if (launchField != null) Instantiate(launchField, root.transform, false).name = "Rocket launch field";
            RefineSpaceportSurfaces(root);
            StormLandmarks.Build(root.transform);
            ExteriorStormAtmosphere.Build(root.transform);
            ExteriorServiceDetails.Build(root.transform);
            root.AddComponent<LaunchpadLighting>();
            ExteriorDecay.Build(root.transform);
            // Authored in world metres, independent of the hall's legacy scale.
            root.transform.localPosition = Vector3.zero;
            var sky = Resources.Load<Material>("Spaceport/SpaceportSky");
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.fogColor = DepotNightLighting.Fog;
            RenderSettings.fogDensity = .024f;
            spaceportEveningWeather = true;
            arenaCamera.farClipPlane = 800;
            // The terminal is assembled at runtime; the tutorial scene's old bake
            // incorrectly culls its roof and interior from exterior viewpoints.
            arenaCamera.useOcclusionCulling = false;
            var mist = Resources.Load<Material>("Spaceport/SpaceportMist");
            if (mist != null)
                for (int i = 0; i < 4; i++)
                {
                    var sheet = new GameObject("Low drifting mist " + i);
                    sheet.transform.SetParent(root.transform, false);
                    sheet.transform.localPosition = new Vector3(0, 2.2f, 44 + i * 24);
                    sheet.transform.localScale = new Vector3(65, 5.5f, 1);
                    var mesh = new Mesh { name = "Spaceport mist quad" };
                    mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(-.5f,.5f,0), new Vector3(.5f,.5f,0) };
                    mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                    mesh.triangles = new[] { 0,2,1,2,3,1 }; mesh.RecalculateBounds();
                    sheet.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = sheet.AddComponent<MeshRenderer>(); renderer.sharedMaterial = mist;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            var yard = transform.Find("Warehouse yard");
            if (yard == null) return;
            var terminal = Resources.Load<GameObject>("Spaceport/ModularHallTerminalSet") ?? Resources.Load<GameObject>("Spaceport/RestoredRoofTerminalSet") ?? Resources.Load<GameObject>("Spaceport/MeshyRackTerminalSet") ?? Resources.Load<GameObject>("Spaceport/CraftedTerminalSet") ?? Resources.Load<GameObject>("Spaceport/TerminalSet");
            var oldHall = yard.Find("Warehouse hall");
            if (terminal != null && oldHall != null)
            {
                var interior = Instantiate(terminal, transform, false);
                interior.name = "Spaceport terminal";
                fullRoofCollider = interior.transform.Find("Meshy roof")?.GetComponentInChildren<MeshCollider>();
                RefineSpaceportSurfaces(interior);
                interior.AddComponent<HorrorHallLighting>();
                var racks = interior.transform.Find("Textured server racks");
                if (racks != null)
                {
                    TightenServerRackBanks(racks, oldHall);
                    RegisterObstacles();
                }
                TerminalWear.Build(interior.transform);
                LoadingBayCorner.Build(interior.transform);
                DetailedFloorDebris.Build(interior.transform);
                var indoorMist = Resources.Load<Material>("Spaceport/TerminalMist");
                var mistTemplate = root.transform.Find("Low drifting mist 0");
                if (indoorMist != null && mistTemplate != null)
                {
                    var atmosphere = new GameObject("Terminal atmosphere").transform;
                    atmosphere.SetParent(transform, false);
                    for (int gate = 0; gate < 4; gate++)
                    {
                        var sheet = Instantiate(mistTemplate.gameObject, atmosphere, false);
                        sheet.name = "Entrance ground mist " + gate;
                        var rotation = Quaternion.Euler(0, gate * 90, 0);
                        sheet.transform.localPosition = rotation * new Vector3(0, .65f, 21.3f);
                        sheet.transform.localRotation = rotation;
                        sheet.transform.localScale = new Vector3(6, 1.6f, 1);
                        sheet.GetComponent<MeshRenderer>().sharedMaterial = indoorMist;
                    }
                }
                // Retain the proven collision volumes and working entrance lights.
                // The replacement is authored at world scale, not the legacy yard scale.
                foreach (var surface in oldHall.GetComponentsInChildren<Renderer>())
                    surface.enabled = false;
                if(interior.transform.Find("Open entrances without gate posts")!=null)
                    foreach(var collider in oldHall.GetComponentsInChildren<BoxCollider>())
                        if(collider.name=="Loading door upright")collider.enabled=false;
                // The literal slash in this object's name is not a hierarchy separator.
                Transform weather = null;
                foreach (Transform child in transform)
                    if (child.name == "Rain / soaked depot") { weather = child; break; }
                if (weather != null)
                    foreach (Transform detail in weather)
                        if (detail.name == "Buckled roof sheet" || detail.name == "Rust runoff / facade grime")
                            detail.gameObject.SetActive(false);
            }
            foreach (Transform child in yard)
            {
                if (child.name.StartsWith("Abandoned highway") || child.name == "Derelict water tower" ||
                    child.name == "Exterior landmarks" || child.name.StartsWith("Ashfall terrain ridge"))
                    child.gameObject.SetActive(false);
                else if (child.name == "Exterior terrain" || child.name == "Ashfall terrain ridge 1")
                {
                    var terrain = Resources.Load<Material>("Spaceport/SpaceportTerrain");
                    if (terrain != null && child.TryGetComponent<Renderer>(out var renderer)) renderer.sharedMaterial = terrain;
                }
            }
        }
    }
}

