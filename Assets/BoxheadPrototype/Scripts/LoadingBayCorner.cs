using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed class LoadingBayCorner : MonoBehaviour
    {
        Material dripMaterial;
        public static GameObject Build(Transform hall)
        {
            var existing=hall.Find("Loading bay maintenance corner");if(existing!=null)return existing.gameObject;
            var prefab=Resources.Load<GameObject>("Spaceport/LoadingBayCornerPickup");
            if(prefab==null)return null;
            var root=Instantiate(prefab,hall,false);root.name="Loading bay maintenance corner";
            root.transform.localPosition=new Vector3(8,0,21.7f);
            root.AddComponent<LoadingBayCorner>();return root;
        }
        void Awake()
        {
            var fixture=new GameObject("Caged maintenance task light");fixture.transform.SetParent(transform,false);
            fixture.transform.localPosition=new Vector3(0,2.40f,.28f);
            fixture.transform.localRotation=Quaternion.Euler(105,0,0);
            var light=fixture.AddComponent<Light>();light.type=LightType.Spot;light.color=new Color(1,.62f,.29f);
            light.intensity=13;light.range=6;light.spotAngle=105;light.innerSpotAngle=65;light.shadows=LightShadows.Soft;
            light.shadowBias=.015f;light.shadowNormalBias=.08f;light.cullingMask=~(1<<30);
            // Restrained reflected task light keeps the open cabinet and pipe readable.
            var bounce=new GameObject("Maintenance reflected light");bounce.transform.SetParent(transform,false);
            bounce.transform.localPosition=new Vector3(0,1.55f,-.35f);
            var fill=bounce.AddComponent<Light>();fill.type=LightType.Point;fill.color=new Color(1,.72f,.43f);
            fill.intensity=1.1f;fill.range=3;fill.shadows=LightShadows.Soft;
            fill.shadowBias=.015f;fill.shadowNormalBias=.06f;fill.cullingMask=~(1<<30);
            var leak=new GameObject("Intermittent pipe drips");leak.transform.SetParent(transform,false);
            leak.transform.localPosition=new Vector3(1,.37f,.15f);leak.transform.localRotation=Quaternion.Euler(90,0,0);
            var particles=leak.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.startLifetime=.4f;main.startSpeed=.3f;main.startSize=.015f;
            main.startColor=new Color(.6f,.7f,.7f,.45f);main.gravityModifier=.6f;main.maxParticles=16;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=particles.emission;emission.rateOverTime=5;
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=2;shape.radius=.008f;
            var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Stretch;
            renderer.lengthScale=2;renderer.velocityScale=.03f;
            dripMaterial=new Material(RuntimeShaders.Find("Universal Render Pipeline/Particles/Unlit"));
            dripMaterial.SetColor("_BaseColor",new Color(.35f,.43f,.42f,.4f));dripMaterial.SetFloat("_Surface",1);
            dripMaterial.SetFloat("_SrcBlend",5);dripMaterial.SetFloat("_DstBlend",10);dripMaterial.SetFloat("_ZWrite",0);
            dripMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");dripMaterial.renderQueue=3000;renderer.sharedMaterial=dripMaterial;
            particles.Play();
        }
        void OnDestroy(){if(dripMaterial!=null)Destroy(dripMaterial);}
    }
}
