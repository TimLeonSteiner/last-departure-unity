using System.Collections.Generic;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        AudioSource menuFeedback;
        AudioClip menuHover,menuConfirm;
        AudioClip skillSelect,skillUnlock;
        string hoveredMenuItem;
        float nextMenuHover;
        SuspendedCore menuCore;
        readonly Dictionary<string,float> menuHighlights=new Dictionary<string,float>();
        void TickMenuPresentation()
        {
            if(menuCore==null)menuCore=FindAnyObjectByType<SuspendedCore>();
            if(menuCore!=null)menuCore.SetMenuPresentation(frontScreen==FrontScreen.Difficulty||frontScreen==FrontScreen.Encounters||frontScreen==FrontScreen.Home||frontScreen==FrontScreen.Launch||frontScreen==FrontScreen.Lobby||(frontScreen==FrontScreen.Options&&optionsReturn==FrontScreen.Home));
            if(menuFeedback!=null)menuFeedback.mute=!soundEnabled;
        }
        void PlayMenuFeedback(bool confirm, bool skill = false)
        {
            if(!soundEnabled||dedicatedServer)return;
            if(!confirm&&Time.unscaledTime<nextMenuHover)return;
            nextMenuHover=Time.unscaledTime+(confirm?.16f:.10f);
            if(menuFeedback==null)
            {
                var obj=new GameObject("Menu feedback");obj.transform.SetParent(transform,false);
                menuFeedback=obj.AddComponent<AudioSource>();menuFeedback.playOnAwake=false;menuFeedback.spatialBlend=0;menuFeedback.ignoreListenerPause=true;
                menuHover=Resources.Load<AudioClip>("Announcer/menu_hover");menuConfirm=Resources.Load<AudioClip>("Announcer/menu_confirm");
                skillSelect=Resources.Load<AudioClip>("SkillFeedback/Select");skillUnlock=Resources.Load<AudioClip>("SkillFeedback/Unlock");
            }
            menuFeedback.mute=false;menuFeedback.volume=soundVolume;
            var clip=skill?(confirm?skillUnlock:skillSelect):(confirm?menuConfirm:menuHover);
            if(clip!=null){menuFeedback.PlayOneShot(clip,confirm?.48f:.28f);}
        }
        float MenuHighlight(Rect rect,string key,bool selected=false)
        {
            bool hover=GUI.enabled&&rect.Contains(Event.current.mousePosition);
            if(Event.current.type==EventType.Repaint)
            {
                if(hover&&hoveredMenuItem!=key){hoveredMenuItem=key;PlayMenuFeedback(false);}
                else if(!hover&&hoveredMenuItem==key)hoveredMenuItem=null;
                menuHighlights.TryGetValue(key,out float amount);
                menuHighlights[key]=Mathf.MoveTowards(amount,hover||selected?1:0,Time.unscaledDeltaTime*8);
            }
            return menuHighlights.TryGetValue(key,out float value)?value:0;
        }
        bool FeedbackButton(Rect rect,string label,GUIStyle style)
        {
            bool clicked=GUI.Button(rect,label,style);
            if(clicked)PlayMenuFeedback(true);
            return clicked;
        }
    }
}
