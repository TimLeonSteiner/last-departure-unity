using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        AudioSource titleVoice;
        AudioSource deathSting;
        bool titleVoicePlayed;

        void SetUpTitleVoice()
        {
            if (dedicatedServer) return;
            var clip = Resources.Load<AudioClip>("Announcer/last_departure");
            if (clip == null) return;
            clip.LoadAudioData();
            var obj = new GameObject("Last Departure title voice");
            obj.transform.SetParent(transform, false);
            titleVoice = obj.AddComponent<AudioSource>();
            titleVoice.playOnAwake = false;
            titleVoice.spatialBlend = 0;
            titleVoice.priority = 0;
            titleVoice.clip = clip;
        }

        void PlayTitleVoiceOnce()
        {
            if (dedicatedServer || titleVoicePlayed || !audioUnlocked || titleVoice == null) return;
            // A muted launch stays silent, including when sound is enabled later.
            titleVoicePlayed = true;
            if (!soundEnabled) return;
            titleVoice.volume = .85f * soundVolume;
            titleVoice.Play();
        }

        void PlayDeathSting()
        {
            if (radio != null) radio.Pause();
            if (musicB != null) musicB.Pause();
            if (titleVoice != null) titleVoice.Stop();
            if (!soundEnabled || !audioUnlocked) return;
            var clip = Resources.Load<AudioClip>("Announcer/death_sting");
            if (clip == null) { PlayEffect(deathSound, .8f); return; }
            if (deathSting == null)
            {
                var obj = new GameObject("Death sting");
                obj.transform.SetParent(transform, false);
                deathSting = obj.AddComponent<AudioSource>();
                deathSting.playOnAwake = false;
                deathSting.spatialBlend = 0;
                deathSting.ignoreListenerVolume = true;
            }
            deathSting.clip = clip;
            deathSting.volume = .55f * soundVolume;
            deathSting.Play();
        }
    }
}
