using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        bool audioCreditsVisible;
        Vector2 audioCreditsScroll;
        TextAsset audioCreditsText, weaponCreditsText;
        bool weaponCreditsSelected = true;
        void DrawAudioCredits(float width,float height)
        {
            if(frontScreen==FrontScreen.Game)return;
            if(!audioCreditsVisible)
            {
                // Entry points belong to their menu layout, never a global floating button.
                return;
            }
            float panelWidth=Mathf.Min(760,width-40),panelHeight=Mathf.Min(600,height-40);
            var panel=new Rect((width-panelWidth)/2,(height-panelHeight)/2,panelWidth,panelHeight);
            Fill(new Rect(0,0,width,height),new Color(0,0,0,.92f));
            GUI.Box(panel,"CREDITS");
            if(GUI.Button(new Rect(panel.x+16,panel.y+30,180,28),"Weapons & models")){weaponCreditsSelected=true;audioCreditsScroll=Vector2.zero;}
            if(GUI.Button(new Rect(panel.x+204,panel.y+30,180,28),"Audio")){weaponCreditsSelected=false;audioCreditsScroll=Vector2.zero;}
            if(audioCreditsText==null)audioCreditsText=Resources.Load<TextAsset>("SoundBibleCredits");
            if(weaponCreditsText==null)weaponCreditsText=Resources.Load<TextAsset>("WeaponCredits");
            var selected=weaponCreditsSelected?weaponCreditsText:audioCreditsText;
            string content=selected!=null?selected.text:"Credits unavailable.";
            var style=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=14};
            float textWidth=panelWidth-48;
            string[] lines=content.Replace("\r","").Split('\n');
            float textHeight=0;
            foreach(string line in lines)textHeight+=Mathf.Max(22,style.CalcHeight(new GUIContent(line),textWidth));
            audioCreditsScroll=GUI.BeginScrollView(new Rect(panel.x+16,panel.y+66,panelWidth-32,panelHeight-158),audioCreditsScroll,new Rect(0,0,textWidth,textHeight));
            float rowY=0;
            foreach(string line in lines)
            {
                float rowHeight=Mathf.Max(22,style.CalcHeight(new GUIContent(line),textWidth));
                var rect=new Rect(0,rowY,textWidth,rowHeight);
                int linkStart=line.IndexOf("https://",System.StringComparison.Ordinal);
                if(linkStart>=0)
                {
                    string url=line.Substring(linkStart).Split(' ')[0];
                    if(GUI.Button(rect,line,style))Application.OpenURL(url);
                }
                else GUI.Label(rect,line,style);
                rowY+=rowHeight;
            }
            GUI.EndScrollView();
            float y=panel.yMax-78;
            if(!weaponCreditsSelected && GUI.Button(new Rect(panel.x+16,y,140,28),"SoundBible sources"))Application.OpenURL("https://soundbible.com/tags-gun.html");
            if(!weaponCreditsSelected && GUI.Button(new Rect(panel.x+164,y,110,28),"CC BY 3.0"))Application.OpenURL("https://creativecommons.org/licenses/by/3.0/");
            if(!weaponCreditsSelected && GUI.Button(new Rect(panel.x+282,y,140,28),"Sampling Plus 1.0"))Application.OpenURL("https://creativecommons.org/licenses/sampling+/1.0/");
            if(GUI.Button(new Rect(panel.x+16,panel.yMax-42,panelWidth-32,28),"Close"))audioCreditsVisible=false;
        }
    }
}
