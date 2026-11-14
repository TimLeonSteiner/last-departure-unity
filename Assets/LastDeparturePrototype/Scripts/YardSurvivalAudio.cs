using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        [Serializable]
        public sealed class ZombieSoundBank { public string category; public AudioClip[] clips; }
        public ZombieSoundBank[] zombieSounds;
        public AudioClip openingMusic, combatMusic, pressureMusic, hordeMusic;
        AudioSource playerDamageVoice;
        AudioClip[][] playerDamageLines;
        readonly int[] lastPlayerDamageLine = { -1, -1, -1 };
        float nextPlayerDamageVoice;
        int playerDamageVoiceTier = -1;
        readonly AudioClip[] tiredLines = new AudioClip[4];
        int lastTiredLine = -1;
        float nextTiredVoice;

        void TickTiredVoice()
        {
            // A wake-up cancels the current sleepy remark, but keeps the cooldown.
            if (sleepState.Amount < .4f)
            {
                if (playerDamageVoice != null && playerDamageVoiceTier == -2)
                { playerDamageVoice.Stop(); playerDamageVoiceTier = -1; }
                return;
            }
            if (!NightmarePlaying || health <= 0 || AudioListener.pause || !soundEnabled || !audioUnlocked ||
                playerDamageVoice == null || playerDamageVoice.isPlaying || Time.time < nextTiredVoice ||
                Time.time < nextPlayerDamageVoice || openingReplyPending || Time.time < announcerUntil ||
                (coreAnnouncer != null && coreAnnouncer.isPlaying)) return;
            string[] names = { "14_getting_tired", "15_stay_awake", "16_eyes_open", "17_need_caffeine" };
            var available = new List<int>();
            for (int i = 0; i < tiredLines.Length; i++)
            {
                if (tiredLines[i] == null) tiredLines[i] = Resources.Load<AudioClip>("PlayerVoice/" + names[i]);
                if (tiredLines[i] != null) available.Add(i);
            }
            if (available.Count == 0) { nextTiredVoice = Time.time + 10; return; }
            if (available.Count > 1) available.Remove(lastTiredLine);
            lastTiredLine = available[UnityEngine.Random.Range(0, available.Count)];
            playerDamageVoiceTier = -2;
            playerDamageVoice.clip = tiredLines[lastTiredLine];
            playerDamageVoice.volume = .7f * soundVolume;
            playerDamageVoice.Play();
            nextTiredVoice = Time.time + UnityEngine.Random.Range(24f, 36f);
        }

        void SetUpPlayerDamageVoice()
        {
            if (dedicatedServer) return;
            string[][] names = {
                new[] { "01_hit_breath", "02_hit_arm", "03_get_back", "04_not_again", "05_still_fighting" },
                new[] { "06_badly_hurt", "07_cant_breathe" },
                new[] { "08_dont_die", "09_stay_awake", "10_last_stand" }
            };
            playerDamageLines = new AudioClip[names.Length][];
            for (int tier = 0; tier < names.Length; tier++)
            {
                var clips = new List<AudioClip>();
                foreach (var name in names[tier])
                {
                    var clip = Resources.Load<AudioClip>("PlayerVoice/" + name);
                    if (clip != null) { clip.LoadAudioData(); clips.Add(clip); }
                }
                playerDamageLines[tier] = clips.ToArray();
            }
            var obj = new GameObject("Player damage voice"); obj.transform.SetParent(transform, false);
            playerDamageVoice = obj.AddComponent<AudioSource>();
            playerDamageVoice.playOnAwake = false; playerDamageVoice.loop = false;
            playerDamageVoice.spatialBlend = 0; playerDamageVoice.priority = 40;
            playerDamageVoice.volume = .7f * soundVolume;
        }
        void ResetPlayerDamageVoice()
        {
            if (playerDamageVoice != null) playerDamageVoice.Stop();
            nextPlayerDamageVoice = 0; playerDamageVoiceTier = -1;
            for (int i = 0; i < lastPlayerDamageLine.Length; i++) lastPlayerDamageLine[i] = -1;
        }
        void PlayPlayerDamageAudio()
        {
            if (dedicatedServer || !soundEnabled || !audioUnlocked) return;
            // Actual death uses the death sound; pleading belongs to a living, critically hurt player.
            if (health <= 0) return;
            int tier = health <= 25 ? 2 : health <= 50 ? 1 : 0;
            var clips = playerDamageLines != null ? playerDamageLines[tier] : null;
            if (playerDamageVoice == null || clips == null || clips.Length == 0)
            {
                PlayEffect(hurtSound, .55f); return;
            }
            // Critical damage may interrupt a less urgent bark, but repeated hits never stack voices.
            bool urgent = playerDamageVoiceTier <= -2 || (tier == 2 && playerDamageVoiceTier < 2);
            if (!urgent && (playerDamageVoice.isPlaying || Time.time < nextPlayerDamageVoice)) return;
            int last = lastPlayerDamageLine[tier];
            int index = UnityEngine.Random.Range(0, clips.Length > 1 && last >= 0 ? clips.Length - 1 : clips.Length);
            if (clips.Length > 1 && last >= 0 && index >= last) index++;
            lastPlayerDamageLine[tier] = index; playerDamageVoiceTier = tier;
            playerDamageVoice.Stop(); playerDamageVoice.clip = clips[index];
            playerDamageVoice.volume = .7f * soundVolume; playerDamageVoice.Play();
            nextPlayerDamageVoice = Time.time + Mathf.Max(5, clips[index].length + 1.5f);
        }
        readonly Dictionary<string, ZombieSoundBank> soundBanks = new Dictionary<string, ZombieSoundBank>();
        readonly Dictionary<string, int> lastVariants = new Dictionary<string, int>();
        readonly List<AudioSource> zombieVoices = new List<AudioSource>();
        readonly Dictionary<AudioSource, float> zombieVoiceGains = new Dictionary<AudioSource, float>();
        const float DefaultRainVolume = .1f;
        float musicVolume = 1, soundVolume = 1, rainVolume = DefaultRainVolume, saveAudioAt = -1;
        bool rainEnabled = true;
        const string MusicVolumeKey = "Dustline.MusicVolume", SoundVolumeKey = "Dustline.SoundVolume",
            RainVolumeKey = "Dustline.RainVolume", RainEnabledKey = "Dustline.RainEnabled";

        void LoadAudioSettings()
        {
            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 1));
            soundVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SoundVolumeKey, 1));
            rainVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(RainVolumeKey, DefaultRainVolume));
            rainEnabled = PlayerPrefs.GetInt(RainEnabledKey, 1) != 0;
            // Enable the requested imported score once; preserve subsequent mute choices.
            if(PlayerPrefs.GetInt("Dustline.ScoreActivation.v2",0)==0){PlayerPrefs.SetInt("Dustline.ImportedMusicEnabled",1);PlayerPrefs.SetFloat(MusicVolumeKey,.7f);PlayerPrefs.SetInt("Dustline.ScoreActivation.v2",1);PlayerPrefs.Save();musicVolume=.7f;}
            musicEnabled = PlayerPrefs.GetInt("Dustline.ImportedMusicEnabled", 1) != 0;
            soundEnabled = PlayerPrefs.GetInt("Dustline.SoundEnabled", 1) != 0;
            ApplyRainVolume();
        }
        void SaveAudioSettings()
        {
            PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume); PlayerPrefs.SetFloat(SoundVolumeKey, soundVolume);
            PlayerPrefs.SetFloat(RainVolumeKey, rainVolume); PlayerPrefs.SetInt(RainEnabledKey, rainEnabled ? 1 : 0);
            PlayerPrefs.SetInt("Dustline.ImportedMusicEnabled", musicEnabled ? 1 : 0); PlayerPrefs.SetInt("Dustline.SoundEnabled", soundEnabled ? 1 : 0);
            PlayerPrefs.Save(); saveAudioAt = -1;
        }
        void ApplyEffectsVolume()
        {
            ApplyBarricadeVolume();
            if (effects != null) effects.volume = .7f * soundVolume;
            if (playerDamageVoice != null) playerDamageVoice.volume = .7f * soundVolume;
            ApplyMovementVolume();
            ApplyWarehouseVolume();
            ApplyRainVolume();
            foreach (var pair in zombieVoiceGains) if (pair.Key != null) pair.Key.volume = pair.Value * soundVolume;
        }
        void ApplyRainVolume()
        {
            if (stormAudio != null) stormAudio.volume = .08f * rainVolume;
        }
        public void ToggleRain()
        {
            rainEnabled = !rainEnabled;
            saveAudioAt = Time.unscaledTime + .4f;
            if (rainEnabled) UnlockAudio();
            UpdateWeather();
        }
        void DrawVolumeControl(Rect row, string label, ref float volume, bool enabled, Action toggle)
        {
            HudLabel(new Rect(row.x, row.y, 180, 28), label + "  " + Mathf.RoundToInt(volume * 100) + "%", textStyle, HudWhite);
            float next = Mathf.Round(GUI.HorizontalSlider(new Rect(row.x + 180, row.y + 8, 238, 22), volume, 0, 1) * 100) / 100;
            next=AudioWheelValue(new Rect(row.x+180,row.y,238,28),next);
            if (!Mathf.Approximately(next, volume))
            {
                float previous=volume;volume = next;PlayVolumeTick(previous,next); ApplyEffectsVolume(); UpdateWeather(); UpdateAdaptiveAudio(0); saveAudioAt = Time.unscaledTime + .4f;
            }
            if (GUI.Button(new Rect(row.x + 436, row.y, 96, 28), enabled ? "Sound on" : "Muted")) toggle();
        }
        AudioSource musicB;
        int musicStage = -1;
        bool musicUsesB;
        float musicBlend = 1, stageChangedAt, nextVocalAt, nextStepAt;
        const int HordeVoiceCount = 8;
        readonly Dictionary<string, List<int>> zombieVariantBags = new Dictionary<string, List<int>>();
        readonly Dictionary<Enemy, Vector2> zombieVocalProfiles = new Dictionary<Enemy, Vector2>();
        float nextHordeCountAt;
        int audibleHordeCount;

        void SetUpAdaptiveAudio()
        {
            if (zombieSounds != null) foreach (var bank in zombieSounds)
                if (bank != null && !string.IsNullOrEmpty(bank.category) && bank.category != "Zombie_Hiss")
                {
                    soundBanks[bank.category] = bank;
                    var fresh = Resources.LoadAll<AudioClip>("HordeVoices/" + bank.category);
                    if (fresh.Length > 0)
                    {
                        // Runtime bank owns its array; scene assets retain the original library.
                        var combined = new List<AudioClip>();
                        if (bank.clips != null) combined.AddRange(bank.clips);
                        Array.Sort(fresh, (a,b) => string.CompareOrdinal(a.name,b.name));
                        combined.AddRange(fresh);
                        soundBanks[bank.category] = new ZombieSoundBank { category = bank.category, clips = combined.ToArray() };
                        foreach (var clip in fresh) clip.LoadAudioData();
                    }
                    if(bank.clips!=null)foreach(var clip in bank.clips)
                        if(clip!=null&&clip.loadState==AudioDataLoadState.Unloaded)clip.LoadAudioData();
                }
            musicB = GameAudioManager.GetOrCreate(transform).CrossfadeMusic; musicB.volume = 0;
            for (int i = 0; i < 24 + HordeVoiceCount; i++)
            {
                var voiceObject = new GameObject("Zombie audio voice " + i); voiceObject.transform.SetParent(transform, false);
                var voice = voiceObject.AddComponent<AudioSource>(); voice.playOnAwake = false;
                voice.spatialBlend = .85f; voice.rolloffMode = AudioRolloffMode.Linear; voice.minDistance = 2; voice.maxDistance = 22;
                voice.dopplerLevel = 0; voice.priority = 160; zombieVoices.Add(voice);
            }
        }

        public void PlayZombieSound(string category, Vector3 position, float gain)
        {
            PlayZombieVoice(category, position, gain, false, UnityEngine.Random.Range(.95f, 1.05f));
        }

        void PlayZombieVoice(string category, Vector3 position, float gain, bool horde, float pitch)
        {
            if (!soundEnabled || !audioUnlocked || !isActiveAndEnabled || !soundBanks.TryGetValue(category, out var bank) || bank.clips == null || bank.clips.Length == 0) return;
            float reach = horde ? 42 : 22;
            if (arenaCamera != null && (arenaCamera.transform.position - position).sqrMagnitude > reach * reach) return;
            AudioSource voice = null;
            // Crowd calls cannot consume the slots needed for attacks, hits and deaths.
            for (int i = horde ? 0 : HordeVoiceCount; i < (horde ? HordeVoiceCount : zombieVoices.Count); i++)
                if (!zombieVoices[i].isPlaying) { voice = zombieVoices[i]; break; }
            if (voice == null) return; // Keep large hordes from creating unbounded voices.
            if (!zombieVariantBags.TryGetValue(category, out var bag))
                zombieVariantBags[category] = bag = new List<int>();
            if (bag.Count == 0) for (int i = 0; i < bank.clips.Length; i++) bag.Add(i);
            int pick = UnityEngine.Random.Range(0, bag.Count);
            if (bag.Count > 1 && lastVariants.TryGetValue(category, out int previous) && bag[pick] == previous)
                pick = (pick + 1) % bag.Count;
            int variant = bag[pick];bag.RemoveAt(pick);
            lastVariants[category] = variant;
            voice.transform.position = position + Vector3.up * .7f; voice.clip = bank.clips[variant];
            voice.maxDistance = reach;voice.spatialBlend = horde ? 1 : .85f;
            zombieVoiceGains[voice] = gain * .7f;
            voice.pitch = pitch; voice.volume = gain * .7f * soundVolume; voice.Play();
        }

        public void PlayZombieSoundAfter(string category, Transform target, float delay, float gain)
        {
            if (isActiveAndEnabled) StartCoroutine(DelayedZombieSound(category, target, delay, gain));
        }
        IEnumerator DelayedZombieSound(string category, Transform target, float delay, float gain)
        {
            yield return new WaitForSeconds(delay);
            if (target != null) PlayZombieSound(category, target.position, gain);
        }

        void UpdateZombieAudio(Enemy enemy, float dt, float range)
        {
            // The combat target may be the core or a teammate; hearing follows this player.
            range = Vector3.Distance(enemy.position, player.position);
            if (Time.time >= nextHordeCountAt)
            {
                audibleHordeCount = 0;nextHordeCountAt = Time.time + .5f;
                foreach (var other in enemies)
                    if (other.hp > 0 && (other.position-player.position).sqrMagnitude < 42*42) audibleHordeCount++;
                // Remove dead individuals without retaining their rigs for the whole run.
                var expired = new List<Enemy>();
                foreach (var entry in zombieVocalProfiles) if (entry.Key.hp <= 0 || !enemies.Contains(entry.Key)) expired.Add(entry.Key);
                foreach (var gone in expired) zombieVocalProfiles.Remove(gone);
            }
            enemy.vocalTimer -= dt; enemy.stepTimer -= dt;
            if (range < 42 && enemy.vocalTimer <= 0 && Time.time >= nextVocalAt)
            {
                if (!zombieVocalProfiles.TryGetValue(enemy, out var profile))
                {
                    int style = UnityEngine.Random.Range(0, 2);
                    // Preserve the recorded throat timbre; broad pitch shifts make
                    // the crowd sound processed rather than like individual voices.
                    profile = new Vector2(style, UnityEngine.Random.Range(.97f,1.03f));
                    zombieVocalProfiles[enemy] = profile;
                }
                float density = Mathf.Clamp01((audibleHordeCount - 1) / 18f);
                int choice = UnityEngine.Random.value < .7f ? (int)profile.x : UnityEngine.Random.Range(0, 2);
                // Choking/gagging is not an ambient call repeated by the whole horde.
                string category = choice == 0 ? "Zombie_Idle" : "Zombie_Attack";
                float gain = Mathf.Lerp(.43f, .32f, density) * UnityEngine.Random.Range(.85f, 1.15f);
                PlayZombieVoice(category, enemy.position, gain, true, profile.y);
                enemy.vocalTimer = UnityEngine.Random.Range(3f, 6.5f);
                nextVocalAt = Time.time + Mathf.Lerp(.7f, .16f, density) * UnityEngine.Random.Range(.75f,1.25f);
            }
            if (range > 1.05f && range < 8 && enemy.stepTimer <= 0 && Time.time >= nextStepAt)
            {
                bool crawling = enemy.zombie != null && enemy.zombie.InjuredLegs == 2;
                // Upright zombies no longer repeat the synthetic shuffle effect.
                if (crawling) PlayZombieSound("Zombie_Crawl", enemy.position, .2f);
                enemy.stepTimer = crawling ? 1.5f : .65f; nextStepAt = Time.time + .12f;
            }
        }

        int DesiredMusicStage()
        {
            if (!fighting || dead) return 0;
            int nearby = 0; foreach (var enemy in enemies) if ((enemy.position - player.position).sqrMagnitude < 8 * 8) nearby++;
            return wave >= 7 || nearby >= 10 || (health <= 25 && nearby >= 4) ? 3 : wave >= 4 || nearby >= 6 ? 2 : 1;
        }
        AudioClip StageClip(int stage) => stage == 0 ? openingMusic : stage == 1 ? combatMusic : stage == 2 ? pressureMusic : hordeMusic;
        void ResetAdaptiveAudio()
        {
            StopAllCoroutines(); foreach (var voice in zombieVoices) voice.Stop();
            zombieVocalProfiles.Clear();zombieVariantBags.Clear();lastVariants.Clear();
            nextHordeCountAt = 0;audibleHordeCount = 0;
            nextVocalAt = nextStepAt = 0; musicStage = -1; ChangeMusicStage(0, true);
        }
        void ChangeMusicStage(int stage, bool immediate = false)
        {
            if (radio == null || musicB == null) return;
            musicStage = stage; stageChangedAt = Time.time;
            if (immediate)
            {
                radio.Stop(); musicB.Stop(); radio.clip = StageClip(stage); musicUsesB = false; musicBlend = 1;
                radio.volume = .24f * musicVolume; musicB.volume = 0;
                if (audioUnlocked && musicEnabled && radio.clip != null) radio.Play();
                return;
            }
            musicUsesB = !musicUsesB; var incoming = musicUsesB ? musicB : radio;
            incoming.Stop(); incoming.clip = StageClip(stage); incoming.volume = 0; musicBlend = 0;
            if (audioUnlocked && musicEnabled && incoming.clip != null) incoming.Play();
        }
        float dialogueMusicGain=1;
        void UpdateAdaptiveAudio(float dt)
        {
            if (saveAudioAt >= 0 && Time.unscaledTime >= saveAudioAt) SaveAudioSettings();
#if UNITY_EDITOR
            if (audioProbeRunning) return;
#endif
            int desired = DesiredMusicStage();
            if (desired != musicStage && (desired == 0 || Time.time - stageChangedAt > 8)) ChangeMusicStage(desired);
            musicBlend = Mathf.MoveTowards(musicBlend, 1, dt / 2.5f);
            float volume = (dead ? .12f : fighting ? .30f : .24f) * musicVolume;
            bool speaking=soundEnabled && ((titleVoice!=null&&titleVoice.isPlaying)||(coreAnnouncer!=null&&coreAnnouncer.isPlaying)||(playerDamageVoice!=null&&playerDamageVoice.isPlaying));
            dialogueMusicGain=Mathf.MoveTowards(dialogueMusicGain,speaking?.28f:1,dt/(speaking?.12f:.8f));
            volume*=dialogueMusicGain;
            var incoming = musicUsesB ? musicB : radio; var outgoing = musicUsesB ? radio : musicB;
            if (incoming == null || outgoing == null) return;
            incoming.volume = volume * Mathf.Sin(musicBlend * Mathf.PI * .5f);
            outgoing.volume = volume * Mathf.Cos(musicBlend * Mathf.PI * .5f);
            if (musicBlend >= 1 && outgoing.isPlaying) outgoing.Stop();
        }
        void ResumeAdaptiveAudio()
        {
            if (frontScreen == FrontScreen.Launch || dead || !musicEnabled || !audioUnlocked) return;
            var incoming = musicUsesB ? musicB : radio; var outgoing = musicUsesB ? radio : musicB;
            if (incoming != null && incoming.clip != null && !incoming.isPlaying) incoming.UnPause();
            if (incoming != null && incoming.clip != null && !incoming.isPlaying) incoming.Play();
            if (musicBlend < 1 && outgoing != null && outgoing.clip != null) outgoing.UnPause();
        }
        void StopAdaptiveAudio()
        {
            if (launchMusic != null) launchMusic.Stop();
            ResetPlayerDamageVoice();
            if (musicB != null) musicB.Stop(); foreach (var voice in zombieVoices) voice.Stop();
        }
