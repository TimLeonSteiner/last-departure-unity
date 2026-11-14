using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        AudioSource launchSting;
        AudioSource launchTransition;
        AudioSource launchMusic;
        float launchMusicStartedAt;
        Texture2D launchBackground;
        Texture2D launchFloorShade;
        VideoPlayer launchVideo;
        VideoPlayer readyVideo;
        bool readyVideoEnded, readyVideoFailed;
        bool launchWhooshPlayed;
        float readyBlendStartedAt = -1;
        const float ReadyImpactGain = .72f;
        float launchStartedAt = -1;
        float launchExitAt = -1;
        GUIStyle launchTitle, launchPrefix;
        [System.NonSerialized] GUIStyle launchPromptFallback;
        [System.NonSerialized] Texture2D launchPromptAtlas;
        const float LaunchDuration = 6f;
        bool launchRenderingSuspended, launchCameraWasEnabled;
        Camera launchPresentationCamera;

        void SuspendLaunchRendering()
        {
            if (launchRenderingSuspended) return;
            launchRenderingSuspended = true;
            launchCameraWasEnabled = arenaCamera != null && arenaCamera.enabled;
            // URP needs an active display camera to present IMGUI, including the video.
            // Keep the expensive world camera suspended but still submit screen frames.
            if (launchPresentationCamera == null)
            {
                var presentation = new GameObject("Launch presentation camera");
                presentation.transform.SetParent(transform, false);
                launchPresentationCamera = presentation.AddComponent<Camera>();
                launchPresentationCamera.cullingMask = 0;
                launchPresentationCamera.clearFlags = CameraClearFlags.SolidColor;
                launchPresentationCamera.backgroundColor = Color.black;
                launchPresentationCamera.allowHDR = false;
                launchPresentationCamera.allowMSAA = false;
                launchPresentationCamera.useOcclusionCulling = false;
                if (arenaCamera != null) launchPresentationCamera.targetDisplay = arenaCamera.targetDisplay;
            }
            launchPresentationCamera.enabled = true;
            if (arenaCamera != null) arenaCamera.enabled = false;
            foreach (var particles in rainfall) particles.Pause();
        }

        void RestoreLaunchRendering()
        {
            if (!launchRenderingSuspended) return;
            launchRenderingSuspended = false;
            if (launchPresentationCamera != null) launchPresentationCamera.enabled = false;
            if (arenaCamera != null) arenaCamera.enabled = launchCameraWasEnabled;
            foreach (var particles in rainfall) particles.Play();
        }

        void ShowLaunchScreen()
        {
            if (dedicatedServer) return;
            frontScreen = FrontScreen.Launch;
            launchStartedAt = -1;
            launchExitAt = -1;
            readyVideoEnded = readyVideoFailed = false;
            launchWhooshPlayed = false;
            readyBlendStartedAt = -1;
            launchBackground = Resources.Load<Texture2D>("Launch/CoreIdlePoster");
            // Read the saved setting here: the launch screen precedes graphics initialization.
            int launchPreset=PlayerPrefs.GetInt(GraphicsKey, Application.platform == RuntimePlatform.WebGLPlayer ? 0 : 2);
            // Desktop cinematics are independent of gameplay quality. The world
            // camera is suspended here, so Low/Fast still get the startup videos.
            bool animate = Application.platform != RuntimePlatform.WebGLPlayer || launchPreset==1 || launchPreset==2;
            SuspendLaunchRendering();
            var idleClip = animate ? Resources.Load<VideoClip>("Launch/CoreIdle") : null;
            if (idleClip != null)
            {
                if (launchVideo == null)
                {
                    var videoObject = new GameObject("Launch background video");
                    videoObject.transform.SetParent(transform, false);
                    launchVideo = videoObject.AddComponent<VideoPlayer>();
                    launchVideo.errorReceived += OnLaunchVideoError;
                }
                launchVideo.playOnAwake = false;
                launchVideo.source = VideoSource.VideoClip;
                launchVideo.clip = idleClip;
                launchVideo.renderMode = VideoRenderMode.APIOnly;
                launchVideo.audioOutputMode = VideoAudioOutputMode.None;
                launchVideo.isLooping = true;
                launchVideo.waitForFirstFrame = true;
                launchVideo.skipOnDrop = true;
                launchVideo.Play();
            }
            if (radio != null) radio.Stop();
            var readyClip = animate ? Resources.Load<VideoClip>("Launch/CoreReady") : null;
            if (readyClip != null)
            {
                var readyObject = new GameObject("Ready transition video");
                readyObject.transform.SetParent(transform, false);
                readyVideo = readyObject.AddComponent<VideoPlayer>();
                readyVideo.playOnAwake = false;
                readyVideo.source = VideoSource.VideoClip;
                readyVideo.clip = readyClip;
                // Keep the complete authored fade while reaching the menu one second sooner.
                readyVideo.playbackSpeed = (float)readyClip.length / Mathf.Max(.5f, (float)readyClip.length - 1f);
                readyVideo.renderMode = VideoRenderMode.APIOnly;
                readyVideo.audioOutputMode = VideoAudioOutputMode.None;
                readyVideo.isLooping = false;
                readyVideo.waitForFirstFrame = true;
                readyVideo.skipOnDrop = true;
                readyVideo.errorReceived += OnLaunchVideoError;
                readyVideo.loopPointReached += OnReadyVideoEnded;
                // Prepare on the Ready interaction, avoiding two decoders at startup.
            }
            if (musicB != null) musicB.Stop();
            if (titleVoice != null) titleVoice.Stop();
            if (stormAudio != null) stormAudio.Stop();
            var obj = new GameObject("Launch cinematic sting");
            obj.transform.SetParent(transform, false);
            launchSting = obj.AddComponent<AudioSource>();
            launchSting.playOnAwake = false;
            launchSting.spatialBlend = 0;
            launchSting.clip = Resources.Load<AudioClip>("Announcer/ready_impact");
            if (launchSting.clip != null) launchSting.clip.LoadAudioData();
            launchTransition = obj.AddComponent<AudioSource>();
            launchTransition.playOnAwake = false;
            launchTransition.spatialBlend = 0;
            launchTransition.clip = Resources.Load<AudioClip>("Announcer/launch_transition");
            if (launchTransition.clip != null) launchTransition.clip.LoadAudioData();
            launchMusic = obj.AddComponent<AudioSource>();
            launchMusic.playOnAwake = false;
            launchMusic.spatialBlend = 0;
            launchMusic.loop = true;
            launchMusic.clip = Resources.Load<AudioClip>("Launch/TitleMusic");
            if (launchMusic.clip == null) launchMusic.clip = openingMusic;
            if (launchMusic.clip != null) launchMusic.clip.LoadAudioData();
            launchMusic.volume = 0;
            launchMusicStartedAt = Time.unscaledTime;
            // Desktop title music starts with the video, before the Ready interaction.
            if (musicEnabled && launchMusic.clip != null) launchMusic.Play();
        }

        void OnLaunchVideoError(VideoPlayer source, string message)
        {
            if (source == readyVideo) readyVideoFailed = true;
            Debug.LogWarning("Launch video unavailable; using poster. " + message);
            source.Stop();
        }

        void OnReadyVideoEnded(VideoPlayer source) { readyVideoEnded = true; }

        void TickLaunchScreen()
        {
            if (launchMusic != null)
            {
                float fadeIn = Mathf.SmoothStep(0, 1, (Time.unscaledTime-launchMusicStartedAt)/1.5f);
                float fadeOut = launchStartedAt < 0 ? 1 : 1-Mathf.Clamp01((Time.unscaledTime-launchStartedAt)/.3f);
                launchMusic.volume = musicEnabled ? .18f*musicVolume*fadeIn*fadeOut : 0;
                if (fadeOut <= 0 && launchMusic.isPlaying) launchMusic.Stop();
            }
            bool pressed = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
            if (launchStartedAt < 0)
            {
                if (!pressed) return;
                UnlockAudio();
                launchStartedAt = Time.unscaledTime;
                if (launchVideo != null && launchVideo.isPlaying) launchVideo.Pause();
                if (readyVideo != null && !readyVideoFailed) readyVideo.Play();
                if (launchSting != null && soundEnabled)
                {
                    launchSting.volume = ReadyImpactGain * soundVolume;
                    launchSting.Play();
                }
                PlayTitleVoiceOnce();
                return;
            }
            if (readyBlendStartedAt < 0 && readyVideo != null && !readyVideoFailed && readyVideo.isPrepared && readyVideo.frame >= 0 && readyVideo.texture != null)
                readyBlendStartedAt = Time.unscaledTime;
            float elapsed = Time.unscaledTime - launchStartedAt;
            bool hasReadyVideo = readyVideo != null && !readyVideoFailed;
            float duration = hasReadyVideo ? (float)readyVideo.clip.length : LaunchDuration;
            float playback = hasReadyVideo ? (float)readyVideo.time : elapsed;
            // The whoosh belongs to the video's fade, before the menu is visible.
            if (!launchWhooshPlayed && playback >= duration-.8f)
            {
                launchWhooshPlayed = true;
                if (launchTransition != null && soundEnabled)
                {
                    launchTransition.volume = .65f * soundVolume;
                    launchTransition.Play();
                }
            }
            // Let the authored fade finish; retain a timeout if the decoder stalls.
            bool readyFinished = hasReadyVideo
                ? readyVideoEnded || elapsed >= (float)readyVideo.clip.length / readyVideo.playbackSpeed + 3f
                : elapsed >= LaunchDuration - 1.75f;
            if (launchExitAt < 0 && (readyFinished || (elapsed > .5f && pressed)))
            {
                launchExitAt = Time.unscaledTime;
                RestoreLaunchRendering();
                StopLaunchSounds();
            }
            if (launchExitAt >= 0)
            {
                float fade = Mathf.Clamp01((Time.unscaledTime-launchExitAt)/.75f);
                if (fade < 1) return;
                if (titleVoice != null) titleVoice.Stop();
                if (launchSting != null) launchSting.Stop();
                if (launchVideo != null) launchVideo.Stop();
                if (readyVideo != null) readyVideo.Stop();
                if (launchMusic != null) launchMusic.Stop();
                // Returning home, restarting, and joining a lobby never enter Launch again.
                frontScreen = FrontScreen.Home;
                ResumeAdaptiveAudio();
                UpdateWeather();
            }
        }

        void StopLaunchSounds()
        {
            RestoreLaunchRendering();
            if (launchVideo != null) launchVideo.Stop();
            if (readyVideo != null) readyVideo.Stop();
            if (launchSting != null) launchSting.Stop();
            if (launchTransition != null) launchTransition.Stop();
            if (launchMusic != null) launchMusic.Stop();
            if (titleVoice != null) titleVoice.Stop();
        }

        void DrawLaunchScreen(float width, float height)
        {
            var previous = GUI.matrix;
            float exit = launchExitAt < 0 ? 0 : Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-launchExitAt)/.75f));
            float elapsed = launchStartedAt < 0 ? 0 : Time.unscaledTime-launchStartedAt;
            float hit = launchStartedAt < 0 ? 0 : Mathf.Exp(-elapsed*12);
            if (launchExitAt >= 0)
            {
                bool enabled = GUI.enabled;
                GUI.enabled = false;
                DrawHomeScreen(width,height);
                GUI.enabled = enabled;
                // The ready clip already faded to black. Reveal only the menu;
                // drawing the idle video/poster here would flash the title again.
                Fill(new Rect(0, 0, width, height), new Color(0, 0, 0, 1-exit));
                return;
            }
            Fill(new Rect(0, 0, width, height), new Color(.008f, .009f, .011f,1-exit));
            Texture artwork = launchVideo != null && launchVideo.isPrepared && launchVideo.frame >= 0 && launchVideo.texture != null
                ? launchVideo.texture : launchBackground;
            if (artwork != null)
            {
                Color previousColor = GUI.color;
                // Cover the viewport without stretching or pillarboxing.
                float backgroundZoom = 1;
                float drift = 0;
                GUI.color = new Color(1, 1, 1, 1-exit);
                GUI.DrawTexture(new Rect((width-width*backgroundZoom)*.5f+drift,
                    (height-height*backgroundZoom)*.5f, width*backgroundZoom, height*backgroundZoom),
                    artwork, ScaleMode.ScaleAndCrop);
                if (readyBlendStartedAt >= 0 && readyVideo != null && !readyVideoFailed && readyVideo.texture != null)
                {
                    float blend = Mathf.SmoothStep(0, 1, (Time.unscaledTime-readyBlendStartedAt)/.22f);
                    GUI.color = new Color(1, 1, 1, blend*(1-exit));
                    GUI.DrawTexture(new Rect(0,0,width,height), readyVideo.texture, ScaleMode.ScaleAndCrop);
                }
                GUI.color = previousColor;
            }
            Fill(new Rect(0, 0, width, height), new Color(.008f, .009f, .011f,
                (artwork != null ? .025f : .94f)*(1-exit)));
            // Shade in viewport coordinates, before the fitted prompt canvas.
            if (launchStartedAt < 0 && Event.current.type == EventType.Repaint)
                DrawLaunchFloorShade(width, height);
            float scale = Mathf.Min(width / 1672f, height / 941f);
            GUI.matrix = previous * Matrix4x4.TRS(new Vector3((width-1672*scale)*.5f, (height-941*scale)*.5f, 0), Quaternion.identity, Vector3.one*scale);
            try
            {
                if (launchTitle == null)
                {
                    var face = Resources.Load<Font>("PauseMenu/InterDisplayBlack");
                    launchTitle = new GUIStyle { font=face, fontSize=152, alignment=TextAnchor.MiddleCenter };
                    launchPrefix = new GUIStyle(launchTitle) { fontSize=48 };
                }
                if (launchPromptFallback == null)
                {
                    launchPromptAtlas = Resources.Load<Texture2D>("Launch/StartPrompt");
                    launchPromptFallback = new GUIStyle(GUI.skin.label)
                    {
                        font = Resources.Load<Font>("Launch/BarlowCondensed-SemiBold") ?? launchTitle.font,
                        fontSize = 39,
                        alignment = TextAnchor.MiddleCenter
                    };
                }
                Color paper = new Color(.83f,.82f,.78f);
                if (artwork == null)
                {
                float zoom = 1;
                // A restrained blood-red glow gives the title depth without extra copy.
                for(int i=24;i>0;i--)
                    Fill(new Rect(836-i*26,474-i*4,i*52,i*8),new Color(.29f,.015f,.009f,.009f*(1-exit)));
                Matrix4x4 canvas = GUI.matrix;
                GUI.matrix = canvas * Matrix4x4.TRS(new Vector3(836*(1-zoom),440*(1-zoom),0),Quaternion.identity,Vector3.one*zoom);
                paper.a = 1-exit;
                HudLabel(new Rect(75,293-exit*40,1522,80),"L A S T",launchPrefix,new Color(.60f,.59f,.56f,1-exit));
                HudLabel(new Rect(75,367+exit*35,1522,185),"DEPARTURE",launchTitle,paper);
                // Brief displaced red impression on the impact, with no continuous flicker.
                if(hit>.02f)
                    HudLabel(new Rect(75+hit*9,367,1522,185),"DEPARTURE",launchTitle,new Color(.65f,.035f,.02f,hit*.22f*(1-exit)));
                Fill(new Rect(636,565,400,2),new Color(.45f,.045f,.025f,(1-exit)*.7f));
                GUI.matrix=canvas;
                }
                if (launchStartedAt < 0 && Event.current.type == EventType.Repaint)
                {
                    float time = Time.unscaledTime;
                    float pulse = .5f + .5f * Mathf.Sin(time * 2f);
                    var prompt = new Rect(612f, 809f, 448f, 112f);
                    // Sample in short steps so the jitter reads as a slight signal twitch.
                    float step = Mathf.Floor(time * 18f) / 18f;
                    float burst = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.52f, .73f, Mathf.PerlinNoise(time * 1.6f, 4f)));
                    prompt.position += new Vector2(
                        Mathf.Sin(step * 53f) * Mathf.Lerp(.35f, 2.1f, burst),
                        Mathf.Sin(step * 37f) * Mathf.Lerp(.2f, .9f, burst));
                    DrawGlowingLaunchPrompt(prompt, pulse);
                }
                else if(launchStartedAt >= 0 && elapsed<.65f && (readyVideo == null || readyVideoFailed))
                {
                    float sweep=Mathf.Clamp01(elapsed/.65f);
                    Fill(new Rect(260+sweep*1152,350,2,220),new Color(.75f,.12f,.06f,(1-sweep)*.5f));
                }
            }
            finally { GUI.matrix = previous; }
        }

        void DrawLaunchFloorShade(float width, float height)
        {
            if (launchFloorShade == null)
            {
                launchFloorShade = new Texture2D(1, 128, TextureFormat.RGBA32, false)
                {
                    name = "Title floor gradient",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                for (int y = 0; y < launchFloorShade.height; y++)
                {
                    float depth = 1f - y / (float)(launchFloorShade.height - 1);
                    launchFloorShade.SetPixel(0, y, new Color(.006f, .006f, .008f, .86f * Mathf.SmoothStep(0f, 1f, depth)));
                }
                launchFloorShade.Apply(false, true);
            }
            float shadeHeight = height * (269f / 941f);
            GUI.DrawTexture(new Rect(0, height - shadeHeight, width, shadeHeight), launchFloorShade, ScaleMode.StretchToFill, true);
        }

        void DrawGlowingLaunchPrompt(Rect prompt, float pulse)
        {
            var lettering = Color.Lerp(new Color(1f, .79f, .65f), new Color(1f, .91f, .78f), pulse);
            if (launchPromptAtlas == null)
            {
                HudLabel(prompt, "PRESS ANY KEY", launchPromptFallback, lettering);
                return;
            }

            // The atlas stacks the lettering, Gaussian halo and drop shadow from top to bottom.
            const float row = 1f / 3f;
            Color previous = GUI.color;
            GUI.color = new Color(.035f, .007f, .005f, .92f);
            GUI.DrawTextureWithTexCoords(prompt, launchPromptAtlas, new Rect(0, 0, 1, row));
            GUI.color = new Color(.78f, .065f, .028f, Mathf.Lerp(.56f, .81f, pulse));
            GUI.DrawTextureWithTexCoords(prompt, launchPromptAtlas, new Rect(0, row, 1, row));
            GUI.color = lettering;
            GUI.DrawTextureWithTexCoords(prompt, launchPromptAtlas, new Rect(0, 2f * row, 1, row));
            GUI.color = previous;
        }
    }
}
