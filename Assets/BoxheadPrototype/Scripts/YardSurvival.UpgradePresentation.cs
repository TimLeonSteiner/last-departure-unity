using UnityEngine;
using UnityEngine.InputSystem;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        static readonly string[] UpgradeArtNames = { "Damage", "Vitality", "Reload", "Core", "Supplies" };
        static readonly string[] UpgradeBenefits = { "+1 weapon damage", "+25 maximum health", "15% faster reloads", "+100 core strength", "+12 scrap" };
        static readonly string[] UpgradeDetails = { "Applies to all weapons, including future unlocks.", "Also restores 25 health.", "Faster reloads for the rest of this run.", "Also repairs 100 core strength.", "For barricades, traps, and turrets." };
        Texture2D upgradeCardAtlas, upgradeBackdrop;
        readonly float[] upgradeHoverBlend = new float[3], upgradeHoverClock = new float[3];
        int upgradeHoverFrame = -1;
        int upgradeSoundChoice = -1;
        GUIStyle upgradeHeadingStyle, upgradeCardStyle, upgradeBenefitStyle, upgradeDetailStyle, upgradeButtonStyle;
        static readonly Color DossierInk = new Color(.91f, .86f, .74f);
        const float UpgradeHoldDuration = 2;
        int upgradeHoldChoice = -1, upgradeHoldFrame = -1;
        float upgradeHoldSeconds;
        bool upgradeSelectionArmed;
        static readonly Key[] UpgradeKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Numpad1, Key.Numpad2, Key.Numpad3 };

        void ResetUpgradeHold()
        {
            upgradeHoldChoice = -1; upgradeHoldSeconds = 0;
            upgradeSelectionArmed = false; upgradeHoldFrame = -1;
            upgradeSoundChoice = -1;
        }

        int AdvanceUpgradeHold(int choice, bool pressed, bool allReleased, bool focused, float dt)
        {
            if (!focused) { ResetUpgradeHold(); return -1; }
            if (!upgradeSelectionArmed)
            { if (allReleased) upgradeSelectionArmed = true; return -1; }
            if (allReleased || choice < 0 || choice != upgradeHoldChoice)
            { upgradeHoldChoice = -1; upgradeHoldSeconds = 0; }
            // A hold must start on its card. Dragging across cards cannot transfer progress.
            if (pressed && choice >= 0) { upgradeHoldChoice = choice; upgradeHoldSeconds = 0; return -1; }
            if (choice < 0 || choice != upgradeHoldChoice) return -1;
            upgradeHoldSeconds += Mathf.Max(0, dt);
            if (upgradeHoldSeconds < UpgradeHoldDuration) return -1;
            int selected = upgradeHoldChoice; ResetUpgradeHold(); return selected;
        }

        int HoveredUpgradeCard()
        {
            if (Mouse.current == null) return -1;
            Vector2 pointer = Mouse.current.position.ReadValue();
            float scale = Mathf.Min(Screen.width / 1200f, Screen.height / 850f);
            pointer = new Vector2((pointer.x - (Screen.width - 1200 * scale) / 2) / scale,
                (Screen.height - pointer.y - (Screen.height - 850 * scale) / 2) / scale);
            for (int i = 0; i < 3; i++)
                if (new Rect(58 + i * 370, 164, 344, 548).Contains(pointer)) return i;
            return -1;
        }

        void TickUpgradeSelection() => TickUpgradeSelection(Time.unscaledDeltaTime);
        void TickUpgradeSelection(float dt)
        {
            if (upgradeHoldFrame >= 0 && Time.frameCount > upgradeHoldFrame + 1) ResetUpgradeHold();
            upgradeHoldFrame = Time.frameCount;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            int choice = -1;
            bool pressed = false, anyHeld = mouse != null && mouse.leftButton.isPressed;
            if (anyHeld) { choice = HoveredUpgradeCard(); pressed = mouse.leftButton.wasPressedThisFrame; }
            if (keyboard != null)
            {
                foreach (var key in UpgradeKeys)
                    if (keyboard[key].isPressed)
                    {
                        // Simultaneous inputs cancel rather than selecting an arbitrary card.
                        if (anyHeld) { choice = -1; pressed = false; break; }
                        anyHeld = true; choice = System.Array.IndexOf(UpgradeKeys, key) % 3;
                        pressed = keyboard[key].wasPressedThisFrame;
                    }
            }
            int soundChoice = Application.isFocused ? (choice >= 0 ? choice : HoveredUpgradeCard()) : -1;
            if (soundChoice != upgradeSoundChoice)
            {
                upgradeSoundChoice = soundChoice;
                if (soundChoice >= 0) PlayMenuFeedback(false, true);
            }
            int selected = AdvanceUpgradeHold(choice, pressed, !anyHeld, Application.isFocused, dt);
            if (selected >= 0) ChooseCoreUpgrade(selected);
        }

        void EnsureUpgradePresentation()
        {
            if (upgradeCardAtlas != null && upgradeHeadingStyle != null) return;
            upgradeCardAtlas = Resources.Load<Texture2D>("UpgradeCards/AnatomicalCards");
            upgradeBackdrop = Resources.Load<Texture2D>("UpgradeCards/VascularBackdrop");
            var font = Resources.Load<Font>("UpgradeCards/CormorantGaramond");
            upgradeHeadingStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = new RectOffset() };
            upgradeCardStyle = new GUIStyle(upgradeHeadingStyle) { fontSize = 27, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            upgradeBenefitStyle = new GUIStyle(upgradeHeadingStyle) { fontSize = 25, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            upgradeDetailStyle = new GUIStyle(upgradeHeadingStyle) { fontSize = 21, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            upgradeButtonStyle = new GUIStyle(upgradeHeadingStyle) { fontSize = 23 };
        }

        void DossierLabel(Rect rect, string text, GUIStyle style, Color color)
        {
            Color previous = style.normal.textColor;
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
            style.normal.textColor = previous;
        }

        void DrawUpgradePulse(float x, float y)
        {
            Vector2[] points = { new Vector2(0,0), new Vector2(35,0), new Vector2(40,-3),
                new Vector2(45,0), new Vector2(50,-19), new Vector2(55,13),
                new Vector2(60,-6), new Vector2(65,0), new Vector2(108,0) };
            Matrix4x4 matrix = GUI.matrix;
            // Unscaled time keeps the ECG moving while the upgrade selection pauses gameplay.
            float sweep = Mathf.Repeat(Time.unscaledTime / .9f, 1) * 132;
            for (int i = 1; i < points.Length; i++)
            {
                Vector2 delta = points[i] - points[i-1];
                int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 3));
                for (int step = 0; step < steps; step++)
                {
                    Vector2 point = points[i-1] + delta * (step / (float)steps);
                    float behind = sweep - point.x;
                    float trail = behind >= 0 && behind < 24 ? 1 - behind / 24 : 0;
                    float head = Mathf.Exp(-Mathf.Pow(behind / 3, 2));
                    Vector2 origin = new Vector2(x,y) + point;
                    GUI.matrix = matrix * Matrix4x4.TRS(origin, Quaternion.Euler(0, 0,
                        Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg), Vector3.one);
                    float length = delta.magnitude / steps + .35f;
                    Fill(new Rect(0,-2,length,5), new Color(.64f,.14f,.09f,trail*.22f));
                    Color line = Color.Lerp(new Color(.40f,.33f,.25f), DossierInk, Mathf.Max(trail,head));
                    Fill(new Rect(0,-head*.4f,length,1.2f+head*.8f),line);
                    GUI.matrix = matrix;
                }
            }
        }

        void DrawDossierUpgrades(float width, float height)
        {
            EnsureUpgradePresentation();
            int hoveredCard = Application.isFocused ? HoveredUpgradeCard() : -1;
            if (upgradeHoverFrame != Time.frameCount)
            {
                bool consecutive = upgradeHoverFrame == Time.frameCount - 1;
                for (int i = 0; i < 3; i++)
                {
                    if (!consecutive) { upgradeHoverBlend[i] = 0; upgradeHoverClock[i] = 0; }
                    if (i == hoveredCard && upgradeHoverBlend[i] <= 0) upgradeHoverClock[i] = 0;
                    upgradeHoverBlend[i] = Mathf.MoveTowards(upgradeHoverBlend[i], i == hoveredCard ? 1 : 0,
                        Time.unscaledDeltaTime * 7);
                    upgradeHoverClock[i] += Time.unscaledDeltaTime;
                }
                upgradeHoverFrame = Time.frameCount;
            }
            Fill(new Rect(0, 0, width, height), new Color(.027f, .023f, .022f, 1));
            if (upgradeBackdrop != null) GUI.DrawTexture(new Rect(0, 0, width, height), upgradeBackdrop, ScaleMode.StretchToFill);
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(width / 1200f, height / 850f);
            GUI.matrix = previous * Matrix4x4.TRS(new Vector3((width - 1200 * scale) / 2, (height - 850 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            try
            {
                Color red = new Color(.32f, .075f, .06f);
                Color ink = new Color(.13f, .095f, .075f);
                DossierLabel(new Rect(160, 48, 880, 62), "W A V E  " + wave + "  C O M P L E T E", upgradeHeadingStyle, DossierInk);
                DrawUpgradePulse(108, 85); DrawUpgradePulse(984, 85);
                Fill(new Rect(400, 118, 400, 1), new Color(.50f, .41f, .30f));
                DossierLabel(new Rect(200, 124, 800, 30), "C H O O S E   A N   U P G R A D E", upgradeDetailStyle, DossierInk);
                for (int i = 0; i < 3; i++)
                {
                    int id = coreOffers[i];
                    Rect card = new Rect(58 + i * 370, 164, 344, 548);
                    float hover = Mathf.SmoothStep(0, 1, upgradeHoverBlend[i]);
                    float phase = upgradeHoverClock[i] % .9f;
                    // Two short beats followed by a rest, rather than a uniform breathing loop.
                    float beat = Mathf.Exp(-Mathf.Pow((phase - .12f) / .055f, 2))
                        + .58f * Mathf.Exp(-Mathf.Pow((phase - .29f) / .07f, 2));
                    float pulseScale = 1 + hover * (.007f + .019f * beat);
                    Matrix4x4 cardMatrix = GUI.matrix;
                    Vector3 pivot = card.center;
                    GUI.matrix = cardMatrix * Matrix4x4.Translate(pivot + Vector3.up * (-3 * hover))
                        * Matrix4x4.Scale(new Vector3(pulseScale, pulseScale, 1)) * Matrix4x4.Translate(-pivot);
                    Color previousTint = GUI.color;
                    GUI.color = Color.Lerp(Color.white, new Color(1, .97f, .90f), hover);
                    // Whole-card atlas: art, parchment, vascular edges, bevels and button are one authored surface.
                    if (upgradeCardAtlas != null)
                    {
                        int column = id % 3, row = id / 3;
                        float insetX = .5f / upgradeCardAtlas.width, insetY = .5f / upgradeCardAtlas.height;
                        Rect uv = new Rect(column / 3f + insetX, 1 - (row + 1) / 2f + insetY,
                            1 / 3f - 2 * insetX, .5f - 2 * insetY);
                        Color cardTint = GUI.color;
                        for (int layer = 3; layer >= 1 && hover > 0; layer--)
                        {
                            float spread = layer * (2 + 2 * beat);
                            GUI.color = new Color(.65f, .08f, .045f, hover * (.05f + .055f * beat));
                            GUI.DrawTextureWithTexCoords(new Rect(card.x - spread, card.y - spread,
                                card.width + spread * 2, card.height + spread * 2), upgradeCardAtlas, uv);
                        }
                        GUI.color = cardTint;
                        GUI.DrawTextureWithTexCoords(card, upgradeCardAtlas, uv);
                    }
                    GUI.color = previousTint;
                    DossierLabel(new Rect(card.x + 21, card.y + 18, 24, 26), (i + 1).ToString(), upgradeDetailStyle, red);
                    DossierLabel(new Rect(card.x + 20, card.y + 269, card.width - 40, 65), UpgradeNames[id], upgradeCardStyle, ink);
                    Fill(new Rect(card.x + 39, card.y + 335, card.width - 78, 1), new Color(.35f, .23f, .16f, .45f));
                    DossierLabel(new Rect(card.x + 29, card.y + 343, card.width - 58, 43), UpgradeBenefits[id], upgradeBenefitStyle, red);
                    DossierLabel(new Rect(card.x + 29, card.y + 390, card.width - 58, 62), UpgradeDetails[id], upgradeDetailStyle, ink);
                    Rect button = new Rect(card.x + 37, card.y + 457, card.width - 74, 58);
                    float progress = upgradeHoldChoice == i ? Mathf.Clamp01(upgradeHoldSeconds / UpgradeHoldDuration) : 0;
                    if (progress > 0) Fill(new Rect(button.x + 6, button.yMax - 8, (button.width - 12) * progress, 3), DossierInk);
                    DossierLabel(button, "HOLD TO UNLOCK", upgradeButtonStyle, DossierInk);
                    GUI.matrix = cardMatrix;
                }
                DossierLabel(new Rect(225, 738, 750, 34), "Hold left mouse on a card / hold 1–3", upgradeDetailStyle, DossierInk);
                if (Time.time < subtitleUntil && !string.IsNullOrEmpty(announcerSubtitle))
                    DossierLabel(new Rect(100, 791, 1000, 38), announcerSubtitle, upgradeDetailStyle, new Color(.68f, .61f, .50f));
            }
            finally { GUI.matrix = previous; }
        }
    }
}
