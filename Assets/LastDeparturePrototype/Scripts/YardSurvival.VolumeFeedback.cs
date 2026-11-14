using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        AudioSource volumeFeedback;
        AudioClip volumeTick;
        float nextVolumeTick;
        float AudioWheelValue(Rect area,float value)
        {
            var e=Event.current;
            if(e==null||e.type!=EventType.ScrollWheel||!area.Contains(e.mousePosition)||Mathf.Abs(e.delta.y)<.001f)return value;
            float next=Mathf.Clamp01(value-Mathf.Sign(e.delta.y)*.02f);
            e.Use();return next;
        }
        void PlayVolumeTick(float previous,float next)
        {
            if(dedicatedServer||Mathf.Approximately(previous,next)||Time.unscaledTime<nextVolumeTick)return;
            EnsureVolumeFeedback();
            if(volumeTick==null)return;
            nextVolumeTick=Time.unscaledTime+.045f;
            volumeFeedback.volume=.2f;volumeFeedback.pitch=Mathf.Lerp(.9f,1.05f,next);
            volumeFeedback.Stop();volumeFeedback.clip=volumeTick;volumeFeedback.Play();
        }
        void PlayOptionsToggleFeedback(bool enabled)
        {
            if(dedicatedServer)return;
            EnsureVolumeFeedback();
            if(volumeTick==null)return;
            // UI feedback stays audible independently of gameplay effects volume and mute.
            volumeFeedback.volume=.275f;
            volumeFeedback.pitch=enabled?1.2f:.8f;
            volumeFeedback.Stop();volumeFeedback.clip=volumeTick;volumeFeedback.Play();
            nextVolumeTick=Time.unscaledTime+.1f;
        }
        void EnsureVolumeFeedback()
        {
            if(volumeFeedback==null)
            {
                var obj=new GameObject("Audio slider feedback");obj.transform.SetParent(transform,false);
                volumeFeedback=obj.AddComponent<AudioSource>();volumeFeedback.playOnAwake=false;volumeFeedback.spatialBlend=0;
                volumeFeedback.ignoreListenerPause=true;volumeFeedback.priority=32;
                volumeTick=Resources.Load<AudioClip>("InterfaceAudio/VolumeTick");
            }
        }
    }
}
