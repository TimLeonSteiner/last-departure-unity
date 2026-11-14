using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        void SetupDeathPhysicsEnvironment()
        {
            var old=transform.Find("Death physics environment");if(old!=null)return;
            var root=new GameObject("Death physics environment").transform;root.SetParent(transform,false);
            PhysicsVolume(root,"Depot floor",new Bounds(new Vector3(0,-.15f,0),new Vector3(100,.3f,100)));
            foreach(var bounds in wallVolumes)PhysicsVolume(root,"Warehouse wall",bounds);
            foreach(var rect in obstacles)PhysicsVolume(root,"Warehouse crate",new Bounds(new Vector3(rect.center.x,1.325f,rect.center.y),new Vector3(rect.width,2.65f,rect.height)));
        }
        static void PhysicsVolume(Transform root,string name,Bounds bounds)
        {
            var obj=new GameObject(name);obj.transform.SetParent(root,false);obj.transform.position=bounds.center;
            var shape=obj.AddComponent<BoxCollider>();shape.size=bounds.size;
        }
        void DefensePhysics(Transform body,Rect footprint,float height)
        {
            if(body==null||body.Find("Death physics cover")!=null)return;
            var obj=new GameObject("Death physics cover");obj.transform.position=new Vector3(footprint.center.x,GroundHeight(body.position)+height*.5f,footprint.center.y);
            obj.transform.SetParent(body,true);var shape=obj.AddComponent<BoxCollider>();shape.size=new Vector3(footprint.width,height,footprint.height);
        }
    }
}
