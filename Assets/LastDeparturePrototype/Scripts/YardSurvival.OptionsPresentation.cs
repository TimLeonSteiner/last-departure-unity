using System;
using System.Globalization;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        [NonSerialized] GUIStyle optionsLabel,optionsSmall,optionsTitle,optionsValue,optionsSlider,optionsThumb;
        static readonly Color OptionsRed=new Color(.52f,.075f,.065f);
        static readonly Color OptionsBorder=new Color(.36f,.34f,.29f,.75f);
        void OptionsBox(Rect r,Color color,float radius=7)
            =>GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,radius);
        void OptionsText(Rect r,string text,GUIStyle style,Color color)=>PauseLabel(r,text,style,color);
        void EnsureOptionsStyles()
        {
            if(optionsLabel!=null&&optionsLabel.fontSize==23&&optionsLabel.font!=null&&optionsTitle!=null&&optionsTitle.fontSize==64)return;
            var face=Resources.Load<Font>("PauseMenu/InterQuiet");
            optionsLabel=new GUIStyle {font=face,fontSize=23,alignment=TextAnchor.MiddleLeft};
            optionsSmall=new GUIStyle(optionsLabel){fontSize=18};
            optionsTitle=new GUIStyle(optionsLabel){font=Resources.Load<Font>("Launch/BarlowCondensed-SemiBold"),fontSize=64};
            optionsValue=new GUIStyle(optionsLabel){alignment=TextAnchor.MiddleRight};
            optionsSlider=new GUIStyle {fixedHeight=24,padding=new RectOffset(0,0,0,0)};
            optionsThumb=new GUIStyle {fixedWidth=18,fixedHeight=24};
        }
        bool OptionsButton(Rect r,string text,bool selected=false)
        {
            MenuHighlight(r,"options:"+text);
            bool hover=r.Contains(Event.current.mousePosition);
            OptionsBox(r,hover?new Color(.55f,.51f,.44f):OptionsBorder);
            OptionsBox(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),selected?OptionsRed:new Color(.12f,.115f,.10f,.92f));
            var style=new GUIStyle(optionsLabel){alignment=TextAnchor.MiddleCenter,fontSize=21};
            OptionsText(r,text,style,PauseInk);
            return FeedbackButton(r,"",GUIStyle.none);
        }
        bool MenuBackButton(Rect r,string label="BACK",bool selected=false)
        {
            EnsureOptionsStyles();
            bool hover=GUI.enabled&&r.Contains(Event.current.mousePosition);
            MenuHighlight(r,"back:"+frontScreen,selected);

            var key=new Rect(r.x,r.y+(r.height-32)/2,54,32);
            Color edge=hover||selected?PauseInk:PauseMuted;
            Fill(key,new Color(.025f,.027f,.03f,.95f));
            DifficultyFrame(key,new Color(edge.r,edge.g,edge.b,.55f));
            var keyStyle=new GUIStyle(optionsSmall){alignment=TextAnchor.MiddleCenter,fontSize=16};
            PauseLabel(key,"ESC",keyStyle,edge);
            var textStyle=new GUIStyle(optionsLabel){fontSize=23};
            PauseLabel(new Rect(r.x+70,r.y,r.width-70,r.height),label,textStyle,hover||selected?PauseInk:PauseMuted);
            return FeedbackButton(r,"",GUIStyle.none);
        }
        bool OptionsToggle(Rect r,bool value)
        {
            MenuHighlight(r,"options-toggle:"+r.y);
            OptionsBox(r,OptionsBorder,16);
            OptionsBox(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),value?new Color(.38f,.065f,.055f):new Color(.12f,.115f,.10f),15);
            float knob=value?r.xMax-28:r.x+6;
            OptionsBox(new Rect(knob,r.y+6,22,22),PauseInk,11);
            OptionsText(new Rect(r.x+(value?12:36),r.y,47,r.height),value?"ON":"OFF",optionsSmall,PauseInk);
            return GUI.Button(r,"",GUIStyle.none);
        }
        float OptionsRange(Rect r,float value,float min,float max,string name)
        {
            GUI.SetNextControlName(name);
            float result=GUI.HorizontalSlider(r,value,min,max,optionsSlider,optionsThumb);
            float fraction=Mathf.InverseLerp(min,max,result),x=r.x+9+(r.width-18)*fraction;
            OptionsBox(new Rect(r.x,r.y+10,r.width,5),new Color(.28f,.27f,.24f),2);
            OptionsBox(new Rect(r.x,r.y+10,Mathf.Max(3,x-r.x),5),OptionsRed,2);
            OptionsBox(new Rect(x-9,r.y+3,18,19),OptionsBorder,3);
            OptionsBox(new Rect(x-7,r.y+5,14,15),GUI.GetNameOfFocusedControl()==name?Color.white:new Color(.74f,.71f,.62f),2);
            return result;
        }
        void OptionsVolume(float y,string label,ref float volume,bool enabled,Action toggle)
        {
            OptionsText(new Rect(102,y,245,36),label,optionsLabel,PauseInk);
            float next=Mathf.Round(OptionsRange(new Rect(351,y+6,335,24),volume,0,1,"Options "+label)*100)/100;
            next=AudioWheelValue(new Rect(351,y,410,36),next);
            if(!Mathf.Approximately(next,volume))
            {float previous=volume;volume=next;PlayVolumeTick(previous,next);ApplyEffectsVolume();UpdateWeather();UpdateAdaptiveAudio(0);saveAudioAt=Time.unscaledTime+.4f;}
            OptionsText(new Rect(696,y,64,36),Mathf.RoundToInt(volume*100)+"%",optionsValue,PauseInk);
            Fill(new Rect(792,y+8,1,22),OptionsBorder);
            if(OptionsToggle(new Rect(820,y+1,88,34),enabled))
            {
                toggle();
                PlayOptionsToggleFeedback(!enabled);
            }
        }
        void OptionsSection(float y,string title)
        {
            TrackedHomeLabel(new Rect(102,y,800,32),title,optionsSmall,4,PauseInk);
            Fill(new Rect(102,y+35,806,1),OptionsBorder);
        }
        void DrawOptionsScreen(float width,float height)
        {
            EnsureOptionsStyles();Matrix4x4 previous=GUI.matrix;
            float scale=Mathf.Min(width/1672f,height/941f);
            Fill(new Rect(0,0,width,height),new Color(.006f,.007f,.008f,.48f));
            GUI.matrix=previous*Matrix4x4.TRS(new Vector3(0,(height-941*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
            try
            {
                OptionsBox(new Rect(46,40,908,843),OptionsBorder,14);
                OptionsBox(new Rect(48,42,904,839),new Color(.025f,.025f,.023f,.87f),12);
                TrackedHomeLabel(new Rect(100,60,300,32),"LAST DEPARTURE",optionsSmall,3,PauseInk);
                TrackedHomeLabel(new Rect(99,98,800,75),"OPTIONS",optionsTitle,3,PauseInk);
                Fill(new Rect(101,177,818,2),OptionsRed);
                OptionsSection(204,"AUDIO");
                OptionsVolume(250,"Music",ref musicVolume,musicEnabled,ToggleMusic);
                OptionsVolume(299,"Effects / Zombies",ref soundVolume,soundEnabled,ToggleSound);
                OptionsVolume(348,"Rain",ref rainVolume,rainEnabled,ToggleRain);
                OptionsSection(405,"CONTROLS");
                OptionsText(new Rect(102,454,245,38),"Mouse sensitivity",optionsLabel,PauseInk);
                float sensitivity=OptionsRange(new Rect(351,461,400,24),mouseSensitivity,.25f,2.5f,"Options sensitivity");
                OptionsText(new Rect(770,454,65,38),sensitivity.ToString("F2",CultureInfo.InvariantCulture),optionsValue,PauseInk);
                if(Mathf.Abs(sensitivity-mouseSensitivity)>.001f){PlayVolumeTick(Mathf.InverseLerp(.25f,2.5f,mouseSensitivity),Mathf.InverseLerp(.25f,2.5f,sensitivity));mouseSensitivity=sensitivity;PlayerPrefs.SetFloat("Dustline.MouseSensitivity",sensitivity);PlayerPrefs.Save();}
                OptionsText(new Rect(102,500,245,38),"Field of view (FOV)",optionsLabel,PauseInk);
                float fov=OptionsRange(new Rect(351,507,400,24),baseFov,60,100,"Options FOV");
                OptionsText(new Rect(770,500,65,38),Mathf.RoundToInt(fov)+"°",optionsValue,PauseInk);
                if(Mathf.Abs(fov-baseFov)>.01f){PlayVolumeTick(Mathf.InverseLerp(60,100,baseFov),Mathf.InverseLerp(60,100,fov));baseFov=fov;PlayerPrefs.SetFloat("Dustline.FOV",fov);PlayerPrefs.Save();arenaCamera.fieldOfView=fov;}
                OptionsSection(557,"GRAPHICS");
                OptionsText(new Rect(102,609,245,44),"Quality preset",optionsLabel,PauseInk);
                for(int slot=0;slot<GraphicsDisplayOrder.Length;slot++)
                {int preset=GraphicsDisplayOrder[slot];if(OptionsButton(new Rect(358+slot*139,610,139,44),GraphicsNames[preset],graphicsPreset==preset))ApplyGraphicsPreset(preset,true);}
                Fill(new Rect(85,807,831,1),OptionsBorder);
                if(MenuBackButton(new Rect(101,817,220,48)))
                {SaveAudioSettings();frontScreen=optionsReturn;}

                OptionsText(new Rect(580,823,330,42),"Settings saved automatically",optionsSmall,PauseMuted);
            }
            finally{GUI.matrix=previous;}
        }
    }
}
