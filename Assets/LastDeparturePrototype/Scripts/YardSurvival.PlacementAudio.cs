using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        readonly AudioClip[] placementClips=new AudioClip[4];
        void PlayPlacementSound(int type)
        {
            if(type<0||type>=placementClips.Length)return;
            if(placementClips[type]==null)placementClips[type]=Resources.Load<AudioClip>("PlacementAudio/"+(type==0?"Barricade":type==3?"Barrel":"Turret"));
            PlayEffect(placementClips[type],.8f);
        }
    }
}
