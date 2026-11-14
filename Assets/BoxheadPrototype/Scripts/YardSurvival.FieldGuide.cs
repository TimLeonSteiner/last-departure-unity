using UnityEngine;
using UnityEngine.InputSystem;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        enum PreparationStep { None, OpenLink, SelectBlueprint, PlaceBlueprint, BuildBlueprint }
        PreparationStep preparationStep;
        Transform preparationTarget;
        float preparationStarted;
        bool preparationStartedThisRun;
        Texture2D preparationOutline, preparationFill, preparationGlow;
        [System.NonSerialized] GUIStyle preparationTitle, preparationCaption, preparationKey, preparationSmall;
        static readonly Color ProjectionInk = new Color(.4f,.93f,1f);
        bool PreparationActive => preparationStep != PreparationStep.None;
        bool PreparationKeyboardVisible => preparationStep == PreparationStep.OpenLink && !HologramBlocking && frontScreen == FrontScreen.Game && !dead;

        void ResetPreparationTutorial()
        {
            preparationStep = PreparationStep.None; preparationTarget = null;
            // A new run resets wave to zero after this call. StartWave must not re-arm the lesson.
            preparationStartedThisRun = false;
        }
        void BeginPreparationTutorial()
        {
            if (wave != 1 || preparationStartedThisRun || dead || !CoreMode) return;
            preparationStartedThisRun = true;
            preparationStep = PreparationStep.OpenLink;
            preparationStarted = Time.unscaledTime;
            aiming = false; building = false; ReleaseMouse();
            if (ghost != null) ghost.gameObject.SetActive(false);
        }
        void FinishPreparationTutorial()
        {
            preparationStep = PreparationStep.None; preparationTarget = null;
            if (!fighting && !dead) nextWaveCountdown = 15;
            if (!HologramBlocking && frontScreen == FrontScreen.Game && !showHelp) CaptureMouse();
            buildFireBlocked = true;
        }
        void OnPreparationMenuOpened()
        {
            if (preparationStep == PreparationStep.OpenLink) preparationStep = PreparationStep.SelectBlueprint;
        }
        void OnPreparationBlueprintSelected()
        {
            if (PreparationActive) { preparationStep = PreparationStep.PlaceBlueprint; preparationTarget = null; }
        }
        void OnPreparationPlaced(Transform target)
        {
            if (preparationStep != PreparationStep.PlaceBlueprint) return;
            if (target == null || ConstructionReady(target)) { FinishPreparationTutorial(); return; }
            preparationTarget = target; preparationStep = PreparationStep.BuildBlueprint;
        }
        void TickPreparationTutorial()
        {
            if (!PreparationActive || showHelp || frontScreen != FrontScreen.Game) return;
            if (dead || fighting) { preparationStep = PreparationStep.None; preparationTarget = null; return; }
            if (preparationStep == PreparationStep.SelectBlueprint && !HologramBlocking)
            { preparationStep = PreparationStep.OpenLink; preparationStarted = Time.unscaledTime; ReleaseMouse(); }
            if (preparationStep == PreparationStep.BuildBlueprint)
            {
                if (preparationTarget == null) preparationStep = PreparationStep.PlaceBlueprint;
                else if (ConstructionReady(preparationTarget)) FinishPreparationTutorial();
            }
        }
        int PreparationBinding(Control action)
        {
            var binding = bindings[(int)action];
            return binding.primary != 0 ? binding.primary : binding.secondary;
        }
        void EnsurePreparationArt()
        {
            if (preparationOutline == null) preparationOutline = Resources.Load<Texture2D>("Tutorial/KeyOutline");
            if (preparationFill == null) preparationFill = Resources.Load<Texture2D>("Tutorial/KeyFill");
            if (preparationGlow == null) preparationGlow = Resources.Load<Texture2D>("Tutorial/KeyGlow");
            if (preparationTitle != null && preparationTitle.fontSize == 30 && preparationKey.alignment == TextAnchor.MiddleCenter) return;
            var font = Resources.Load<Font>("PauseMenu/InterQuiet") ?? GUI.skin.font;
            preparationTitle = new GUIStyle(GUI.skin.label) { font=font, fontSize=30, alignment=TextAnchor.MiddleCenter, wordWrap=true };
            preparationCaption = new GUIStyle(preparationTitle) { fontSize=19 };
            preparationKey = new GUIStyle(preparationTitle) { fontSize=17, wordWrap=false };
            preparationSmall = new GUIStyle(preparationTitle) { fontSize=13 };
            preparationSmall.normal.textColor=ProjectionInk; preparationSmall.hover.textColor=Color.white;
        }
        void ProjectionLabel(Rect rect, string text, GUIStyle style, Color color)
        {
            Color old=GUI.contentColor;GUI.contentColor=color;
            style.normal.textColor=Color.white;GUI.Label(rect,text,style);GUI.contentColor=old;
        }
        void ProjectionTexture(Rect rect, Texture2D texture, Color color)
        {
            if (texture == null) return;
            Color previous=GUI.color; GUI.color=color; GUI.DrawTexture(rect,texture); GUI.color=previous;
        }
        string ProjectionKeyName(int code)
        {
            if (code <= 0) return code==0?"UNBOUND":BindingName(code).ToUpperInvariant();
            switch ((Key)code)
            {
                case Key.Backquote:return "^"; case Key.Equals:return "`";
                case Key.Tab:return "TAB"; case Key.Escape:return "ESC";
                case Key.LeftShift:case Key.RightShift:return "SHIFT";
                case Key.LeftCtrl:case Key.RightCtrl:return "CTRL";
                case Key.LeftAlt:return "ALT"; case Key.RightAlt:return "ALT GR";
                case Key.LeftMeta:case Key.RightMeta:return "WIN";
                case Key.CapsLock:return "CAPS";case Key.Backspace:return "BACK";
                case Key.Enter:return "ENTER";case Key.Space:return "SPACE";
                case Key.ContextMenu:return "MENU";
                default:return BindingName(code).ToUpperInvariant();
            }
        }
        bool projectionFoundBinding;
        Vector2 projectionSelectedAnchor;
        static Vector2 ProjectKeyboardPoint(float x, float y)
        {
            // A tilted projection plane: the far row recedes, the near row widens.
            float depth = 1f / (1f - (y - 440f) * .00035f);
            Vector2 point = new Vector2((x - 600f) * .98f * depth, (y - 440f) * .96f * depth);
            const float roll = 0f * Mathf.Deg2Rad;
            return new Vector2(600f + point.x * Mathf.Cos(roll) - point.y * Mathf.Sin(roll),
                440f + point.x * Mathf.Sin(roll) + point.y * Mathf.Cos(roll));
        }
        void ProjectionLine(Vector2 from,Vector2 to,Color ink)
        {
            Matrix4x4 saved=GUI.matrix;
            GUI.matrix=saved*Matrix4x4.TRS(new Vector3(from.x,from.y,0),Quaternion.Euler(0,0,Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg),Vector3.one);
            Fill(new Rect(0,0,(to-from).magnitude,1.2f),ink);GUI.matrix=saved;
        }
        void ProjectionKey(float x,float y,float units,Key key,int selected)
        {
            float keyWidth=units*64-4;
            Vector2 origin=ProjectKeyboardPoint(x,y);
            Vector2 along=(ProjectKeyboardPoint(x+keyWidth,y)-origin)/keyWidth;
            Vector2 down=(ProjectKeyboardPoint(x,y+49)-origin)/49;
            Matrix4x4 saved=GUI.matrix, plane=Matrix4x4.identity;
            plane.m00=along.x;plane.m10=along.y;plane.m01=down.x;plane.m11=down.y;
            plane.m03=origin.x;plane.m13=origin.y;
            GUI.matrix=saved*plane;
            var r=new Rect(0,0,keyWidth,49);
            bool lit=(int)key==selected || (int)key==projectionSecondaryBinding;
            projectionFoundBinding |= lit;
            float pulse=.88f+.12f*Mathf.Sin(Time.unscaledTime*3);
            if(lit)
            {
                ProjectionTexture(new Rect(r.x-12,r.y-10,r.width+24,r.height+20),preparationGlow,new Color(.1f,.9f,1,pulse*.7f));
                ProjectionTexture(r,preparationFill,new Color(.2f,.92f,1,pulse));
            }
            ProjectionTexture(r,preparationOutline,new Color(.4f,.94f,1,lit?1:.65f));
            preparationKey.normal.textColor=lit?new Color(.015f,.13f,.16f):new Color(.48f,.78f,.83f,.85f);
            GUI.Label(new Rect(r.x+6,r.y+6,r.width-12,r.height-12),ProjectionKeyName((int)key),preparationKey);
            GUI.matrix=saved;
            if((int)key==selected) projectionSelectedAnchor=ProjectKeyboardPoint(x+keyWidth*.5f,y-3);
        }
        void ProjectionRow(float y, Key[] keys, float[] widths, int selected)
        {
            float x=110;
            for(int i=0;i<keys.Length;i++) { float w=widths==null?1:widths[i]; ProjectionKey(x,y,w,keys[i],selected); x+=64*w; }
        }
        void DrawPreparationKeyboard(float width,float height, bool dodgeLesson = false)
        {
            projectionSecondaryBinding = dodgeLesson ? PreparationBinding(Control.Slide) : 0;
            EnsurePreparationArt();
            Fill(new Rect(0,0,width,height),new Color(.005f,.018f,.025f,.35f));
            Matrix4x4 previous=GUI.matrix;
            float scale=Mathf.Min(width/1200,height/760);
            GUI.matrix=previous*Matrix4x4.TRS(new Vector3((width-1200*scale)/2,(height-760*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            int selected=PreparationBinding(dodgeLesson ? Control.Dodge : Control.Hologram);
            float reveal=Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-(dodgeLesson ? chargerLessonStarted : preparationStarted))/.7f));
            Color previousColor=GUI.color;GUI.color=new Color(1,1,1,reveal);
            ProjectionLabel(new Rect(300,55,600,30),dodgeLesson ? "CHARGER INCOMING" : "W A V E   1   C O M P L E T E",preparationCaption,new Color(.7f,.87f,.9f));
            string shortcut=ProjectionKeyName(selected);
            ProjectionLabel(new Rect(110,155,980,52),dodgeLesson ? (selected == 0 ? "Move sideways to avoid the charge." : "Press " + shortcut + " to dodge.") : (selected==0?"Open your field link to prepare for the next wave.":"Press "+shortcut+" to prepare for the next wave."),preparationTitle,new Color(.7f,.97f,1));
            ProjectionLabel(new Rect(150,209,900,32),dodgeLesson ? "Or press " + ProjectionKeyName(projectionSecondaryBinding) + " while moving to slide out of its path." : "Open your field link",preparationCaption,new Color(.4f,.67f,.72f));
            projectionFoundBinding=false;
            ProjectionKey(110,276,1.25f,Key.Escape,selected);
            for(int i=0;i<12;i++)ProjectionKey(222+i*70,276,1,(Key)((int)Key.F1+i),selected);
            ProjectionRow(332,new[]{Key.Backquote,Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4,Key.Digit5,Key.Digit6,Key.Digit7,Key.Digit8,Key.Digit9,Key.Digit0,Key.Minus,Key.Equals,Key.Backspace},new[]{1f,1,1,1,1,1,1,1,1,1,1,1,1,2},selected);
            ProjectionRow(386,new[]{Key.Tab,Key.Q,Key.W,Key.E,Key.R,Key.T,Key.Y,Key.U,Key.I,Key.O,Key.P,Key.LeftBracket,Key.RightBracket,Key.Backslash},new[]{1.5f,1,1,1,1,1,1,1,1,1,1,1,1,1.5f},selected);
            ProjectionRow(440,new[]{Key.CapsLock,Key.A,Key.S,Key.D,Key.F,Key.G,Key.H,Key.J,Key.K,Key.L,Key.Semicolon,Key.Quote,Key.Enter},new[]{1.75f,1,1,1,1,1,1,1,1,1,1,1,2.25f},selected);
            ProjectionRow(494,new[]{Key.LeftShift,Key.Z,Key.X,Key.C,Key.V,Key.B,Key.N,Key.M,Key.Comma,Key.Period,Key.Slash,Key.RightShift},new[]{2.25f,1,1,1,1,1,1,1,1,1,1,2.75f},selected);
            ProjectionRow(548,new[]{Key.LeftCtrl,Key.LeftMeta,Key.LeftAlt,Key.Space,Key.RightAlt,Key.RightMeta,Key.ContextMenu,Key.RightCtrl},new[]{1.25f,1.25f,1.25f,6.25f,1.25f,1.25f,1.25f,1.25f},selected);
            if(projectionFoundBinding)
            {
                Vector2 end=projectionSelectedAnchor;
                Vector2 elbow=new Vector2(end.x+65,255);
                ProjectionLine(end,elbow,new Color(.4f,.95f,1,.65f));
                ProjectionLine(elbow,elbow+Vector2.right*110,new Color(.4f,.95f,1,.65f));
            }
            // Remapped mouse buttons, arrows or numpad keys remain visible even outside the main keyboard.
            if(!projectionFoundBinding && selected!=0)
            {
                ProjectionTexture(new Rect(470,608,260,46),preparationGlow,ProjectionInk);
                ProjectionLabel(new Rect(470,608,260,46),shortcut,preparationCaption,ProjectionInk);
            }
            else
            {
                for(int i=0;i<3;i++)Fill(new Rect(350+i*36,625+i*4,500-i*72,1),new Color(.25f,.8f,.9f,.14f-i*.03f));
            }
            if (dodgeLesson) ProjectionLabel(new Rect(200,670,800,30),shortcut + "  DODGE     /     " + ProjectionKeyName(projectionSecondaryBinding) + "  SLIDE",preparationSmall,ProjectionInk);
            if (!dodgeLesson)
            {
                ProjectionLabel(new Rect(350,670,500,30),"1 / 3     /     PREPARE",preparationSmall,new Color(.55f,.8f,.84f));
                if(GUI.Button(new Rect(450,708,300,28),"Open field link",preparationSmall))OpenHologram(1);
                if(GUI.Button(new Rect(1000,710,150,25),"Skip tutorial",preparationSmall))FinishPreparationTutorial();
            }
            GUI.color=previousColor;GUI.matrix=previous;
        }
        float chargerLessonStarted;
        int projectionSecondaryBinding;
        bool chargerLessonActive;

        void ShowWaveLesson()
        {
            chargerLessonActive = wave == ChargerIntroWave;
            if (chargerLessonActive) chargerLessonStarted = Time.unscaledTime;
            string tip = wave switch
            {
                3 => "SPITTER · Keep moving and stay out of acid pools.",
                4 => "SCREAMER · When you see !, interrupt the wind-up before it calls more infected.",
                5 => "ARMORED WORKER · Keep space to move around its melee attacks.",
                6 => "BRUTE · Keep your distance. Heavy hits break barricades faster.",
                ChargerIntroWave => "CHARGER · Dodge sideways as it rushes.",
                _ => "WAVE " + wave + " – Hold the center!"
            };
            ShowBanner(tip);
        }

        void DrawPreparationHint(float width,float height)
        {
            if(!PreparationActive || PreparationKeyboardVisible || dead || showHelp || frontScreen!=FrontScreen.Game)return;
            if (building && !HologramBlocking) return;
            EnsurePreparationArt();
            string text;
            if(preparationStep==PreparationStep.SelectBlueprint)
                text="2 / 3   SELECT A DEFENSE   /   BUILD tab: choose a blueprint";
            else if(preparationStep==PreparationStep.BuildBlueprint)
                text="3 / 3   BUILD   /   Move close and hold "+ControlHint(Control.Fire)+" on your blueprint";
            else text="3 / 3   PLACE   /   Find a clear spot, then press "+ControlHint(Control.Fire);
            float w=Mathf.Min(900,width-48);
            float top=HologramBlocking?24:122;
            Fill(new Rect((width-w)/2,top,w,58),new Color(.005f,.035f,.045f,.78f));
            Fill(new Rect((width-w)/2,top,2,58),ProjectionInk);
            ProjectionLabel(new Rect((width-w)/2+14,top+3,w-28,50),text,preparationCaption,ProjectionInk);
            if(HologramBlocking && GUI.Button(new Rect(width-160,86,140,26),"Skip tutorial",preparationSmall))FinishPreparationTutorial();
        }
    }
}
