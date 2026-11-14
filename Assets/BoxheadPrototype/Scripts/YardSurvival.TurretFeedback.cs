using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed class TurretShotFeedback : MonoBehaviour
    {
        float life=.09f;
        void Update(){life-=Time.deltaTime;if(life<=0)Destroy(gameObject);}
    }
    public sealed partial class YardSurvival
    {
        void ShowTurretShot(Vector3 origin,Vector3 direction,Vector3 target)
        {
            Vector3 muzzle=origin+direction*.65f;
            var shot=new GameObject("Sentry muzzle and tracer");shot.transform.SetParent(actors,false);shot.transform.position=muzzle;shot.AddComponent<TurretShotFeedback>();
            var line=shot.AddComponent<LineRenderer>();line.sharedMaterial=muzzleMaterial;line.positionCount=2;line.SetPosition(0,muzzle);line.SetPosition(1,target);line.startWidth=.019f;line.endWidth=.009f;line.startColor=new Color(1,.8f,.35f);line.endColor=new Color(1,.38f,.1f,.5f);
            var flash=GameObject.CreatePrimitive(PrimitiveType.Sphere);flash.transform.SetParent(shot.transform,false);flash.transform.position=muzzle;flash.transform.rotation=Quaternion.LookRotation(direction);flash.transform.localScale=new Vector3(.09f,.09f,.22f);Destroy(flash.GetComponent<Collider>());flash.GetComponent<Renderer>().sharedMaterial=muzzleMaterial;
            var light=shot.AddComponent<Light>();light.type=LightType.Point;light.range=3;light.intensity=2.5f;light.color=new Color(1,.64f,.24f);
            if(soundEnabled&&audioUnlocked&&turretShotSound!=null)
            {
                var sound=new GameObject("Sentry discharge audio");sound.transform.SetParent(actors,false);sound.transform.position=muzzle;
                var source=sound.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1f;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=2;source.maxDistance=26;source.dopplerLevel=0;source.volume=effects.volume*.45f;source.pitch=1;source.clip=turretShotSound;
                // Configure before playback: scanning short-lived shots mid-sound caused inconsistent muffling.
                var filter=sound.AddComponent<AudioLowPassFilter>();filter.lowpassResonanceQ=1;
                filter.enabled=displayedDrowsiness>.001f;filter.cutoffFrequency=Mathf.Lerp(22000,4000,displayedDrowsiness);dreamFilters.Add(filter);
                source.Play();Destroy(sound,turretShotSound.length/source.pitch+.1f);
            }
        }
    }
}
