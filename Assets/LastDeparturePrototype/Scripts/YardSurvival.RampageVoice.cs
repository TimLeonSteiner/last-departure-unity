using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        readonly Queue<float> rampageKills = new Queue<float>();
        readonly AudioClip[] rampageLines = new AudioClip[3];
        int rampageObservedKills, lastRampageLine = -1;
        float nextRampageVoice, rampagePendingUntil;

        void ResetRampageVoice()
        {
            rampageKills.Clear(); rampageObservedKills = kills;
            nextRampageVoice = rampagePendingUntil = 0; lastRampageLine = -1;
        }

        void TickRampageVoice()
        {
            if (dedicatedServer || dead || frontScreen != FrontScreen.Game || showHelp || upgradePending || AudioListener.pause) return;
            if (NightmareMode && sleepState.Amount >= .4f)
            {
                // Discard sleepy streaks rather than cheering about them after waking.
                rampageKills.Clear(); rampageObservedKills = kills; rampagePendingUntil = 0;
                if (playerDamageVoice != null && playerDamageVoiceTier == -3)
                { playerDamageVoice.Stop(); playerDamageVoiceTier = -1; }
                return;
            }
            if (kills < rampageObservedKills) ResetRampageVoice();
            // The shared kill total also allows celebrations during a co-op team streak.
            while (rampageKills.Count > 0 && Time.time - rampageKills.Peek() > 10) rampageKills.Dequeue();
            int gained = kills - rampageObservedKills; rampageObservedKills = kills;
            for (int i = 0; i < Mathf.Min(gained, 5); i++) rampageKills.Enqueue(Time.time);
            while (rampageKills.Count > 5) rampageKills.Dequeue();
            if (gained > 0 && rampageKills.Count >= 5 && Time.time >= nextRampageVoice)
            { rampagePendingUntil = Time.time + 2; rampageKills.Clear(); }
            if (rampagePendingUntil <= Time.time || Time.time < nextRampageVoice) return;
            if (health <= 50 || !soundEnabled || !audioUnlocked)
            { rampagePendingUntil = 0; return; }
            if (playerDamageVoice == null || playerDamageVoice.isPlaying || Time.time < nextPlayerDamageVoice ||
                openingReplyPending || Time.time < announcerUntil || (coreAnnouncer != null && coreAnnouncer.isPlaying)) return;
            string[] names = { "18_woohoo_hell_yeah", "19_back_to_hell", "20_talking_about" };
            var available = new List<int>();
            for (int i = 0; i < rampageLines.Length; i++)
            {
                if (rampageLines[i] == null) rampageLines[i] = Resources.Load<AudioClip>("PlayerVoice/" + names[i]);
                if (rampageLines[i] != null) available.Add(i);
            }
            rampagePendingUntil = 0;
            if (available.Count == 0) return;
            if (available.Count > 1) available.Remove(lastRampageLine);
            lastRampageLine = available[UnityEngine.Random.Range(0, available.Count)];
            playerDamageVoiceTier = -3;
            playerDamageVoice.clip = rampageLines[lastRampageLine];
            playerDamageVoice.volume = .7f * soundVolume; playerDamageVoice.Play();
            nextRampageVoice = Time.time + 45;
            nextTiredVoice = Mathf.Max(nextTiredVoice, Time.time + 8);
        }
    }
}
