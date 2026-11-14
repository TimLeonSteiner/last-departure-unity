using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace YardSurvivalPrototype
{
    // World-space decoration outside the playable perimeter; no collision or gameplay lights.
    public sealed class ExteriorStormAtmosphere : MonoBehaviour
    {
        Material mist;
        Mesh quad;
        readonly List<GameObject> distantSheets=new List<GameObject>();
        public static ExteriorStormAtmosphere Build(Transform parent)
        {
            var existing=parent.Find("Exterior drifting storm");
            if(existing!=null)return existing.GetComponent<ExteriorStormAtmosphere>();
            var root=new GameObject("Exterior drifting storm");root.transform.SetParent(parent,false);
            var component=root.AddComponent<ExteriorStormAtmosphere>();component.Create();return component;
        }
        void Create()
        {
            var source=Resources.Load<Material>("Spaceport/SpaceportMist");
            if(source==null)return;
            mist=new Material(source){name="Cold drifting exterior haze"};
            mist.SetColor("_Color",new Color(.045f,.066f,.087f,.26f));
            quad=new Mesh{name="Exterior haze sheet"};
            quad.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0)};
            quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};quad.triangles=new[]{0,2,1,2,3,1};quad.RecalculateBounds();
            for(int side=0;side<4;side++)for(int layer=0;layer<3;layer++)
            {
                var yaw=Quaternion.Euler(0,side*90,0);
                var sheet=new GameObject("Windblown mist "+side+" / "+layer);sheet.transform.SetParent(transform,false);
                sheet.transform.localPosition=yaw*new Vector3(layer%2==0?-4:6,1.7f+layer*.6f,38+layer*25);
                sheet.transform.localRotation=yaw;sheet.transform.localScale=new Vector3(72,3.4f+layer*1.2f,1);
                sheet.AddComponent<MeshFilter>().sharedMesh=quad;
                var renderer=sheet.AddComponent<MeshRenderer>();renderer.sharedMaterial=mist;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                if(layer>0)distantSheets.Add(sheet);
            }
        }
        void Update()
        {
            foreach(var sheet in distantSheets)if(sheet.activeSelf==RuntimeVisualBudget.Cheap)sheet.SetActive(!RuntimeVisualBudget.Cheap);
        }
        void OnDestroy(){if(mist!=null)Destroy(mist);if(quad!=null)Destroy(quad);}
    }
}
