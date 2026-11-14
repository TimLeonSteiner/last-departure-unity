using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        enum Control { Forward, Back, Left, Right, Jump, Slide, Fire, Aim, Reload, Pickup, Build, Barricade, Turret, Trap, Rotate, Wave, Restart, Music, Sound, Flashlight, SlotSpin, Barrel, Caffeine, Hologram, Dodge, DropWeapon, EquipHammer }
        sealed class Binding
        {
            public readonly string label;
            public readonly int primaryDefault, secondaryDefault;
            public int primary, secondary;
            public Binding(string label, int primary, int secondary = 0)
            { this.label = label; this.primary = primaryDefault = primary; this.secondary = secondaryDefault = secondary; }
        }
        // Positive codes are Input System keys; negative codes are mouse buttons.
        readonly Binding[] bindings = {
            new Binding("Forward", (int)Key.W, (int)Key.UpArrow), new Binding("Backward", (int)Key.S, (int)Key.DownArrow),
            new Binding("Left", (int)Key.A, (int)Key.LeftArrow), new Binding("Right", (int)Key.D, (int)Key.RightArrow),
            new Binding("Jump", (int)Key.Space), new Binding("Slide", (int)Key.LeftCtrl, (int)Key.C),
            new Binding("Fire / Place", -1), new Binding("Aim", -2), new Binding("Reload", (int)Key.R),
            new Binding("Pick up weapon", (int)Key.E), new Binding("", 0),
            new Binding("Barricade", (int)Key.Digit1), new Binding("Turret", (int)Key.Digit2), new Binding("", 0),
            new Binding("Rotate", (int)Key.Q), new Binding("Next wave", (int)Key.Enter), new Binding("Restart", (int)Key.T),
            new Binding("Mute music", (int)Key.M), new Binding("Mute effects", (int)Key.N), new Binding("Flashlight", (int)Key.F), new Binding("", 0), new Binding("Barrel (hammer equipped)", (int)Key.Digit3), new Binding("Take caffeine", (int)Key.V), new Binding("Holographic menu", (int)Key.Tab),
            new Binding("Dodge", (int)Key.LeftShift, (int)Key.RightShift), new Binding("Drop weapon", (int)Key.G), new Binding("Hammer / previous weapon", (int)Key.Digit5)
        };
        int bindingAction = -1, bindingSlot;
        bool showBindings = true;
        Vector2 bindingScroll;
        string bindingMessage = "Click a binding, then press a key or mouse button.";
        static string BindingPref(int action, int slot) => "Dustline.Controls." + ((Control)action) + "." + slot;
        static bool ValidBinding(int code) => code == 0 || (code >= -5 && code < 0) ||
            (Enum.IsDefined(typeof(Key), code) && code != (int)Key.H && code != (int)Key.Escape);
        void LoadBindings()
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (i == (int)Control.SlotSpin || i == (int)Control.Trap) { b.primary = b.secondary = 0; continue; }
                int primary = PlayerPrefs.GetInt(BindingPref(i, 0), b.primaryDefault);
                int secondary = PlayerPrefs.GetInt(BindingPref(i, 1), b.secondaryDefault);
                b.primary = ValidBinding(primary) ? primary : b.primaryDefault;
                b.secondary = ValidBinding(secondary) ? secondary : b.secondaryDefault;
            }
            int dodgeAction = (int)Control.Dodge;
            var dodgeBinding = bindings[dodgeAction];
            for (int i = 0; i < dodgeAction; i++)
            {
                var existing = bindings[i];
                if (!PlayerPrefs.HasKey(BindingPref(dodgeAction, 0)) &&
                    (existing.primary == dodgeBinding.primary || existing.secondary == dodgeBinding.primary))
                    dodgeBinding.primary = 0;
                if (!PlayerPrefs.HasKey(BindingPref(dodgeAction, 1)) &&
                    (existing.primary == dodgeBinding.secondary || existing.secondary == dodgeBinding.secondary))
                    dodgeBinding.secondary = 0;
            }
            // Migrate the previous defaults while retaining unrelated custom bindings.
            if(PlayerPrefs.GetInt("Dustline.HammerKeysVersion",0)<2)
            {
                if(bindings[(int)Control.Trap].primary==(int)Key.Digit4)bindings[(int)Control.Trap].primary=(int)Key.Digit3;
                if(bindings[(int)Control.Build].primary==(int)Key.B||bindings[(int)Control.Build].primary==(int)Key.Digit3){bindings[(int)Control.Build].primary=(int)Key.Digit4;bindings[(int)Control.Build].secondary=(int)Key.B;}
                PlayerPrefs.SetInt("Dustline.HammerKeysVersion",2);SaveBindings();
            }
            if (PlayerPrefs.GetInt("Dustline.InventoryKeysVersion", 0) < 1)
            {
                var hammer = bindings[(int)Control.Build];
                if (hammer.primary == (int)Key.Digit4 && hammer.secondary == (int)Key.B)
                { hammer.primary = (int)Key.B; hammer.secondary = 0; }
                PlayerPrefs.SetInt("Dustline.InventoryKeysVersion", 1); SaveBindings();
            }
            if (PlayerPrefs.GetInt("Dustline.ToolKeysVersion", 0) < 1)
            {
                var hammer = bindings[(int)Control.Build];
                if (hammer.primary == (int)Key.B || hammer.primary == (int)Key.Digit4)
                { hammer.primary = (int)Key.Digit5; hammer.secondary = (int)Key.B; }
                PlayerPrefs.SetInt("Dustline.ToolKeysVersion", 1); SaveBindings();
            }
            if (PlayerPrefs.GetInt("Dustline.BuildSelectionVersion", 0) < 1)
            {
                var build = bindings[(int)Control.Build];
                if (build.primary == (int)Key.Digit5 && build.secondary == (int)Key.B)
                { build.primary = (int)Key.B; build.secondary = 0; }
                PlayerPrefs.SetInt("Dustline.BuildSelectionVersion", 1); SaveBindings();
            }
            if (PlayerPrefs.GetInt("Dustline.FiveSlotHotbarVersion", 0) < 1)
            {
                var barrel = bindings[(int)Control.Barrel];
                if (barrel.primary == (int)Key.Digit4) barrel.primary = (int)Key.Digit3;
                if (barrel.secondary == (int)Key.Digit4) barrel.secondary = 0;
                PlayerPrefs.SetInt("Dustline.FiveSlotHotbarVersion", 1); SaveBindings();
            }
            // Retain the enum slot for saved bindings, but retire the B shortcut.
            bindings[(int)Control.Build].primary=bindings[(int)Control.Build].secondary=0;
        }
        void SaveBindings()
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                PlayerPrefs.SetInt(BindingPref(i, 0), bindings[i].primary);
                PlayerPrefs.SetInt(BindingPref(i, 1), bindings[i].secondary);
            }
            PlayerPrefs.Save();
        }
        static ButtonControl BoundButton(int code)
        {
            if (code > 0) return Keyboard.current == null ? null : Keyboard.current[(Key)code];
            var mouse = Mouse.current;
            if (mouse == null) return null;
            switch (code) { case -1: return mouse.leftButton; case -2: return mouse.rightButton;
                case -3: return mouse.middleButton; case -4: return mouse.backButton; case -5: return mouse.forwardButton; default: return null; }
        }
        bool Bound(Control action, bool pressed)
        {
            if (HologramBlocking && action != Control.Hologram) return false;
            var b = bindings[(int)action];
            var first = BoundButton(b.primary); var second = BoundButton(b.secondary);
            return pressed ? (first != null && first.wasPressedThisFrame) || (second != null && second.wasPressedThisFrame)
                : (first != null && first.isPressed) || (second != null && second.isPressed);
        }
        bool Pressed(Control action) => Bound(action, true);
        bool Held(Control action) => Bound(action, false);
        static string BindingName(int code)
        {
            switch (code) { case 0: return "—"; case -1: return "Left mouse"; case -2: return "Right mouse";
                case -3: return "Middle mouse"; case -4: return "Mouse 4"; case -5: return "Mouse 5"; }
            return Keyboard.current != null ? Keyboard.current[(Key)code].displayName : ((Key)code).ToString();
        }
        string ControlHint(Control action)
        {
            var b = bindings[(int)action];
            return "[" + BindingName(b.primary != 0 ? b.primary : b.secondary) + "]";
        }
        bool AssignBinding(int action, int slot, int code)
        {
            if (!ValidBinding(code)) { bindingMessage = "H and Esc are reserved for Menu / Cancel."; return false; }
            for (int i = 0; code != 0 && i < bindings.Length; i++)
                if (i != action && (bindings[i].primary == code || bindings[i].secondary == code))
                { bindingMessage = "Already assigned: " + bindings[i].label + ". Change or clear that binding first."; return false; }
            if (slot == 0) bindings[action].primary = code; else bindings[action].secondary = code;
            bindingMessage = "Binding updated.";
            return true;
        }
        bool CaptureBinding()
        {
            if (bindingAction < 0) return false;
            // The cancel button must not become a left-mouse binding before OnGUI handles it.
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Vector2 p = new Vector2(mouse.position.ReadValue().x, Screen.height - mouse.position.ReadValue().y) / HudScale;
                Rect help = HelpRect(Screen.width / HudScale, Screen.height / HudScale);
                if (new Rect(help.x + 332, help.y + 342, 224, 30).Contains(p)) return true;
            }
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            { bindingAction = -1; bindingMessage = "Cancelled."; return true; }
            int code = 0;
            bool found = false;
            if (keyboard != null)
                foreach (var key in keyboard.allKeys)
                    if (key.wasPressedThisFrame) { code = (int)key.keyCode; found = true; break; }
            if (!found)
                for (int mouseCode = -1; mouseCode >= -5; mouseCode--)
                    if (BoundButton(mouseCode)?.wasPressedThisFrame == true) { code = mouseCode; found = true; break; }
            if (found && AssignBinding(bindingAction, bindingSlot, code)) { SaveBindings(); bindingAction = -1; }
            return true;
        }
        void DrawBindings(Rect help)
        {
            Rect viewport = new Rect(help.x + 24, help.y + 104, 532, 204);
            bindingScroll = GUI.BeginScrollView(viewport, bindingScroll, new Rect(0, 0, 510, bindings.Length * 32));
            bool previousEnabled = GUI.enabled;
            GUI.enabled = bindingAction < 0;
            int row = 0;
            for (int i = 0; i < bindings.Length; i++)
            {
                if (i == (int)Control.SlotSpin || i == (int)Control.Trap) continue;
                int rowY = row++ * 32;
                GUI.Label(new Rect(0, rowY, 185, 28), bindings[i].label, textStyle);
                for (int slot = 0; slot < 2; slot++)
                {
                    int code = slot == 0 ? bindings[i].primary : bindings[i].secondary;
                    if (GUI.Button(new Rect(185 + slot * 160, rowY, 122, 28), bindingAction == i && bindingSlot == slot ? "Press …" : BindingName(code)))
                    { bindingAction = i; bindingSlot = slot; bindingMessage = "Press a key or mouse button. Esc cancels."; }
                    if (GUI.Button(new Rect(310 + slot * 160, rowY, 28, 28), "×"))
                    { AssignBinding(i, slot, 0); SaveBindings(); }
                }
            }
            GUI.enabled = previousEnabled;
            GUI.EndScrollView();
            GUI.Label(new Rect(help.x + 24, help.y + 313, 532, 28), bindingMessage, textStyle);
            GUI.enabled = previousEnabled && bindingAction < 0;
            if (GUI.Button(new Rect(help.x + 24, help.y + 342, 200, 30), "Restore defaults"))
            {
                foreach (var b in bindings) { b.primary = b.primaryDefault; b.secondary = b.secondaryDefault; }
                SaveBindings(); bindingMessage = "Default bindings saved.";
            }
            GUI.enabled = previousEnabled;
        }
