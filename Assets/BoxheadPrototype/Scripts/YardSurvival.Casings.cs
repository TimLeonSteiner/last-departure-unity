using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        readonly Mesh[] casingMeshes=new Mesh[3];
        Mesh CasingMesh(int kind)
        {
            if(casingMeshes[kind]!=null)return casingMeshes[kind];
            const int sides=24;
            // Closed primer base, extractor rim, case wall and inward lip around an open mouth.
            float[] radius={0,.52f,.57f,.57f,.49f,.49f,.43f,.43f,0};
            float[] z={0,0,.045f,.11f,.14f,.96f,1, .18f,.18f};
            float width=kind==2?.020f:kind==1?.011f:.010f,length=kind==2?.055f:kind==1?.050f:.024f;
            var vertices=new Vector3[radius.Length*sides];var uv=new Vector2[vertices.Length];var triangles=new int[(radius.Length-1)*sides*6];
            for(int ring=0;ring<radius.Length;ring++)for(int i=0;i<sides;i++)
            {float a=i*Mathf.PI*2/sides;int v=ring*sides+i;vertices[v]=new Vector3(Mathf.Cos(a)*radius[ring]*width,Mathf.Sin(a)*radius[ring]*width,z[ring]*length);uv[v]=new Vector2(i/(float)sides,z[ring]);}
            int at=0;for(int ring=0;ring<radius.Length-1;ring++)for(int i=0;i<sides;i++)
            {int a=ring*sides+i,b=ring*sides+(i+1)%sides,c=a+sides,d=b+sides;triangles[at++]=a;triangles[at++]=b;triangles[at++]=c;triangles[at++]=b;triangles[at++]=d;triangles[at++]=c;}
            var mesh=new Mesh{name="Hollow spent cartridge "+kind};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return casingMeshes[kind]=mesh;
        }
        void EjectCasing()
        {
            int count=0;for(int i=flashes.Count-1;i>=0;i--)if(flashes[i].casing&&++count>=90){Destroy(flashes[i].body);flashes.RemoveAt(i);}
            int kind=WeaponCatalog.IsShotgun(weaponId)||weaponId==4?2:weaponId==2||weaponId==11||weaponId==12||weaponId==13?0:1;
            var casing=new GameObject("Spent cartridge casing");casing.transform.SetParent(actors,false);
            casing.transform.position=arenaCamera.transform.position+arenaCamera.transform.right*.25f+arenaCamera.transform.forward*.45f-arenaCamera.transform.up*.12f;
            casing.transform.rotation=arenaCamera.transform.rotation*Quaternion.Euler(20,90,10);
            casing.AddComponent<MeshFilter>().sharedMesh=CasingMesh(kind);casing.AddComponent<MeshRenderer>().sharedMaterial=brassMaterial;
            flashes.Add(new Flash{body=casing,life=3,casing=true,velocity=arenaCamera.transform.right*1.9f+Vector3.up*1.7f+player.forward*.25f});
        }
    }
}
