using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        void RegisterModularHallWalls()
        {
            var template=Resources.Load<GameObject>("Spaceport/ModularHallTerminalSet");
            var walls=template!=null?template.transform.Find("Hall wall panels"):null;
            if(walls==null)return;
            foreach(var collider in walls.GetComponentsInChildren<BoxCollider>())
            {
                var t=collider.transform;var center=t.TransformPoint(collider.center);
                var size=Vector3.Scale(collider.size,t.lossyScale);var right=t.rotation*new Vector3(size.x,0,0);var up=t.rotation*new Vector3(0,size.y,0);var forward=t.rotation*new Vector3(0,0,size.z);
                size=new Vector3(Mathf.Abs(right.x)+Mathf.Abs(up.x)+Mathf.Abs(forward.x),Mathf.Abs(right.y)+Mathf.Abs(up.y)+Mathf.Abs(forward.y),Mathf.Abs(right.z)+Mathf.Abs(up.z)+Mathf.Abs(forward.z));
                center.x/=ArenaScale;center.z/=ArenaScale;size.x/=ArenaScale;size.z/=ArenaScale;
                if(collider.name=="Server cabinet collision")
                    AddPerimeter(new Rect(center.x-size.x/2,center.z-size.z/2,size.x,size.z),size.y);
                else wallVolumes.Add(new Bounds(center,size));
            }
        }
    }
}
