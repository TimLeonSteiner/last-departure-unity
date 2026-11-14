#if UNITY_EDITOR
using System;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunAnnouncerChecks()
        {
            var report=new System.Text.StringBuilder();
            void Check(bool ok,string message){if(!ok)throw new Exception(message);report.AppendLine("PASS "+message);}
            bool savedSound=soundEnabled;float savedVolume=soundVolume;
            try
            {
                PlayFromMenu();soundEnabled=true;soundVolume=.8f;TickCoreAnnouncer();
                for(int i=0;i<announcerClips.Length;i++)
                    Check(announcerClips[i]!=null&&announcerClips[i].length>.5f,"Loaded audio: "+AnnouncerFiles[i]);
                Check(wave==1&&coreAnnouncer.clip==announcerClips[3]&&coreAnnouncer.isPlaying,"Opening voice plays on starting the game");
                Check(openingReplyPending,"Player reply waits behind opening speech");
                coreAnnouncer.Stop();announcerUntil=Time.time-1;TickCoreAnnouncer();
                Check(!openingReplyPending&&playerDamageVoice.isPlaying,"Player answers after opening speech finishes");
                for(int i=0;i<openingReplies.Length;i++) Check(openingReplies[i]!=null&&openingReplies[i].length>.3f,"Loaded opening reply: "+OpeningReplyFiles[i]);
                int firstReply=lastOpeningReply;
                PlayFromMenu();coreAnnouncer.Stop();announcerUntil=Time.time-1;TickCoreAnnouncer();
                Check(lastOpeningReply!=firstReply,"Consecutive runs choose different opening replies");
                PlayFromMenu();
                DamageCore(8);
                Check(!openingReplyPending,"Urgent core warning cancels pending player reply");
                Check(coreAnnouncer.clip==announcerClips[0]&&announcerPriority==2,"Core damage interrupts the opening with the attack warning");
                float deadline=announcerUntil;DamageCore(8);
                Check(announcerUntil==deadline&&queuedAnnouncement<0,"Repeated damage does not spam the warning");
                DamageCore(coreHealth-100);
                Check(coreAnnouncer.clip==announcerClips[1]&&announcerPriority==3,"Critical damage interrupts the attack warning");
                PauseFromGame();Check(AudioListener.pause&&Time.timeScale==0,"Pause suspends audio and its announcement clock");
                ResumeFromMenu();Check(!AudioListener.pause&&Time.timeScale==1,"Resume restores audio");
                soundEnabled=false;TickCoreAnnouncer();Check(coreAnnouncer.volume==0,"Sound mute applies to announcer");
                soundEnabled=true;TickCoreAnnouncer();Check(Mathf.Abs(coreAnnouncer.volume-.72f)<.001f,"Announcer follows sound volume");
                SetAnnouncerDrowsiness(1);Check(announcerMuffle.enabled&&announcerMuffle.cutoffFrequency==900,"Drowsiness audio hook muffles speech");
                SetAnnouncerDrowsiness(0);Check(!announcerMuffle.enabled,"Wake-up hook restores clear speech");
                DamageCore(coreHealth);Check(coreAnnouncer.clip==announcerClips[2]&&announcerPriority==4,"Destruction takes highest priority");
                PlayFromMenu();ResetCoreAnnouncer();OfferCoreUpgrades();
                Check(coreAnnouncer.clip==announcerClips[4],"Wave clear plays its voice line");
                ChooseCoreUpgrade(0);Check(queuedAnnouncement==5,"Upgrade voice waits for the clear line");
                announcerUntil=Time.time-1;TickCoreAnnouncer();
                Check(coreAnnouncer.clip==announcerClips[5],"Queued upgrade voice plays");
                ResetCoreAnnouncer();Check(!coreAnnouncer.isPlaying&&queuedAnnouncement<0&&announcerSubtitle=="","Reset clears voice and subtitles");
                PlayFromMenu(); ResetCoreAnnouncer(); ResetPlayerDamageVoice();
                sleepState.Reset(.8f); nextTiredVoice = 0; TickTiredVoice();
                Check(playerDamageVoice.isPlaying && playerDamageVoiceTier == -2, "Drowsiness plays a tired line");
                foreach (var clip in tiredLines) Check(clip != null && clip.length > .3f, "Tired recording loads");
                int firstTired = lastTiredLine;
                playerDamageVoice.Stop(); TickTiredVoice();
                Check(!playerDamageVoice.isPlaying, "Tired lines respect their cooldown");
                nextTiredVoice = 0; TickTiredVoice();
                Check(lastTiredLine != firstTired, "Tired lines do not repeat consecutively");
                PlayPlayerDamageAudio();
                Check(playerDamageVoiceTier >= 0, "Damage interrupts a tired remark");
                ResetPlayerDamageVoice(); nextTiredVoice = 0; TickTiredVoice();
                sleepState.Wake(8); TickTiredVoice();
                Check(!playerDamageVoice.isPlaying, "Waking stops a tired remark");
                sleepState.Reset(.8f); nextTiredVoice = 0; soundEnabled = false; TickTiredVoice();
                Check(!playerDamageVoice.isPlaying, "Muted effects suppress tired remarks");
                soundEnabled = true; PauseFromGame(); TickTiredVoice();
                Check(!playerDamageVoice.isPlaying, "Pause suppresses tired remarks");
                ResumeFromMenu();
                ResetPlayerDamageVoice(); ResetCoreAnnouncer(); ResetRampageVoice(); health = maxPlayerHealth;
                sleepState.Reset(0);
                kills += 4; TickRampageVoice();
                Check(!playerDamageVoice.isPlaying, "Four kills do not trigger a rampage cheer");
                kills++; TickRampageVoice();
                Check(playerDamageVoice.isPlaying && playerDamageVoiceTier == -3, "Five quick kills trigger a rampage cheer");
                foreach (var clip in rampageLines) Check(clip != null && clip.length > .3f, "Rampage recording loads");
                int firstCheer = lastRampageLine;
                playerDamageVoice.Stop(); kills += 5; TickRampageVoice();
                Check(!playerDamageVoice.isPlaying, "Rampage cooldown prevents repeated cheers");
                nextRampageVoice = 0; kills += 5; TickRampageVoice();
                Check(lastRampageLine != firstCheer, "Rampage cheers do not repeat consecutively");
                PlayPlayerDamageAudio(); Check(playerDamageVoiceTier >= 0, "Damage interrupts a rampage cheer");
                ResetPlayerDamageVoice(); ResetRampageVoice(); health = 25; kills += 5; TickRampageVoice();
                Check(!playerDamageVoice.isPlaying, "Critical health suppresses celebrations");
                health = maxPlayerHealth; ResetRampageVoice();
                rampageKills.Enqueue(Time.time - 11); kills += 4; TickRampageVoice();
                Check(!playerDamageVoice.isPlaying, "Old kills expire from the streak window");
                ResetRampageVoice(); sleepState.Reset(.4f); kills += 5; TickRampageVoice();
                Check(!playerDamageVoice.isPlaying && rampagePendingUntil == 0, "Tired player never cheers for a streak");
                sleepState.Reset(0); TickRampageVoice();
                Check(!playerDamageVoice.isPlaying, "Waking does not replay a sleepy streak");
                kills += 5; TickRampageVoice();
                Check(playerDamageVoice.isPlaying, "A fresh awake streak can trigger a cheer");
                sleepState.Reset(.4f); TickRampageVoice();
                Check(!playerDamageVoice.isPlaying, "Becoming tired stops an active cheer");
                return report.ToString();
            }
            finally{soundEnabled=savedSound;soundVolume=savedVolume;ShowHome();}
        }
    }
}
#endif
