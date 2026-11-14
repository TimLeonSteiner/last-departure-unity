from pathlib import Path
p=Path('LastDeparturePrototype/Scripts/YardSurvival.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('SetUpAudio();\n            ResetRun();','SetUpAudio();\n            ResetRun();\n            InitializeFrontEnd();')
s=s.replace('ClearArsenalProjectiles();','RestoreFrontEndEffects();\n            ClearArsenalProjectiles();',1)
s=s.replace('if (bindingAction >= 0) PauseMovementAudio(true);','if (FrontEndUpdate()) return;\n            if (bindingAction >= 0) PauseMovementAudio(true);',1)
s=s.replace('yaw += look.x * .12f; pitch = Mathf.Clamp(pitch - look.y * .12f','yaw += look.x * .12f * mouseSensitivity; pitch = Mathf.Clamp(pitch - look.y * .12f * mouseSensitivity')
s=s.replace('if (health == 0) { dead = true; ResetMovementAudio(); ghost.gameObject.SetActive(false); ReleaseMouse(); PlayEffect(deathSound, .65f); ShowBanner("DEPOT VERLOREN – T für einen neuen Versuch"); }','if (health == 0) BeginDeathSequence();')
s=s.replace('float width = Screen.width / scale, height = Screen.height / scale;','float width = Screen.width / scale, height = Screen.height / scale;\n            if (DrawFrontEnd(width, height)) { GUI.matrix = previousMatrix; GUI.color = previousColor; return; }',1)
s=s.replace('void OnDisable() {','void OnDisable() { RestoreFrontEndEffects();')
p.write_text(s,encoding='utf-8')
# Ensure all audio configurators preserve the expanded banks.
p=Path('LastDeparturePrototype/Editor/WeaponAudioSetup.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('AdditionalWeaponImport.ConfigureAudio(game);','AdditionalWeaponImport.ConfigureAudio(game);\n        AnimatedArsenalImport.ConfigureAudio(game);');p.write_text(s,encoding='utf-8')
# Match new weapons to the existing reverberation families.
p=Path('LastDeparturePrototype/Scripts/YardSurvivalWarehouseAudio.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('if (!soundEnabled || !audioUnlocked || arenaCamera == null || weaponId >= warehouseTails.Length) return;','if (!soundEnabled || !audioUnlocked || arenaCamera == null || WeaponCatalog.IsMelee(weaponId)) return;\n            int bank = weaponId < 8 ? weaponId : ArsenalAudio[weaponId - 8];');s=s.replace('warehouseReflections[weaponId]','warehouseReflections[bank]').replace('warehouseTails[weaponId]','warehouseTails[bank]');p.write_text(s,encoding='utf-8')
