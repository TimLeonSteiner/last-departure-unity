from pathlib import Path
p=Path('BoxheadPrototype/Scripts/YardSurvival.cs');s=p.read_text(encoding='utf-8-sig').replace('weaponId = 0; ammunition = MagazineSize;','weaponId = 10; ammunition = MagazineSize;').replace('EquipWeapon(0, MagazineSize);','EquipWeapon(10, MagazineSize);').replace('pistolModel != null && weaponId == 0 && ammunition == 12','animatedWeaponModels != null && weaponId == 10 && ammunition == 15').replace('!TakePickup(farPickup) && weaponId == 0','!TakePickup(farPickup) && weaponId == 10').replace('ammunition == MagazineSize && weaponId == 0','ammunition == MagazineSize && weaponId == 10');p.write_text(s,encoding='utf-8')
p=Path('BoxheadPrototype/Scripts/YardSurvivalWeaponsAudio.cs');s=p.read_text(encoding='utf-8-sig');needle='                    for (int ev = 0; ev < 3; ev++)';s=s.replace(needle,'''                    if (id >= 8)
                    {
                        ValidateWeaponClip(bank.shot);ValidateWeaponClip(bank.reload);ValidateWeaponClip(bank.action);
                        int family=ArsenalAudio[id-8];
                        if (!WeaponCatalog.IsMelee(id) && bank.shot!=weaponSounds[family].shot) throw new Exception("Incorrect reused sound family "+id);
                        continue;
                    }
'''+needle,1);p.write_text(s,encoding='utf-8')
