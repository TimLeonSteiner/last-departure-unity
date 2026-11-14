#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunInventoryChecks()
        {
            if (coop != null && coop.IsListening) throw new InvalidOperationException("Inventory checks need an offline test session.");
            var log = new StringBuilder();
            Action<bool, string> check = (pass, label) => { log.AppendLine((pass ? "PASS " : "FAIL ") + label); };
            bool active = enabled;
            Keyboard testKeyboard = null;
            var previousBackground=InputSystem.settings.backgroundBehavior;
            var previousUpdateMode=InputSystem.settings.updateMode;
            try
            {
                enabled = false; PlayFromMenu();
                check(weaponInventory.Count == 1, "New run starts with only the pistol");
                int initialFloorWeapons=pickups.Count;
                ResetWeaponInventory(); RememberEquippedWeapon();
                ammunition = 4; reserveAmmo = 19; fireTimer = .7f;
                EquipWeapon(14, WeaponCatalog.All[14].capacity);
                check(weaponId == 14, "Weapon grant equips Persuader alongside starter Beretta");
                check(weaponInventory.Count == 2 && weaponInventory[0].ammo == 4 && weaponInventory[0].reserve == 19, "Weapon grant keeps previous weapon and finite ammunition");
                ammunition = 7; reserveAmmo = 23;
                check(SwitchInventorySlot(0) && weaponId == WeaponCatalog.Starter && ammunition == 4 && reserveAmmo == 19, "Switch restores depleted starter ammunition");
                check(fireTimer > .5f, "Switching back preserves shot recovery");
                check(SwitchInventorySlot(1) && ammunition == 7 && reserveAmmo == 23, "Other slot retains its own magazine and reserve");
                BeginReload(); check(reloading, "Reload begins on current weapon");
                SwitchInventorySlot(0); check(!reloading, "Switch cancels unfinished reload");
                SwitchInventorySlot(1); check(ammunition == 7 && reserveAmmo == 23, "Canceled reload grants no ammunition");
                EquipWeapon(14, WeaponCatalog.All[14].capacity);
                check(weaponInventory.Count == 2 && ammunition == 40 && reserveAmmo == 120, "Duplicate grant replenishes existing slot without duplication");
                check(pickups.Count == initialFloorWeapons, "Grants below capacity create no extra floor weapons");
                foreach(int id in new[]{0,3,6,8,9,13,15}) EquipWeapon(id, WeaponCatalog.All[id].capacity);
                check(weaponInventory.Count == 4 && weaponInventory.Select(w => w.weapon).Distinct().Count() == 4, "Weapon grants never exceed four unique slots");
                SwitchInventorySlot(0);
                check(WrappedInventorySlot(-1) == 3 && WrappedInventorySlot(1) == 1, "Mouse-wheel slot selection wraps in both directions");
                testKeyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                // The headless review runs from EditorApplication.update. Select the
                // package's player update explicitly and apply synthetic state directly,
                // so editor focus and queued-event timestamps cannot swallow the test input.
                var update=typeof(InputSystem).GetMethod("Update",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(InputUpdateType)},null);
                void PlayerInputUpdate()=>update.Invoke(null,new object[]{InputUpdateType.Dynamic});
                // Prime edge tracking before the first event on the synthetic device.
                _ = testKeyboard.digit4Key.wasPressedThisFrame;
                PlayerInputUpdate();InputState.Change(testKeyboard,new KeyboardState(Key.Digit4),InputUpdateType.Dynamic);
                UpdateWeaponInventoryInput();
                check(weaponId == weaponInventory[3].weapon && !building, "Number 4 selects fourth weapon");
                PlayerInputUpdate();InputState.Change(testKeyboard,new KeyboardState(),InputUpdateType.Dynamic);
                PlayerInputUpdate();InputState.Change(testKeyboard,new KeyboardState(Key.Digit5),InputUpdateType.Dynamic);
                UpdateBuildingInput();check(building && InventoryVisible && InventoryVisibleCount==5,"Number 5 equips hammer and keeps five-slot inventory visible");
                foreach(var pair in new[]{(Key.Digit1,0),(Key.Digit2,1),(Key.Digit3,3)})
                {
                    PlayerInputUpdate();InputState.Change(testKeyboard,new KeyboardState(),InputUpdateType.Dynamic);
                    PlayerInputUpdate();InputState.Change(testKeyboard,new KeyboardState(pair.Item1),InputUpdateType.Dynamic);
                    UpdateBuildingInput();check(building && selectedDefense==pair.Item2,"Hammer build choice "+pair.Item1);
                }
                PlayerInputUpdate();InputState.Change(testKeyboard,new KeyboardState(),InputUpdateType.Dynamic);
                PlayerInputUpdate();InputState.Change(testKeyboard,new KeyboardState(Key.Digit4),InputUpdateType.Dynamic);
                UpdateBuildingInput();check(!building && weaponId==weaponInventory[3].weapon,"4 returns from hammer to fourth weapon");
                check(!WeaponCatalog.CanAim(9),"Heavy rifle rejects zoom");
                check(Resources.Load<AudioClip>("InterfaceAudio/WeaponSwitch")!=null,"Selected switching sound loads");
                check(Resources.Load<Texture2D>("HudBuild/Hammer")!=null,"Actual hammer asset thumbnail loads");
                ToggleHammerSlot();
                PlayerInputUpdate();InputState.Change(testKeyboard,new KeyboardState(),InputUpdateType.Dynamic);
                PlayerInputUpdate();InputState.Change(testKeyboard,new KeyboardState(Key.B),InputUpdateType.Dynamic);
                UpdateBuildingInput();check(!HologramBlocking,"B no longer opens the build menu");
                PlayerInputUpdate();InputState.Change(testKeyboard,new KeyboardState(),InputUpdateType.Dynamic);
                PlayerInputUpdate();InputState.Change(testKeyboard,new KeyboardState(Key.Tab),InputUpdateType.Dynamic);
                TickHologram();check(hologramOpen && hologramTab==0,"Tab opens Inventory while hammer is equipped");
                SelectHologramTab(1);check(hologramTab==1,"Build is accessible through menu tab");
                RestoreHologram();building=false;
                PlayerInputUpdate();InputState.Change(testKeyboard,new KeyboardState(),InputUpdateType.Dynamic);
                int before = weaponId;
                building = true;
                PlayerInputUpdate();InputState.Change(testKeyboard,new KeyboardState(Key.Digit2),InputUpdateType.Dynamic);
                UpdateWeaponInventoryInput(); check(weaponId == before, "Weapon hotkeys yield to defense keys in build mode");
                building = false; frontScreen = FrontScreen.Pause;
                check(!SwitchInventorySlot(0) && weaponId == before, "Pause blocks weapon switching");
                frontScreen = FrontScreen.Game; dead = true;
                check(!SwitchInventorySlot(0), "Death blocks weapon switching"); dead = false;
                check(!SwitchInventorySlot(-1) && !SwitchInventorySlot(17), "Invalid slots cannot grant weapons");
                foreach (float width in new[] { 1000f, 1280f, 1920f })
                {
                    bool valid = true;
                    for(int selected=0;selected<weaponInventory.Count;selected++)
                    {
                        weaponId=weaponInventory[selected].weapon;
                        int start=InventoryFirstVisible;
                        valid &= selected>=start && selected<start+InventoryVisibleCount;
                        for(int i=start;i<start+InventoryVisibleCount;i++)
                        {
                            Rect card=InventoryCardRect(i,width,720);
                            valid &= card.xMin>=220 && card.xMax<=width-220 && card.yMin>=0 && card.yMax<=720;
                            for(int j=start;j<i;j++)valid &= !card.Overlaps(InventoryCardRect(j,width,720));
                        }
                    }
                    check(valid,"Compact inventory keeps every selected weapon visible and clears corner HUD at width "+width);
                }
                ResetRun(); frontScreen=FrontScreen.Game;
                EquipWeapon(0,10,21);EquipWeapon(3,2,8);EquipWeapon(14,7,23);
                ammunition=5;reserveAmmo=17;fireTimer=.7f;CaptureInventoryWeapon();
                var incoming=SpawnPickup(13,new Vector3(player.position.x,0,player.position.z),6,19);
                check(TakePickup(incoming)&&weaponId==13&&weaponInventory.Count==4,"E pickup swaps equipped weapon when four slots are full");
                var dropped=pickups.Find(w=>w.weapon==14&&!w.waveDrop);
                check(dropped!=null&&dropped.ammo==5&&dropped.reserve==17,"Swapped weapon keeps exact magazine and reserve on the ground");
                int equipped=weaponId;int floorCount=pickups.Count;
                check(DropInventoryWeapon(0)&&weaponId==equipped&&weaponInventory.Count==3&&pickups.Count==floorCount+1,"Inventory drop removes selected unequipped slot only");
                PlayerInputUpdate();InputState.Change(testKeyboard,new KeyboardState(Key.G),InputUpdateType.Dynamic);
                UpdateWeaponInventoryInput();
                check(weaponInventory.Count==2&&weaponId!=equipped,"G drops equipped weapon and selects remaining weapon");
                PlayerInputUpdate();InputState.Change(testKeyboard,new KeyboardState(),InputUpdateType.Dynamic);
                while(weaponInventory.Count>0)if(!DropInventoryWeapon(0))throw new Exception("Drop failed");
                check(!HasEquippedWeapon&&ammunition==0&&!reloading,"Dropping last weapon leaves player unarmed");
                Fire(Vector3.forward);BeginReload();check(!reloading&&ammunition==0,"Unarmed player cannot fire or reload");
                dropped.position=new Vector3(player.position.x,0,player.position.z);
                check(TakePickup(dropped)&&weaponId==14&&ammunition==5&&reserveAmmo==17,"Dropped weapon can be recovered without refilling ammunition");
                check(fireTimer>.5f,"Dropping and recovering cannot bypass shot cooldown");
                ResetRun(); check(weaponInventory.Count == 1 && weaponId == WeaponCatalog.Starter, "Restart restores wave-one inventory");
            }
            catch (Exception ex) { log.AppendLine("FAIL " + ex); }
            finally
            {
                if (testKeyboard != null) InputSystem.RemoveDevice(testKeyboard);
                InputSystem.settings.backgroundBehavior=previousBackground;
                InputSystem.settings.updateMode=previousUpdateMode;
                ResetRun(); ReleaseMouse(); enabled = active;
            }
            return log + "COMPLETE inventory " + DateTime.Now.ToString("s") + "\n";
        }
        public void PrepareInventoryHudReview(bool full)
        {
            PlayFromMenu(); fighting = false; remainingToSpawn = 0; nextWaveCountdown = -1;
            foreach (int id in full ? new[] { 12, 3, 0, 14 } : new[] { 12, 3, 0 }) EquipWeapon(id, WeaponCatalog.All[id].capacity);
            SwitchInventorySlot(weaponInventory.FindIndex(w => w.weapon == 12)); ammunition = 9; reserveAmmo = 28; bannerTimer = 0;
        }
    }
}
#endif
