using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        AudioSource skillFeedback;
        AudioClip skillSelectClip,skillInstallClip;
        void PlaySkillFeedback(bool installed)
        {
            if(!soundEnabled||dedicatedServer)return;
            if(skillFeedback==null)
            {
                var obj=new GameObject("Skill node feedback");obj.transform.SetParent(transform,false);
                skillFeedback=obj.AddComponent<AudioSource>();skillFeedback.playOnAwake=false;
                skillFeedback.spatialBlend=0;skillFeedback.ignoreListenerPause=true;skillFeedback.priority=32;
                skillSelectClip=Resources.Load<AudioClip>("InterfaceAudio/SkillNode");
                skillInstallClip=Resources.Load<AudioClip>("InterfaceAudio/SkillInstall");
            }
            var clip=installed?skillInstallClip:skillSelectClip;if(clip==null)return;
            skillFeedback.volume=soundVolume;skillFeedback.Stop();skillFeedback.PlayOneShot(clip,installed?.48f:.32f);
        }
    }
}
