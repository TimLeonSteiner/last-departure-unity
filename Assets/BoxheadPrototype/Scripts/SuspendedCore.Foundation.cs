using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class SuspendedCore
    {
        void BuildPlatformFoundation()
        {
            var root=new GameObject("Integrated heart floor foundation").transform;
            root.SetParent(transform,false);
            var steel=Resources.Load<Material>("Spaceport/CraftedTerminal_Steel");
            var dark=Resources.Load<Material>("Spaceport/CraftedTerminal_Asphalt");
            var trim=Resources.Load<Material>("Spaceport/CraftedTerminal_Rust");
            if(steel==null||dark==null||trim==null){Destroy(root.gameObject);return;}
            // A floor-level circular mounting collar slips under the existing ramp.
            // Panel joints and the outer gasket remain flush, preserving every approach.
            for(int panel=0;panel<16;panel++)
            {
                var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
                for(int step=0;step<=16;step++)
                {
                    float a=(panel+(step/16f))*(Mathf.PI*2/16)+.002f*(1-step/8f);
                    var radial=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
                    const float outer=4.5f;
                    foreach(var v in new[]{radial*4.03f+Vector3.up*.018f,radial*outer+Vector3.up*.008f})
                    {vertices.Add(v);uv.Add(new Vector2(v.x,v.z));}
                    if(step==16)continue;
                    int n=step*2;triangles.AddRange(new[]{n,n+1,n+2,n+1,n+3,n+2});
                }
                MeshObject("Inset foundation plate",vertices,triangles,steel,root,uv);
                float angle=(panel+.5f)*Mathf.PI*2/16;
                var direction=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                var anchor=Shape("Recessed floor anchor",PrimitiveType.Cube,direction*4.29f+Vector3.up*.016f,new Vector3(.17f,.016f,.28f),dark,root);
                anchor.localRotation=Quaternion.Euler(0,angle*Mathf.Rad2Deg,0);
                Shape("Foundation anchor head",PrimitiveType.Cylinder,direction*4.29f+Vector3.up*.028f,new Vector3(.07f,.008f,.07f),trim,root);
            }
            // Covered service trenches extend from behind the machinery into the hall grid.
            foreach(float x in new[]{-2.6f,2.6f})
            {
                Shape("Flush service channel bed",PrimitiveType.Cube,new Vector3(x,.006f,5.55f),new Vector3(.58f,.012f,4.3f),dark,root);
                foreach(float edge in new[]{-.29f,.29f})
                    Shape("Service channel edge",PrimitiveType.Cube,new Vector3(x+edge,.014f,5.55f),new Vector3(.035f,.012f,4.3f),trim,root);
                for(int i=0;i<12;i++)
                    Shape("Removable service cover",PrimitiveType.Cube,new Vector3(x,.016f,3.6f+i*.35f),new Vector3(.50f,.014f,.32f),steel,root);
                Shape("Floor service access frame",PrimitiveType.Cube,new Vector3(x,.012f,7.85f),new Vector3(.86f,.02f,.65f),dark,root);
                Shape("Floor service access cover",PrimitiveType.Cube,new Vector3(x,.024f,7.85f),new Vector3(.76f,.012f,.55f),steel,root);
                Shape("Access cover recessed handle",PrimitiveType.Cube,new Vector3(x,.032f,7.85f),new Vector3(.22f,.008f,.045f),dark,root);
            }
            // Keep the dressing to three batches and no new navigation obstacles.
            var batches=new Dictionary<Material,List<CombineInstance>>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();
                if(!batches.TryGetValue(renderer.sharedMaterial,out var list))
                    batches.Add(renderer.sharedMaterial,list=new List<CombineInstance>());
                list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                renderer.enabled=false;Destroy(filter.gameObject);
            }
            foreach(var batch in batches)
            {
                var mesh=new Mesh{name="Heart foundation / "+batch.Key.name};mesh.CombineMeshes(batch.Value.ToArray());meshes.Add(mesh);
                var obj=new GameObject(mesh.name);obj.transform.SetParent(root,false);
                obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=batch.Key;
            }
        }
    }
}
