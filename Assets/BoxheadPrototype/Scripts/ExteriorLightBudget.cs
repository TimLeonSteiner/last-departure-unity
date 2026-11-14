using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed class ExteriorLightBudget : MonoBehaviour
    {
        readonly List<Light> lights=new List<Light>();
        readonly List<Light> shadowCandidates=new List<Light>();
        float nextUpdate;
        public int ActiveShadowCount {get;private set;}
        void Start()
        {
            var amber=Resources.Load<Material>("Spaceport/AmberGlow");
            for(int side=0;side<4;side++)
            {
                var rotation=Quaternion.Euler(0,side*90,0);
                int end=side==0?140:82;
                for(int d=27;d<end;d+=15)foreach(int edge in new[]{-1,1})
                    Marker(rotation*new Vector3(edge*5.7f,.23f,d),amber);
            }
            for(int z=142;z<223;z+=5)foreach(int edge in new[]{-1,1})
                if((z+edge)%4!=0)Marker(new Vector3(edge*5.7f,.20f,z),amber);
            lights.AddRange(GetComponentsInChildren<Light>());
            foreach(var light in lights)if(light.type==LightType.Spot&&light.shadows!=LightShadows.None)shadowCandidates.Add(light);
            Refresh();
        }
        void Marker(Vector3 position,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name="Powered runway marker";
            obj.transform.SetParent(transform,false);obj.transform.localPosition=position;obj.transform.localScale=new Vector3(.24f,.025f,.31f);
            var collider=obj.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            obj.GetComponent<Renderer>().sharedMaterial=material;
            var source=new GameObject("Runway marker spill");source.transform.SetParent(transform,false);
            source.transform.localPosition=position+Vector3.up*.05f;
            var lamp=source.AddComponent<Light>();lamp.type=LightType.Point;lamp.range=1.7f;lamp.intensity=2.1f;
            lamp.color=new Color(1,.57f,.22f);lamp.shadows=LightShadows.None;lamp.cullingMask=~(1<<30);
        }
        void Update(){if(Time.unscaledTime>=nextUpdate){nextUpdate=Time.unscaledTime+.5f;Refresh();}}
        public void Refresh()
        {
            var camera=Camera.main;if(camera==null)return;
            RefreshFor(camera.transform.position);
        }
        public void RefreshFor(Vector3 eye)
        {
            shadowCandidates.RemoveAll(light=>light==null);
            foreach(var light in lights)if(light!=null)
                light.enabled=(light.transform.position-eye).sqrMagnitude<Mathf.Pow(light.range+65,2);
            shadowCandidates.Sort((a,b)=>(a.transform.position-eye).sqrMagnitude.CompareTo((b.transform.position-eye).sqrMagnitude));
            ActiveShadowCount=0;
            foreach(var light in shadowCandidates)
            {
                bool nearby=light.enabled&&ActiveShadowCount<8;
                light.shadows=nearby?LightShadows.Soft:LightShadows.None;
                if(nearby)ActiveShadowCount++;
            }
        }
    }
}
