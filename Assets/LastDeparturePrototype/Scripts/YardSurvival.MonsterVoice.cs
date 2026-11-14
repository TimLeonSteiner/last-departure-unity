using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        const float MonsterVoiceRange = 18f;
        const float MonsterVoiceRepeatDelay = 40f;
        float nextMonsterScan;
        bool hasMeshyMonsterRoster;
        float nextMonsterVoice, monsterSubtitleUntil;
        string monsterSubtitle;
        readonly HashSet<int> calledOutMonsters = new HashSet<int>();
        readonly HashSet<int> spottedScreamers = new HashSet<int>();
        float nextScreamerWarning;
        AudioClip screamerSpottedClip;

        sealed class MonsterCallout
        {
            public readonly string ClipPrefix;
            public readonly string[] Lines;
            public bool Introduced;
            public int NextWarning;
            public float RepeatAfter;
            public readonly AudioClip[] Clips;
            public readonly Queue<int> Rotation = new Queue<int>();
            public int LastLine = -1;

            public MonsterCallout(string clipPrefix, params string[] lines)
            {
                ClipPrefix = clipPrefix;
                Lines = lines;
                Clips = new AudioClip[lines.Length];
            }
        }

        readonly MonsterCallout[] monsterCallouts =
        {
            new MonsterCallout("spitter", "Whoa! What the hell is that thing spitting?", "Ugh. Another one spitting acid.", "That stuff is eating through the floor.", "Great. More acid. Just what I needed.", "I can smell that thing from here."),
            new MonsterCallout("screamer", "Whoa! What is that? My ears!", "My ears are still ringing.", "Christ, that went straight through my skull.", "Ugh, I hate that sound.", "Now it has brought all its friends. Perfect."),
            new MonsterCallout("armored_worker", "Whoa! What is that? It's covered in armor!", "Of course. This one has armor.", "That is a lot of metal for a dead guy.", "My bullets are barely scratching that thing.", "Big, ugly, and wrapped in steel. Lucky me.")
        };

        void ResetMonsterVoice()
        {
            nextMonsterVoice = monsterSubtitleUntil = 0;
            nextMonsterScan = 0;
            monsterSubtitle = null;
            calledOutMonsters.Clear();
            spottedScreamers.Clear();
            nextScreamerWarning = 0;
            foreach (var callout in monsterCallouts)
            {
                callout.Introduced = false;
                callout.NextWarning = 0;
                callout.RepeatAfter = 0;
                callout.Rotation.Clear();
                callout.LastLine = -1;
            }
        }

        void TickMonsterVoice()
        {
            if (dedicatedServer || !hasMeshyMonsterRoster || AudioListener.pause || coreLost || dead ||
                Time.time < nextMonsterScan ||
                Time.time < nextMonsterVoice || Time.time < nextPlayerDamageVoice ||
                openingReplyPending || Time.time < announcerUntil ||
                (playerDamageVoice != null && playerDamageVoice.isPlaying)) return;
            nextMonsterScan = Time.time + .25f;
            if (TryScreamerSpottedWarning()) return;

            Enemy spotted = null;
            MonsterCallout selected = null;
            float nearest = MonsterVoiceRange * MonsterVoiceRange;
            Vector3 eye = arenaCamera.transform.position;
            foreach (var enemy in enemies)
            {
                if (enemy.variant < 0 || enemy.variant >= zombieModels.Length ||
                    enemy.hp <= 0 || enemy.zombie == null || enemy.zombie.IsCollapsing) continue;
                int voiceIndex = zombieModels[enemy.variant].name switch
                {
                    "Spitter" => 0,
                    "Screamer" => 1,
                    "ArmoredWorker" => 2,
                    _ => -1
                };
                if (voiceIndex < 0) continue;
                if (voiceIndex == 0 && !enemy.zombie.name.StartsWith("Spitter", System.StringComparison.Ordinal)) continue;
                if (voiceIndex == 1 && Time.time < enemy.screamReactionAt) continue;
                var callout = monsterCallouts[voiceIndex];
                if (calledOutMonsters.Contains(enemy.netId) || Time.time < callout.RepeatAfter) continue;
                Vector3 target = enemy.body.position + Vector3.up * 1.15f;
                Vector3 offset = target - eye;
                float distanceSquared = offset.sqrMagnitude;
                if (distanceSquared >= nearest || distanceSquared < .001f) continue;

                Vector3 view = arenaCamera.WorldToViewportPoint(target);
                if (view.z <= 0 || view.x < .05f || view.x > .95f || view.y < .05f || view.y > .95f) continue;
                // A Spitter at the edge of the screen should not trigger a reaction.
                if (voiceIndex == 0 && (Mathf.Abs(view.x - .5f) > .12f || Mathf.Abs(view.y - .5f) > .18f)) continue;
                float distance = Mathf.Sqrt(distanceSquared);
                if (ShotStopDistance(eye, offset / distance) + .05f < distance) continue;

                spotted = enemy;
                selected = callout;
                nearest = distanceSquared;
            }
            if (spotted == null) return;

            bool firstEncounter = !selected.Introduced;
            int line = firstEncounter ? 0 : NextMonsterLine(selected);
            selected.Introduced = true;
            calledOutMonsters.Add(spotted.netId);
            selected.RepeatAfter = Time.time + MonsterVoiceRepeatDelay;
            string clipPath = "PlayerVoice/Encounters/" + selected.ClipPrefix;
            string suffix = line == 0 ? "_first" : line == 1 ? "_incoming" : line == 2 ? "_return" : "_alt" + line;
            var clip = selected.Clips[line] ?? (selected.Clips[line] = Resources.Load<AudioClip>(clipPath + suffix));
            if (line == 2 && clip == null)
            {
                line = 1;
                clip = Resources.Load<AudioClip>(clipPath + "_incoming");
            }
            monsterSubtitle = selected.Lines[line];
            monsterSubtitleUntil = Time.time + Mathf.Max(2.5f, clip != null ? clip.length + .25f : 0);
            nextMonsterVoice = monsterSubtitleUntil + 12f;
            if (clip == null || !soundEnabled || !audioUnlocked || playerDamageVoice == null) return;

            playerDamageVoiceTier = -4;
            playerDamageVoice.clip = clip;
            playerDamageVoice.volume = .7f * soundVolume;
            playerDamageVoice.Play();
            nextPlayerDamageVoice = Time.time + clip.length + .25f;
        }

        bool TryScreamerSpottedWarning()
        {
            if (Time.time < nextScreamerWarning) return false;
            Vector3 eye = arenaCamera.transform.position;
            foreach (var enemy in enemies)
            {
                // Use the actual actor, not a roster slot: another enemy can never
                // borrow the Screamer warning. The reaction remains separately gated by its scream.
                if (enemy.hp <= 0 || enemy.body == null || enemy.zombie == null || enemy.zombie.IsCollapsing ||
                    !enemy.zombie.name.StartsWith("Screamer", System.StringComparison.Ordinal) ||
                    spottedScreamers.Contains(enemy.netId) || !float.IsPositiveInfinity(enemy.screamReactionAt)) continue;
                Vector3 target = enemy.body.position + Vector3.up * 1.15f;
                Vector3 offset = target - eye;
                float distanceSquared = offset.sqrMagnitude;
                if (distanceSquared < .001f || distanceSquared > MonsterVoiceRange * MonsterVoiceRange) continue;
                Vector3 view = arenaCamera.WorldToViewportPoint(target);
                if (view.z <= 0 || view.x < .05f || view.x > .95f || view.y < .05f || view.y > .95f) continue;
                float distance = Mathf.Sqrt(distanceSquared);
                if (ShotStopDistance(eye, offset / distance) + .05f < distance) continue;
                if (screamerSpottedClip == null) screamerSpottedClip = Resources.Load<AudioClip>("PlayerVoice/Encounters/screamer_spotted");
                if (screamerSpottedClip == null) return false;
                spottedScreamers.Add(enemy.netId);
                nextScreamerWarning = Time.time + 60f;
                monsterSubtitle = "Screamer incoming.";
                monsterSubtitleUntil = Time.time + Mathf.Max(2.5f, screamerSpottedClip.length + .25f);
                // Do not apply the usual twelve-second chatter gap: the post-scream
                // reaction must be able to follow the actual scream promptly.
                nextMonsterVoice = monsterSubtitleUntil + .25f;
                if (soundEnabled && audioUnlocked && playerDamageVoice != null)
                {
                    playerDamageVoiceTier = -4;
                    playerDamageVoice.clip = screamerSpottedClip;
                    playerDamageVoice.volume = .7f * soundVolume;
                    playerDamageVoice.Play();
                    nextPlayerDamageVoice = Time.time + screamerSpottedClip.length + .25f;
                }
                return true;
            }
            return false;
        }

        int NextMonsterLine(MonsterCallout callout)
        {
            if (callout.Rotation.Count == 0)
            {
                var choices = new List<int>();
                for (int i = 1; i < callout.Lines.Length; i++) choices.Add(i);
                while (choices.Count > 0)
                {
                    int at = Random.Range(0, choices.Count);
                    if (callout.Rotation.Count == 0 && choices.Count > 1 && choices[at] == callout.LastLine)
                        at = (at + 1) % choices.Count;
                    callout.Rotation.Enqueue(choices[at]); choices.RemoveAt(at);
                }
            }
            return callout.LastLine = callout.Rotation.Dequeue();
        }

        void DrawMonsterSubtitle(float width, float height)
        {
            if (dead || showHelp || Time.time >= monsterSubtitleUntil || Time.time < subtitleUntil) return;
            float captionWidth = Mathf.Min(620, width - 40);
            var box = new Rect((width - captionWidth) / 2, building ? BuildChoiceCard(0, width, height).y - 48 : height - 180, captionWidth, 36);
            DrawBottomSubtitle(box, monsterSubtitle);
        }
    }
}
