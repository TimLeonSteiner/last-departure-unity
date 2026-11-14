using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class SuspendedCore
    {
        HeartServiceDetails serviceDetails;
        public bool HasModularPlatform {get;private set;}
        bool BuildModularHeartPlatform()
        {
            var prefab=Resources.Load<GameObject>("HeartPlatform/Modular/Platform");
            if(prefab==null)return false;
            var model=Instantiate(prefab,transform,false);
            model.name="Modular biomechanical heart platform";
            // Use the authored gameplay meshes; retain dense source meshes in the
            // prefab for close-up art work without submitting them in every shadow pass.
            foreach(var lod in model.GetComponentsInChildren<LODGroup>())lod.ForceLOD(1);
            heart=model.transform.Find("Heart");
            if(heart==null){Destroy(model);return false;}
            var renderers=heart.GetComponentsInChildren<Renderer>(true);
            chainedHeartSurface=new Material(renderers[0].sharedMaterial){name="Living modular heart"};
            foreach(var renderer in renderers)renderer.sharedMaterial=chainedHeartSurface;
            HasMeshyPlatform=true;HasModularPlatform=true;
            foreach(int side in new[]{-1,1})
            {
                PlatformBarrier(new Vector3(side*2.3f,.8f,-.6f),new Vector3(1.6f,1.3f,1.15f));
                PlatformBarrier(new Vector3(side*3.02f,1.9f,.65f),new Vector3(.75f,3.5f,.85f));
            }
            BuildPlatformFloorCollider();
            BuildPlatformFoundation();
            serviceDetails=HeartServiceDetails.Build(transform);
            return true;
        }
    }
}
