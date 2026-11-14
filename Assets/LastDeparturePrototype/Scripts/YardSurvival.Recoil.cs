using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        sealed class RecoilProfile
        {
            public readonly float rise,side,returnSpeed,limit,weapon,weaponReturn,aimScale;
            public RecoilProfile(float rise,float side,float recovery,float limit,float weapon,float weaponReturn,float aim)
            {this.rise=rise;this.side=side;returnSpeed=recovery;this.limit=limit;this.weapon=weapon;this.weaponReturn=weaponReturn;aimScale=aim;}
        }
        // Camera angles in degrees; viewmodel impulses are independent of damage.
        static readonly RecoilProfile[] recoilProfiles={
            new RecoilProfile(1.65f,.38f,8.2f,10.5f,1.8f,8,.62f), // Belt-fed LMG: climbing, heavy burst.
            new RecoilProfile(.72f,.28f,5.8f,12,.95f,12,.72f), // Minigun: sustained vibration and climb.
            new RecoilProfile(1.35f,.16f,12,4,.9f,9,.55f), // P9: light and quick.
            new RecoilProfile(4.4f,.32f,7,7,3.2f,5.5f,.68f), // Boomstick: large shoulder kick.
            new RecoilProfile(3.5f,.22f,6.2f,6,2.8f,4.8f,.75f), // Launcher: slow, heavy shove.
            new RecoilProfile(.42f,.1f,9,1,1.5f,5,.9f), // Pitchfork: swing feedback.
            new RecoilProfile(4.9f,.75f,8,9,3.6f,6,.7f), // Dual sawnoffs: alternating sides.
            new RecoilProfile(.65f,.07f,10.5f,2,.65f,7,.45f), // Crossbow: soft release.
            new RecoilProfile(.95f,.52f,9.5f,8,1.05f,11,.52f), // Dual SMGs: alternating burst sway.
            new RecoilProfile(2.85f,.35f,8.5f,7.5f,2.15f,6.5f,.58f), // Heavy rifle: deliberate punch.
            new RecoilProfile(.2f,.06f,14,.6f,.8f,10,1), // Knife: small slash feedback.
            new RecoilProfile(2.5f,.29f,10,6,1.7f,8,.6f), // Talon: firm, sharp kick.
            new RecoilProfile(1.12f,.21f,13.5f,3.5f,.78f,10,.5f), // Beretta: faster recovery than P9.
            new RecoilProfile(3.3f,.42f,9,6.5f,2.45f,6,.65f), // Heavy pistol: pronounced snap.
            new RecoilProfile(.82f,.24f,8.6f,7,1.15f,10.5f,.5f), // Persuader: easy vertical control.
            new RecoilProfile(2.15f,.33f,7.4f,9.5f,1.95f,7,.57f), // FAL: rising automatic bursts.
            new RecoilProfile(4.1f,.18f,6.5f,7.8f,3.05f,4.5f,.6f) // Sniper: strong, slow recovery.
        };
        float recoilSide,recoilShotAt=-100;
        int recoilBurst;
        void ResetRecoil(){cameraKick=weaponKick=recoilSide=0;recoilBurst=0;recoilShotAt=-100;}
        void TickRecoil(float dt)
        {
            var profile=recoilProfiles[weaponId];
            if(skillRanks[1]>=2)dt*=1.25f;
            cameraKick=Mathf.MoveTowards(cameraKick,0,dt*profile.returnSpeed);
            recoilSide=Mathf.MoveTowards(recoilSide,0,dt*profile.returnSpeed*.7f);
            weaponKick=Mathf.MoveTowards(weaponKick,0,dt*profile.weaponReturn);
        }
        void ApplyWeaponRecoil()
        {
            var profile=recoilProfiles[weaponId];
            if(Time.time-recoilShotAt>Mathf.Max(.3f,ActiveWeapon.interval*2.5f))recoilBurst=0;
            int shot=recoilBurst++;recoilShotAt=Time.time;
            float control=WeaponCatalog.IsMelee(weaponId)?1:SkillRecoilScale;
            float aimScale=(aiming?profile.aimScale:1)*control;
            float climb=WeaponCatalog.IsAutomatic(weaponId)?1+Mathf.Min(shot,8)*.045f:1;
            float sway=weaponId==6||weaponId==8?(shot%2==0?1:-1):Mathf.Sin((shot+1)*1.7f+weaponId*.8f);
            cameraKick=Mathf.Min(profile.limit*control,cameraKick+profile.rise*aimScale*climb);
            recoilSide=Mathf.Clamp(recoilSide+profile.side*sway*aimScale*(skillRanks[2]>=2?.65f:1),-3*control,3*control);
            weaponKick=Mathf.Min(profile.weapon*1.7f*control,weaponKick+profile.weapon*(aiming?.7f:1)*control);
        }
    }
}
