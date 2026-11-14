using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        [Header("Nightmare / solo heart defense")]
        public bool nightmareEnabled = true;
        [Min(0)] public float initialAwakeDuration = 30;
        [Min(5)] public float drowsinessBuildupSeconds = 38;
        [Min(1)] public float caffeineDuration = 60;
        [Range(0, 1)] public float caffeineDropChance = .04f;
        [Min(1)] public float lightningWakeDuration = 8;
        public Vector2 lightningInterval = new Vector2(55, 100);
        readonly DrowsinessState sleepState = new DrowsinessState();
        sealed class CaffeineDrop { public Transform body; public Vector3 position; public float age; }
        readonly List<CaffeineDrop> caffeineDrops = new List<CaffeineDrop>();
        readonly Dictionary<Enemy, NightmareAppearance> dreamEnemies = new Dictionary<Enemy, NightmareAppearance>();
        readonly List<Enemy> staleDreamEnemies = new List<Enemy>();
        readonly List<AudioLowPassFilter> dreamFilters = new List<AudioLowPassFilter>();
        Shader nightmareShader;
        Volume dreamVolume;
        VolumeProfile dreamProfile;
        Light dreamLightning;
        LineRenderer dreamBolt;
        Material caffeineMaterial, lightningMaterial;
        AudioSource dreamThunder;
        AudioClip dreamThunderClip;
        float dreamClock, lightningCountdown, lightningFlash, thunderDelay = -1, audioScan;
        float displayedDrowsiness;
        bool stormFlashActive;
        Color preFlashAmbient, preFlashFog;
        static readonly int StormFlashId = Shader.PropertyToID("_StormLightningFlash");
        bool NightmareMode => nightmareEnabled && CoreMode && !dedicatedServer;
        bool NightmarePlaying => NightmareMode && frontScreen == FrontScreen.Game && !dead && !showHelp && !upgradePending && bindingAction < 0;

        void ResetDrowsiness()
        {
            ClearDrowsinessPresentation();
            foreach (var drop in caffeineDrops) if (drop.body != null) Destroy(drop.body.gameObject);
            caffeineDrops.Clear(); dreamEnemies.Clear();
            sleepState.Reset(0);
            nextTiredVoice = 0; lastTiredLine = -1;
            sleepState.Wake(initialAwakeDuration);
            dreamClock = lightningFlash = audioScan = 0; thunderDelay = -1;
            // Let tension build before the first strike, without a predictable opening cue.
            lightningCountdown = Random.Range(40f, 70f);
            if (dreamThunder != null) dreamThunder.Stop();
        }
        void EnsureDreamEffects()
        {
            if (dreamVolume != null) return;
            nightmareShader = Resources.Load<Shader>("NightmareSilhouette");
            var root = new GameObject("Nightmare perception"); root.transform.SetParent(transform, false);
            dreamVolume = root.AddComponent<Volume>(); dreamVolume.isGlobal = true; dreamVolume.priority = 80;
            dreamProfile = ScriptableObject.CreateInstance<VolumeProfile>(); dreamVolume.sharedProfile = dreamProfile;
            var vignette = dreamProfile.Add<Vignette>(); vignette.intensity.Override(.52f); vignette.smoothness.Override(.8f); vignette.rounded.Override(true);
            var color = dreamProfile.Add<ColorAdjustments>(); color.saturation.Override(-60); color.postExposure.Override(-.12f);
            var aberration = dreamProfile.Add<ChromaticAberration>(); aberration.intensity.Override(.25f);
            var lightObject = new GameObject("Lightning reveal"); lightObject.transform.SetParent(root.transform, false);
            dreamLightning = lightObject.AddComponent<Light>(); dreamLightning.type = LightType.Directional;
            dreamLightning.color = new Color(.68f, .79f, 1); dreamLightning.shadows = LightShadows.Soft;
            dreamLightning.transform.rotation = Quaternion.Euler(48, -32, 0); dreamLightning.intensity = 0;
            dreamLightning.cullingMask = ~(1 << 30);
            dreamLightning.shadowBias = .025f; dreamLightning.shadowNormalBias = .15f;
            var bolt = new GameObject("Distant lightning bolt"); bolt.transform.SetParent(root.transform, false);
            dreamBolt = bolt.AddComponent<LineRenderer>(); dreamBolt.useWorldSpace = true; dreamBolt.positionCount = 8;
            dreamBolt.startWidth = .32f; dreamBolt.endWidth = .08f;
            lightningMaterial = new Material(RuntimeShaders.Find("Universal Render Pipeline/Unlit"));
            lightningMaterial.SetColor("_BaseColor", new Color(2, 2.5f, 3)); dreamBolt.sharedMaterial = lightningMaterial; dreamBolt.enabled = false;
            dreamThunder = root.AddComponent<AudioSource>(); dreamThunder.playOnAwake = false; dreamThunder.spatialBlend = 0;
            dreamThunderClip = CreateDreamThunder(); dreamThunder.clip = dreamThunderClip;
            caffeineMaterial = new Material(RuntimeShaders.Find("Universal Render Pipeline/Lit"));
            caffeineMaterial.SetColor("_BaseColor", new Color(.55f, .75f, .55f));
            caffeineMaterial.EnableKeyword("_EMISSION"); caffeineMaterial.SetColor("_EmissionColor", new Color(.12f, .23f, .1f));
        }
        static AudioClip CreateDreamThunder()
        {
            const int rate = 22050; var samples = new float[rate * 3];
            var random = new System.Random(1729); float low = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate, noise = (float)random.NextDouble() * 2 - 1;
                low = Mathf.Lerp(low, noise, .045f);
                samples[i] = (low * 1.8f + noise * .12f * Mathf.Exp(-t * 12) + Mathf.Sin(t * 183) * .10f) * Mathf.Exp(-t * 1.6f) * Mathf.Min(1, t * 50);
            }
            var clip = AudioClip.Create("Storm wake-up rumble", samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
        void TickDrowsiness(float dt)
        {
            if (!NightmarePlaying) return;
            EnsureDreamEffects();
            dreamClock += dt; sleepState.Tick(dt, drowsinessBuildupSeconds);
            if (Pressed(Control.Caffeine)) ConsumeCaffeine();
            lightningCountdown -= dt;
            if (lightningCountdown <= 0)
            {
                TriggerDreamLightning();
                float minimum = Mathf.Max(10, lightningInterval.x);
                lightningCountdown = Random.Range(minimum, Mathf.Max(minimum, lightningInterval.y));
            }
            lightningFlash = Mathf.Max(0, lightningFlash - dt);
            float flashAge = .45f - lightningFlash;
            // Hold the initial strike long enough to register, then leave a softer cloud afterglow.
            float flash = lightningFlash > 0
                ? Mathf.Max(Mathf.Exp(-Mathf.Max(0, flashAge - .05f) * 24), flashAge >= .16f ? .55f * Mathf.Exp(-(flashAge - .16f) * 10) : 0) : 0;
            ApplyStormFlash(flash);
            dreamBolt.enabled = lightningFlash > .18f;
            if (thunderDelay >= 0)
            {
                thunderDelay -= dt;
                if (thunderDelay < 0 && soundEnabled && audioUnlocked) { dreamThunder.volume = soundVolume * .7f; dreamThunder.Play(); }
            }
            dreamThunder.mute = !soundEnabled || !audioUnlocked;
            UpdateCaffeineDrops(dt);
            TickTiredVoice();
            SetDreamPresentation(sleepState.Amount);
        }
        void TriggerDreamLightning()
        {
            sleepState.Wake(lightningWakeDuration); lightningFlash = .45f; thunderDelay = Random.Range(1.2f, 2.6f);
            SetDreamPresentation(0);
            if (dreamBolt == null) return;
            float side = Random.value < .5f ? -1 : 1;
            // The scene-wide light arrives from the same side as the visible bolt.
            dreamLightning.transform.rotation = Quaternion.LookRotation(new Vector3(-side * 38, -38, -40));
            for (int i = 0; i < 8; i++)
                dreamBolt.SetPosition(i, new Vector3(side * (38 + (i == 0 ? 0 : Random.Range(-3f, 3f))), 38 - i * 4.7f, 40 + i * .8f));
        }
        void ApplyStormFlash(float strength)
        {
            if (dreamLightning != null) dreamLightning.intensity = strength * 8;
            Shader.SetGlobalFloat(StormFlashId, strength);
            if (strength > 0)
            {
                if (!stormFlashActive)
                {
                    preFlashAmbient = RenderSettings.ambientLight;
                    preFlashFog = RenderSettings.fogColor;
                    stormFlashActive = true;
                }
                // Cloud-scattered light lifts the shadows slightly; the directional strike
                // still casts roof and wall shadows and reaches through the openings.
                RenderSettings.ambientLight = Color.Lerp(preFlashAmbient, new Color(.025f, .035f, .055f), strength);
                RenderSettings.fogColor = Color.Lerp(preFlashFog, new Color(.18f, .24f, .34f), strength);
            }
            else if (stormFlashActive)
            {
                RenderSettings.ambientLight = preFlashAmbient;
                RenderSettings.fogColor = preFlashFog;
                stormFlashActive = false;
            }
        }
        bool ConsumeCaffeine()
        {
            if (!NightmarePlaying || !sleepState.Drink(caffeineDuration)) return false;
            if(skillRanks[4]>=2)health=Mathf.Min(maxPlayerHealth,health+15);
            SetDreamPresentation(sleepState.Amount); return true;
        }
        void ApplyDrowsyAim()
        {
            if (!NightmarePlaying) return;
            float amount = sleepState.Amount;
            // Applied before the shot ray is constructed, so sight picture and actual aim agree.
            arenaCamera.transform.localRotation *= Quaternion.Euler(
                Mathf.Sin(dreamClock * .83f) * amount * 1.25f,
                Mathf.Sin(dreamClock * 1.13f + .7f) * amount * 1.05f,
                Mathf.Sin(dreamClock * .61f) * amount * .65f);
            arenaCamera.fieldOfView *= Mathf.Lerp(1, .93f, amount);
        }
        void RefreshDreamEnemies()
        {
            // LateUpdate runs after both authoritative animation and guest interpolation.
            if (!NightmarePlaying) { ClearDrowsinessPresentation(); return; }
            SetDreamPresentation(sleepState.Amount);
            foreach (var enemy in enemies)
            {
                if (enemy.body == null) continue;
                if (!dreamEnemies.TryGetValue(enemy, out var appearance))
                {
                    appearance = enemy.body.GetComponent<NightmareAppearance>();
                    if (appearance == null) appearance = enemy.body.gameObject.AddComponent<NightmareAppearance>();
                    appearance.Initialize(nightmareShader); dreamEnemies.Add(enemy, appearance);
                }
                appearance.SetAmount(displayedDrowsiness, dreamClock);
            }
            staleDreamEnemies.Clear();
            foreach (var pair in dreamEnemies)
                if (pair.Key.body == null || pair.Value == null)
                    staleDreamEnemies.Add(pair.Key);
                else
                    // Ragdolls outlive their combat entry. Keep their perception state until despawn.
                    pair.Value.SetAmount(displayedDrowsiness, dreamClock);
            foreach (var enemy in staleDreamEnemies) dreamEnemies.Remove(enemy);
        }
        internal void PrepareNightmareDeath(ImportedZombie zombie)
        {
            if (!NightmareMode || zombie == null || zombie.transform.parent == null) return;
            EnsureDreamEffects();
            var body = zombie.transform.parent;
            foreach (var pair in dreamEnemies)
                if (pair.Key.body == body)
                { pair.Value.SetAmount(NightmarePlaying ? sleepState.Amount : 0, dreamClock); return; }
            // A newly spawned enemy can die before its first LateUpdate presentation pass.
            var appearance = body.GetComponent<NightmareAppearance>();
            if (appearance == null) appearance = body.gameObject.AddComponent<NightmareAppearance>();
            appearance.Initialize(nightmareShader);
            appearance.SetAmount(NightmarePlaying ? sleepState.Amount : 0, dreamClock);
            dreamEnemies.Add(new Enemy { body = body }, appearance);
        }
        void SetDreamPresentation(float amount)
        {
            displayedDrowsiness = amount;
            if (dreamVolume != null) dreamVolume.weight = amount;
            SetAnnouncerDrowsiness(amount);
            audioScan -= Time.deltaTime;
            if (audioScan <= 0 && amount > 0)
            {
                audioScan = .5f;
                foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                {
                    if (source.ignoreListenerPause || source == coreAnnouncer || source == screamerCall || source == dreamThunder || source.GetComponent<AudioLowPassFilter>() != null) continue;
                    var filter = source.gameObject.AddComponent<AudioLowPassFilter>(); filter.lowpassResonanceQ = 1; dreamFilters.Add(filter);
                }
            }
            for (int i = dreamFilters.Count - 1; i >= 0; i--)
            {
                var filter = dreamFilters[i]; if (filter == null) { dreamFilters.RemoveAt(i); continue; }
                filter.enabled = amount > .001f; filter.cutoffFrequency = Mathf.Lerp(22000, filter.gameObject.name=="Sentry discharge audio"?4000:1100, amount);
            }
        }
        void ClearDrowsinessPresentation()
        {
            SetDreamPresentation(0);
            foreach (var pair in dreamEnemies) if (pair.Value != null) pair.Value.SetAmount(0, dreamClock);
            ApplyStormFlash(0);
            if (dreamBolt != null) dreamBolt.enabled = false;
        }
        static float CaffeineChanceForWave(int stage, float chance)
            => Mathf.Clamp01(chance) * Mathf.Min(1, 32f / WaveZombieCount(stage));
        void DropCaffeine(Enemy enemy)
        {
            if (!NightmareMode || enemy == null || caffeineDrops.Count >= 24 || Random.value >= CaffeineChanceForWave(wave, caffeineDropChance)) return;
            SpawnCaffeineDrop(enemy.position);
        }
        void SpawnCaffeineDrop(Vector3 position)
        {
            EnsureDreamEffects();
            GameObject pill=CaffeinePickupVisual.Create(actors);
            pill.transform.localRotation=Quaternion.Euler(15,0,0);
            pill.name="Caffeine pill / walk over to collect";
            pill.transform.position=position+Vector3.up*.25f;
            caffeineDrops.Add(new CaffeineDrop { body = pill.transform, position = position });
        }
        void UpdateCaffeineDrops(float dt)
        {
            for (int i = caffeineDrops.Count - 1; i >= 0; i--)
            {
                var drop = caffeineDrops[i]; drop.age += dt;
                if (drop.body == null) { caffeineDrops.RemoveAt(i); continue; }
                bool collected = player != null && Vector3.Distance(player.position, drop.position) < 1.6f
                    && PickupLineClear(player.position, drop.position) && sleepState.Collect(1) == 1;
                if (drop.body == null || collected || drop.age > 120)
                { if (drop.body != null) Destroy(drop.body.gameObject); caffeineDrops.RemoveAt(i); continue; }
                drop.body.position = drop.position + Vector3.up * (.28f + Mathf.Sin(dreamClock * 2.5f) * .055f);
                drop.body.Rotate(Vector3.up, dt * 32);
            }
        }
        void DrawDrowsinessHud(float width, float height)
        {
            if (!NightmarePlaying) return;
            EnsureDreadHud();
            float x=width-142,y=32;
            DreadLabel(new Rect(x,y,110,22),"DROWSINESS",dreadCaption,DreadMuted,TextAnchor.MiddleRight);
            DreadLabel(new Rect(x,y+22,110,38),Mathf.RoundToInt(sleepState.Amount*100)+"%",dreadValue,HudWhite,TextAnchor.MiddleRight);
            Rect gauge=new Rect(width-44,y+67,10,132);
            Fill(gauge,new Color(.02f,.02f,.02f,.5f));DreadFrame(gauge,DreadMuted);
            float fill=130*Mathf.Clamp01(sleepState.Amount);
            Fill(new Rect(gauge.x+1,gauge.yMax-1-fill,8,fill),HudRed);
            for(int tick=0;tick<=8;tick++)Fill(new Rect(gauge.x-(tick%2==0?7:4),gauge.y+tick*16.375f,tick%2==0?5:2,1),DreadMuted);
            DrawTabletSymbol(new Rect(width-58,y+207,26,28));
            DreadLabel(new Rect(x,y+239,110,18),"C A F F E I N E",dreadSmall,HudWhite,TextAnchor.MiddleRight);
            DreadLabel(new Rect(x,y+259,110,24),"x"+sleepState.Caffeine+"  "+ControlHint(Control.Caffeine),dreadCaption,HudWhite,TextAnchor.MiddleRight);
            if(sleepState.CaffeineSeconds>0)
                DreadLabel(new Rect(x,y+286,110,18),Mathf.CeilToInt(sleepState.CaffeineSeconds)+"s ACTIVE",dreadSmall,DreadMuted,TextAnchor.MiddleRight);
        }
        void DisposeDreamEffects()
        {
            ClearDrowsinessPresentation();
            foreach (var filter in dreamFilters) if (filter != null) Destroy(filter);
            dreamFilters.Clear();
            if (dreamProfile != null) Destroy(dreamProfile);
            if (dreamThunderClip != null) Destroy(dreamThunderClip);
            if (caffeineMaterial != null) Destroy(caffeineMaterial);
            if (lightningMaterial != null) Destroy(lightningMaterial);
        }
    }
}
