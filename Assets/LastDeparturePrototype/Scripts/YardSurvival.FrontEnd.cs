using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        enum FrontScreen { Game, Home, Pause, Options, Lobby, Launch, Encounters, Difficulty }
        FrontScreen frontScreen=FrontScreen.Launch, optionsReturn=FrontScreen.Home;
        float mouseSensitivity=1, deathTime;
        float baseFov=75;
        Camera viewmodelCamera;
        void ConfigureViewmodelCamera()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            baseFov=Mathf.Clamp(PlayerPrefs.GetFloat("Dustline.FOV",75),60,100);
            arenaCamera.fieldOfView=baseFov;arenaCamera.cullingMask &= ~(1<<30);
            if(viewmodelCamera==null){var child=arenaCamera.transform.Find("Viewmodel camera");viewmodelCamera=child!=null?child.GetComponent<Camera>():new GameObject("Viewmodel camera").AddComponent<Camera>();}
            viewmodelCamera.transform.SetParent(arenaCamera.transform,false);viewmodelCamera.transform.localPosition=Vector3.zero;viewmodelCamera.transform.localRotation=Quaternion.identity;
            viewmodelCamera.fieldOfView=70;viewmodelCamera.nearClipPlane=.035f;viewmodelCamera.farClipPlane=5;viewmodelCamera.cullingMask=1<<30;
            var data=viewmodelCamera.GetUniversalAdditionalCameraData();data.renderType=CameraRenderType.Overlay;data.renderPostProcessing=false;
            var stack=arenaCamera.GetUniversalAdditionalCameraData().cameraStack;if(stack!=null&&!stack.Contains(viewmodelCamera))stack.Add(viewmodelCamera);
        }
        Vector3 deathCameraStart;
        Quaternion deathRotationStart;
        Volume deathVolume;
        ColorAdjustments deathColor;
        GUIStyle menuTitle, menuSmall, menuButton;
        GUIStyle pauseBrand, pauseHeading, pauseCaption, pauseAction, pauseStats;
        int pauseSelection;
        bool pauseKeyboardNavigation;
        static readonly Color PauseInk = new Color(.91f, .90f, .86f);
        static readonly Color PauseMuted = new Color(.58f, .60f, .61f);
        void InitializeFrontEnd()
        {
            selectedDifficulty=(RunDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyPreference,0),0,2);
            selectedPlayerSkin=Mathf.Clamp(PlayerPrefs.GetInt("Dustline.PlayerSkin",0),0,Mathf.Max(0,(playerSkinModels?.Length??1)-1));
            mouseSensitivity=PlayerPrefs.GetFloat("Dustline.MouseSensitivity",1);
            var obj=new GameObject("Death grading");obj.transform.SetParent(transform,false);
            deathVolume=obj.AddComponent<Volume>();deathVolume.isGlobal=true;deathVolume.priority=100;deathVolume.weight=0;
            deathVolume.profile=ScriptableObject.CreateInstance<VolumeProfile>();deathColor=deathVolume.profile.Add<ColorAdjustments>();deathColor.saturation.Override(-95);deathColor.postExposure.Override(-.45f);
            arenaCamera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            arenaCamera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            ShowHome();
            ShowLaunchScreen();
        }
        void RestoreFrontEndEffects()
        {
            RestoreHologram();
            deathTime=0;if(deathVolume!=null)deathVolume.weight=0;
            AudioListener.volume=1;AudioListener.pause=false;Time.timeScale=1;
            if(arenaCamera!=null)arenaCamera.fieldOfView=baseFov;
            if(viewmodelCamera!=null)viewmodelCamera.enabled=true;
        }
        void ShowHome()
        {
            StopLaunchSounds();
            audioCreditsVisible=false;
            StopCoop();RestoreFrontEndEffects();ResetRun();frontScreen=FrontScreen.Home;showHelp=false;bindingAction=-1;ReleaseMouse();
            if(gun!=null)gun.gameObject.SetActive(false);
            if(arenaCamera!=null)
            {
                arenaCamera.transform.position=new Vector3(1.0f,3.1f,-7.2f);
                arenaCamera.transform.rotation=Quaternion.LookRotation(new Vector3(-1.9f,3.7f,0)-arenaCamera.transform.position);
                arenaCamera.fieldOfView=55;
            }
            StopWarehouseAudio();PauseMovementAudio(true);
        }
        void PlayFromMenu()
        {
            UnlockAudio();RestoreFrontEndEffects();ResetRun();
#if UNITY_EDITOR
            // Local, one-use preview; never included in a player build.
            const string chargerPreview = "Library/ChargerPreviewOnce.request";
            if (System.IO.File.Exists(chargerPreview))
            {
                wave = 16; // StartWave advances to 17.
                supplies = 100;
                skillPoints = skillRanks.Length * SkillMaxRank;
                editorPreviewRun = true;
                ResetWeaponInventory();
                EquipWeapon(0, int.MaxValue, int.MaxValue);
                EquipWeapon(1, int.MaxValue, int.MaxValue);
                System.IO.File.Delete(chargerPreview);
                Debug.Log("Charger preview: wave 17, MG + minigun, 100 scrap, 27 skill points. One-use preset consumed.");
            }
#endif
            frontScreen=FrontScreen.Game;CaptureMouse();StartWave();
        }
        void PauseFromGame()
        {
            pauseSelection=0;pauseKeyboardNavigation=false;
            if(viewmodelCamera!=null)viewmodelCamera.enabled=false;
            frontScreen=FrontScreen.Pause;optionsReturn=FrontScreen.Pause;showHelp=false;aiming=false;ReleaseMouse();PauseMovementAudio(true);AudioListener.pause=true;Time.timeScale=0;
        }
        void ResumeFromMenu()
        {
            if(viewmodelCamera!=null)viewmodelCamera.enabled=true;
            frontScreen=FrontScreen.Game;showHelp=false;bindingAction=-1;AudioListener.pause=false;Time.timeScale=1;CaptureMouse();
        }
        void ToggleBuilding()
        {
            OpenHologram(1);
        }
        void ShowOptions(FrontScreen back)
        {
            optionsReturn=back;frontScreen=FrontScreen.Options;ReleaseMouse();
        }
        bool FrontEndUpdate()
        {
            TickMenuPresentation();
            TickEncounterDiscovery();
            TickArchiveNavigation();
            if (frontScreen == FrontScreen.Launch) { TickLaunchScreen(); return true; }
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame&&bindingAction<0)
            {
                if (frontScreen == FrontScreen.Game && HologramBlocking) { CloseHologram(); TickHologram(); return true; }
                if(audioCreditsVisible)audioCreditsVisible=false;
                else if(frontScreen==FrontScreen.Home)audioCreditsVisible=false;
                else if(frontScreen==FrontScreen.Difficulty){CloseDifficultyScreen();}
                else if(frontScreen==FrontScreen.Encounters){CloseEncounters();}
                else if(frontScreen==FrontScreen.Options){SaveAudioSettings();frontScreen=optionsReturn;}
                else if(frontScreen==FrontScreen.Pause)ResumeFromMenu();
                else if(frontScreen==FrontScreen.Game&&!dead){if(building){building=false;UpdateDefensePreview();CaptureMouse();return true;}PauseFromGame();}
            }
            else if(frontScreen==FrontScreen.Pause&&!audioCreditsVisible&&Keyboard.current!=null)
            {
                var keyboard=Keyboard.current;
                if(keyboard.downArrowKey.wasPressedThisFrame||keyboard.upArrowKey.wasPressedThisFrame)
                {
                    pauseKeyboardNavigation=true;
                    PlayMenuFeedback(false);
                    pauseSelection=(pauseSelection+(keyboard.downArrowKey.wasPressedThisFrame?1:2))%3;
                }
                if(keyboard.enterKey.wasPressedThisFrame||keyboard.numpadEnterKey.wasPressedThisFrame)
                    ActivatePauseAction(pauseSelection);
            }
            if((frontScreen==FrontScreen.Home||frontScreen==FrontScreen.Difficulty)&&arenaCamera!=null)arenaCamera.fieldOfView=55;
            if(frontScreen!=FrontScreen.Game){ReleaseMouse();UpdateAdaptiveAudio(Mathf.Min(Time.unscaledDeltaTime,.05f));return true;}
            if(dead){TickDeath(Mathf.Min(Time.unscaledDeltaTime,.05f));return true;}
            return false;
        }
        void BeginDeathSequence()
        {
            ResetPlayerDamageVoice();
            if (dedicatedServer) { dead = true; return; }
            dead=true;deathTime=0;deathCameraStart=arenaCamera.transform.position;deathRotationStart=arenaCamera.transform.rotation;
            ResetMovementAudio();ghost.gameObject.SetActive(false);gun.gameObject.SetActive(false);building=false;aiming=false;ReleaseMouse();StopWarehouseAudio();PlayDeathSting();
        }
        void TickDeath(float dt)
        {
            deathTime+=dt;
            float drop=Mathf.SmoothStep(0,1,deathTime/.8f);
            Vector3 floor=new Vector3(deathCameraStart.x,.22f,deathCameraStart.z);
            arenaCamera.transform.position=Vector3.Lerp(deathCameraStart,floor,drop);
            arenaCamera.transform.rotation=Quaternion.Slerp(deathRotationStart,Quaternion.Euler(-68,yaw,24),drop);
            deathVolume.weight=Mathf.Clamp01(deathTime/.7f);AudioListener.volume=Mathf.Lerp(1,.28f,Mathf.Clamp01(deathTime/.7f));
            if(deathTime>3.5f||HasCoopPartner||IsCoopGuest)return;
            for(int i=0;i<enemies.Count;i++)
            {
                var e=enemies[i];Vector3 focus=new Vector3(player.position.x,0,player.position.z);
                Vector3 delta=focus-e.position;
                bool near=delta.magnitude<1.45f;
                if(!near){Vector3 movement=delta.normalized*dt*1.5f;Vector3 next=Move(e.position,movement,EnemyRadius,false);foreach(var other in enemies)if(other!=e&&Vector3.Distance(next,other.position)<.8f){next=e.position;break;}float travelled=Vector3.Distance(e.position,next);e.position=next;e.body.position=OnGround(next);e.zombie?.TickMovement(dt,travelled,false);}
                else {e.zombie?.TickMovement(dt,0,true);e.body.rotation=Quaternion.LookRotation(delta.normalized)*Quaternion.Euler(18,0,0);}
                if(!near)TurnZombie(e.body,delta,dt);
            }
        }
        bool MenuButton(Rect rect,string label,bool accent=false)
        {
            Fill(rect,accent?new Color(.62f,.08f,.10f,.95f):new Color(.085f,.10f,.11f,.94f));
            Fill(new Rect(rect.x,rect.y,3,rect.height),accent?new Color(.95f,.24f,.19f):new Color(.48f,.53f,.49f));
            MenuHighlight(rect,"button:"+label);
            menuButton.normal.textColor=new Color(.93f,.92f,.85f);return FeedbackButton(rect,label,menuButton);
        }
        void ActivatePauseAction(int index)
        {
            PlayMenuFeedback(true);
            if(index==0)ResumeFromMenu();
            else if(index==1)ShowOptions(FrontScreen.Pause);
            else ShowHome();
        }
        void PauseLabel(Rect rect,string label,GUIStyle style,Color color)
        {
            style.normal.textColor=color;
            GUI.Label(rect,label,style);
        }
        void EnsureFrontTypography()
        {
            if(pauseBrand!=null)return;
            var face=Resources.Load<Font>("PauseMenu/InterQuiet");
            var strong=Resources.Load<Font>("PauseMenu/InterDisplayBlack");
            pauseBrand=new GUIStyle {font=face,fontSize=22,fontStyle=FontStyle.Normal,
                alignment=TextAnchor.MiddleLeft,clipping=TextClipping.Clip};
            pauseHeading=new GUIStyle(pauseBrand){font=strong,fontSize=64};
            pauseAction=new GUIStyle(pauseBrand){fontSize=32};
            pauseCaption=new GUIStyle(pauseBrand){fontSize=15};
            pauseStats=new GUIStyle(pauseCaption){alignment=TextAnchor.MiddleRight};
        }
        void DrawPauseScreen(float width,float height)
        {
            EnsureFrontTypography();
            Matrix4x4 previous=GUI.matrix;
            bool wasEnabled=GUI.enabled;
            GUI.enabled=wasEnabled&&!audioCreditsVisible;
            // Keep the paused scene visible; only shade it for legible menu text.
            Fill(new Rect(0,0,width,height),new Color(.012f,.013f,.015f,.38f));
            for(int i=0;i<48;i++)
                Fill(new Rect(width*i/48f,0,width/48f+1,height),
                    new Color(.008f,.009f,.011f,.76f*Mathf.Pow(1-i/47f,2)));
            if(Screen.height<500)
            {
                GUI.matrix=Matrix4x4.identity;
                try { DrawCompactPauseMenu(Screen.width,Screen.height); }
                finally { GUI.matrix=previous;GUI.enabled=wasEnabled; }
                GUI.matrix=Matrix4x4.identity;
                try { DrawPauseFooter(Screen.width,Screen.height); }
                finally { GUI.matrix=previous; }
                return;
            }
            float availableHeight=Mathf.Max(1,height-(Screen.width<560?120:72)/HudScale);
            float scale=Mathf.Min(width/1672f,availableHeight/760f);
            GUI.matrix=previous*Matrix4x4.TRS(new Vector3((width-1672*scale)*.5f,(availableHeight-760*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
            try
            {
                Color muted=new Color(.60f,.58f,.54f);
                Color rule=new Color(.62f,.60f,.55f,.36f);
                TrackedHomeLabel(new Rect(80,126,900,48),"LAST DEPARTURE",pauseBrand,6,PauseInk);
                PauseLabel(new Rect(78,264,800,100),"PAUSED",pauseHeading,PauseInk);
                if(Event.current.type==EventType.MouseMove||Event.current.type==EventType.MouseDown)
                    pauseKeyboardNavigation=false;
                for(int i=0;i<3;i++)
                {
                    Rect rect=new Rect(80,400+i*72,510,60);
                    bool hovered=rect.Contains(Event.current.mousePosition);
                    if(hovered&&!pauseKeyboardNavigation)pauseSelection=i;
                    bool selected=pauseSelection==i;
                    if(i==0)
                    {
                        if(MenuBackButton(rect,"RESUME",selected)){ResumeFromMenu();return;}
                        continue;
                    }
                    float highlight=MenuHighlight(rect,"pause:"+i,selected);
                    if(highlight>0)
                    {
                        // Soft red wash fades into the scene instead of a solid button block.
                        for(int j=0;j<32;j++)
                            Fill(new Rect(rect.x+j*rect.width/32,rect.y,rect.width/32+1,rect.height),
                                new Color(.42f,.025f,.018f,.35f*(1-j/31f)*highlight));
                        Fill(new Rect(rect.x,rect.y+6,4,rect.height-12),new Color(.80f,.045f,.025f));
                    }
                    if(GUI.Button(rect,GUIContent.none,GUIStyle.none)){ActivatePauseAction(i);return;}
                    TrackedHomeLabel(new Rect(110+highlight*5,rect.y,465,rect.height),
                        i==0?"RESUME":i==1?"OPTIONS":"MAIN MENU",pauseAction,2,selected?PauseInk:muted);
                }
                Fill(new Rect(80,639,470,1),rule);

            }
            finally { GUI.matrix=previous; GUI.enabled=wasEnabled; }
            GUI.matrix=Matrix4x4.identity;
            try { DrawPauseFooter(Screen.width,Screen.height); }
            finally { GUI.matrix=previous; }
        }
        void DrawPauseFooter(float width,float height)
        {
            // Footer uses one coordinate system, independent of the cinematic menu scale.
            const float margin=20, gap=16, row=32, creditsWidth=138;
            string stats="WAVE "+wave.ToString("00")+"   /   "+kills+" KILLS   /   "+supplies+" SCRAP";
            var label=new GUIStyle(pauseCaption){fontSize=14,alignment=TextAnchor.MiddleLeft};
            float statsWidth=label.CalcSize(new GUIContent(stats)).x;
            bool stacked=width<Mathf.Max(560,margin*2+statsWidth+creditsWidth+gap);
            float footerHeight=stacked?row*2+gap:row;
            float y=height-margin-footerHeight;
            Fill(new Rect(0,y-8,width,height-y+8),new Color(.015f,.018f,.02f,.88f));
            Fill(new Rect(margin,y-8,width-margin*2,1),new Color(.62f,.60f,.55f,.3f));
            var statRect=new Rect(margin,y,stacked?width-margin*2:width-margin*2-creditsWidth-gap,row);
            label.fontSize=Mathf.Min(14,Mathf.FloorToInt(14*statRect.width/Mathf.Max(1,statsWidth)));
            PauseLabel(statRect,stats,label,PauseInk);
            var button=new Rect(width-margin-creditsWidth,stacked?y+row+gap:y,creditsWidth,row);
            bool enabled=GUI.enabled;GUI.enabled=enabled&&!audioCreditsVisible;
            Fill(button,new Color(.10f,.11f,.12f,.96f));
            MenuHighlight(button,"pause:credits");
            label.fontSize=14;label.alignment=TextAnchor.MiddleCenter;
            PauseLabel(button,"Sound-Credits",label,PauseInk);
            if(FeedbackButton(button,"",GUIStyle.none))audioCreditsVisible=true;
            GUI.enabled=enabled;
        }
        void DrawCompactPauseMenu(float width,float height)
        {
            var heading=new GUIStyle(pauseHeading){fontSize=24};
            var action=new GUIStyle(pauseAction){fontSize=16};
            PauseLabel(new Rect(20,8,width-40,28),"PAUSED",heading,PauseInk);
            float row=Mathf.Min(40,Mathf.Max(20,(height-(width<560?156:108))/3));
            if(Event.current.type==EventType.MouseMove||Event.current.type==EventType.MouseDown)
                pauseKeyboardNavigation=false;
            for(int i=0;i<3;i++)
            {
                var rect=new Rect(20,38+i*row,Mathf.Min(280,width-40),row);
                if(GUI.enabled&&rect.Contains(Event.current.mousePosition)&&!pauseKeyboardNavigation)pauseSelection=i;
                bool selected=pauseSelection==i;
                float highlight=MenuHighlight(rect,"pause:"+i,selected);
                if(highlight>0)Fill(rect,new Color(.42f,.025f,.018f,.35f*highlight));
                PauseLabel(new Rect(rect.x+10,rect.y,rect.width-20,rect.height),i==0?"RESUME":i==1?"OPTIONS":"MAIN MENU",action,selected?PauseInk:PauseMuted);
                if(GUI.Button(rect,GUIContent.none,GUIStyle.none)){ActivatePauseAction(i);return;}
            }
        }
        [System.NonSerialized] GUIStyle homeTitle,homeAction,homeSmall,homeField,homeStatus,homeNavigation;
        void TrackedHomeLabel(Rect rect,string text,GUIStyle style,float tracking,Color color)
        {
            float x=rect.x;
            foreach(char character in text)
            {
                var glyph=new GUIContent(character.ToString());
                float advance=style.CalcSize(glyph).x;
                PauseLabel(new Rect(x,rect.y,advance+4,rect.height),glyph.text,style,color);
                x+=advance+tracking;
            }
        }
        bool HomeNavigation(Rect rect,string label,bool selected=false,bool small=false)
        {
            bool hover=GUI.enabled&&rect.Contains(Event.current.mousePosition);
            float highlight=MenuHighlight(rect,"home:"+label,selected);
            var style=small?homeSmall:homeNavigation;
            if(highlight>0)
            {
                for(int j=0;j<32;j++)
                    Fill(new Rect(rect.x+j*rect.width/32,rect.y,rect.width/32,rect.height),new Color(.42f,.025f,.018f,.5f*(1-j/31f)*highlight));
                Fill(new Rect(rect.x-12,rect.y+10,4,rect.height-20),new Color(.82f,.045f,.025f,highlight));
            }
            var textRect=rect;textRect.x+=highlight*6;
            TrackedHomeLabel(textRect,label,style,small?2:2.5f,GUI.enabled?(hover||selected?PauseInk:new Color(.74f,.72f,.67f)):PauseMuted);
            return FeedbackButton(rect,"",GUIStyle.none);
        }
        bool HomeAction(Rect rect,string label,bool primary=false,bool large=false)
        {
            bool hover=GUI.enabled&&rect.Contains(Event.current.mousePosition);
            Color rule=new Color(.63f,.66f,.64f,.6f);
            Fill(new Rect(rect.x,rect.yMax-1,primary?Mathf.Min(198,rect.width):rect.width,1),hover?PauseInk:rule);
            MenuHighlight(rect,"action:"+label);
            bool clicked=FeedbackButton(rect,"",GUIStyle.none);
            PauseLabel(new Rect(rect.x+8,rect.y,rect.width-16,rect.height),label,homeAction,
                !GUI.enabled?PauseMuted:PauseInk);
            return clicked;
        }
        string HomeField(Rect rect,string value,int limit,string name)
        {
            Fill(rect,new Color(.10f,.12f,.13f,.95f));
            GUI.SetNextControlName(name);
            string result=GUI.TextField(rect,value,limit,homeField);
            Fill(new Rect(rect.x,rect.yMax-2,rect.width,2),GUI.GetNameOfFocusedControl()==name?PauseInk:PauseMuted);
            return result;
        }
        void DrawHomeScreen(float width,float height)
        {
            EnsureFrontTypography();
            if(homeTitle==null||homeNavigation==null||homeTitle.fontSize!=66)
            {
                var face=Resources.Load<Font>("PauseMenu/InterQuiet");
                var strong=Resources.Load<Font>("Launch/BarlowCondensed-SemiBold");
                homeTitle=new GUIStyle {font=strong,fontSize=66,fontStyle=FontStyle.Normal,alignment=TextAnchor.MiddleLeft};
                homeAction=new GUIStyle(homeTitle){font=face,fontSize=27};
                homeNavigation=new GUIStyle(homeTitle){fontSize=34};
                homeSmall=new GUIStyle(homeTitle){font=face,fontSize=20};
                homeField=new GUIStyle {font=face,fontSize=25,alignment=TextAnchor.MiddleLeft,
                    padding=new RectOffset(16,16,8,8),clipping=TextClipping.Clip,normal={textColor=PauseInk}};
                homeStatus=new GUIStyle(homeSmall){wordWrap=true,alignment=TextAnchor.UpperLeft};
            }
            Matrix4x4 previous=GUI.matrix;
            bool wasEnabled=GUI.enabled;
            Fill(new Rect(0,0,width,height),new Color(.012f,.015f,.019f,.12f));
            for(int i=0;i<48;i++)Fill(new Rect(width*i/48f,0,width/48f,height),
                new Color(.008f,.011f,.014f,.65f*Mathf.Pow(1-i/47f,2)));
            float scale=Mathf.Min(width/1672f,height/941f);
            GUI.matrix=previous*Matrix4x4.TRS(new Vector3((width-1672*scale)*.5f,(height-941*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
            try
            {
                TrackedHomeLabel(new Rect(102,109,1100,80),"LAST DEPARTURE",homeTitle,3,PauseInk);
                Fill(new Rect(103,191,176,2),new Color(.45f,.18f,.12f,.9f));
                TrackedHomeLabel(new Rect(103,208,800,30),"CONTAINMENT ARCHIVE",homeSmall,5,PauseMuted);
                GUI.enabled=wasEnabled&&!audioCreditsVisible;

                if(Event.current.type==EventType.MouseMove)
                    for(int i=0;i<5;i++)if(new Rect(103,285+i*76,435,62).Contains(Event.current.mousePosition))homeSelection=i;
                string[] actions={"PLAY","ENCOUNTERS","OPTIONS","CREDITS","QUIT"};
                for(int i=0;i<actions.Length;i++)
                    if(HomeNavigation(new Rect(103,285+i*76,435,62),actions[i],homeSelection==i)){ActivateHomeAction(i);return;}
                GUI.enabled=wasEnabled;
            }
            finally {GUI.matrix=previous;GUI.enabled=wasEnabled;}
        }
        bool DrawFrontEnd(float width,float height)
        {
            if (frontScreen == FrontScreen.Launch) { DrawLaunchScreen(width, height); return true; }
            if(frontScreen==FrontScreen.Game&&!dead)return false;
            if(frontScreen==FrontScreen.Home&&!dead){DrawHomeScreen(width,height);return true;}
            if(frontScreen==FrontScreen.Difficulty&&!dead){DrawDifficultyScreen(width,height);return true;}
            if(frontScreen==FrontScreen.Encounters&&!dead){DrawEncounters(width,height);return true;}
            if(frontScreen==FrontScreen.Pause&&!dead){DrawPauseScreen(width,height);return true;}
            if(frontScreen==FrontScreen.Options&&!dead){DrawOptionsScreen(width,height);return true;}
            menuTitle ??=new GUIStyle(GUI.skin.label){fontSize=72,fontStyle=FontStyle.Bold};
            menuSmall ??=new GUIStyle(GUI.skin.label){fontSize=15};menuButton ??=new GUIStyle(GUI.skin.button){fontSize=20,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(22,12,8,8),normal={background=null},hover={background=null},active={background=null}};
            float left=width*.1f, top=Mathf.Max(35,height*(frontScreen==FrontScreen.Options?.10f:.20f));float panel=Mathf.Min(440,width*.8f);
            Fill(new Rect(0,0,width,height),new Color(.018f,.025f,.026f,dead?.35f:.7f));
            PrepareDeathBlood();
            if(dead)
            {
                DrawDeathBlood(width,height);
                if(deathTime<.8f)return true;
                if (!PartnerCanContinue)
                {
                    DrawRunRecap(width, height);
                    return true;
                }
                DrawDeathHeading(width,height);
                HudLabel(new Rect(0,height*.34f+98,width,35),$"WAVE {wave:00}    /    {kills} TEAM KILLS",centerStyle,HudWhite);
                if(deathTime>2.3f){float x=width/2-panel/2;float actionsY=Mathf.Max(height*.62f,height*.34f+224);if(MenuButton(new Rect(x,actionsY,panel,52),"Play again",true)){PlayFromMenu();}if(MenuButton(new Rect(x,actionsY+64,panel,52),"Main menu"))ShowHome();}
                return true;
            }
            menuTitle.fontSize=72;menuTitle.alignment=TextAnchor.MiddleLeft;
            HudLabel(new Rect(left,top,width-left,100),frontScreen==FrontScreen.Home?"LAST DEPARTURE":frontScreen==FrontScreen.Options?"OPTIONS":"PAUSE",menuTitle,HudWhite);
            Fill(new Rect(left,top+100,panel,3),new Color(.77f,.11f,.12f));
            float y=top+130;
            {
                bool home=frontScreen==FrontScreen.Home;
                if(MenuButton(new Rect(left,y,panel,56),home?"Play Game":"Resume",true)){if(home){ShowDifficultyScreen();}else ResumeFromMenu();}
                if(MenuButton(new Rect(left,y+70,panel,56),"Options"))ShowOptions(frontScreen);

                if(!home&&MenuButton(new Rect(left,y+140,panel,56),"Main menu"))ShowHome();
                if(!home)HudLabel(new Rect(left,y+215,panel,64),"Build barricades. Recover weapons.\nDefend the depot against the next wave.",textStyle,new Color(.67f,.70f,.66f));
            }
            HudLabel(new Rect(left,height-48,width-left*2,24),"D27   //   WAREHOUSE SURVIVAL",menuSmall,new Color(.48f,.55f,.51f));return true;
        }
    }
}
