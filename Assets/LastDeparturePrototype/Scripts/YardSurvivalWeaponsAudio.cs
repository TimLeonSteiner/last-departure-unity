using System;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        [Serializable]
        public sealed class WeaponSoundBank
        {
            public string weapon;
            public AudioClip shot, reload, action;
            public bool reloadIncludesAction;
            public AudioClip[] shotVariations;
            public AudioClip[] automaticShotVariations;
        }

        public WeaponSoundBank[] weaponSounds;
        public AudioClip turretShotSound;
        readonly int[] shotVariant = new int[WeaponCatalog.All.Length];
        int lastShotWeapon = -1;
        float lastShotAt = float.NegativeInfinity;
        void PlayWeaponShot()
        {
            if(weaponId==1&&!minigunSpinning){minigunSpinning=true;PlayEffect(minigunStart,.45f);}
            // One sound per actual discharge keeps release, reload and empty magazines in sync.
            PlayEffect(SelectWeaponShotClip(Time.time), .85f);
        }

        AudioClip SelectWeaponShotClip(float at)
        {
            AudioClip clip = WeaponClip(0);
            if (weaponSounds != null && weaponId < weaponSounds.Length)
            {
                var bank = weaponSounds[weaponId];
                bool continuingBurst = lastShotWeapon == weaponId &&
                    at >= lastShotAt && at - lastShotAt <= ActiveWeapon.interval * 1.5f;
                var variations = continuingBurst && bank != null && bank.automaticShotVariations != null && bank.automaticShotVariations.Length > 0
                    ? bank.automaticShotVariations : bank?.shotVariations;
                if (variations != null && variations.Length > 0)
                {
                    int variant = shotVariant[weaponId] % variations.Length;
                    shotVariant[weaponId] = (variant + 1) % variations.Length;
                    if (variations[variant] != null) clip = variations[variant];
                }
            }
            lastShotWeapon = weaponId;
            lastShotAt = at;
            return clip;
        }
        AudioClip WeaponClip(int eventId)
        {
            if (weaponSounds != null && weaponId < weaponSounds.Length)
            {
                var bank = weaponSounds[weaponId];
                if (bank != null)
                {
                    var clip = eventId == 0 ? bank.shot : eventId == 1 ? bank.reload : bank.action;
                    if (clip != null) return clip;
                }
            }
            return eventId == 0 ? shotSound : eventId == 1 ? reloadSound : boltSound;
        }

#if UNITY_EDITOR
        public string RunWeaponAudioChecks()
        {
            if(weaponSounds==null || weaponSounds.Length!=17)throw new Exception("Expected 17 mapped sound banks");
            for(int id=0;id<17;id++){
                if (!WeaponCatalog.IsAvailable(id)) continue;
                var bank=weaponSounds[id];if(bank==null || bank.weapon!=WeaponCatalog.All[id].name)throw new Exception("Sound mapping "+id);
                ValidateWeaponClip(bank.shot);if(!WeaponCatalog.IsMelee(id))ValidateWeaponClip(bank.reload);ValidateWeaponClip(bank.action);
            }
            ValidateWeaponClip(turretShotSound);
            return "AUDIO PASS: 17 weapon banks, existing licensed sound families reused, loaded PCM output and sentry shot validated.";
        }
        static void ValidateWeaponClip(AudioClip clip)
        {
            if(clip!=null&&clip.loadState==AudioDataLoadState.Unloaded)clip.LoadAudioData();
            if (clip == null || clip.loadState != AudioDataLoadState.Loaded)
                throw new Exception(clip==null?"Missing weapon clip":"Unloaded weapon clip: "+clip.name+" state="+clip.loadState+" channels="+clip.channels+" frequency="+clip.frequency);
            var samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0)) throw new Exception("Unreadable weapon PCM: " + clip.name);
            foreach (float sample in samples) if (Mathf.Abs(sample) > .001f) return;
            throw new Exception("Silent weapon clip: " + clip.name);
        }
#endif
    }
}