#if UNITY_EDITOR
        public string RunRainAudioChecks()
        {
            float priorRain = rainVolume, priorSound = soundVolume, priorIntensity = rainIntensity, priorSave = saveAudioAt;
            bool priorEnabled = rainEnabled, priorEffects = soundEnabled, priorUnlocked = audioUnlocked;
            try
            {
                if (stormAudio == null || rainAmbience == null || stormAudio.clip != rainAmbience || !stormAudio.loop || rainAmbience.length < 20)
                    throw new Exception("Missing assigned looping rain ambience");
                var pcm = new float[rainAmbience.samples * rainAmbience.channels];
                if (!rainAmbience.GetData(pcm, 0)) throw new Exception("Unreadable rain audio");
                double energy = 0;
                foreach (float sample in pcm) { if (Mathf.Abs(sample) >= .99f) throw new Exception("Clipped rain audio"); energy += sample * sample; }
                if (Math.Sqrt(energy / pcm.Length) < .01) throw new Exception("Silent rain audio");
                audioUnlocked = rainEnabled = soundEnabled = true; rainIntensity = 1; rainVolume = .6f;
                ApplyEffectsVolume(); UpdateWeather();
                float rainGain = stormAudio.volume;
                soundVolume = 0; ApplyEffectsVolume(); ToggleSound(); UpdateWeather();
                if (stormAudio.mute || !Mathf.Approximately(stormAudio.volume, rainGain) || effects.volume != 0)
                    throw new Exception("Gun effects volume/mute changed rain");
                soundEnabled = true; soundVolume = .7f; ApplyEffectsVolume();
                float effectGain = effects.volume;
                rainVolume = .25f; ApplyEffectsVolume();
                if (!Mathf.Approximately(stormAudio.volume, .02f) || !Mathf.Approximately(effects.volume, effectGain))
                    throw new Exception("Rain volume changed gun effects");
                ToggleRain();
                if (!stormAudio.mute || !soundEnabled || !Mathf.Approximately(effects.volume, effectGain)) throw new Exception("Independent rain mute failed");
                ToggleRain(); if (stormAudio.mute) throw new Exception("Rain unmute failed");
                rainVolume = 0; ApplyEffectsVolume(); UpdateWeather();
                if (!stormAudio.mute) throw new Exception("Zero rain volume failed");
                rainVolume = .6f; rainIntensity = 0; UpdateWeather();
                if (!stormAudio.mute) throw new Exception("Zero rainfall intensity failed");
                rainIntensity = 1; audioUnlocked = false; UpdateWeather();
                if (!stormAudio.mute) throw new Exception("Browser audio unlock gate failed");
                return "RAIN AUDIO PASS: assigned non-silent loop; independent gun/rain volume and mute; zero level/intensity; browser unlock gate.";
            }
            finally
            {
                rainVolume = priorRain; soundVolume = priorSound; rainIntensity = priorIntensity;
                rainEnabled = priorEnabled; soundEnabled = priorEffects; audioUnlocked = priorUnlocked; saveAudioAt = priorSave;
                ApplyEffectsVolume(); UpdateWeather();
            }
        }
        public string RunAdaptiveAudioChecks()
        {
            bool priorMusicEnabled = musicEnabled, priorSoundEnabled=soundEnabled, priorUnlocked=audioUnlocked, priorProbe=audioProbeRunning;
            float priorMusicVolume=musicVolume,priorSoundVolume=soundVolume;
            float priorSaveAudioAt = saveAudioAt;
            try
            {
            musicEnabled = soundEnabled = true;musicVolume=soundVolume=.7f;audioProbeRunning=false;ApplyEffectsVolume();
            ResetRun(); UnlockAudio();
            if (soundBanks.Count != 15 || soundBanks.ContainsKey("Zombie_Hiss")) throw new Exception("Expected 15 zombie sound banks without hisses");
            foreach (var bank in soundBanks.Values)
            {
                if (bank.clips.Length < 4) throw new Exception("Missing variations: " + bank.category);
                foreach (var clip in bank.clips)
                {
                    if (clip == null || clip.loadState != AudioDataLoadState.Loaded) throw new Exception("Unloaded zombie sound: " + bank.category);
                    var samples = new float[clip.samples * clip.channels];
                    if (!clip.GetData(samples, 0)) throw new Exception("Unreadable zombie sound");
                    bool audible = false; foreach (float sample in samples) if (Mathf.Abs(sample) > .001f) { audible = true; break; }
                    if (!audible) throw new Exception("Silent zombie sound: " + clip.name);
                }
            }
            for (int stage = 0; stage < 4; stage++)
            {
                if (StageClip(stage) == null || StageClip(stage).length < 30) throw new Exception("Missing soundtrack stage " + stage);
                fighting = stage > 0; wave = stage == 3 ? 7 : stage == 2 ? 4 : 1;
                if (DesiredMusicStage() != stage) throw new Exception("Wrong wave music stage");
            }
            wave = 1; fighting = true; health = 20;
            for (int i = 0; i < 4; i++) SpawnEnemy(new Vector3(i * .5f, 0, 3));
            if (DesiredMusicStage() != 3) throw new Exception("Low health threat escalation failed");
            ChangeMusicStage(1, true); ChangeMusicStage(2); UpdateAdaptiveAudio(.5f);
            if (radio.clip != combatMusic || musicB.clip != pressureMusic || radio.volume <= 0 || musicB.volume <= 0) throw new Exception("Music crossfade failed: radio="+(radio.clip?.name??"null")+" gain="+radio.volume+" incoming="+(musicB.clip?.name??"null")+" gain="+musicB.volume+" blend="+musicBlend+" volume="+musicVolume+" probe="+audioProbeRunning);
            ToggleMusic(); if (radio.isPlaying || musicB.isPlaying) throw new Exception("Music mute failed"); ToggleMusic();
            PlayZombieSound("Zombie_Idle", player.position, .4f); int prior = lastVariants["Zombie_Idle"];
            PlayZombieSound("Zombie_Idle", player.position, .4f); if (lastVariants["Zombie_Idle"] == prior) throw new Exception("Repeated zombie variation");
            ToggleSound(); foreach (var voice in zombieVoices) if (voice.isPlaying) throw new Exception("Zombie sound mute failed"); ToggleSound();
            dead = true; if (DesiredMusicStage() != 0) throw new Exception("Death music reset failed");
            ResetRun(); if (radio.clip != openingMusic || musicStage != 0 || musicB.isPlaying) throw new Exception("Restart music reset failed");
            return "ADAPTIVE AUDIO PASS: all original and expanded banks loaded and non-silent; four soundtrack cues; wave/threat escalation; crossfade; no consecutive voice repeats; music/sound mute; death and restart.";
            }
            finally
            {
                musicEnabled = priorMusicEnabled;soundEnabled=priorSoundEnabled;audioUnlocked=priorUnlocked;audioProbeRunning=priorProbe;musicVolume=priorMusicVolume;soundVolume=priorSoundVolume;saveAudioAt=priorSaveAudioAt;ApplyEffectsVolume();
                effects.mute=!soundEnabled;if(radio!=null)radio.volume=.24f*musicVolume;if(musicB!=null)musicB.volume=0;
                if (!musicEnabled) { if (radio != null) radio.Pause(); if (musicB != null) musicB.Pause(); }
            }
        }
#endif
    }
}
