using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    // Instance-owned materials: weathering the hall never alters shared exterior assets.
    public sealed class HallPatina : MonoBehaviour
    {
        readonly List<Material> materials = new List<Material>();
        public static void Apply(Transform hall)
        {
            HallSurfaceTextures.Bind();
            var owner = hall.GetComponent<HallPatina>();
            if (owner == null) owner = hall.gameObject.AddComponent<HallPatina>();
            var copies = new Dictionary<Material, Material>();
            foreach (var renderer in hall.GetComponentsInChildren<MeshRenderer>())
            {
                var source = renderer.sharedMaterial;
                if (source == null || source.name.Contains("Patina") || !source.HasProperty("_Wear")) continue;
                if(source.HasProperty("_Concrete") && source.GetFloat("_Concrete") > .5f) continue;
                string name = source.name;
                if (name.Contains("Glass") || name.Contains("Glow") || name.Contains("Lens") || name.Contains("Mist")) continue;
                if (!copies.TryGetValue(source,out var worn))
                {
                    worn = new Material(source) { name = source.name + " / Patina" };
                    worn.SetFloat("_Wear",name.Contains("Asphalt") ? .12f : name.Contains("Rack") ? .16f : .24f); copies.Add(source,worn); owner.materials.Add(worn);
                }
                renderer.sharedMaterial = worn;
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                block.SetFloat("_Smoothness",name.Contains("Asphalt") ? .35f : .16f);
                block.SetFloat("_Metallic",name.Contains("Asphalt") ? 0 : .28f);
                renderer.SetPropertyBlock(block);
            }
        }
        void OnDestroy() { foreach (var material in materials) if (material != null) Destroy(material); }
    }
}
