using UnityEngine;
using UnityEngine.InputSystem;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        [System.NonSerialized] GUIStyle difficultyBrand,difficultyHeading,difficultyChoice;
        [System.NonSerialized] Texture2D difficultyWear,difficultyRedMetal;
        [System.NonSerialized] Texture2D[] difficultyPortraits;
        static readonly Color DifficultyRed=new Color(.57f,.12f,.075f);
        static readonly Color DifficultyRule=new Color(.40f,.39f,.35f,.65f);
        void ShowDifficultyScreen()
        {
            frontScreen=FrontScreen.Difficulty;showHelp=false;ReleaseMouse();
        }
        void CloseDifficultyScreen(){frontScreen=FrontScreen.Home;homeSelection=0;ReleaseMouse();}
        void StartDifficultyRun(){if(frontScreen==FrontScreen.Difficulty)PlayFromMenu();}
        void TickDifficultyNavigation()
        {
            var keys=Keyboard.current;if(keys==null)return;
            int step=keys.downArrowKey.wasPressedThisFrame||keys.rightArrowKey.wasPressedThisFrame?1:
                keys.upArrowKey.wasPressedThisFrame||keys.leftArrowKey.wasPressedThisFrame?-1:0;
            if(step!=0)
            {
                int index=System.Array.IndexOf(DifficultyOrder,selectedDifficulty);
                SelectDifficulty(DifficultyOrder[(index+step+3)%3]);PlayMenuFeedback(false);
            }
            if(keys.enterKey.wasPressedThisFrame||keys.numpadEnterKey.wasPressedThisFrame)
            {PlayMenuFeedback(true);StartDifficultyRun();}
        }
        void EnsureDifficultyStyles()
        {
            if(difficultyBrand!=null&&difficultyBrand.fontSize==86&&difficultyBrand.font!=null&&
                difficultyHeading!=null&&difficultyHeading.fontSize==44&&
                difficultyChoice!=null&&difficultyChoice.fontSize==48&&
                difficultyWear!=null&&difficultyRedMetal!=null&&
                difficultyPortraits!=null&&difficultyPortraits.Length==3&&
                difficultyPortraits[0]!=null&&difficultyPortraits[1]!=null&&difficultyPortraits[2]!=null)return;
            var condensed=Resources.Load<Font>("UpgradeCards/Anton-Regular");
            difficultyBrand=new GUIStyle{font=condensed,fontSize=86,alignment=TextAnchor.MiddleCenter,normal={textColor=PauseInk}};
            difficultyHeading=new GUIStyle(difficultyBrand){fontSize=44,alignment=TextAnchor.MiddleLeft};
            difficultyChoice=new GUIStyle(difficultyHeading){fontSize=48};
            difficultyWear=Resources.Load<Texture2D>("DifficultyMenu/TerminalPanel");
            difficultyRedMetal=Resources.Load<Texture2D>("DifficultyMenu/RedMetal");
            difficultyPortraits = new[]
            {
                Resources.Load<Texture2D>("DifficultyMenu/PortraitEasy"),
                Resources.Load<Texture2D>("DifficultyMenu/PortraitNormal"),
                Resources.Load<Texture2D>("DifficultyMenu/PortraitHard")
            };
        }
        void DifficultyFrame(Rect rect,Color color)
        {
            Fill(new Rect(rect.x,rect.y,rect.width,1),color);Fill(new Rect(rect.x,rect.yMax-1,rect.width,1),color);
            Fill(new Rect(rect.x,rect.y,1,rect.height),color);Fill(new Rect(rect.xMax-1,rect.y,1,rect.height),color);
        }
        void DifficultyCorners(Rect r,Color color)
        {
            foreach(float x in new[]{r.x,r.xMax-18})foreach(float y in new[]{r.y,r.yMax-18})
            {Fill(new Rect(x,y==r.y?y:y+17,18,1),color);Fill(new Rect(x==r.x?x:x+17,y,1,18),color);}
        }
        void DrawDifficultyScreen(float width,float height)
        {
            EnsureDifficultyStyles();
            var previous=GUI.matrix;bool enabled=GUI.enabled;
            Color previousColor=GUI.color,previousContentColor=GUI.contentColor;
            GUI.color=GUI.contentColor=Color.white;
            Fill(new Rect(0,0,width,height),new Color(.003f,.004f,.005f,.68f));
            float scale=Mathf.Min(width/1600f,height/900f);
            GUI.matrix=previous*Matrix4x4.TRS(new Vector3((width-1600*scale)/2,(height-900*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            try
            {
                GUI.enabled=enabled&&!audioCreditsVisible;
                PauseLabel(new Rect(250,18,1100,110),"LAST DEPARTURE",difficultyBrand,PauseInk);
                Fill(new Rect(540,137,520,3),DifficultyRed);
                var panel=new Rect(160,177,1280,624);
                Fill(new Rect(168,185,1280,624),new Color(0,0,0,.35f));
                if(difficultyWear!=null)GUI.DrawTexture(panel,difficultyWear,ScaleMode.StretchToFill);
                else Fill(panel,new Color(.026f,.029f,.028f,.98f));
                Fill(panel,new Color(.006f,.008f,.011f,.62f));
                PauseLabel(new Rect(205,201,1000,61),"SELECT DIFFICULTY",difficultyHeading,PauseInk);
                Fill(new Rect(205,274,548,1),DifficultyRule);

                for(int i=0;i<3;i++)
                {
                    var mode=DifficultyOrder[i];bool selected=mode==selectedDifficulty;
                    var r=new Rect(205,305+i*118,548,102);
                    bool hover=GUI.enabled&&r.Contains(Event.current.mousePosition);
                    if(selected&&difficultyRedMetal!=null)GUI.DrawTexture(r,difficultyRedMetal,ScaleMode.StretchToFill);
                    else if(difficultyWear!=null)GUI.DrawTextureWithTexCoords(r,difficultyWear,new Rect(.12f,.12f,.78f,.68f),true);
                    else Fill(r,new Color(.045f,.045f,.04f,.9f));
                    Fill(r,selected?new Color(.008f,.002f,.002f,.18f):new Color(.006f,.008f,.011f,.70f));
                    if(hover)Fill(r,new Color(1,1,1,.035f));
                    DifficultyFrame(r,selected?DifficultyRed:hover?PauseMuted:new Color(.24f,.24f,.21f,.65f));
                    if(selected)Fill(new Rect(r.x,r.y,6,r.height),new Color(.76f,.18f,.12f));
                    PauseLabel(new Rect(r.x+24,r.y+12,500,76),DifficultyName(mode),difficultyChoice,selected||hover?PauseInk:PauseMuted);
                    if(FeedbackButton(r,"",GUIStyle.none))SelectDifficulty(mode);
                }
                Fill(new Rect(785, 211, 1, 455), DifficultyRule);
                int portraitIndex = System.Array.IndexOf(DifficultyOrder, selectedDifficulty);
                if (difficultyPortraits != null && portraitIndex >= 0 && portraitIndex < difficultyPortraits.Length && difficultyPortraits[portraitIndex] != null)
                {
                    var portraitRect = new Rect(882, 211, 455, 455);
                    GUI.DrawTexture(portraitRect, difficultyPortraits[portraitIndex], ScaleMode.ScaleToFit);
                }
                Fill(new Rect(205,688,1190,1),DifficultyRule);
                var back=new Rect(205,711,200,66);var start=new Rect(1008,711,387,66);
                bool startHover=start.Contains(Event.current.mousePosition);


                if(difficultyRedMetal!=null)GUI.DrawTexture(start,difficultyRedMetal,ScaleMode.StretchToFill);else Fill(start,new Color(.34f,.065f,.04f));
                if(startHover)Fill(start,new Color(1,1,1,.055f));
                DifficultyCorners(new Rect(start.x+7,start.y+7,start.width-14,start.height-14),new Color(.71f,.31f,.23f));
                PauseLabel(new Rect(start.x+48,start.y,320,66),"START RUN",difficultyHeading,PauseInk);
                if(MenuBackButton(back)){CloseDifficultyScreen();return;}
                if(FeedbackButton(start,"",GUIStyle.none)){StartDifficultyRun();return;}
            }
            finally{GUI.matrix=previous;GUI.enabled=enabled;GUI.color=previousColor;GUI.contentColor=previousContentColor;}
        }
    }
}
