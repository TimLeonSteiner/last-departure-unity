using UnityEngine;
using UnityEngine.Rendering;

namespace YardSurvivalPrototype
{
    // Red alarms retain the horror mood; steady cool pools make the main aisles readable.
    public sealed class HorrorHallLighting : MonoBehaviour
    {
        ReflectionProbe hallProbe;
        static readonly Color EmergencyRed = new Color(1f, .035f, .012f);
        readonly System.Collections.Generic.List<Light> emergencyLights = new System.Collections.Generic.List<Light>();
        readonly System.Collections.Generic.List<float> peakIntensities = new System.Collections.Generic.List<float>();
        readonly System.Collections.Generic.List<Renderer> emergencyLenses = new System.Collections.Generic.List<Renderer>();
        MaterialPropertyBlock lensProperties;
        float pulseStartedAt;

        void Update()
        {
            // A synchronized slow alarm pulse; scaled time keeps it still while paused.
            float wave = .5f + .5f * Mathf.Cos((Time.time - pulseStartedAt) * (2 * Mathf.PI / 2.4f));
            float strength = Mathf.Lerp(.35f, 1f, wave * wave);
            for (int i = 0; i < emergencyLights.Count; i++)
                if (emergencyLights[i] != null) emergencyLights[i].intensity = peakIntensities[i] * strength;
            foreach (var lens in emergencyLenses)
            {
                if (lens == null) continue;
                lens.GetPropertyBlock(lensProperties);
                lensProperties.SetColor("_BaseColor", EmergencyRed * Mathf.Lerp(.3f, 1f, strength));
                lensProperties.SetFloat("_Glow", 1.8f * strength);
                lens.SetPropertyBlock(lensProperties);
            }
        }

        void Start()
        {
            // Capture after the complete hall and its practical fixtures have been created.
            hallProbe.RenderProbe();
        }
        void Awake()
        {
            pulseStartedAt = Time.time;
            lensProperties = new MaterialPropertyBlock();
            // Reflected crimson sky light reveals the surviving trusses and torn roof rim.
            foreach(float x in new[]{-12f,12f})
            {
                var spill=new GameObject("Watcher reflected roof light");spill.transform.SetParent(transform,false);
                spill.transform.localPosition=new Vector3(x,7.1f,14);
                var light=spill.AddComponent<Light>();light.type=LightType.Point;
                light.color=new Color(1,.13f,.09f);light.intensity=9;light.range=15;
                light.shadows=LightShadows.None;light.cullingMask=~(1<<30);
            }
            foreach (var lens in GetComponentsInChildren<Renderer>(true))
                if (lens.name == "Spaceport_EmergencyLens") emergencyLenses.Add(lens);
            // Fixture geometry is authored in DustlineTerminalCraft.blend.
            // The imported suspended lamps have their lenses at y=6.39.
            for(int x=-1;x<=1;x+=2)
                for(int z=-16;z<=16;z+=8)
                {
                    var housing=new GameObject("Suspended rack lamp emission");housing.transform.SetParent(transform,false);
                    housing.transform.localPosition=new Vector3(x*10.8f,6.34f,z);
                    housing.transform.localRotation=Quaternion.Euler(90,0,0);
                    var light=housing.AddComponent<Light>();light.type=LightType.Spot;
                    // Reach the floor from the suspended fixture, even at the pulse trough.
                    light.color=EmergencyRed;light.intensity=38;light.range=14;
                    emergencyLights.Add(light);peakIntensities.Add(light.intensity);
                    light.spotAngle=72;light.innerSpotAngle=44;light.shadows=LightShadows.Soft;
                    light.cullingMask=~(1<<30);
                    light.shadowBias=.025f;light.shadowNormalBias=.15f;
                }
            for(int side=-1;side<=1;side+=2)
                for(int row=-1;row<=1;row++)
                {
                    var lamp=new GameObject("Rack inspection practical");lamp.transform.SetParent(transform,false);
                    lamp.transform.localPosition=new Vector3(side*6.5f,6.8f,row*15);
                    lamp.transform.localRotation=Quaternion.Euler(90,0,0);
                    var light=lamp.AddComponent<Light>();light.type=LightType.Spot;
                    light.color=new Color(.68f,.79f,.92f);light.intensity=18;light.range=13;
                    light.spotAngle=85;light.innerSpotAngle=48;light.shadows=LightShadows.Soft;
                    light.shadowBias=.025f;light.shadowNormalBias=.15f;
                    light.cullingMask=~(1<<30);
                }
            // Local sky spill at the actual roof breaches reveals their torn edges.
            foreach(var patch in FullRoofLayout.Load()?.rainPatches ?? new Rect[0])
            {
                var opening=patch.center;
                var spill=new GameObject("Roof breach sky spill");spill.transform.SetParent(transform,false);
                spill.transform.localPosition=new Vector3(opening.x*YardSurvival.ArenaScale,7.55f,opening.y*YardSurvival.ArenaScale);
                spill.transform.localRotation=Quaternion.Euler(90,0,0);
                var light=spill.AddComponent<Light>();light.type=LightType.Spot;
                light.color=new Color(.52f,.65f,.80f);light.intensity=18;light.range=12;light.spotAngle=52;light.innerSpotAngle=30;
                light.shadows=LightShadows.Soft;light.shadowBias=.015f;light.shadowNormalBias=.08f;
                light.cullingMask=~(1<<30);
            }
            // Small upward spill represents light reflected by the suspended fixtures.
            foreach(int side in new[]{-1,1})
                foreach(int row in new[]{-1,0,1})
                {
                    var bounce=new GameObject("Fixture ceiling spill");bounce.transform.SetParent(transform,false);
                    bounce.transform.localPosition=new Vector3(side*6.5f,7.2f,row*15);
                    bounce.transform.localRotation=Quaternion.Euler(-90,0,0);
                    var light=bounce.AddComponent<Light>();light.type=LightType.Spot;
                    light.color=new Color(.58f,.62f,.65f);light.intensity=.8f;light.range=2.5f;
                    light.spotAngle=140;light.innerSpotAngle=90;light.cullingMask=~(1<<30);
                }
            var probeObject=new GameObject("Hall material reflections");probeObject.transform.SetParent(transform,false);
            probeObject.transform.localPosition=new Vector3(0,3,0);
            var probe=probeObject.AddComponent<ReflectionProbe>();hallProbe=probe;probe.mode=ReflectionProbeMode.Realtime;
            probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.resolution=256;probe.size=new Vector3(48,10,48);probe.center=Vector3.zero;
            probe.boxProjection=true;probe.intensity=.16f;probe.farClipPlane=160;probe.cullingMask=~(1<<30);
            RenderSettings.reflectionIntensity=.16f;
            Update();
        }
    }
}
