using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class SuspendedCore
    {
        HeartServiceDetails serviceDetails;
        Renderer[] webCoreDecorations;


        public void ApplyWebCoreBudget()
        {
            if (webCoreDecorations == null) return;
            bool lightweight = RuntimeVisualBudget.Minimum;
            foreach (var renderer in webCoreDecorations)
                renderer.forceRenderingOff = lightweight;
            if (serviceDetails != null) serviceDetails.gameObject.SetActive(!lightweight);
        }

        void CacheWebCoreDecorations(Transform model)
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer) return;
            var decorations = new System.Collections.Generic.List<Renderer>();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.transform.IsChildOf(heart)) continue;
                for (var part = renderer.transform; part != model; part = part.parent)
                {
                    string name = part.name;
                    if (name.StartsWith("Tube-", System.StringComparison.Ordinal) ||
                        name.StartsWith("Vessel ", System.StringComparison.Ordinal) ||
                        name.StartsWith("Root distribution hose", System.StringComparison.Ordinal) ||
                        name.StartsWith("Organic root hub", System.StringComparison.Ordinal) ||
                        name.Contains("suspension chain"))
                    {
                        decorations.Add(renderer);
                        break;
                    }
                }
            }
            webCoreDecorations = decorations.ToArray();
            ApplyWebCoreBudget();
        }
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
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterial = chainedHeartSurface;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // The deck and small platform details still receive shadows.
            // Exclude them from the real-time shadow passes.
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            HasMeshyPlatform=true;HasModularPlatform=true;
            foreach(int side in new[]{-1,1})
            {
                PlatformBarrier(new Vector3(side*2.3f,.8f,-.6f),new Vector3(1.6f,1.3f,1.15f));
                PlatformBarrier(new Vector3(side*3.02f,1.9f,.65f),new Vector3(.75f,3.5f,.85f));
            }
            BuildPlatformFloorCollider();
            BuildPlatformFoundation();
            serviceDetails=HeartServiceDetails.Build(transform);
            CacheWebCoreDecorations(model.transform);
            return true;
        }
    }
}
