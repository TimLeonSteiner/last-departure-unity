using System.Collections.Generic;
using UnityEngine;
namespace YardSurvivalPrototype
{
    // Shared by mesh animation and decorative shaders; gameplay lights are not managed here.
    public sealed class RuntimeVisualBudget : MonoBehaviour
    {
        public static int Preset {get;private set;}=2;
        public static bool Cheap => Preset==0 || Preset==3;
        public static bool Minimum => Preset==3;
        struct Entry {public Light light;public int mask;}
        readonly List<Entry> lights=new List<Entry>();
        float next;
        bool cached;
        public static void Set(int preset){Preset=preset;Shader.SetGlobalFloat("_D27CheapTissue",Cheap?1:0);}
        void CacheLights()
        {
            foreach(string name in new[]{"Spaceport terminal","Spaceport exterior"})
            {
                var root=transform.Find(name);if(root==null)continue;
                foreach(var light in root.GetComponentsInChildren<Light>(true))
                    if(light.type!=LightType.Directional)lights.Add(new Entry{light=light,mask=light.cullingMask});
            }
        }
        void LateUpdate()
        {
            if(!cached){CacheLights();cached=true;}
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;
            var camera=Camera.main;if(camera==null)return;
            Vector3 eye=camera.transform.position;
            lights.RemoveAll(e=>e.light==null);
            lights.Sort((a,b)=>Score(a.light,eye).CompareTo(Score(b.light,eye)));
            int budget=Minimum?6:Preset==0?12:Preset==1?24:int.MaxValue;
            int used=0;
            foreach(var entry in lights)
            {
                bool eligible=entry.light.enabled&&entry.light.gameObject.activeInHierarchy;
                entry.light.cullingMask=!eligible||used++<budget?entry.mask:0;
            }
        }
        static float Score(Light light,Vector3 eye)=> (light.transform.position-eye).sqrMagnitude/Mathf.Max(1,light.range*light.range);
        void OnDestroy(){foreach(var entry in lights)if(entry.light!=null)entry.light.cullingMask=entry.mask;Set(2);}
    }
}
