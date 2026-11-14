#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunDrowsinessChecks()
        {
            var report = new System.Text.StringBuilder();
            void Check(bool ok, string message) { if (!ok) throw new Exception(message); report.AppendLine("PASS " + message); }
            float savedChance = caffeineDropChance;
            var randomState = UnityEngine.Random.state;
            try
            {
                var clock = new DrowsinessState(); clock.Reset(0); clock.Tick(19, 38);
                Check(Mathf.Approximately(clock.Amount, .5f), "Sleep builds at the configured rate");
                clock.Collect(100); Check(clock.Caffeine == 64 && clock.Collect(1) == 0, "Stack caps at 64 without accepting excess");
                Check(clock.Drink(60) && Mathf.Approximately(clock.Amount, .5f) && clock.Caffeine == 63, "One dose begins recovery without an instant snap");
                Check(!clock.Drink(60) && clock.Caffeine == 63, "Active caffeine cannot waste a dose");
                clock.Tick(20, 38); clock.Wake(8); clock.Tick(20, 38);
                Check(clock.CaffeineSeconds == 20 && clock.Amount == 0, "Lightning never shortens caffeine protection");
                clock.Tick(39, 38); Check(Mathf.Approximately(clock.Amount, .5f), "Only time after protection expires builds sleep");
                clock.Tick(-10, 38); Check(Mathf.Approximately(clock.Amount, .5f), "Negative elapsed time cannot reverse state");

                PlayFromMenu(); lightningCountdown = 1000; TickDrowsiness(0);
                Check(sleepState.Amount == 0 && sleepState.Caffeine == 0, "Run begins fully awake with an empty pill inventory");
                Check(!ConsumeCaffeine(), "Empty inventory cannot provide caffeine");
                Check(dreamVolume.weight == 0 && !announcerMuffle.enabled, "Opening vision and voice are clear");
                TickDrowsiness(initialAwakeDuration);
                Check(sleepState.Amount == 0 && dreamVolume.weight == 0, "Opening grace period stays clear");
                TickDrowsiness(drowsinessBuildupSeconds * .25f);
                Check(Mathf.Approximately(sleepState.Amount, .25f), "Drowsiness builds gradually after the opening grace period");
                TickDrowsiness(drowsinessBuildupSeconds * .5f);
                Check(nightmareShader != null && nightmareShader.isSupported, "Nightmare shader is available to the game");
                Check(announcerMuffle.enabled && dreamVolume.weight > .5f, "Dream state drives both voice and vision");
                Check(dreamFilters.Count > 0 && dreamFilters.Where(f => f != null).All(f => f.enabled && f.cutoffFrequency < 10000), "Environmental and weapon sources receive the dream low-pass filter");
                var activeClip = coreAnnouncer.clip;
                SpawnEnemy(new Vector3(0, 0, -5)); var victim = enemies.Last();
                var renderer = victim.body.GetComponentInChildren<SkinnedMeshRenderer>();
                Check(renderer != null, "Loaded actual skinned zombie for perception checks");
                var original = renderer.sharedMaterials; bool partEnabled = renderer.enabled;
                RefreshDreamEnemies();
                Check(renderer.sharedMaterial.shader == nightmareShader && renderer.enabled == partEnabled, "Nightmare surfaces preserve combat renderer flags");
                var origin = victim.zombie.RegionCenter(5) - Vector3.forward * 3;
                Check(victim.zombie.Trace(new Ray(origin, Vector3.forward), 6, out _, out _), "Smudged zombie remains hittable");
                arenaCamera.transform.localRotation = Quaternion.identity; arenaCamera.fieldOfView = 75; dreamClock = 2; ApplyDrowsyAim();
                Check(Quaternion.Angle(arenaCamera.transform.localRotation, Quaternion.identity) > .1f && arenaCamera.fieldOfView < 75, "Drowsiness changes actual aim and narrows field of view");
                TriggerDreamLightning(); RefreshDreamEnemies();
                Check(sleepState.Amount == 0 && sleepState.WakeSeconds == 8 && !announcerMuffle.enabled && coreAnnouncer.clip == activeClip, "Lightning clears perception without restarting speech");
                Check(renderer.sharedMaterials.SequenceEqual(original), "Waking restores exact original zombie materials");
                lightningCountdown = 1000; TickDrowsiness(7);
                Check(sleepState.Amount == 0 && dreamLightning.intensity == 0 && !dreamBolt.enabled, "Flash ends while wake protection remains");
                TickDrowsiness(2); Check(sleepState.Amount > 0, "Sleep returns after wake window");
                caffeineDropChance = 1;
                SpawnEnemy(player.position); var pillCarrier = enemies.Last();
                HurtArsenalEnemy(pillCarrier, 1000, Vector3.forward);
                Check(caffeineDrops.Count == 1 && sleepState.Caffeine == 0, "Zombie death drops a pill into the world rather than inventory");
                UpdateCaffeineDrops(.01f);
                Check(caffeineDrops.Count == 0 && sleepState.Caffeine == 1, "Walking over a zombie drop collects one pill");
                Check(ConsumeCaffeine() && sleepState.Caffeine == 0 && sleepState.CaffeineSeconds == 60, "Gameplay caffeine action consumes the collected pill");
                PauseFromGame(); float before = sleepState.CaffeineSeconds; TickDrowsiness(100); RefreshDreamEnemies();
                Check(sleepState.CaffeineSeconds == before && dreamVolume.weight == 0, "Pause freezes timers and clears perception from menu");
                Check(dreamFilters.Where(f => f != null).All(f => !f.enabled), "Pause restores unfiltered menu audio");
                ResumeFromMenu(); TickDrowsiness(61); Check(sleepState.Amount > 0, "Caffeine expiry resumes drowsiness");
                sleepState.Reset(.7f); SpawnCaffeineDrop(player.position); UpdateCaffeineDrops(.01f);
                Check(sleepState.Caffeine == 1 && caffeineDrops.Count == 0, "Walk-over pickup joins stack once");
                sleepState.Collect(64); SpawnCaffeineDrop(player.position); UpdateCaffeineDrops(.01f);
                Check(sleepState.Caffeine == 64 && caffeineDrops.Count == 1, "Full stack leaves can on ground");
                UpdateCaffeineDrops(121); Check(caffeineDrops.Count == 0, "Expired drops are cleaned up");
                caffeineDropChance = 1; HurtArsenalEnemy(victim, 1000, Vector3.forward);
                Check(caffeineDrops.Count == 1, "Projectile or melee death creates a caffeine drop");
                RefreshDreamEnemies();
                Check(renderer.sharedMaterial.shader == nightmareShader, "Dead ragdoll retains nightmare appearance after combat removal");
                sleepState.Wake(8); RefreshDreamEnemies();
                Check(renderer.sharedMaterials.SequenceEqual(original), "Lightning also restores original corpse materials");
                sleepState.Reset(.8f); RefreshDreamEnemies();
                Check(renderer.sharedMaterial.shader == nightmareShader, "Existing corpse becomes indistinct again when drowsiness returns");
                SpawnEnemy(new Vector3(0, 0, -7)); var rayVictim = enemies.Last();
                ApplyRayDamage(rayVictim, 0, rayVictim.position + Vector3.up, Vector3.forward, 1000);
                RefreshDreamEnemies();
                Check(rayVictim.body.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial.shader == nightmareShader,
                    "Enemy killed before first presentation frame never reveals its normal corpse");
                Check(caffeineDrops.Count == 2, "Bullet or sniper death creates a caffeine drop");
                caffeineDropChance = 0; SpawnEnemy(new Vector3(0, 0, -8)); HurtArsenalEnemy(enemies.Last(), 1000, Vector3.forward);
                Check(caffeineDrops.Count == 2, "Zero drop probability creates no drop");
                float amount = sleepState.Amount; OfferCoreUpgrades(); TickDrowsiness(100);
                Check(sleepState.Amount == amount, "Upgrade selection freezes sleep progression");
                upgradePending = false; dead = true; Check(!ConsumeCaffeine(), "Death prevents caffeine consumption");
                RefreshDreamEnemies(); Check(dreamVolume.weight == 0 && !announcerMuffle.enabled, "Death clears sleep effects for the death sequence");
                ShowHome(); Check(caffeineDrops.Count == 0 && !announcerMuffle.enabled && dreamLightning.intensity == 0, "Menu clears drops, audio and lightning");
                return report.ToString();
            }
            finally { caffeineDropChance = savedChance; UnityEngine.Random.state = randomState; ShowHome(); }
        }
        public void PreviewNightmare(bool awake)
        {
            PlayFromMenu(); remainingToSpawn = 0; lightningCountdown = 1000;
            player.position = new Vector3(0, .65f, -10);
            SpawnEnemy(new Vector3(-1, 0, -5)); SpawnEnemy(new Vector3(1.2f, 0, -2.7f)); SpawnEnemy(new Vector3(-.8f, 0, 2));
            foreach (var enemy in enemies) enemy.body.rotation = Quaternion.Euler(0, 180, 0);
            EnsureDreamEffects(); sleepState.Reset(awake ? 0 : .9f); dreamClock = 2;
            SetDreamPresentation(sleepState.Amount); RefreshDreamEnemies();
            if (awake) { lightningFlash = .3f; dreamLightning.intensity = 1.3f; }
            Time.timeScale = 0;
        }
        public void EndNightmarePreview() { ShowHome(); }
    }
}
#endif
