using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public AudioClip[] footstepSounds;
        public AudioClip jumpSound, landingSound, slideSound, slideStopSound;
        AudioSource slideAudio;
        // Movement uses fast game-world units. One contact every half second at walk speed.
        const float FootstepTravel = WalkSpeed * .5f;
        float footstepDistance;
        int footstepIndex = -1;
        bool movementAudioPaused;

        void SetUpMovementAudio()
        {
            var obj = new GameObject("Player slide foley");
            obj.transform.SetParent(transform, false);
            slideAudio = obj.AddComponent<AudioSource>();
            slideAudio.playOnAwake = false; slideAudio.loop = false; slideAudio.spatialBlend = 0;
            ApplyMovementVolume();
        }
        void ApplyMovementVolume()
        {
            if (slideAudio != null) slideAudio.volume = .7f * .45f * soundVolume * (slideTimer > 0 ? Mathf.Lerp(.35f, 1, slideTimer / SlideDuration) : 1);
        }
        void BeginSlideAudio()
        {
            footstepDistance = 0;
            if (slideAudio == null || slideSound == null || !soundEnabled || !audioUnlocked) return;
            slideAudio.clip = slideSound; slideAudio.pitch = 1;
            ApplyMovementVolume(); slideAudio.Play();
        }
        void StopSlideAudio(bool playStop)
        {
            if (slideAudio != null) slideAudio.Stop();
            if (playStop) PlayEffect(slideStopSound, .2f);
        }
        void ResetMovementAudio()
        {
            StopSlideAudio(false); footstepDistance = 0; footstepIndex = -1; movementAudioPaused = false;
        }
        void PauseMovementAudio(bool paused)
        {
            if (movementAudioPaused == paused) return;
            movementAudioPaused = paused;
            if (slideAudio == null) return;
            if (paused) slideAudio.Pause();
            else if (soundEnabled && audioUnlocked && slideTimer > 0) slideAudio.UnPause();
        }
        void UpdateFootsteps(float travelled, bool grounded, bool sliding)
        {
            ApplyMovementVolume();
            // Count actual travel, so pushing into a wall cannot create footsteps.
            if (movementAudioPaused || !grounded || sliding || travelled < .0001f) { footstepDistance = 0; return; }
            footstepDistance += travelled;
            if (footstepDistance + .00001f < FootstepTravel) return;
            footstepDistance = Mathf.Max(0, footstepDistance - FootstepTravel) % FootstepTravel;
            if (footstepSounds == null || footstepSounds.Length == 0) return;
            // Different real takes, without a conspicuous 1-2-3-4 loop or adjacent repeats.
            int count = footstepSounds.Length;
            int next;
            if (count > 1 && footstepIndex >= 0 && footstepIndex < count)
            {
                next = Random.Range(0, count - 1);
                if (next >= footstepIndex) next++;
            }
            else next = Random.Range(0, count);
            footstepIndex = next;
            PlayEffect(footstepSounds[next], Random.Range(.21f, .24f));
        }

#if UNITY_EDITOR
        public string RunFootstepTimingChecks()
        {
            var randomState = Random.state;
            float previousDistance = footstepDistance;
            int previousIndex = footstepIndex;
            bool previousPause = movementAudioPaused;
            bool previousSound = soundEnabled;
            try
            {
                soundEnabled = false; // Verify timing without playing a burst of test footsteps.
                if (footstepSounds == null || footstepSounds.Length != 4)
                    throw new System.Exception("Expected four concrete bootstep takes.");
                foreach (var clip in footstepSounds)
                {
                    if (clip == null) throw new System.Exception("Missing bootstep clip.");
                    if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                    if (clip.loadState != AudioDataLoadState.Loaded || clip.length > .7f)
                        throw new System.Exception("Bootstep unavailable or not a short one-shot: " + clip.name);
                    var samples = new float[clip.samples * clip.channels];
                    if (!clip.GetData(samples, 0) || !System.Array.Exists(samples, x => Mathf.Abs(x) > .001f))
                        throw new System.Exception("Bootstep has no readable audio: " + clip.name);
                }
                movementAudioPaused = false; footstepDistance = 0; footstepIndex = -1;
                int contacts = 0;
                for (int frame = 0; frame < 120; frame++)
                {
                    int before = footstepIndex;
                    UpdateFootsteps(WalkSpeed / 60, true, false);
                    if (before != footstepIndex) contacts++;
                }
                if (contacts != 4) throw new System.Exception("Expected four contacts in two walking seconds; got " + contacts);
                int last = footstepIndex;
                UpdateFootsteps(FootstepTravel, false, false);
                UpdateFootsteps(FootstepTravel, true, true);
                UpdateFootsteps(0, true, false);
                movementAudioPaused = true; UpdateFootsteps(FootstepTravel, true, false);
                if (footstepIndex != last || footstepDistance != 0)
                    throw new System.Exception("Footsteps during air/slide/stationary/pause.");
                movementAudioPaused = false;
                for (int i = 0; i < 20; i++)
                {
                    last = footstepIndex; UpdateFootsteps(FootstepTravel, true, false);
                    if (last == footstepIndex) throw new System.Exception("Adjacent repeated bootstep.");
                }
                return "FOOTSTEPS PASS: four loaded non-silent short takes; 2 contacts/second at walk speed; no air, slide, stationary or paused steps; no adjacent repeated takes.";
            }
            finally { Random.state = randomState; footstepDistance = previousDistance; footstepIndex = previousIndex; movementAudioPaused = previousPause; soundEnabled = previousSound; }
        }
#endif
    }
}
