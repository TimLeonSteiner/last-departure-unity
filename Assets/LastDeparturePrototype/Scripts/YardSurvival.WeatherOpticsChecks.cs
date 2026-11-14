#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Text;using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;
namespace YardSurvivalPrototype { public sealed partial class YardSurvival {
public string RunWeatherOpticsReview(){
const string folder="Assets/LastDeparturePrototype/WeatherOpticsReview";Directory.CreateDirectory(folder);var log=new StringBuilder();
var oldPosition=arenaCamera.transform.position;var oldRotation=arenaCamera.transform.rotation;bool oldOn=flashlightOn,oldBuilding=building;bool oldView=viewmodelCamera.enabled;
var target=new RenderTexture(1280,800,24);target.Create();var image=new Texture2D(1280,800,TextureFormat.RGB24,false);var oldActive=RenderTexture.active;
try{
building=false;viewmodelCamera.enabled=false;
foreach(var system in rainfall){system.Simulate(2,true,true,true);system.Play();}
Action<bool,string> check=(ok,label)=>log.AppendLine((ok?"PASS ":"FAIL ")+label);
check(Resources.Load<Material>("NightRain")?.shader.name=="Universal Render Pipeline/Particles/Lit","Rain uses URP local lighting/shadows rather than unlit blue");
int live=0,indoors=0;foreach(var system in rainfall){var particles=new ParticleSystem.Particle[system.main.maxParticles];int n=system.GetParticles(particles);live+=n;for(int i=0;i<n;i++)if(Mathf.Abs(particles[i].position.x)<18&&Mathf.Abs(particles[i].position.z)<18)indoors++;}
check(live>0&&indoors==0,"Live outdoor particles="+live+", particles under warehouse roof="+indoors);
check(flashlight.cookie!=null&&flashlight.shadows==LightShadows.Soft&&flashlight.cullingMask==(~(1<<30)),"Shadowed torch uses smooth falloff and excludes hands/weapons");
check(GetComponentsInChildren<Light>().Count(l=>l.name=="Entrance LED pool"&&l.enabled)==4,"Four localized entrance light pools");
check(GetComponentsInChildren<Volume>().Any(v=>v.name=="Entrance optical glow"&&v.sharedProfile==Resources.Load<VolumeProfile>("NightOptics")),"Actual entrance optical glow volume uses retained build profile");
foreach(var renderer in GetComponentsInChildren<Renderer>())if(renderer.name.Contains("Entrance")&&renderer.name.Contains("LED"))log.AppendLine("LED "+renderer.name+" material="+renderer.sharedMaterial.name+" emission="+renderer.sharedMaterial.GetColor("_EmissionColor")+" keyword="+renderer.sharedMaterial.IsKeywordEnabled("_EMISSION"));
Func<string,Vector3,Vector3,bool,Color[]> capture=(name,position,toward,on)=>{
arenaCamera.transform.position=position;arenaCamera.transform.rotation=Quaternion.LookRotation(toward-position);flashlightOn=on;UpdateFlashlight();
RenderPipeline.SubmitRenderRequest(arenaCamera,new RenderPipeline.StandardRequest{destination=target});RenderPipeline.SubmitRenderRequest(arenaCamera,new RenderPipeline.StandardRequest{destination=target});
RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());return image.GetPixels();};
capture("Indoor-entrance-off",new Vector3(0,1.65f,13),new Vector3(0,2,18),false);
capture("Indoor-entrance-on",new Vector3(0,1.65f,13),new Vector3(0,2,18),true);
capture("Window-outdoor-off",new Vector3(6,1.65f,15),new Vector3(6,2,23),false);
capture("Window-outdoor-on",new Vector3(6,1.65f,15),new Vector3(6,2,23),true);
capture("Outside-entrance",new Vector3(0,1.65f,22),new Vector3(0,2,15),true);
capture("Outside-rain",new Vector3(0,1.65f,20),new Vector3(0,.6f,23.5f),true);
capture("Close-wall-on",new Vector3(6,1.65f,16.8f),new Vector3(6,.55f,17.6f),true);
capture("Close-wall-off",new Vector3(6,1.65f,16.8f),new Vector3(6,.55f,17.6f),false);
foreach(var pair in new[]{("Opaque-roof",new Vector3(0,1.65f,0),new Vector3(0,8,0)),("Opaque-wall",new Vector3(6,5,17),new Vector3(6,5,18))}){
var on=capture(pair.Item1+"-rain",pair.Item2,pair.Item3,true);foreach(var system in rainfall)system.GetComponent<Renderer>().enabled=false;
var off=capture(pair.Item1+"-no-rain",pair.Item2,pair.Item3,true);foreach(var system in rainfall)system.GetComponent<Renderer>().enabled=true;
float delta=on.Select((c,i)=>Mathf.Abs(c.r-off[i].r)+Mathf.Abs(c.g-off[i].g)+Mathf.Abs(c.b-off[i].b)).Average()/3;
check(delta<.00005f,pair.Item1+" depth blocks outside particles: rendered mean difference="+delta.ToString("F7"));}
log.AppendLine("COMPLETE "+DateTime.Now.ToString("s")+". Images are actual fresh Play world-camera views; visual inspection required.");return log.ToString();
}finally{arenaCamera.transform.position=oldPosition;arenaCamera.transform.rotation=oldRotation;flashlightOn=oldOn;building=oldBuilding;viewmodelCamera.enabled=oldView;UpdateFlashlight();RenderTexture.active=oldActive;target.Release();Destroy(target);Destroy(image);}
}
}}
#endif
