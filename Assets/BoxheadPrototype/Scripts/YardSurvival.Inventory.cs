using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        [Serializable] sealed class InventoryWeapon
        {
            public int weapon, ammo, reserve;
            public float readyAt;
        }
        readonly List<InventoryWeapon> weaponInventory = new List<InventoryWeapon>();
        const int WeaponSlotLimit = 4;
        bool HasEquippedWeapon => weaponInventory.Exists(w => w.weapon == weaponId);
        GUIStyle inventoryNameStyle, inventorySmallStyle, inventoryKeyStyle;
        static readonly Key[] InventoryKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
        static readonly string[] InventoryNames = { "LMG", "MINIGUN", "P9", "BOOMSTICK", "GRENADE\nLAUNCHER",
            "PITCHFORK", "DUAL\nSAWNOFFS", "CROSSBOW", "DUAL SMGS", "HEAVY RIFLE", "KNIFE", "TALON",
            "BERETTA", "HEAVY PISTOL", "PERSUADER", "FAL", "SNIPER" };

        void ResetWeaponInventory() => weaponInventory.Clear();
        void CaptureInventoryWeapon()
        {
            var slot = weaponInventory.Find(w => w.weapon == weaponId);
            if (slot == null) return;
            slot.ammo = ammunition; slot.reserve = reserveAmmo;
            slot.readyAt = Time.time + Mathf.Max(0, fireTimer);
        }
        void RememberEquippedWeapon()
        {
            var slot = weaponInventory.Find(w => w.weapon == weaponId);
            if (slot == null) { slot = new InventoryWeapon { weapon = weaponId }; weaponInventory.Add(slot); }
            slot.ammo = ammunition; slot.reserve = reserveAmmo;
        }
        bool InventoryInputAllowed => frontScreen == FrontScreen.Game && !dead && !showHelp && bindingAction < 0;
        bool SwitchInventorySlot(int index)
        {
            if (!InventoryInputAllowed || index < 0 || index >= weaponInventory.Count) return false;
            var target = weaponInventory[index];
            bool wasBuilding = building;
            if (building) { building = false; buildMenuVisible = false; UpdateDefensePreview(); TickHammer(0); }
            if (target.weapon == weaponId) { if(wasBuilding) PlayEquipmentSwitch(); return true; }
            PlayEquipmentSwitch();
            if (IsCoopGuest) { SendCoopCommand("switch", 0, target.weapon); return true; }
            float readyAt = target.readyAt;
            EquipWeapon(target.weapon, target.ammo, target.reserve);
            // Switching cancels a reload, but cannot refill ammunition or bypass a shot's recovery.
            fireTimer = Mathf.Max(.18f, readyAt - Time.time);
            UpdateArsenalAudioEvents();
            return true;
        }
        int WrappedInventorySlot(int direction)
        {
            int current = weaponInventory.FindIndex(w => w.weapon == weaponId);
            return weaponInventory.Count == 0 ? -1 : (Mathf.Max(0, current) + direction + weaponInventory.Count) % weaponInventory.Count;
        }
        bool InventoryKeyConflicts(Key key)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                if (i == (int)Control.Barricade || i == (int)Control.Turret || i == (int)Control.Trap || i == (int)Control.Barrel) continue;
                if (bindings[i].primary == (int)key || bindings[i].secondary == (int)key) return true;
            }
            return false;
        }
        void UpdateWeaponInventoryInput()
        {
            if (!InventoryInputAllowed || building || HologramBlocking) return;
            if (Pressed(Control.DropWeapon)) { DropInventoryWeapon(weaponInventory.FindIndex(w=>w.weapon==weaponId)); return; }
            var keyboard = Keyboard.current;
            if (keyboard != null)
                for (int i = 0; i < InventoryKeys.Length && i < weaponInventory.Count; i++)
                    if (!InventoryKeyConflicts(InventoryKeys[i]) && keyboard[InventoryKeys[i]].wasPressedThisFrame)
                    { SwitchInventorySlot(i); return; }
            if (Cursor.lockState != CursorLockMode.Locked || Mouse.current == null) return;
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > .01f) SwitchInventorySlot(WrappedInventorySlot(scroll > 0 ? -1 : 1));
        }
        bool InventoryVisible => InventoryInputAllowed && !HologramBlocking;
        Vector3 WeaponDropPosition(Vector3 origin, Vector3 forward)
        {
            var start=new Vector3(origin.x,0,origin.z);
            var direction=new Vector3(forward.x,0,forward.z).normalized;
            var position=Move(start,direction*1.2f,.2f,true);
            return OnGround(new Vector3(position.x,0,position.z));
        }
        bool DropInventoryWeapon(int index)
        {
            if(!InventoryInputAllowed||upgradePending||index<0||index>=weaponInventory.Count)return false;
            var slot=weaponInventory[index];
            if(IsCoopGuest){SendCoopCommand("drop",0,slot.weapon);return true;}
            CaptureInventoryWeapon();
            var dropped=SpawnPickup(slot.weapon,WeaponDropPosition(player.position,player.forward),slot.ammo,slot.reserve);
            if(dropped==null)return false;
            dropped.readyAt=slot.readyAt;
            weaponInventory.RemoveAt(index);
            if(slot.weapon==weaponId)
            {
                reloading=false;reloadTimer=0;aiming=false;aimBlend=0;
                if(weaponInventory.Count>0)
                {
                    var next=weaponInventory[Mathf.Min(index,weaponInventory.Count-1)];
                    EquipWeapon(next.weapon,next.ammo,next.reserve);fireTimer=Mathf.Max(.18f,next.readyAt-Time.time);
                }
                else {ammunition=reserveAmmo=0;if(gun!=null)gun.gameObject.SetActive(false);}
            }
            hologramSelection=Mathf.Clamp(hologramSelection,0,Mathf.Max(0,weaponInventory.Count-1));
            hologramRenderedTab=-1;
            return true;
        }
        float InventoryHudHeight => InventoryVisible ? 76 : 0;
        int InventoryFirstVisible
        {
            get { int selected=Mathf.Max(0,weaponInventory.FindIndex(w=>w.weapon==weaponId));
                return Mathf.Clamp(selected-2,0,Mathf.Max(0,weaponInventory.Count-5)); }
        }
        int InventoryVisibleCount => 5;
        float InventoryCellWidth(float width) => Mathf.Min(82,(width-520)/5);
        Rect InventoryBounds(float width,float height)
        {
            float w=InventoryVisibleCount*InventoryCellWidth(width);
            return new Rect((width-w)/2-28,height-77,w+56,58);
        }
        Rect InventoryCardRect(int index,float width,float height)
        {
            float cell=InventoryCellWidth(width),w=InventoryVisibleCount*cell;
            return new Rect((width-w)/2+(index-InventoryFirstVisible)*cell,height-71,cell-5,38);
        }
        void DrawWeaponInventory(float width,float height)
        {
            if(!InventoryVisible)return;
            EnsureDreadHud();
            for(int i=0;i<5;i++)
            {
                bool hammer=i==4;
                bool occupied=hammer || i<weaponInventory.Count;
                bool selected=hammer ? building : !building && occupied && weaponInventory[i].weapon==weaponId;
                Rect card=InventoryCardRect(i,width,height);
                Fill(card,new Color(.01f,.012f,.014f,selected?.65f:.35f));
                DreadFrame(card,selected?HudRed:DreadRule);
                string key=hammer?ControlHint(Control.EquipHammer):(!InventoryKeyConflicts(InventoryKeys[i])?BindingName((int)InventoryKeys[i]):"·");
                DreadLabel(new Rect(card.x+6,card.y+4,17,22),key,dreadSmall,selected?HudRed:DreadMuted);
                var imageRect=new Rect(card.x+21,card.y+3,card.width-25,32);
                if(hammer)DrawHammerInventoryImage(imageRect);
                else if(occupied)DrawDreadWeaponGlyph(imageRect,weaponInventory[i].weapon,selected?HudWhite:DreadMuted);
                if(occupied && Cursor.lockState!=CursorLockMode.Locked && GUI.Button(card,GUIContent.none,GUIStyle.none))
                { if(hammer)ToggleHammerSlot();else SwitchInventorySlot(i); }
            }
        }
    }
}
