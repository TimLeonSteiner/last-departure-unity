using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class SuspendedCore
    {
        MeshCollider platformFloorCollider;
        public float PlatformShotDistance(Ray ray,float limit)
        {
            foreach(var bounds in BayBarriers)if(bounds.IntersectRay(ray,out float distance)&&distance>=0)limit=Mathf.Min(limit,distance);
            if(platformFloorCollider!=null&&platformFloorCollider.Raycast(ray,out var hit,limit))limit=hit.distance;
            return limit;
        }
        public bool HasMeshyPlatform {get;private set;}
        public float PlatformFloor(Vector3 world)
        {
            if(!HasMeshyPlatform)return 0;
            var p=transform.InverseTransformPoint(world);
            float radius=new Vector2(p.x,p.z).magnitude;
            return transform.position.y+Mathf.Clamp01((4.15f-radius)/.65f)*.18f;
        }
        bool BuildMeshyPlatform()
        {
            if(BuildModularHeartPlatform())return true;
            var prefab=Resources.Load<GameObject>("HeartPlatform/Platform");
            if(prefab==null)return false;
            var model=Instantiate(prefab,transform,false);model.name="Meshy biomechanical heart platform";
            heart=model.transform;HasMeshyPlatform=true;
            chainedHeartSurface=new Material(model.GetComponent<MeshRenderer>().sharedMaterial);
            chainedHeartSurface.name="Living biomechanical platform";
            model.GetComponent<MeshRenderer>().sharedMaterial=chainedHeartSurface;
            var properties=new MaterialPropertyBlock();
            properties.SetMatrix("_HeartFromObject",Matrix4x4.Scale(Vector3.one/4.7f));
            model.GetComponent<MeshRenderer>().SetPropertyBlock(properties);
            // Only the pumps and rear arch feet obstruct planar navigation.
            foreach(int side in new[]{-1,1})
            {
                PlatformBarrier(new Vector3(side*2.33f,.75f,-.70f),new Vector3(1.15f,1.4f,1.5f));
                PlatformBarrier(new Vector3(side*3.5f,1.8f,1.75f),new Vector3(.85f,3.5f,1.1f));
            }
            BuildPlatformFloorCollider();
            return true;
        }
        void PlatformBarrier(Vector3 center,Vector3 size)
        {
            var obj=new GameObject("Platform machinery collision");obj.transform.SetParent(transform,false);obj.transform.localPosition=center;
            obj.AddComponent<BoxCollider>().size=size;
            BayBarriers.Add(new Bounds(transform.TransformPoint(center),Vector3.Scale(size,transform.lossyScale)));
        }
        void BuildPlatformFloorCollider()
        {
            // A smooth low deck and shallow perimeter ramp keep feet and ragdolls
            // clear of small decorative ridges in the generated surface.
            const int count=64;var vertices=new Vector3[1+count*2];var triangles=new int[count*9];
            vertices[0]=Vector3.up*.18f;
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                vertices[1+i]=radial*3.5f+Vector3.up*.18f;vertices[1+count+i]=radial*4.15f;
                int next=(i+1)%count,t=i*9;
                triangles[t]=0;triangles[t+1]=1+next;triangles[t+2]=1+i;
                triangles[t+3]=1+i;triangles[t+4]=1+next;triangles[t+5]=1+count+next;
                triangles[t+6]=1+i;triangles[t+7]=1+count+next;triangles[t+8]=1+count+i;
            }
            var mesh=new Mesh{name="Walkable platform ramp"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateBounds();meshes.Add(mesh);
            var floor=new GameObject("Walkable platform ramp");floor.transform.SetParent(transform,false);platformFloorCollider=floor.AddComponent<MeshCollider>();platformFloorCollider.sharedMesh=mesh;
        }
    }
    public sealed partial class YardSurvival
    {
        float GroundHeight(Vector3 position)=>HasCoreObjective?coreVisual.PlatformFloor(position):0;
        Vector3 OnGround(Vector3 position,float height=0){position.y=GroundHeight(position)+height;return position;}
    }
}
