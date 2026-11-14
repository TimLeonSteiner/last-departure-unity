using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        GUIStyle dreadCaption, dreadValue, dreadSmall, dreadNumber;
        static readonly Color DreadMuted = new Color(.66f, .65f, .60f);
        static readonly Color DreadRule = new Color(.8f, .77f, .69f, .38f);

        void EnsureDreadHud()
        {
            if (dreadCaption != null) return;
            var font = Resources.Load<Font>("PauseMenu/InterQuiet");
            dreadCaption = new GUIStyle { font = font, fontSize = 12, alignment = TextAnchor.MiddleLeft };
            dreadSmall = new GUIStyle(dreadCaption) { fontSize = 10 };
            var display = Resources.Load<Font>("PauseMenu/InterDisplayBlack");
            dreadValue = new GUIStyle(dreadCaption) { font = display, fontSize = 20 };
            dreadNumber = new GUIStyle(dreadCaption) { font = display, fontSize = 38 };
        }
        void DreadLabel(Rect rect, string text, GUIStyle style, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var previous = style.alignment;style.alignment = align;
            HudLabel(rect, text, style, color);style.alignment = previous;
        }
        void DreadFrame(Rect rect, Color color)
        {
            Fill(new Rect(rect.x,rect.y,rect.width,1),color);
            Fill(new Rect(rect.x,rect.yMax-1,rect.width,1),color);
            Fill(new Rect(rect.x,rect.y,1,rect.height),color);
            Fill(new Rect(rect.xMax-1,rect.y,1,rect.height),color);
        }
        void DrawMinimalVitals(float width, float height)
        {
            EnsureDreadHud();
            DreadLabel(new Rect(30,28,22,36),"//",dreadValue,HudRed);
            DreadLabel(new Rect(58,30,75,32),"W A V E",dreadCaption,HudWhite);
            DreadLabel(new Rect(134,20,85,48),wave.ToString("00"),dreadNumber,HudWhite);
            Fill(new Rect(32,73,180,1),DreadRule);
            DreadLabel(new Rect(32,82,100,24),"S C R A P",dreadCaption,DreadMuted);
            DreadLabel(new Rect(132,78,80,30),supplies.ToString(),dreadValue,HudWhite,TextAnchor.MiddleRight);

            DreadLabel(new Rect(32,height-112,180,22),"H E A L T H",dreadCaption,DreadMuted);
            DreadLabel(new Rect(30,height-90,88,48),Mathf.Max(0,health).ToString(),dreadNumber,health<=30?HudRed:HudWhite);
            DreadLabel(new Rect(120,height-80,92,32),"/ "+maxPlayerHealth,dreadValue,DreadMuted);
            DrawDreadMeter(new Rect(32,height-33,180,5),health/(float)maxPlayerHealth);
        }
        void DrawMinimalAmmo(float width,float height)
        {
            EnsureDreadHud();
            bool melee=WeaponCatalog.IsMelee(weaponId);
            DreadLabel(new Rect(width-248,height-112,216,22),melee?"M E L E E":"A M M U N I T I O N",dreadCaption,DreadMuted,TextAnchor.MiddleRight);
            if(melee) DreadLabel(new Rect(width-248,height-90,216,48),"READY",dreadValue,HudWhite,TextAnchor.MiddleRight);
            else
            {
                float reserveWidth=dreadValue.CalcSize(new GUIContent("/ "+reserveAmmo.ToString("00"))).x;
                float reserveX=width-32-reserveWidth;
                float currentWidth=dreadNumber.CalcSize(new GUIContent(ammunition.ToString("00"))).x;
                DreadLabel(new Rect(reserveX,height-79,reserveWidth,34),"/ "+reserveAmmo.ToString("00"),dreadValue,DreadMuted);
                DreadLabel(new Rect(reserveX-currentWidth-9,height-90,currentWidth,48),ammunition.ToString("00"),dreadNumber,ammunition==0?HudRed:HudWhite);
                DrawAmmoSymbol(new Rect(reserveX-currentWidth-39,height-72,23,23));
            }
            DreadLabel(new Rect(width-280,height-35,248,20),!HasEquippedWeapon?"UNARMED":reloading?"RELOADING":ActiveWeapon.name.ToUpperInvariant(),dreadSmall,reloading?HudRed:DreadMuted,TextAnchor.MiddleRight);
        }
        void DrawDreadWeaponGlyph(Rect rect,int id,Color color)
        {
            if(!dreadWeaponImages.TryGetValue(id,out var texture))
            { texture=Resources.Load<Texture2D>("HudWeapons/"+id.ToString("00")); if(texture!=null)dreadWeaponImages[id]=texture; }
            if(texture==null){DreadLabel(rect,InventoryNames[id].Replace("\n"," "),dreadSmall,color,TextAnchor.MiddleCenter);return;}
            Color previous=GUI.color;GUI.color=new Color(1,1,1,color==HudWhite?1:.65f);
            GUI.DrawTexture(rect,texture,ScaleMode.ScaleToFit,true);GUI.color=previous;
        }
        readonly System.Collections.Generic.Dictionary<int,Texture2D> dreadWeaponImages=new System.Collections.Generic.Dictionary<int,Texture2D>();
        void DrawDreadMeter(Rect rect,float fraction)
        {
            Fill(rect,new Color(.02f,.02f,.02f,.65f));DreadFrame(rect,DreadMuted);
            Fill(new Rect(rect.x+1,rect.y+1,(rect.width-2)*Mathf.Clamp01(fraction),rect.height-2),HudRed);
        }
        void DreadLine(Vector2 a,Vector2 b,Color color,float thickness=1)
        {
            var matrix=GUI.matrix;GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(a.x,a.y,0),Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg),Vector3.one);
            Fill(new Rect(0,0,Vector2.Distance(a,b),thickness),color);GUI.matrix=matrix;
        }
        void DrawAmmoSymbol(Rect r)
        {
            for(int i=0;i<3;i++){float x=r.x+i*7;Fill(new Rect(x,r.y+6,4,14),HudWhite);
                DreadLine(new Vector2(x,r.y+6),new Vector2(x+2,r.y),HudWhite,2);
                DreadLine(new Vector2(x+2,r.y),new Vector2(x+4,r.y+6),HudWhite,2);
                Fill(new Rect(x,r.y+21,4,2),HudWhite);}
        }
        void DrawTabletSymbol(Rect r)
        {
            GUI.DrawTexture(new Rect(r.x+3,r.y+4,20,20),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,HudWhite,0,10);
            DreadLine(new Vector2(r.x+7,r.y+20),new Vector2(r.x+19,r.y+8),new Color(.12f,.13f,.12f),2);
        }
        void DrawCoffeeSymbol(Rect r)
        {
            DreadLine(new Vector2(r.x+2,r.y+12),new Vector2(r.x+5,r.y+23),HudWhite,2);
            DreadLine(new Vector2(r.x+5,r.y+23),new Vector2(r.x+17,r.y+23),HudWhite,2);
            DreadLine(new Vector2(r.x+17,r.y+23),new Vector2(r.x+20,r.y+12),HudWhite,2);
            Fill(new Rect(r.x+2,r.y+12,18,2),HudWhite);DreadFrame(new Rect(r.x+20,r.y+13,6,7),HudWhite);
            Fill(new Rect(r.x,r.y+27,24,1),DreadMuted);
            for(int i=0;i<3;i++){float x=r.x+5+i*5;DreadLine(new Vector2(x,r.y+9),new Vector2(x-1,r.y+5),DreadMuted);DreadLine(new Vector2(x-1,r.y+5),new Vector2(x+1,r.y+1),DreadMuted);}
        }
    }
}
