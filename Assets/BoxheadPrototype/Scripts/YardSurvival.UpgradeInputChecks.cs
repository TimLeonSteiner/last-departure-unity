#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunUpgradeInputChecks()
        {
            var report = new System.Text.StringBuilder();
            var originalKeyboard = Keyboard.current;
            var originalMouse = Mouse.current;
            var testKeyboard = InputSystem.AddDevice<Keyboard>();
            var testMouse = InputSystem.AddDevice<Mouse>();
            void Check(bool ok, string label) { if (!ok) throw new Exception(label); report.AppendLine("PASS " + label); }
            try
            {
                ResetUpgradeHold();
                Check(AdvanceUpgradeHold(0, true, false, true, 5) < 0 && !upgradeSelectionArmed,
                    "Wave-ending held click cannot arm or select an upgrade");
                AdvanceUpgradeHold(-1, false, true, true, 0);
                AdvanceUpgradeHold(0, true, false, true, 0);
                Check(AdvanceUpgradeHold(0, false, false, true, 1.9f) < 0, "Hold shorter than two seconds does not select");
                AdvanceUpgradeHold(-1, false, true, true, 0);
                Check(upgradeHoldSeconds == 0 && upgradeHoldChoice == -1, "Release cancels partial hold");
                AdvanceUpgradeHold(0, true, false, true, 0);
                AdvanceUpgradeHold(0, false, false, true, 1.5f);
                Check(AdvanceUpgradeHold(1, false, false, true, 2) < 0 && upgradeHoldSeconds == 0,
                    "Dragging onto another card cancels instead of transferring progress");
                Check(AdvanceUpgradeHold(0, false, false, true, 3) < 0, "Reentering a card requires a fresh press");
                AdvanceUpgradeHold(0, true, false, true, 0);
                AdvanceUpgradeHold(0, false, false, true, 1);
                AdvanceUpgradeHold(0, false, false, false, 3);
                Check(!upgradeSelectionArmed && upgradeHoldSeconds == 0, "Focus loss cancels and requires release before retrying");
                AdvanceUpgradeHold(-1, false, true, true, 0);
                AdvanceUpgradeHold(2, true, false, true, 0);
                Check(AdvanceUpgradeHold(2, false, false, true, 2) == 2,
                    "Exactly two seconds confirms the held card");
                Check(AdvanceUpgradeHold(2, false, false, true, 3) < 0, "A completed hold cannot confirm twice");
                for (int i = 0; i < 3; i++)
                {
                    PreviewCoreUpgrades();
                    InputSystem.QueueStateEvent(testMouse, new MouseState());
                    InputSystem.QueueStateEvent(testKeyboard, new KeyboardState()); InputSystem.Update();
                    testKeyboard.MakeCurrent(); testMouse.MakeCurrent(); TickUpgradeSelection(0);
                    var key = i == 0 ? Key.Digit1 : i == 1 ? Key.Digit2 : Key.Digit3;
                    InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(key)); InputSystem.Update();
                    TickUpgradeSelection(0);
                    Check(upgradePending, "Number " + (i + 1) + " press alone cannot select");
                    InputSystem.Update(); TickUpgradeSelection(1.9f);
                    Check(upgradePending, "Number " + (i + 1) + " waits for full hold");
                    InputSystem.Update(); TickUpgradeSelection(.11f);
                    Check(!upgradePending && (i == 0 ? weaponBoost == 1 : i == 1 ? maxPlayerHealth == 125 : maxCoreHealth == 600),
                        "Number " + (i + 1) + " hold applies matching card once");
                }
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState()); InputSystem.Update();
                PreviewCoreUpgrades();
                float scale = Mathf.Min(Screen.width / 1200f, Screen.height / 850f);
                Vector2 position = new Vector2((Screen.width - 1200 * scale) / 2 + 600 * scale,
                    Screen.height - ((Screen.height - 850 * scale) / 2 + 300 * scale));
                InputSystem.QueueStateEvent(testMouse, new MouseState { position = position }.WithButton(MouseButton.Left)); InputSystem.Update();
                TickUpgradeSelection(3);
                Check(upgradePending, "Mouse held when the wave ends cannot select");
                InputSystem.QueueStateEvent(testMouse, new MouseState { position = position }); InputSystem.Update(); TickUpgradeSelection(0);
                Check(HoveredUpgradeCard() == 1, "Hover hits middle card at current screen size");
                InputSystem.QueueStateEvent(testMouse, new MouseState { position = position }.WithButton(MouseButton.Left)); InputSystem.Update(); TickUpgradeSelection(0);
                InputSystem.Update(); TickUpgradeSelection(1.9f);
                Check(upgradePending && upgradeHoldChoice == 1, "Mouse hold displays progress without selecting early");
                InputSystem.Update(); TickUpgradeSelection(.11f);
                Check(!upgradePending && maxPlayerHealth == 125, "Two-second mouse hold selects hovered card");
                PreviewCoreUpgrades();
                Check(!upgradeSelectionArmed && upgradeHoldSeconds == 0, "New offer clears prior hold progress");
                return report.ToString();
            }
            finally
            {
                InputSystem.QueueStateEvent(testMouse, new MouseState());
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                InputSystem.Update();
                InputSystem.RemoveDevice(testMouse); InputSystem.RemoveDevice(testKeyboard);
                if (originalMouse != null) originalMouse.MakeCurrent();
                if (originalKeyboard != null) originalKeyboard.MakeCurrent();
            }
        }
    }
}
#endif