#if UNITY_EDITOR
        void RunControlChecks()
        {
            int[] original = new int[bindings.Length * 2];
            string previousMessage = bindingMessage;
            for (int i = 0; i < bindings.Length; i++) { original[i * 2] = bindings[i].primary; original[i * 2 + 1] = bindings[i].secondary; }
            try
            {
                foreach (var b in bindings) { b.primary = b.primaryDefault; b.secondary = b.secondaryDefault; }
                if (!AssignBinding((int)Control.Jump, 0, (int)Key.L) || bindings[(int)Control.Jump].primary != (int)Key.L)
                    throw new Exception("Jump can be rebound");
                if (AssignBinding((int)Control.Slide, 0, (int)Key.L) || bindings[(int)Control.Slide].primary != (int)Key.LeftCtrl)
                    throw new Exception("Conflicting binding leaves existing controls intact");
                if (!AssignBinding((int)Control.Slide, 1, -4) || bindings[(int)Control.Slide].secondary != -4)
                    throw new Exception("Mouse side buttons can be bound as an alternate");
                if (AssignBinding((int)Control.Jump, 0, (int)Key.H) || AssignBinding((int)Control.Jump, 0, (int)Key.Escape) || ValidBinding(9999))
                    throw new Exception("Reserved and invalid keys rejected");
                if (!AssignBinding((int)Control.Jump, 0, 0) || bindings[(int)Control.Jump].primary != 0)
                    throw new Exception("Bindings can be cleared");
            }
            finally
            {
                for (int i = 0; i < bindings.Length; i++) { bindings[i].primary = original[i * 2]; bindings[i].secondary = original[i * 2 + 1]; }
                bindingMessage = previousMessage;
            }
        }
#endif
    }
}
