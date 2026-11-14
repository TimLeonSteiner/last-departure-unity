using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        float injuryStress, injurySeverity, injuryBeatClock, injuryBeatAge = 1;
        AudioSource injuryHeartbeat;
        AudioClip injuryHeartbeatClip;
        Texture2D injuryVignette;
        bool InjuryFeedbackActive => !dedicatedServer && frontScreen == FrontScreen.Game &&
            !dead && health > 0 && !showHelp && !upgradePending && bindingAction < 0 &&
            !HologramBlocking && Time.timeScale > 0;
        void RegisterInjuryHit()
        {
            injuryStress = Mathf.Clamp01(injuryStress + .3f);
            injuryBeatClock = Mathf.Min(injuryBeatClock, .08f);
        }
        void ResetInjuryFeedback()
        {
            injuryStress = injurySeverity = injuryBeatClock = 0; injuryBeatAge = 1;
            if (injuryHeartbeat != null) injuryHeartbeat.Stop();
        }
        void TickInjuryFeedback()
        {
            if (!InjuryFeedbackActive)
            {
                if (injuryHeartbeat != null) injuryHeartbeat.Stop();
                if (dead || health <= 0) ResetInjuryFeedback();
                return;
            }
            float dt = Mathf.Min(Time.deltaTime, .05f);
            injuryStress = Mathf.MoveTowards(injuryStress, 0, dt * .13f);
            float lowHealth = Mathf.Clamp01((.55f - (float)health / Mathf.Max(1, maxPlayerHealth)) / .5f);
            float target = Mathf.Clamp01(lowHealth + injuryStress * .28f);
            injurySeverity = Mathf.MoveTowards(injurySeverity, target, dt * (target > injurySeverity ? 3 : .65f));
            injuryBeatAge += dt;
            if (injurySeverity < .015f)
            {
                injuryBeatClock = 0;
                if (injuryHeartbeat != null) injuryHeartbeat.Stop();
                return;
            }
            injuryBeatClock -= dt;
            if (injuryBeatClock <= 0)
            {
                injuryBeatClock += 60f / Mathf.Lerp(72, 144, injurySeverity);
                injuryBeatAge = 0;
                if (soundEnabled && audioUnlocked && !AudioListener.pause)
                { EnsureInjuryHeartbeat(); injuryHeartbeat.Play(); }
            }
            if (injuryHeartbeat != null)
            {
                injuryHeartbeat.volume = soundEnabled ? Mathf.Lerp(.08f, .65f, injurySeverity) * injurySeverity * soundVolume : 0;
                if (!soundEnabled || !audioUnlocked) injuryHeartbeat.Stop();
            }
        }
        void EnsureInjuryHeartbeat()
        {
            if (injuryHeartbeat != null) return;
            const int rate = 24000;
            var samples = new float[(int)(rate * .36f)];
            for (int i = 0; i < samples.Length; i++)
            { float t = (float)i / rate; samples[i] = InjuryThump(t, 56) + .65f * InjuryThump(t - .135f, 69); }
            injuryHeartbeatClip = AudioClip.Create("Player heartbeat double thump", samples.Length, 1, rate, false);
            injuryHeartbeatClip.SetData(samples, 0);
            var obj = new GameObject("Player injury heartbeat"); obj.transform.SetParent(transform, false);
            injuryHeartbeat = obj.AddComponent<AudioSource>();
            injuryHeartbeat.playOnAwake = false; injuryHeartbeat.loop = false;
            injuryHeartbeat.spatialBlend = 0; injuryHeartbeat.priority = 30; injuryHeartbeat.clip = injuryHeartbeatClip;
        }
        static float InjuryThump(float t, float hz)
        {
            if (t < 0 || t > .2f) return 0;
            float envelope = (1 - Mathf.Exp(-t * 450)) * Mathf.Exp(-t * 32);
            return envelope * (.7f * Mathf.Sin(t * hz * Mathf.PI * 2) + .18f * Mathf.Sin(t * hz * Mathf.PI * 4));
        }
        float InjuryBeat => Mathf.Exp(-injuryBeatAge * 20) + .55f * Mathf.Exp(-Mathf.Abs(injuryBeatAge - .135f) * 32);
        void ApplyInjuryCamera()
        {
            if (!InjuryFeedbackActive || arenaCamera == null) return;
            float strength = injurySeverity * (.12f + .15f * InjuryBeat) + injuryStress * .2f;
            float t = Time.time;
            // Applied after the movement camera rebuilds its base pose; never accumulates.
            arenaCamera.transform.localRotation *= Quaternion.Euler(Mathf.Sin(t * 23) * strength,
                Mathf.Sin(t * 19 + 1) * strength * .6f, Mathf.Sin(t * 17) * strength * .65f);
        }
        void DrawInjuryFeedback(float width, float height)
        {
            if (!InjuryFeedbackActive || injurySeverity < .01f) return;
            if (injuryVignette == null)
            {
                const int size = 128;
                injuryVignette = new Texture2D(size, size, TextureFormat.RGBA32, false);
                injuryVignette.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float radius = new Vector2(x / (size - 1f) * 2 - 1, y / (size - 1f) * 2 - 1).magnitude;
                    pixels[y * size + x] = new Color(.55f, .015f, .008f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.48f, 1.35f, radius)));
                }
                injuryVignette.SetPixels(pixels); injuryVignette.Apply(false, true);
            }
            Color previous = GUI.color;
            GUI.color = new Color(1, 1, 1, injurySeverity * (.25f + .2f * InjuryBeat));
            GUI.DrawTexture(new Rect(0, 0, width, height), injuryVignette); GUI.color = previous;
        }
        void DisposeInjuryFeedback()
        {
            if (injuryHeartbeatClip != null) Destroy(injuryHeartbeatClip);
            if (injuryVignette != null) Destroy(injuryVignette);
        }
    }
}
