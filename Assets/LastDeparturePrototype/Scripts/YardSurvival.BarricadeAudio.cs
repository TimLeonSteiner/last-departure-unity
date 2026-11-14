using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        AudioClip[][] woodClips;
        readonly AudioSource[] woodVoices = new AudioSource[12];
        readonly float[] woodGains = new float[12];
        readonly float[] gateImpactAt = new float[4];
        readonly int[] lastWoodClip = { -1, -1, -1 };
        void SetupBarricadeAudio()
        {
            if (woodClips != null || dedicatedServer) return;
            woodClips = new AudioClip[3][];
            string[] names = { "impact", "splinter", "breach" }; int[] counts = { 5, 3, 2 };
            for (int group = 0; group < 3; group++)
            {
                woodClips[group] = new AudioClip[counts[group]];
                for (int i = 0; i < counts[group]; i++) woodClips[group][i] = Resources.Load<AudioClip>("BarricadeAudio/" + names[group] + "_" + (i + 1));
            }
            for (int i = 0; i < woodVoices.Length; i++)
            {
                var obj = new GameObject("Barricade spatial audio " + i); obj.transform.SetParent(transform, false);
                var source = obj.AddComponent<AudioSource>(); source.playOnAwake = false;
                source.spatialBlend = 1; source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 4; source.maxDistance = 48; source.dopplerLevel = 0;
                source.priority = i < 8 ? 100 : 70;
                woodVoices[i] = source;
            }
        }
        public void PlayBarricadeDamage(Vector3 position, bool splinter, bool breach)
        {
            if (dedicatedServer || !isActiveAndEnabled || !soundEnabled || !audioUnlocked) return;
            SetupBarricadeAudio();
            int side = Mathf.Abs(position.z) >= Mathf.Abs(position.x) ? (position.z > 0 ? 0 : 2) : (position.x > 0 ? 1 : 3);
            int pressure = 0;
            foreach (var enemy in enemies)
                if ((enemy.position-position).sqrMagnitude < 25) pressure++;
            float intensity = Mathf.Clamp01(pressure / 8f);
            // Limit simultaneous synchronized blows per doorway; breaking has reserved voices.
            if (Time.unscaledTime >= gateImpactAt[side])
            {
                gateImpactAt[side] = Time.unscaledTime + .075f;
                PlayWoodVoice(0, position, Mathf.Lerp(.2f, .32f, intensity));
            }
            if (splinter || breach) PlayWoodVoice(breach ? 2 : 1, position, breach ? .48f : .3f);
        }
        void PlayWoodVoice(int group, Vector3 position, float gain)
        {
            int start = group == 0 ? 0 : 8, end = group == 0 ? 8 : 12;
            int slot = -1;
            for (int i = start; i < end; i++) if (!woodVoices[i].isPlaying) { slot = i; break; }
            if (slot < 0)
            {
                if (group != 2) return;
                slot = 8; // A final collapse takes priority over a minor splinter.
            }
            int variant = UnityEngine.Random.Range(0, woodClips[group].Length);
            if (variant == lastWoodClip[group]) variant = (variant + 1) % woodClips[group].Length;
            lastWoodClip[group] = variant;
            var source = woodVoices[slot]; source.Stop();
            source.transform.position = position; source.clip = woodClips[group][variant];
            source.pitch = UnityEngine.Random.Range(.88f, 1.12f);
            woodGains[slot] = gain; source.volume = gain * soundVolume; source.Play();
        }
        void ApplyBarricadeVolume()
        {
            for (int i = 0; i < woodVoices.Length; i++) if (woodVoices[i] != null) woodVoices[i].volume = woodGains[i] * soundVolume;
        }
        void StopBarricadeAudio()
        {
            foreach (var source in woodVoices) if (source != null) source.Stop();
            for (int i = 0; i < gateImpactAt.Length; i++) gateImpactAt[i] = 0;
        }
#if UNITY_EDITOR
        public string RunBarricadeAudioChecks()
        {
            SetupBarricadeAudio();
            foreach (var group in woodClips) foreach (var clip in group)
                if (clip == null || clip.samples == 0) throw new System.Exception("Missing wood audio clip");
            bool oldEnabled = soundEnabled, oldUnlocked = audioUnlocked; float oldVolume = soundVolume;
            try
            {
                soundEnabled = audioUnlocked = true; soundVolume = .5f;
                StopBarricadeAudio(); PlayBarricadeDamage(Vector3.forward * 21.5f, true, false);
                if (woodVoices[0].clip == null || woodVoices[8].clip == null) throw new System.Exception("Impact and splinter did not layer");
                for (int i = 0; i < 100; i++) PlayBarricadeDamage(Vector3.forward * 21.5f, true, true);
                soundVolume = 0; ApplyBarricadeVolume();
                foreach (var source in woodVoices) if (source.volume != 0) throw new System.Exception("Volume slider ignored");
                StopBarricadeAudio(); soundEnabled = false; PlayBarricadeDamage(Vector3.zero, true, true);
                foreach (var source in woodVoices) if (source.isPlaying) throw new System.Exception("Muted wood sound played");
                return "PASS: ten loaded clips; impact/splinter layering; bounded 12-source horde pool; effects volume and mute; stop/reset.";
            }
            finally { StopBarricadeAudio(); soundEnabled = oldEnabled; audioUnlocked = oldUnlocked; soundVolume = oldVolume; ApplyBarricadeVolume(); }
        }
#endif
    }
}
