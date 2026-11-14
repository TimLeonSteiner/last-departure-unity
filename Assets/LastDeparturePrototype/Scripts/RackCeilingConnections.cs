using UnityEngine;
using UnityEngine.Rendering;

namespace YardSurvivalPrototype
{
    public static class RackCeilingConnections
    {
        public static void Build(Transform hall)
        {
            var asset=Resources.Load<GameObject>("Spaceport/TerminalCableExtensions");
            if(asset==null)return;
            var root=Object.Instantiate(asset,hall,false);root.name="Rack ceiling connections";
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                string key=renderer.name.Replace("CableRoof_","");
                var material=Resources.Load<Material>("Spaceport/"+key);
                if(material!=null)renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                renderer.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
            }
        }
    }
}
