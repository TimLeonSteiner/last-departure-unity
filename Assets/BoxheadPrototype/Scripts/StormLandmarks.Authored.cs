using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class StormLandmarks
    {
        bool Authored(string resource,Vector3 position)
        {
            var model=Resources.Load<GameObject>("Spaceport/"+resource);if(model==null)return false;
            var obj=Instantiate(model,section,false);obj.name=resource;obj.transform.localPosition=position;
            foreach(var renderer in obj.GetComponentsInChildren<MeshRenderer>())
            {
                string key=renderer.name.Substring(renderer.name.LastIndexOf('_')+1);
                if(!authoredSurfaces.TryGetValue(key,out var material))
                {
                    var source=Resources.Load<Material>("Spaceport/"+key)??steel;
                    material=new Material(source){name="Authored exterior / "+key};
                    if(key=="Steel")material.SetColor("_BaseColor",new Color(.21f,.23f,.24f));
                    if(key=="Hull")material.SetColor("_BaseColor",new Color(.37f,.39f,.40f));
                    material.SetFloat("_Metallic",key=="Ink"?.2f:.65f);material.SetFloat("_Smoothness",.36f);
                    authoredSurfaces.Add(key,material);labelMaterials.Add(material);
                }
                renderer.sharedMaterial=material;
            }
            return true;
        }
    }
}
