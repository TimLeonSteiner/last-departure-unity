using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed class ExteriorDecay : MonoBehaviour
    {
        public static void Build(Transform exterior)
        {
            var decay=exterior.GetComponent<ExteriorDecay>();
            if(decay==null)decay=exterior.gameObject.AddComponent<ExteriorDecay>();
            decay.Apply();
        }
        void Apply()
        {
            foreach(var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                var material=renderer.sharedMaterial;
                if(material==null||!material.HasProperty("_ExteriorWear"))continue;
                string name=material.name;
                if(name.Contains("Glow")||name.Contains("Lens")||name.Contains("Glass"))continue;
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                block.SetFloat("_ExteriorWear",1);
                block.SetFloat("_PaintWear",name.Contains("Paint")||name.Contains("Hull")?1:0);
                block.SetFloat("_Smoothness",name.Contains("Asphalt")?.28f:.19f);
                renderer.SetPropertyBlock(block);
            }
        }
        void Start()
        {
            var template=transform.Find("Low drifting mist 0");
            if(template==null||transform.Find("Crosswind haze 1 / 0")!=null)return;
            for(int side=1;side<4;side++)for(int layer=0;layer<2;layer++)
            {
                var mist=Instantiate(template.gameObject,transform,false);
                mist.name="Crosswind haze "+side+" / "+layer;
                var rotation=Quaternion.Euler(0,side*90,0);
                mist.transform.localPosition=rotation*new Vector3(0,2.4f,50+layer*32);
                mist.transform.localRotation=rotation;
                mist.transform.localScale=new Vector3(58,5.5f,1);
            }
        }
    }
}
