using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace YardSurvivalPrototype
{
    public sealed class HeartServiceDetails : MonoBehaviour
    {
        readonly List<Material> materials=new List<Material>();
        readonly List<Mesh> meshes=new List<Mesh>();
        Light pulseLight;
        public ParticleSystem Steam {get;private set;}
        public float PulseIntensity => pulseLight!=null?pulseLight.intensity:0;
        public void SetPulse(float pulse,bool lost)
        {
            if(pulseLight!=null)pulseLight.intensity=lost?.12f:1.1f+pulse*1.4f;
            if(lost&&Steam!=null&&Steam.isEmitting)Steam.Stop(false,ParticleSystemStopBehavior.StopEmitting);
        }
        public static HeartServiceDetails Build(Transform core)
        {
            var root=new GameObject("Heart maintenance lighting and markings");root.transform.SetParent(core,false);
            var detail=root.AddComponent<HeartServiceDetails>();detail.Create();return detail;
        }
        Light Lamp(string name,Vector3 position,Vector3 target,Color color,float intensity,float range,float cone)
        {
            var obj=new GameObject(name);obj.transform.SetParent(transform,false);obj.transform.localPosition=position;
            obj.transform.localRotation=Quaternion.LookRotation(target-position);
            var light=obj.AddComponent<Light>();light.type=cone>0?LightType.Spot:LightType.Point;
            light.color=color;light.intensity=intensity;light.range=range;light.spotAngle=cone>0?cone:90;
            light.innerSpotAngle=light.spotAngle*.6f;light.shadows=LightShadows.None;light.cullingMask=~(1<<30);return light;
        }
        void Create()
        {
            pulseLight=Lamp("Heartbeat reflected red spill",new Vector3(0,.85f,-.15f),Vector3.zero,new Color(1,.055f,.018f),1.1f,4.6f,0);
            var steel=Resources.Load<Material>("Spaceport/CraftedTerminal_Steel");
            var lens=new Material(RuntimeShaders.Find("Universal Render Pipeline/Lit")){name="Warm service lamp glass"};materials.Add(lens);
            lens.color=new Color(.65f,.40f,.16f);lens.EnableKeyword("_EMISSION");lens.SetColor("_EmissionColor",new Color(1,.62f,.28f)*1.8f);
            foreach(int side in new[]{-1,1})
            {
                var p=new Vector3(side*2.95f,3.75f,.4f);var target=new Vector3(side*2.2f,.7f,-.6f);
                var light=Lamp("Frame maintenance light "+side,p,target,new Color(1,.72f,.43f),9,5.5f,62);
                // Compact fixtures are seated on the rear frame instead of floating in space.
                Box("Maintenance lamp housing",p,new Vector3(.27f,.15f,.23f),steel,light.transform.localRotation);
                Box("Maintenance lamp lens",p+light.transform.forward*.125f,new Vector3(.21f,.10f,.02f),lens,light.transform.localRotation);
            }
            Mark("CoreAccess",new Vector3(0,.009f,-5.35f),0,new Vector2(3.1f,.58f));
            Mark("KeepClear",new Vector3(0,.009f,5.35f),180,new Vector2(2.8f,.53f));
            Mark("Panel01",new Vector3(-5.5f,.009f,-1),90,new Vector2(1.25f,.28f));
            Mark("Panel02",new Vector3(5.5f,.009f,1),270,new Vector2(1.25f,.28f));
            Mark("Panel03",new Vector3(-1,.009f,6.1f),180,new Vector2(1.25f,.28f));
            Mark("Panel04",new Vector3(1,.009f,-6.1f),0,new Vector2(1.25f,.28f));
            BuildSteam(steel);
        }
        void Box(string name,Vector3 p,Vector3 size,Material material,Quaternion rotation)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(transform,false);
            obj.transform.localPosition=p;obj.transform.localScale=size;obj.transform.localRotation=rotation;
            var collider=obj.GetComponent<Collider>();collider.enabled=false;Destroy(collider);obj.GetComponent<Renderer>().sharedMaterial=material;
        }
        void Mark(string name,Vector3 p,float yaw,Vector2 size)
        {
            var material=new Material(RuntimeShaders.Find("Universal Render Pipeline/Lit")){name="Worn floor stencil / "+name};materials.Add(material);
            material.SetTexture("_BaseMap",Resources.Load<Texture2D>("HeartService/"+name));material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.4f);material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Smoothness",.14f);material.SetOverrideTag("RenderType","TransparentCutout");material.renderQueue=2450;
            var mesh=new Mesh{name="Floor stencil"};meshes.Add(mesh);
            mesh.vertices=new[]{new Vector3(-size.x/2,0,-size.y/2),new Vector3(size.x/2,0,-size.y/2),new Vector3(-size.x/2,0,size.y/2),new Vector3(size.x/2,0,size.y/2)};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};mesh.triangles=new[]{0,2,1,2,3,1};mesh.RecalculateNormals();mesh.RecalculateBounds();
            var obj=new GameObject("Floor stencil / "+name);obj.transform.SetParent(transform,false);obj.transform.localPosition=p;obj.transform.localRotation=Quaternion.Euler(0,yaw,0);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        void BuildSteam(Material steel)
        {
            var outlet=new Vector3(2.36f,1.05f,-.62f);
            Box("Pump pressure release outlet",outlet,new Vector3(.13f,.17f,.13f),steel,Quaternion.identity);
            var obj=new GameObject("Intermittent pressure release steam");obj.transform.SetParent(transform,false);obj.transform.localPosition=outlet+Vector3.up*.1f;
            obj.transform.localRotation=Quaternion.Euler(-78,0,-14);
            Steam=obj.AddComponent<ParticleSystem>();Steam.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=Steam.main;main.duration=9;main.loop=true;main.startDelay=1.5f;main.startLifetime=new ParticleSystem.MinMaxCurve(1.4f,2.2f);
            main.startSpeed=new ParticleSystem.MinMaxCurve(.25f,.48f);main.startSize=new ParticleSystem.MinMaxCurve(.18f,.35f);
            main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.startColor=new Color(.68f,.72f,.72f,.18f);
            main.maxParticles=24;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=Steam.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(.1f,4,4,3,.18f)});
            var shape=Steam.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12;shape.radius=.035f;
            var size=Steam.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.5f,1,2.8f));
            var color=Steam.colorOverLifetime;color.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.18f),new GradientAlphaKey(0,1)});color.color=gradient;
            var velocity=Steam.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=.08f;velocity.y=.17f;velocity.z=.04f;
            var material=new Material(Resources.Load<Shader>("HeartSteam")){name="Light responsive pressure steam"};materials.Add(material);
            var renderer=Steam.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            Steam.Play();
        }
        void OnDestroy(){foreach(var material in materials)if(material!=null)Destroy(material);foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);}
    }
}
