using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public AudioClip minigunStart,minigunStop,grenadeExplosion,pitchforkImpact,knifeImpact,crossbowImpact,crossbowFleshImpact;
        public AudioClip[] jumpVariations,landingVariations;
        bool minigunSpinning;
        int jumpVariant,landingVariant;
        void UpdateArsenalAudioEvents()
        {
            if(minigunSpinning&&(frontScreen!=FrontScreen.Game||dead||building||reloading||ammunition<=0||weaponId!=1||!Held(Control.Fire)))
            {minigunSpinning=false;if(frontScreen==FrontScreen.Game&&!dead)PlayEffect(minigunStop,.45f);}
        }
        void PlayJumpEvent(bool landing)
        {
            var clips=landing?landingVariations:jumpVariations;
            int index=landing?landingVariant++:jumpVariant++;
            PlayEffect(clips!=null&&clips.Length>0?clips[index%clips.Length]:landing?landingSound:jumpSound,landing?.38f:.24f);
        }
    }
}
