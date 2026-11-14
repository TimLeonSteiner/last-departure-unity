using UnityEngine;
using UnityEngine.InputSystem;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        const string EncounterPrefix = "LastDeparture.Encounters.v1.";
        int homeSelection, encounterSelection = -1;
        float nextEncounterScan;
        Vector2 encounterScroll;
        const float EncounterListHeight = 623f;
        const float EncounterRowPitch = 105f;
        const float EncounterRowHeight = 98f;
        float EncounterContentHeight => Mathf.Max(0, (EncounterCount - 1) * EncounterRowPitch + EncounterRowHeight);
        [System.NonSerialized] GUIStyle archiveTitle, archiveBody, archiveSmall, archiveName, archiveRow;
        Texture2D archiveBackdrop, specimenArtwork, bruteArtwork;
        // Review tools can exercise unlocked layouts without changing the player's save.
#if UNITY_EDITOR
        bool encounterReviewUnlocked;
#endif
        int EncounterCount => zombieModels?.Length ?? 0;
        string EncounterId(int index) => zombieModels[index].name;

        // Swap the display positions without changing the variant indices used by co-op.
        int EncounterDisplayIndex(int index)
        {
            if (EncounterId(index) == "Charger" && index + 1 < EncounterCount &&
                EncounterId(index + 1) == "04-Cargo-Brute") return index + 1;
            if (EncounterId(index) == "04-Cargo-Brute" && index > 0 &&
                EncounterId(index - 1) == "Charger") return index - 1;
            return index;
        }
        bool EncounterKnown(int index)
        {
#if UNITY_EDITOR
            if(encounterReviewUnlocked)return true;
#endif
            return PlayerPrefs.GetInt(EncounterPrefix + EncounterId(index), 0) > 0;
        }
        bool EncounterNew(int index) => EncounterKnown(index) && PlayerPrefs.GetInt(EncounterPrefix + EncounterId(index) + ".Read", 0) == 0;
        void TickEncounterDiscovery()
        {
            if (frontScreen != FrontScreen.Game || dead || arenaCamera == null || Time.unscaledTime < nextEncounterScan) return;
            nextEncounterScan = Time.unscaledTime + .5f;
            bool changed = false;
            foreach (var enemy in enemies)
            {
                if(enemy.body == null || enemy.hp <= 0 || enemy.variant < 0 || enemy.variant >= EncounterCount || EncounterKnown(enemy.variant))continue;
                Vector3 target = enemy.body.position + Vector3.up;
                Vector3 screen = arenaCamera.WorldToViewportPoint(target);
                if(screen.z <= 0 || screen.z > 35 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1)continue;
                Vector3 sight=target-arenaCamera.transform.position;
                if(ShotStopDistance(arenaCamera.transform.position,sight.normalized)+.04f<sight.magnitude)continue;
                PlayerPrefs.SetInt(EncounterPrefix + EncounterId(enemy.variant),1);
                changed = true;
            }
            if(changed)PlayerPrefs.Save();
        }
        void ActivateHomeAction(int index)
        {
            if(index==0)ShowDifficultyScreen();
            else if(index==1)ShowEncounters();
            else if(index==2)ShowOptions(FrontScreen.Home);
            else if(index==3)audioCreditsVisible=true;
            else
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying=false;
#else
                Application.Quit();
#endif
            }
        }
        void TickArchiveNavigation()
        {
            var keys=Keyboard.current;
            if(keys==null || audioCreditsVisible)return;
            if(frontScreen==FrontScreen.Difficulty){TickDifficultyNavigation();return;}
            int direction=keys.downArrowKey.wasPressedThisFrame?1:keys.upArrowKey.wasPressedThisFrame?-1:0;
            if(frontScreen==FrontScreen.Home)
            {
                if(direction!=0){homeSelection=(homeSelection+direction+5)%5;PlayMenuFeedback(false);}
                if(keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame){PlayMenuFeedback(true);ActivateHomeAction(homeSelection);}
            }
            else if(frontScreen==FrontScreen.Encounters && direction!=0 && EncounterCount>0)
            {
                int row = encounterSelection < 0 ? 0 : EncounterDisplayIndex(encounterSelection);
                SelectEncounter(EncounterDisplayIndex((row+direction+EncounterCount)%EncounterCount));
                float top = EncounterDisplayIndex(encounterSelection) * EncounterRowPitch;
                encounterScroll.y = Mathf.Clamp(encounterScroll.y, top + EncounterRowHeight - EncounterListHeight, top);
                encounterScroll.y = Mathf.Clamp(encounterScroll.y, 0, Mathf.Max(0, EncounterContentHeight - EncounterListHeight));
                PlayMenuFeedback(false);
            }
        }
        void ShowEncounters()
        {
            audioCreditsVisible=false;frontScreen=FrontScreen.Encounters;ReleaseMouse();encounterScroll=Vector2.zero;
            int first=0;
            for (int row = 0; row < EncounterCount; row++)
            {
                int index = EncounterDisplayIndex(row);
                if (EncounterKnown(index)) { first = index; break; }
            }
            SelectEncounter(EncounterCount>0?first:-1);
        }
        void CloseEncounters()
        {
            ReleaseEncounterPreview();
            frontScreen=FrontScreen.Home;homeSelection=1;
        }
        void SelectEncounter(int index)
        {
            if (index != encounterSelection)
                ReleaseEncounterPreview();
            encounterSelection=index;
            if(index<0 || !EncounterKnown(index))return;
#if UNITY_EDITOR
            if(!encounterReviewUnlocked)
#endif
            {
                PlayerPrefs.SetInt(EncounterPrefix+EncounterId(index)+".Read",1);PlayerPrefs.Save();
            }
        }
        string EncounterName(int i) => EncounterId(i) switch
        {
            "01-Dockworker-Shambler" => "SHAMBLER",
            "ArmoredWorker" => "ARMORED WORKER",
            "04-Cargo-Brute" => "BRUTE",
            _ => EncounterId(i).ToUpperInvariant()
        };
        void EncounterFacts(int i,out string description,out string behavior,out string tactic,out string notes)
        {
            switch(EncounterId(i))
            {
                case "Spitter":
                    description="A ranged infected that spits acid across the hall.";
                    behavior="Ranged acid attack";tactic="Keep moving";
                    notes="Break its line of sight. Move away from acid pools before they can keep burning you.";break;
                case "Charger":
                    description="A rushing infected that can knock you off your feet.";
                    behavior="Wind-up, then charge";tactic="Step out of its path";
                    notes="Watch the wind-up. A collision stops its rush; injured legs interrupt its special attack.";break;
                case "Screamer":
                    description="Its scream draws more infected into the hall and rallies nearby zombies.";
                    behavior="Calls reinforcements";tactic="Interrupt the wind-up";
                    notes="Prioritize it before the scream. A heavy hit during the wind-up stops the call; stay clear of the incoming horde.";break;
                case "04-Cargo-Brute":
                    description="A bulky infected that can take more hits than a shambler.";
                    behavior="Heavy melee attacks";tactic="Keep your distance";
                    notes="Brute hits barricades harder than a shambler. Thin the crowd before it reaches your defenses.";break;
                case "ArmoredWorker":
                    description="An infected industrial worker, identifiable by its protective gear.";
                    behavior="Close-range attacker";tactic="Preserve escape routes";
                    notes="Do not let its approach pin you beside a barricade. Keep space to reposition between attacks.";break;
                default:
                    description="A shambling infected worker. Common among the first waves.";
                    behavior="Advances into melee";tactic="Keep your distance";
                    notes="Dangerous in a crowd. Use barricades to slow its approach and avoid being surrounded.";break;
            }
        }
        void EnsureArchiveStyles()
        {
            if(archiveBackdrop==null)archiveBackdrop=Resources.Load<Texture2D>("MenuArtwork/Archive");
            if(specimenArtwork==null)specimenArtwork=Resources.Load<Texture2D>("MenuArtwork/Specimens-Meshy");
            if(archiveTitle!=null && archiveTitle.fontSize==66 && archiveBody.fontSize==26)return;
            var strong=Resources.Load<Font>("Launch/BarlowCondensed-SemiBold");
            var face=Resources.Load<Font>("PauseMenu/InterQuiet");
            archiveTitle=new GUIStyle{font=strong,fontSize=66,alignment=TextAnchor.MiddleLeft};
            archiveName=new GUIStyle(archiveTitle){fontSize=56};
            archiveRow=new GUIStyle(archiveTitle){fontSize=30};
            archiveSmall=new GUIStyle{font=face,fontSize=19,alignment=TextAnchor.MiddleLeft};
            archiveBody=new GUIStyle(archiveSmall){fontSize=26,wordWrap=true,alignment=TextAnchor.UpperLeft};
        }
        Rect SpecimenUV(int index)
        {
            int slot=EncounterId(index) switch {"Spitter"=>1,"Screamer"=>2,"ArmoredWorker"=>3,"Charger"=>4,_=>0};
            // Boundaries follow the transparent gaps in the source-reference artwork.
            float[] edges={0,.215f,.40f,.595f,.77f,1};
            return new Rect(edges[slot],0,edges[slot+1]-edges[slot],1);
        }
        void DrawSpecimenArt(Rect rect,int index,bool portrait=false,bool silhouette=false)
        {
            if(index<0)return;
            if (EncounterId(index) == "04-Cargo-Brute")
            {
                if (bruteArtwork == null) bruteArtwork = Resources.Load<Texture2D>("Encounters/04-Cargo-Brute");
                if (bruteArtwork == null) return;
                Color tint = GUI.color;
                if (silhouette) GUI.color = new Color(.035f,.035f,.04f,1);
                if (portrait) GUI.DrawTextureWithTexCoords(rect, bruteArtwork, new Rect(.25f,.56f,.50f,.28f), true);
                else GUI.DrawTexture(rect, bruteArtwork, ScaleMode.ScaleToFit, true);
                GUI.color = tint;
                return;
            }
            if(specimenArtwork==null)return;
            Rect uv=SpecimenUV(index);
            if(portrait)
            {
                uv.x+=uv.width*.18f;uv.width*=.64f;uv.y=.68f;uv.height=.32f;
            }
            else
            {
                float aspect=specimenArtwork.width*uv.width/specimenArtwork.height;
                float w=Mathf.Min(rect.width,rect.height*aspect),h=w/aspect;
                rect=new Rect(rect.center.x-w/2,rect.yMax-h,w,h);
            }
            Color previous=GUI.color;
            if(silhouette)GUI.color=new Color(.035f,.035f,.04f,1);
            GUI.DrawTextureWithTexCoords(rect,specimenArtwork,uv,true);
            GUI.color=previous;
        }
        void ArchiveBorder(Rect rect,Color ink)
        {
            Fill(new Rect(rect.x,rect.y,rect.width,1),ink);Fill(new Rect(rect.x,rect.yMax-1,rect.width,1),ink);
            Fill(new Rect(rect.x,rect.y,1,rect.height),ink);Fill(new Rect(rect.xMax-1,rect.y,1,rect.height),ink);
        }
        void DrawEncounters(float width,float height)
        {
            EnsureArchiveStyles();
            if(archiveBackdrop!=null)GUI.DrawTexture(new Rect(0,0,width,height),archiveBackdrop,ScaleMode.ScaleAndCrop);
            else Fill(new Rect(0,0,width,height),new Color(.015f,.015f,.018f));
            Matrix4x4 previous=GUI.matrix;
            float scale=Mathf.Min(width/1672f,height/941f);
            GUI.matrix=previous*Matrix4x4.TRS(new Vector3((width-1672*scale)/2,(height-941*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            try
            {
                Color rule=new Color(.60f,.57f,.52f,.35f),red=new Color(.78f,.035f,.025f);
                TrackedHomeLabel(new Rect(98,26,750,90),"ENCOUNTERS",archiveTitle,2,PauseInk);
                Fill(new Rect(100,114,180,2),red);
                TrackedHomeLabel(new Rect(100,123,650,35),"DISCOVERED HOSTILES",archiveSmall,4,PauseMuted);
                int known=0;for(int i=0;i<EncounterCount;i++)if(EncounterKnown(i))known++;
                TrackedHomeLabel(new Rect(1300,55,340,35),known.ToString("00")+" / "+EncounterCount.ToString("00")+" DISCOVERED",archiveSmall,3,PauseInk);
                Fill(new Rect(535,108,1070,1),rule);
                Rect list=new Rect(70,182,435,645);
                Fill(list,new Color(.012f,.012f,.014f,.40f));ArchiveBorder(list,rule);
                var viewport = new Rect(72, 194, 431, EncounterListHeight);
                bool listOverflows = EncounterContentHeight > viewport.height;
                if (listOverflows)
                    encounterScroll = GUI.BeginScrollView(viewport, encounterScroll,
                        new Rect(0, 0, 408, EncounterContentHeight), false, false);
                else
                {
                    encounterScroll = Vector2.zero;
                    GUI.BeginGroup(viewport);
                }
                for(int rowIndex=0;rowIndex<EncounterCount;rowIndex++)
                {
                    int i = EncounterDisplayIndex(rowIndex);
                    bool unlocked=EncounterKnown(i),selected=encounterSelection==i;
                    Rect row=new Rect(0,rowIndex*EncounterRowPitch,408,EncounterRowHeight);
                    Fill(row,selected?new Color(.31f,.025f,.018f,.88f):new Color(.025f,.025f,.028f,.72f));ArchiveBorder(row,rule);
                    if(selected)Fill(new Rect(0,row.y,5,row.height),red);
                    Rect portrait=new Rect(13,row.y+4,110,90);
                    Fill(portrait,new Color(.08f,.08f,.085f,.8f));
                    DrawSpecimenArt(portrait,unlocked?i:0,true,!unlocked);ArchiveBorder(portrait,rule);
                    if(!unlocked)
                    {
                        Rect shackle=new Rect(58,row.y+35,16,16);ArchiveBorder(shackle,PauseMuted);
                        Fill(new Rect(54,row.y+47,24,18),PauseMuted);Fill(new Rect(64,row.y+52,4,8),new Color(.04f,.04f,.045f));
                    }
                    PauseLabel(new Rect(140,row.y+6,250,78),unlocked?EncounterName(i):"UNKNOWN",archiveRow,unlocked?PauseInk:PauseMuted);
                    if(unlocked && EncounterNew(i) && !selected)
                    {
                        Fill(new Rect(332,row.y+72,62,23),red);
                        PauseLabel(new Rect(339,row.y+69,60,27),"NEW",archiveSmall,PauseInk);
                    }
                    MenuHighlight(row,"encounter:"+i,selected);
                    if(FeedbackButton(row,"",GUIStyle.none))SelectEncounter(i);
                }
                if (listOverflows) GUI.EndScrollView();
                else GUI.EndGroup();
                bool revealed=encounterSelection>=0 && EncounterKnown(encounterSelection);
                Rect specimen=new Rect(535,130,495,725);
                if (!revealed || !DrawEncounterPreview(specimen, encounterSelection))
                    DrawSpecimenArt(specimen,revealed?encounterSelection:0,false,!revealed);
                Rect dossier=new Rect(1060,182,575,659);
                Fill(dossier,new Color(.012f,.012f,.014f,.76f));ArchiveBorder(dossier,rule);
                if(revealed)
                {
                    EncounterFacts(encounterSelection,out string description,out string behavior,out string tactic,out string notes);
                    archiveName.fontSize=EncounterName(encounterSelection).Length>13?44:56;
                    PauseLabel(new Rect(1090,188,515,78),EncounterName(encounterSelection),archiveName,PauseInk);
                    ArchiveBorder(new Rect(1092,269,196,31),rule);
                    TrackedHomeLabel(new Rect(1105,267,190,34),"SPECIMEN "+(EncounterDisplayIndex(encounterSelection)+1).ToString("00"),archiveSmall,2,PauseInk);
                    Fill(new Rect(1320,269,164,31),new Color(.43f,.025f,.018f,.9f));
                    TrackedHomeLabel(new Rect(1333,267,155,34),"OBSERVED",archiveSmall,2,PauseInk);
                    PauseLabel(new Rect(1090,326,515,106),description,archiveBody,PauseInk);
                    string movement=EncounterId(encounterSelection) switch {"Charger"=>"Charging","Spitter"=>"Limping","Screamer"=>"Erratic","ArmoredWorker" or "04-Cargo-Brute"=>"Heavy",_=>"Shambling"};
                    string[] labels={"MOVEMENT","BEHAVIOR","COUNTER"},values={movement,behavior,tactic};
                    for(int i=0;i<3;i++)
                    {
                        float y=438+i*58;Fill(new Rect(1090,y,515,1),rule);
                        TrackedHomeLabel(new Rect(1094,y+8,186,43),labels[i],archiveSmall,2,PauseMuted);
                        PauseLabel(new Rect(1290,y+12,310,50),values[i],archiveBody,PauseInk);
                    }
                    Fill(new Rect(1090,650,515,1),red);
                    TrackedHomeLabel(new Rect(1090,665,510,40),"FIELD NOTES",archiveRow,2,PauseInk);
                    PauseLabel(new Rect(1090,715,515,115),notes,archiveBody,PauseInk);
                }
                else
                {
                    PauseLabel(new Rect(1090,210,515,78),"UNKNOWN",archiveName,PauseInk);
                    PauseLabel(new Rect(1090,335,490,160),"Encounter this hostile during a run to reveal its identity and field notes.",archiveBody,PauseMuted);
                    if(known==0)PauseLabel(new Rect(1090,540,490,120),"Your first discovery starts in the hall.",archiveBody,PauseMuted);
                }
                Rect back=new Rect(55,867,270,48);MenuHighlight(back,"archive:back");
                ArchiveBorder(new Rect(55,876,60,30),PauseInk);
                PauseLabel(new Rect(65,871,60,40),"ESC",archiveSmall,PauseInk);
                TrackedHomeLabel(new Rect(135,871,200,40),"BACK",archiveSmall,4,PauseInk);
                if(FeedbackButton(back,"",GUIStyle.none))CloseEncounters();
            }
            finally{GUI.matrix=previous;}
        }
    }
}
