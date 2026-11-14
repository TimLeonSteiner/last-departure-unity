from pathlib import Path
r=Path('LastDeparturePrototype')
p=r/'Scripts/WeaponCatalog.cs';s=p.read_text(encoding='utf-8-sig');start=s.index('            new Definition("P12');end=s.index('            new Definition("LMG');s=s[:start]+s[end:];a=s.index('public static bool IsMelee');b=s.index('public static int TierForWave',a);s=s[:a]+'''public const int Starter = 2;
        public static bool IsMelee(int id) => id == 5 || id == 10;
        public static bool IsShotgun(int id) => id == 3 || id == 6;
        public static bool IsScoped(int id) => id == 16;
        public static bool IsAutomatic(int id) => id == 0 || id == 1 || id == 8 || id == 14 || id == 15;
        '''+s[b:];p.write_text(s,encoding='utf-8')
p=r/'Scripts/YardSurvival.cs';s=p.read_text(encoding='utf-8-sig');a=s.index('        GameObject ModelFor');b=s.index('\n        void EquipWeapon',a);s=s[:a]+'        GameObject ModelFor(int id) => animatedWeaponModels[id];'+s[b:];s=s.replace('weaponId = 10; ammunition','weaponId = WeaponCatalog.Starter; ammunition').replace('EquipWeapon(10, MagazineSize)','EquipWeapon(WeaponCatalog.Starter, MagazineSize)').replace('weaponId == 15) return','weaponId == 7) return');p.write_text(s,encoding='utf-8')
p=r/'Scripts/YardSurvival.AnimatedArsenal.cs';s=p.read_text(encoding='utf-8-sig').replace('weaponId == 13','weaponId == 5');p.write_text(s,encoding='utf-8')
p=r/'Scripts/YardSurvival.AdditionalWeapons.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('weaponId == 12 || weaponId == 15','weaponId == 4 || weaponId == 7').replace('weaponId == 12);','weaponId == 4);').replace('WeaponCatalog.All.Length != 25','WeaponCatalog.All.Length != 17').replace('Expected 25','Expected 17').replace('25 WEAPONS PASS','17 WEAPONS PASS');p.write_text(s,encoding='utf-8')
p=r/'Scripts/YardSurvivalWarehouseAudio.cs';s=p.read_text(encoding='utf-8-sig').replace('int bank = weaponId < 8 ? weaponId : ArsenalAudio[weaponId - 8];','int bank = ArsenalAudio[weaponId];');p.write_text(s,encoding='utf-8')
p=r/'Editor/AnimatedArsenalImport.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('        ConfigureAudio(game);\n        game.turretModel','        game.weaponModel = game.pistolModel = game.sniperModel = game.heavyModel = game.firstPersonArms = null;\n        game.additionalWeaponModels = Array.Empty<GameObject>();\n        game.turretModel');s=s.replace('new YardSurvival.WeaponSoundBank[WeaponCatalog.All.Length]','new YardSurvival.WeaponSoundBank[Packages.Length]').replace('        Array.Copy(game.weaponSounds,banks,Math.Min(game.weaponSounds.Length,banks.Length));','        var families=game.weaponSounds;');s=s.replace('var b=banks[YardSurvival.ArsenalAudio[i]];','var b=families[YardSurvival.ArsenalAudio[i]];').replace('banks[i+8]','banks[i]').replace('WeaponCatalog.All[i+8]','WeaponCatalog.All[i]');
s=s.replace('new[]{0,12,13,97,140,159,110,139},new[]{0,25,0,0,80,115,50,79}','new[]{0,12,13,110,131,160,121,130},new[]{0,44,0,0,91,115,56,90}')
s=s.replace('new[]{0,10,11,90,120,150,102,119},new[]{0,10,11,90,140,170,102,120}','new[]{0,20,21,59,81,123,66,80},new[]{0,15,16,95,124,150,112,123}')
s=s.replace('new[]{0,6,7,68,98,123,80,97},new[]{0,10,11,110,237,266,200,236}','new[]{0,6,7,50,81,105,62,80},new[]{0,10,11,98,199,239,171,198}')
s=s.replace('new[]{0,25,0,0,116,145,80,115}','new[]{0,60,0,0,96,116,71,95}').replace('new[]{0,14,15,96,219,248,170,218},\n        new[]{0,8','new[]{0,20,21,155,221,248,165,220},\n        new[]{0,8');p.write_text(s,encoding='utf-8')
p=r/'Editor/YardPrototypeEditor.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('        ConfigureWeapon(game);\n        ConfigurePistol(game);\n        LibraryWeaponImport.Configure(game);\n        FpsHandsImport.Configure(game);','        AnimatedArsenalImport.Setup();');
for line in ['case "libraryweapons": LibraryWeaponImport.Setup(); break;','case "additionalweapons": AdditionalWeaponImport.Setup(); break;','case "handssetup": FpsHandsImport.Configure(UnityEngine.Object.FindAnyObjectByType<YardSurvival>()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); break;']:
 s=s.replace(line,'')
p.write_text(s,encoding='utf-8')
p=r/'Scripts/YardSurvivalWeaponsAudio.cs';s=p.read_text(encoding='utf-8-sig');a=s.index('        public string RunWeaponAudioChecks()');b=s.index('        static void ValidateWeaponClip',a);s=s[:a]+'''        public string RunWeaponAudioChecks()
        {
            if(weaponSounds==null || weaponSounds.Length!=17)throw new Exception("Expected 17 mapped sound banks");
            for(int id=0;id<17;id++){
                var bank=weaponSounds[id];if(bank==null || bank.weapon!=WeaponCatalog.All[id].name)throw new Exception("Sound mapping "+id);
                ValidateWeaponClip(bank.shot);ValidateWeaponClip(bank.reload);ValidateWeaponClip(bank.action);
            }
            ValidateWeaponClip(turretShotSound);
            return "AUDIO PASS: 17 weapon banks, existing licensed sound families reused, loaded PCM output and sentry shot validated.";
        }
'''+s[b:];p.write_text(s,encoding='utf-8')
# Remap capture/functional checks to the new stable catalog.
p=r/'Scripts/YardSurvival.ArsenalChecks.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('int id=8','int id=0').replace('new[]{12,13,14,15,16,17,18,21}','new[]{4,5,6,7,8,9,10,13}').replace('EquipWeapon(18,1)','EquipWeapon(10,1)').replace('EquipWeapon(13,1)','EquipWeapon(5,1)').replace('EquipWeapon(15,1)','EquipWeapon(7,1)').replace('EquipWeapon(12,1)','EquipWeapon(4,1)');p.write_text(s,encoding='utf-8')
