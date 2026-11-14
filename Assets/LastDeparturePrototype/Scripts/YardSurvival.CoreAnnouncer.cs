using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        AudioSource coreAnnouncer;
        AudioLowPassFilter announcerMuffle;
        float announcerDrowsiness;
        readonly AudioClip[] announcerClips=new AudioClip[6];
        readonly AudioClip[,] announcerVariants = new AudioClip[6,3];
        readonly int[] announcerRotation = new int[6];
        bool announcerVariantsLoaded;
        static readonly string[,] AnnouncerAlternates = {
            { "The dead have reached the heart.", "The core is under siege." },
            { "The heart is one breath from silence.", "Its last pulse is fading." },
            { "The final heartbeat has passed.", "Darkness has claimed the heart." },
            { "More footsteps in the dark. Another wave begins.", "The dead are stirring again." },
            { "For a moment, the halls fall silent.", null },
            { "New strength awakens within you.", "The heart has granted another gift." }
        };
        static readonly string[] AnnouncerFiles={"core_attack","core_critical","core_destroyed","wave_start","wave_clear","upgrade_ready"};
        static readonly string[] AnnouncerLines={"They're attacking our core. Defend it!","The heart is failing. Drive them back!","The heart is silent. We have fallen.","They come for the heart. Hold your ground.","The wave is broken. Choose your strength.","Your strength grows. Prepare for their return."};
        int announcerPriority,queuedAnnouncement=-1;
        float announcerUntil,nextCoreWarning,subtitleUntil,queuedUntil;
        bool criticalAnnounced;
        readonly AudioClip[] openingReplies = new AudioClip[3];
        static readonly string[] OpeningReplyFiles = { "11_kick_some_ass", "12_my_turn", "13_send_them_back" };
        bool openingReplyPending, openingReplyScheduled;
        int lastOpeningReply = -1;
        string announcerSubtitle="";
        int AnnouncerPriority(int id)=>id==2?4:id==1?3:id==0?2:1;
        void ResetCoreAnnouncer()
        {
            if(coreAnnouncer!=null)coreAnnouncer.Stop();
            openingReplyPending = openingReplyScheduled = false;
            queuedAnnouncement=-1;announcerPriority=0;announcerUntil=nextCoreWarning=subtitleUntil=0;criticalAnnounced=false;announcerSubtitle="";
            SetAnnouncerDrowsiness(0);
        }
        void EnsureCoreAnnouncer()
        {
            if(coreAnnouncer==null)
            {
                var obj=new GameObject("Core announcer");obj.transform.SetParent(transform,false);
                coreAnnouncer=obj.AddComponent<AudioSource>();coreAnnouncer.playOnAwake=false;coreAnnouncer.spatialBlend=0;coreAnnouncer.priority=0;
                announcerMuffle=obj.AddComponent<AudioLowPassFilter>();announcerMuffle.enabled=false;
                SetAnnouncerDrowsiness(announcerDrowsiness);
            }
            for(int i=0;i<announcerClips.Length;i++)
                if(announcerClips[i]==null)announcerClips[i]=Resources.Load<AudioClip>("Announcer/"+AnnouncerFiles[i]);
            if (!announcerVariantsLoaded)
            {
                for (int i = 0; i < announcerClips.Length; i++)
                {
                    announcerVariants[i, 0] = announcerClips[i];
                    for (int v = 1; v < announcerVariants.GetLength(1); v++)
                        if (AnnouncerAlternates[i, v - 1] != null)
                            announcerVariants[i, v] = Resources.Load<AudioClip>("Announcer/" + AnnouncerFiles[i] + "_alt" + v);
                }
                announcerVariantsLoaded=true;
            }
        }
        void RequestAnnouncement(int id)
        {
            if(!HasCoreObjective||dedicatedServer||id<0||id>=AnnouncerFiles.Length)return;
            EnsureCoreAnnouncer();
            int priority=AnnouncerPriority(id);
            if(Time.time<announcerUntil && priority<=announcerPriority)
            {
                if(queuedAnnouncement<0||priority>=AnnouncerPriority(queuedAnnouncement)){queuedAnnouncement=id;queuedUntil=Time.time+6;}
                return;
            }
            if(priority>announcerPriority)queuedAnnouncement=-1;
            PlayAnnouncement(id);
        }
        void PlayAnnouncement(int id)
        {
            int variant = announcerRotation[id]++ % (AnnouncerAlternates[id, 1] == null ? 2 : 3);
            if(announcerVariants[id,variant]==null)variant=0;
            announcerClips[id]=announcerVariants[id,variant];
            if (id == 3 && wave == 1 && !openingReplyScheduled)
            { openingReplyPending = openingReplyScheduled = true; }
            if (AnnouncerPriority(id) > 1) openingReplyPending = false;
            coreAnnouncer.Stop();coreAnnouncer.clip=announcerClips[id];
            coreAnnouncer.volume=soundEnabled&&audioUnlocked?soundVolume*.9f:0;
            if(coreAnnouncer.clip!=null)coreAnnouncer.Play();
            announcerPriority=AnnouncerPriority(id);announcerSubtitle=variant==0?AnnouncerLines[id]:AnnouncerAlternates[id,variant-1];
            announcerUntil=Time.time+(coreAnnouncer.clip!=null?coreAnnouncer.clip.length:3)+.35f;subtitleUntil=announcerUntil;
        }
        // Shared sleep state drives the filter without restarting an active voice clip.
        public void SetAnnouncerDrowsiness(float amount)
        {
            announcerDrowsiness=Mathf.Clamp01(amount);
            if(announcerMuffle==null)return;
            announcerMuffle.enabled=announcerDrowsiness>.001f;
            announcerMuffle.cutoffFrequency=Mathf.Lerp(18000,900,1-Mathf.Pow(1-announcerDrowsiness,3));
            announcerMuffle.lowpassResonanceQ=1;
        }
        void AnnounceCoreDamage()
        {
            if(coreHealth<=0){RequestAnnouncement(2);return;}
            if(coreHealth<=maxCoreHealth*.25f&&!criticalAnnounced)
            {criticalAnnounced=true;nextCoreWarning=Time.time+20;RequestAnnouncement(1);return;}
            if(Time.time>=nextCoreWarning){nextCoreWarning=Time.time+30;RequestAnnouncement(0);}
        }
        void TickCoreAnnouncer()
        {
            if(coreAnnouncer!=null)coreAnnouncer.volume=soundEnabled&&audioUnlocked?soundVolume*.9f:0;
            if(coreHealth>maxCoreHealth*.4f)criticalAnnounced=false;
            if(queuedAnnouncement>=0&&Time.time>=announcerUntil)
            {
                int id=queuedAnnouncement;queuedAnnouncement=-1;
                if(Time.time<=queuedUntil&&!coreLost&&frontScreen==FrontScreen.Game)PlayAnnouncement(id);
            }
            TickOpeningReply();
        }
        void TickOpeningReply()
        {
            if (!openingReplyPending) return;
            if (AudioListener.pause) return;
            if (dead || health <= 50 || coreLost || frontScreen != FrontScreen.Game)
            { openingReplyPending = false; return; }
            if (Time.time < announcerUntil || (coreAnnouncer != null && coreAnnouncer.isPlaying)) return;
            openingReplyPending = false;
            if (dedicatedServer || !soundEnabled || !audioUnlocked || playerDamageVoice == null || playerDamageVoice.isPlaying) return;
            var available = new System.Collections.Generic.List<int>();
            for (int i = 0; i < openingReplies.Length; i++)
            {
                if (openingReplies[i] == null) openingReplies[i] = Resources.Load<AudioClip>("PlayerVoice/" + OpeningReplyFiles[i]);
                if (openingReplies[i] != null) available.Add(i);
            }
            if (available.Count == 0) return;
            if (available.Count > 1) available.Remove(lastOpeningReply);
            int selected = available[Random.Range(0, available.Count)];
            lastOpeningReply = selected;
            playerDamageVoiceTier = -1;
            playerDamageVoice.clip = openingReplies[selected];
            playerDamageVoice.volume = .7f * soundVolume;
            playerDamageVoice.Play();
            nextPlayerDamageVoice = Time.time + openingReplies[selected].length + .5f;
        }
        Texture2D subtitleShadow;

        void DrawBottomSubtitle(Rect rect, string caption)
        {
            if (Event.current.type == EventType.Repaint)
            {
                if (subtitleShadow == null)
                {
                    const int size = 64;
                    subtitleShadow = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    {
                        name = "Feathered subtitle backing",
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear
                    };
                    var pixels = new Color32[size * size];
                    for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float horizontal = Mathf.Min(x, size - 1 - x) / 12f;
                        float vertical = Mathf.Min(y, size - 1 - y) / 25f;
                        float alpha = Mathf.SmoothStep(0, 1, horizontal) * Mathf.SmoothStep(0, 1, vertical);
                        pixels[y * size + x] = new Color32(0, 0, 0, (byte)(alpha * 176));
                    }
                    subtitleShadow.SetPixels32(pixels);
                    subtitleShadow.Apply(false, true);
                }

                float textWidth = centerStyle.CalcSize(new GUIContent(caption)).x;
                float shadowWidth = Mathf.Min(rect.width + 32, textWidth + 92);
                GUI.DrawTexture(new Rect(rect.center.x - shadowWidth / 2, rect.y - 10, shadowWidth, rect.height + 20), subtitleShadow);
            }
            HudLabel(rect, caption, centerStyle, HudWhite);
        }

        void DrawCoreAnnouncer(float width,float height)
        {
            if(Time.time>=subtitleUntil||string.IsNullOrEmpty(announcerSubtitle))return;
            float w = Mathf.Min(620, width - 40);
            var subtitle = new Rect((width - w) / 2, building ? BuildChoiceCard(0, width, height).y - 48 : height - 180, w, 36);
            DrawBottomSubtitle(subtitle, announcerSubtitle);
        }
    }
}
