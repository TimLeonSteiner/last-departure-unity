using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class ImportedZombie
    {
        bool archetypeDetailed;
        Material detailSteel, detailRust, detailCloth, detailPale;
        readonly Bounds[] detailBounds = new Bounds[6];
        readonly Vector3[][] detailSurface = new Vector3[6][];
        readonly System.Collections.Generic.List<Mesh> costumeMeshes = new System.Collections.Generic.List<Mesh>();

        void AddArchetypeDetails()
        {
            if (preserveSourceAppearance || archetypeDetailed || archetype == null) return;
            archetypeDetailed = true;
            detailSteel = Resources.Load<Material>("Spaceport/Steel");
            detailRust = Resources.Load<Material>("Spaceport/Rust");
            detailCloth = Resources.Load<Material>("Spaceport/Ink");
            detailPale = Resources.Load<Material>("Spaceport/Hull");
            var frame = transform.parent != null ? transform.parent : transform;
            for (int region = 0; region < 6; region++)
            {
                var mesh = new Mesh(); parts[region].BakeMesh(mesh, false);
                var vertices = mesh.vertices;
                if (vertices.Length > 0)
                {
                    // BakeMesh(false) contains the skin's rendered scale already.
                    Vector3 World(Vector3 p) => parts[region].transform.position + parts[region].transform.rotation * p;
                    var bounds = new Bounds(frame.InverseTransformPoint(World(vertices[0])), Vector3.zero);
                    foreach (var vertex in vertices) bounds.Encapsulate(frame.InverseTransformPoint(World(vertex)));
                    detailBounds[region] = bounds;
                    detailSurface[region]=new Vector3[vertices.Length];
                    for(int i=0;i<vertices.Length;i++)detailSurface[region][i]=frame.InverseTransformPoint(World(vertices[i]));
                }
                Destroy(mesh);
            }
            switch (archetype.kind)
            {
                case ZombieKind.Shambler:
                    Plate(5,"Left harness strap",-.27f,.12f,.045f,.72f,detailCloth,8);
                    Plate(5,"Right harness strap",.27f,.12f,.045f,.72f,detailCloth,-8);
                    Plate(5,"Work belt",0,-.30f,.82f,.055f,detailCloth);
                    Plate(5,"Belt buckle",0,-.30f,.12f,.10f,detailSteel);
                    Plate(5,"Damaged ID card",-.21f,.24f,.17f,.15f,detailPale,-12);
                    Plate(5,"ID stripe",-.21f,.25f,.14f,.025f,detailRust,-12,.035f);
                    Plate(3,"Torn knee patch",0,0,.5f,.14f,detailCloth,12);
                    Scar(0,-.24f,.08f,22);
                    break;
                case ZombieKind.Limper:
                    Plate(5,"Diagonal utility sling",0,.03f,.075f,.85f,detailCloth,28);
                    Plate(5,"Utility pouch",.25f,-.3f,.26f,.23f,detailCloth,-8);
                    Plate(5,"Pouch clasp",.25f,-.27f,.08f,.08f,detailSteel);
                    Plate(3,"Broken leg brace outer",.33f,-.1f,.10f,.65f,detailSteel,-5);
                    Plate(3,"Brace knee strap",0,.12f,.85f,.07f,detailCloth);
                    Plate(3,"Brace ankle strap",0,-.32f,.7f,.06f,detailCloth);
                    Plate(3,"Brace hinge",.28f,.12f,.23f,.11f,detailRust);
                    Scar(5,-.12f,.18f,-25); Scar(0,.22f,-.1f,-15);
                    break;
                case ZombieKind.Twitcher:
                    Plate(5,"Broken restraint strap",0,-.1f,.82f,.055f,detailCloth,-12);
                    Plate(5,"Restraint clasp",.12f,-.12f,.12f,.1f,detailSteel,-12);
                    Plate(1,"Left restraint cuff",0,-.32f,.85f,.08f,detailSteel);
                    Plate(2,"Right restraint cuff",0,-.32f,.85f,.08f,detailSteel);
                    Plate(5,"Hanging restraint tail",.26f,-.28f,.05f,.35f,detailCloth,17);
                    Scar(0,-.19f,-.15f,30); Scar(5,-.12f,.23f,24); Scar(5,.12f,.12f,-22);
                    break;
                case ZombieKind.Brute:
                    Plate(5,"Cargo harness left",-.27f,.15f,.09f,.66f,detailCloth,12);
                    Plate(5,"Cargo harness right",.27f,.15f,.09f,.66f,detailCloth,-12);
                    Plate(5,"Cargo waist belt",0,-.16f,.94f,.07f,detailCloth);
                    Plate(5,"Heavy harness buckle",0,-.16f,.14f,.12f,detailSteel);
                    Plate(1,"Salvaged shoulder plate",0,.32f,.95f,.29f,detailSteel,-12);
                    Plate(1,"Shoulder reinforcement",0,.32f,.65f,.055f,detailRust,-12,.06f);
                    Plate(2,"Forearm wrap",0,-.18f,.88f,.16f,detailCloth,8);
                    Scar(0,-.2f,.12f,18); Scar(5,.24f,-.26f,25);
                    break;
            }
        }
        void Scar(int region,float x,float y,float angle)
        {
            Plate(region,"Old wound",x,y,.075f,.19f,detailRust,angle,.015f);
            for(int stitch=0;stitch<3;stitch++)
                Plate(region,"Wound stitch",x,y-.055f+stitch*.055f,.13f,.012f,detailCloth,angle,.022f);
        }
        void Plate(int region,string name,float x,float y,float width,float height,Material material,float angle=0,float depth=.02f)
        {
            var bounds=detailBounds[region];
            if(bounds.size.sqrMagnitude<.00001f)return;
            Transform bone = region==0?gaitHead:region==5?gaitChest:null;
            if(bone==null&&severJoints!=null&&severJoints.Length>region)bone=severJoints[region];
            if(bone==null)bone=parts[region].rootBone;
            if(bone==null)return;
            var frame=transform.parent!=null?transform.parent:transform;
            var obj=new GameObject(name);obj.transform.SetParent(bone,false);
            const int columns=4,rows=10;
            var vertices=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[vertices.Length];var triangles=new int[columns*rows*6];
            Quaternion rotation=Quaternion.Euler(0,0,angle);
            for(int row=0;row<=rows;row++)for(int col=0;col<=columns;col++)
            {
                Vector3 offset=rotation*new Vector3((col/(float)columns-.5f)*width*bounds.size.x,(row/(float)rows-.5f)*height*bounds.size.y,0);
                Vector3 p=new Vector3(bounds.center.x+x*bounds.size.x+offset.x,bounds.center.y+y*bounds.size.y+offset.y,0);
                p.x=Mathf.Clamp(p.x,bounds.min.x+.005f,bounds.max.x-.005f);p.y=Mathf.Clamp(p.y,bounds.min.y+.005f,bounds.max.y-.005f);
                float nearest=float.MaxValue,z=bounds.max.z;
                int sampleStep=Mathf.Max(1,detailSurface[region].Length/768);
                for(int sample=0;sample<detailSurface[region].Length;sample+=sampleStep)
                {
                    var surface=detailSurface[region][sample];
                    if(surface.z<bounds.center.z)continue;
                    float distance=(surface.x-p.x)*(surface.x-p.x)+(surface.y-p.y)*(surface.y-p.y);
                    if(distance<nearest){nearest=distance;z=surface.z;}
                }
                p.z=z+.004f+depth*.12f;
                int index=row*(columns+1)+col;vertices[index]=bone.InverseTransformPoint(frame.TransformPoint(p));uv[index]=new Vector2(col/(float)columns,row/(float)rows);
            }
            int t=0;
            for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
            {
                int a=row*(columns+1)+col,b=a+1,c=a+columns+1,d=c+1;
                triangles[t++]=a;triangles[t++]=b;triangles[t++]=c;triangles[t++]=b;triangles[t++]=d;triangles[t++]=c;
            }
            var costume=new Mesh{name=name};costume.vertices=vertices;costume.uv=uv;costume.triangles=triangles;costume.RecalculateNormals();costume.RecalculateBounds();costumeMeshes.Add(costume);
            obj.AddComponent<MeshFilter>().sharedMesh=costume;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
            attachedDetails[region].Add(obj);
        }
        void ReleaseCostumeMeshes(){foreach(var mesh in costumeMeshes)if(mesh!=null)Destroy(mesh);}
    }
}
