using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace YardSurvivalPrototype
{
    // An independent detail layer keeps the authored terminal and its collision intact.
    public sealed partial class TerminalWear : MonoBehaviour
    {
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly Dictionary<Material, List<CombineInstance>> batches = new Dictionary<Material, List<CombineInstance>>();
        Material steel, rust, ink, ivory, red;
        Mesh cube;
        public static TerminalWear Build(Transform hall)
        {
            var old = hall.Find("Terminal wear and wayfinding");
            if (old != null) return old.GetComponent<TerminalWear>();
            var root = new GameObject("Terminal wear and wayfinding"); root.transform.SetParent(hall, false);
            var detail = root.AddComponent<TerminalWear>(); detail.Create(); return detail;
        }
        void Add(Mesh mesh, Material material, Matrix4x4 matrix)
        {
            if (material == null) return;
            if (!batches.TryGetValue(material, out var list)) batches[material] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = mesh, transform = matrix });
        }
        void Box(Vector3 p, Vector3 size, Material material, Quaternion rotation)
            => Add(cube, material, Matrix4x4.TRS(p, rotation, size));
        void Create()
        {
            steel = Resources.Load<Material>("Spaceport/Steel"); rust = Resources.Load<Material>("Spaceport/Rust");
            ink = Resources.Load<Material>("Spaceport/Ink"); ivory = Resources.Load<Material>("Spaceport/Hull");
            red = Resources.Load<Material>("Spaceport/RedGlow");
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            primitive.SetActive(false); cube = primitive.GetComponent<MeshFilter>().sharedMesh; Destroy(primitive);
            var random = new System.Random(270113);
            // Insets stay inside solid roof tiles, leaving the three rain shafts open.
            const float scale = 23f / 17f;
            // The Meshy replacement already supplies the underside and roof seams.
            bool hasMeshyRoof = transform.parent.Find("Meshy roof") != null;
            for (int x = -15; !hasMeshyRoof && x <= 15; x += 6)
                for (int z = -15; z <= 15; z += 6)
                {
                    if(DepotModels.IsCollapsedRoofTile(x,z))continue;
                    if ((x == -3 && z == -9) || (x == 9 && z == 3) || (x == -9 && z == 15)) continue;
                    for (int strip = 0; strip < 3; strip++)
                    {
                        float px = x * scale + (strip - 1) * 2.25f;
                        float sag = .10f + (float)random.NextDouble() * .38f;
                        Sheet(new Vector3(px, 7.94f, z * scale), 1.94f, 6.7f, sag, random);
                    }
                    // Split seams and missing fasteners expose dark gaps beneath buckled lining.
                    Box(new Vector3(x * scale, 7.92f, z * scale - 3.45f), new Vector3(7.1f,.055f,.12f), rust, Quaternion.identity);
                    if ((x + z) % 12 == 0)
                    {
                        var p = new Vector3(x * scale + 2.1f, 7.55f, z * scale + 2.7f);
                        Box(p, new Vector3(.48f,.035f,1.15f), rust, Quaternion.Euler(38,17,12));
                        Box(p + new Vector3(.3f,.12f,.2f), new Vector3(.065f,.07f,1.8f), steel, Quaternion.Euler(16,0,-7));
                    }
                }
            if (transform.parent.Find("Hall wall panels/Solid wall design") == null && transform.parent.Find("Hall wall panels") != null)
                BuildModularHallDetails();
            foreach (var pair in batches)
            {
                var mesh = new Mesh { name = "Terminal wear / " + pair.Key.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(pair.Value.ToArray(), true, true); ownedMeshes.Add(mesh);
                var obj = new GameObject(mesh.name); obj.transform.SetParent(transform, false);
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = pair.Key;
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
            batches.Clear();
            HallPatina.Apply(transform.parent);
        }
        void Sheet(Vector3 origin, float width, float length, float sag, System.Random random)
        {
            const int rows = 9;
            var vertices = new Vector3[rows * 3]; var triangles = new List<int>();
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < 3; col++)
                {
                    float t = row / (float)(rows - 1);
                    float drop = Mathf.Sin(t * Mathf.PI) * sag;
                    float crumple = (float)random.NextDouble() * .075f;
                    vertices[row * 3 + col] = new Vector3((col - 1) * width / 2,
                        -drop - crumple - (col == 1 ? .075f : 0), (t - .5f) * length);
                    if (row == 0 || col == 0) continue;
                    int a = (row - 1) * 3 + col - 1, b = a + 1, c = row * 3 + col - 1, d = c + 1;
                    // Downward-facing lining: keep normals coherent for the hall lighting.
                    triangles.AddRange(new[] { a,b,c,b,d,c });
                }
            var mesh = new Mesh { name = "Sagging corrugated lining" }; mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds(); ownedMeshes.Add(mesh);
            Add(mesh, steel, Matrix4x4.Translate(origin));
            Box(origin + new Vector3(-width/2,-.025f,0), new Vector3(.055f,.045f,length), rust, Quaternion.identity);
        }
        readonly List<Material> ownedMaterials = new List<Material>();
        Material concrete;
        void Sign(int side)
        {
            if (concrete == null)
            {
                concrete = new Material(steel) { name = "Chipped concrete lintels" };
                concrete.SetColor("_BaseColor",new Color(.23f,.245f,.25f));
                concrete.SetFloat("_Wear",.28f); concrete.SetFloat("_Concrete",1);
                concrete.SetFloat("_Metallic",0); concrete.SetFloat("_Smoothness",.1f); ownedMaterials.Add(concrete);
            }
            var rotation = Quaternion.Euler(0,side * 90,0);
            var origin = rotation * new Vector3(0,5.55f,23.02f);
            foreach(float edge in new[]{-1f,1f})
                Box(rotation*new Vector3(edge*13.3f,.55f,23.12f),new Vector3(18.4f,1.05f,.28f),concrete,rotation);
            // A structural lintel and pier, with paint directly on the masonry.
            Box(origin,new Vector3(9.3f,2.1f,.30f),concrete,rotation);
            Box(rotation*new Vector3(-3.8f,2.6f,22.99f),new Vector3(1.12f,3.8f,.32f),concrete,rotation);
            // A fabricated wayfinding cabinet stands off the lintel on visible brackets.
            Box(origin+rotation*new Vector3(0,.03f,-.26f),new Vector3(8.8f,1.72f,.18f),ink,rotation);
            Box(origin+rotation*new Vector3(0,.03f,-.36f),new Vector3(8.54f,1.48f,.045f),steel,rotation);
            Box(origin+rotation*new Vector3(.54f,.11f,-.388f),new Vector3(7.12f,1.18f,.016f),ink,rotation);
            foreach(float y in new[]{-.82f,.88f})Box(origin+rotation*new Vector3(0,y,-.385f),new Vector3(8.9f,.045f,.09f),steel,rotation);
            foreach(float x in new[]{-4.4f,4.4f})Box(origin+rotation*new Vector3(x,.03f,-.385f),new Vector3(.045f,1.7f,.09f),steel,rotation);
            foreach(float x in new[]{-4.13f,-2.1f,2.1f,4.13f})
            {
                Box(origin+rotation*new Vector3(x,.03f,-.18f),new Vector3(.16f,2.2f,.16f),steel,rotation);
                foreach(float y in new[]{-.66f,.72f})
                {
                    Box(origin+rotation*new Vector3(x,y,-.406f),new Vector3(.09f,.09f,.025f),ivory,rotation*Quaternion.Euler(0,0,45));
                    Box(origin+rotation*new Vector3(x,y,-.423f),new Vector3(.045f,.012f,.006f),ink,rotation);
                }
            }
            // Sector stripe, separate title plate, fine divider and secondary route information.
            Box(origin+rotation*new Vector3(-3.77f,.03f,-.395f),new Vector3(.48f,1.33f,.02f),ivory,rotation);
            for(int bar=0;bar<=side;bar++)Box(origin+rotation*new Vector3(-3.77f,-.4f+bar*.23f,-.41f),new Vector3(.3f,.065f,.008f),ink,rotation);
            Box(origin+rotation*new Vector3(.42f,-.30f,-.405f),new Vector3(6.5f,.018f,.008f),steel,rotation);
            Stencil(side,"Title",origin+rotation*new Vector3(.35f,.24f,-.411f),rotation,new Vector2(6.75f,.79f));
            Stencil(side,"Detail",origin+rotation*new Vector3(-.5f,-.53f,-.412f),rotation,new Vector2(4.3f,.22f));
            Box(origin+rotation*new Vector3(3.35f,-.53f,-.413f),new Vector3(.58f,.035f,.01f),ivory,rotation);
            foreach(float sign in new[]{-1f,1f})Box(origin+rotation*new Vector3(3.56f,-.53f+sign*.09f,-.413f),new Vector3(.27f,.035f,.01f),ivory,rotation*Quaternion.Euler(0,0,sign*-42));
            Stencil(side,"Number",rotation*new Vector3(-3.8f,2.7f,22.815f),rotation,new Vector2(.85f,1.65f));
            // Surface-mounted power feed and a junction box tie the sign into the building.
            Box(origin+rotation*new Vector3(4.62f,-.2f,-.24f),new Vector3(.035f,2.8f,.035f),steel,rotation);
            Box(origin+rotation*new Vector3(4.28f,1.18f,-.24f),new Vector3(.72f,.035f,.035f),steel,rotation);
            Box(origin+rotation*new Vector3(4.62f,-1.25f,-.28f),new Vector3(.28f,.34f,.13f),ink,rotation);
            foreach(float y in new[]{-.6f,.5f})Box(origin+rotation*new Vector3(4.62f,y,-.27f),new Vector3(.13f,.035f,.04f),steel,rotation);
            // Broken lower edges and exposed aggregate make the lintel silhouette irregular.
            var random=new System.Random(234+side);
            for(int i=0;i<23;i++)
            {
                float x=-4.4f+i*.4f;
                Box(origin+rotation*new Vector3(x,-1.02f,-.02f),new Vector3(.18f+(float)random.NextDouble()*.2f,.08f+(float)random.NextDouble()*.12f,.28f),concrete,rotation*Quaternion.Euler(0,0,(float)random.NextDouble()*16-8));
            }
            foreach(float x in new[]{-2.6f,2.6f})
            {
                Box(origin+rotation*new Vector3(x,1.05f,-.52f),new Vector3(.13f,.10f,.5f),steel,rotation);
                Box(origin+rotation*new Vector3(x,1.02f,-.76f),new Vector3(.78f,.14f,.28f),ink,rotation);
                Box(origin+rotation*new Vector3(x,.94f,-.76f),new Vector3(.62f,.018f,.19f),Resources.Load<Material>("Spaceport/AmberGlow"),rotation);
                var wash=new GameObject("Bay "+(side+1)+" shielded sign lamp").AddComponent<Light>();wash.transform.SetParent(transform,false);
                wash.transform.localPosition=origin+rotation*new Vector3(x,.91f,-.78f);
                wash.transform.localRotation=rotation*Quaternion.LookRotation(new Vector3(0,-1,.35f));
                wash.type=LightType.Spot;wash.spotAngle=105;wash.innerSpotAngle=65;wash.range=2.8f;wash.intensity=2.5f;wash.color=new Color(1,.84f,.65f);wash.shadows=LightShadows.None;
            }
            var lamp = new GameObject("Bay " + (side+1) + " warm stencil wash").AddComponent<Light>();
            lamp.transform.SetParent(transform,false); lamp.transform.localPosition=origin+rotation*new Vector3(-4.1f,.2f,-.8f);
            lamp.type=LightType.Point; lamp.color=new Color(1,.65f,.34f); lamp.intensity=1.5f; lamp.range=5.5f; lamp.shadows=LightShadows.None;
            Box(origin+rotation*new Vector3(-4.1f,.2f,-.28f),new Vector3(.25f,.55f,.19f),ink,rotation);
            Box(origin+rotation*new Vector3(-4.1f,.2f,-.39f),new Vector3(.13f,.36f,.06f),Resources.Load<Material>("Spaceport/AmberGlow"),rotation);
            SignMachining(side,origin,rotation);
        }
        void Stencil(int side,string kind,Vector3 position,Quaternion rotation,Vector2 size)
        {
            var texture=Resources.Load<Texture2D>("AreaStencils/"+side+"-"+kind);
            var material=new Material(RuntimeShaders.Find("Universal Render Pipeline/Lit")) { name="Weathered paint / "+side+" / "+kind };
            material.SetTexture("_BaseMap",texture); material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_AlphaClip",1); material.SetFloat("_Cutoff",.38f); material.EnableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_EMISSION"); material.SetTexture("_EmissionMap",texture); material.SetColor("_EmissionColor",new Color(.035f,.035f,.032f));
            material.SetFloat("_Smoothness",.08f); material.SetFloat("_Metallic",0);
            material.SetOverrideTag("RenderType","TransparentCutout"); material.renderQueue=2450; ownedMaterials.Add(material);
            var mesh=new Mesh { name="Painted stencil quad" };
            mesh.vertices=new[]{new Vector3(-size.x/2,-size.y/2,0),new Vector3(size.x/2,-size.y/2,0),new Vector3(-size.x/2,size.y/2,0),new Vector3(size.x/2,size.y/2,0)};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one}; mesh.triangles=new[]{0,2,1,2,3,1};
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); ownedMeshes.Add(mesh);
            var obj=new GameObject("Painted stencil / "+side+" / "+kind); obj.transform.SetParent(transform,false);
            obj.transform.localPosition=position; obj.transform.localRotation=rotation;
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        void OnDestroy() { foreach(var mesh in ownedMeshes) if(mesh!=null) Destroy(mesh); foreach(var material in ownedMaterials) if(material!=null) Destroy(material); }
    }
}
