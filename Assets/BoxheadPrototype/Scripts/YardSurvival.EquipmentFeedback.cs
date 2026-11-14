using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        AudioClip equipmentSwitchClip;
        Texture2D hammerInventoryImage;
        float nextEquipmentSound;

        void PlayEquipmentSwitch()
        {
            if (Time.unscaledTime < nextEquipmentSound) return;
            if (equipmentSwitchClip == null) equipmentSwitchClip = Resources.Load<AudioClip>("InterfaceAudio/WeaponSwitch");
            PlayEffect(equipmentSwitchClip, .65f);
            nextEquipmentSound = Time.unscaledTime + .08f;
        }

        void ToggleHammerSlot()
        {
            if (!InventoryInputAllowed || HologramBlocking) return;
            if (building)
            {
                building = false; buildMenuVisible = false;
                buildFireBlocked = Held(Control.Fire);
                UpdateDefensePreview(); TickHammer(0); CaptureMouse(); PlayEquipmentSwitch();
                return;
            }
            SelectDefense(System.Array.IndexOf(AvailableDefenses, selectedDefense) >= 0 ? selectedDefense : 0);
        }

        void DrawHammerInventoryImage(Rect rect)
        {
            if (hammerInventoryImage == null) hammerInventoryImage = Resources.Load<Texture2D>("HudBuild/Hammer");
            if (hammerInventoryImage != null) GUI.DrawTexture(rect, hammerInventoryImage, ScaleMode.ScaleToFit, true);
        }
    }
}
