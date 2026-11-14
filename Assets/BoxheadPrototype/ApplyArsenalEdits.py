from pathlib import Path
root=Path('BoxheadPrototype')
def edit(path,pairs):
 p=root/path;s=p.read_text(encoding='utf-8-sig')
 for a,b in pairs:
  if a not in s: print('MISSING',path,a[:70])
  s=s.replace(a,b)
 p.write_text(s,encoding='utf-8')
names=[('LMG / BELT FED',80,2,7,.105,5.1,1.6),('MINIGUN',150,1,9,.06,2.3,.8),('P9 / PISTOL',15,1,1,.29,1.65,1.3),('BOOMSTICK',6,1,3,1.05,4.2,3.2),('GRENADE LAUNCHER',1,7,6,1.,2.8,3.2),('PITCHFORK',1,4,1,.75,0.,1.1),('DUAL SAWNOFFS',4,1,5,.55,2.8,3.8),('CROSSBOW',1,5,2,.8,2.6,1.4),('DUAL SMGS',60,1,4,.075,2.1,.95),('HEAVY RIFLE',20,3,6,.35,3.4,2.2),('KNIFE',1,3,1,.5,0.,.6),('TALON',8,3,3,.4,2.7,2.4),('BERETTA',15,1,1,.25,2.7,1.3),('HEAVY PISTOL',7,3,4,.5,2.7,2.7),('PERSUADER',40,1,3,.085,2.8,1.),('FAL / BATTLE RIFLE',20,3,5,.23,2.7,2.1),('BOLT / SNIPER',5,6,5,1.1,1.85,3.)]
extra=',\n'+',\n'.join(f'            new Definition("{n}", {c}, {d}, {w}, {t}f, {r}f, {k}f)' for n,c,d,w,t,r,k in names)
edit(Path('Scripts/WeaponCatalog.cs'), [('new Definition("M3 / GREASE GUN", 30, 1, 2, .185f, 2.4f, 1.0f)','new Definition("M3 / GREASE GUN", 30, 1, 2, .185f, 2.4f, 1.0f)'+extra),('public static int TierForWave','public static bool IsMelee(int id) => id == 13 || id == 18;\n        public static bool IsShotgun(int id) => id == 5 || id == 11 || id == 14;\n        public static bool IsScoped(int id) => id == 2 || id == 24;\n        public static bool IsAutomatic(int id) => id == 1 || id == 3 || id == 4 || id == 6 || id == 7 || id == 8 || id == 9 || id == 16 || id == 22 || id == 23;\n        public static int TierForWave')])
edit(Path('Scripts/YardSurvival.cs'),[
('GameObject ModelFor(int id) => id >= 4','GameObject ModelFor(int id) => id >= 8 ? animatedWeaponModels[id - 8] : id >= 4'),
('pickups.Count >= 16','pickups.Count >= WeaponCatalog.All.Length * 2'),
('var visual = Instantiate(ModelFor(id), root).transform;','var visual = Instantiate(ModelFor(id), root).transform;\n            var animated = visual.GetComponent<AnimatedWeaponRig>(); if (animated != null) animated.GroundPose();'),
('Vector3 position = spots[i];','Vector3 position = i < spots.Length ? spots[i] : new Vector3(-7 + (i - spots.Length) % 6 * 2.8f, 0, 7 + (i - spots.Length) / 6 * 2.0f);'),
('weaponId == 2 ? 42 : 58','WeaponCatalog.IsScoped(weaponId) ? 42 : 58'),
('weaponId == 0 || weaponId == 2 || weaponId == 5 ? Pressed(Control.Fire) : Held(Control.Fire)','WeaponCatalog.IsAutomatic(weaponId) ? Held(Control.Fire) : Pressed(Control.Fire)'),
('ammunition--;','if (!WeaponCatalog.IsMelee(weaponId)) ammunition--;'),
('if (dead || reloading || ammunition == MagazineSize) return;','if (dead || reloading || ammunition == MagazineSize || WeaponCatalog.IsMelee(weaponId)) return;'),
('var casing = Box("Spent brass"','if (WeaponCatalog.IsMelee(weaponId) || weaponId == 15) return;\n            var casing = Box("Spent brass"'),
('void UpdateFlashes(float dt)\n        {','void UpdateFlashes(float dt)\n        {\n            UpdateArsenalProjectiles(dt);'),
('void ResetRun()\n        {','void ResetRun()\n        {\n            ClearArsenalProjectiles();'),
('new Vector3(4, .65f, 10)','new Vector3(2.3f, .65f, 10)'),('new Vector3(4, 0, 13)','new Vector3(2.3f, 0, 13)'),('SpawnEnemy(new Vector3(4, 0, 18));','SpawnEnemy(new Vector3(2.3f, 0, 18));')])
edit(Path('Scripts/YardSurvival.AdditionalWeapons.cs'),[
('weaponId != 2','!WeaponCatalog.IsScoped(weaponId)'),
('if (weaponId != 5)', 'if (WeaponCatalog.IsMelee(weaponId)) { MeleeAttack(direction); return; }\n            if (weaponId == 12 || weaponId == 15) { LaunchArsenalProjectile(direction, weaponId == 12); return; }\n            if (!WeaponCatalog.IsShotgun(weaponId))'),
('WeaponCatalog.All.Length != 8','WeaponCatalog.All.Length != 25'),('Expected eight distinct weapon definitions','Expected 25 distinct weapon definitions'),
('ammunition != full - 1','ammunition != full - (WeaponCatalog.IsMelee(id) ? 0 : 1)'),
('BeginReload(); Fire(Vector3.up);','if (WeaponCatalog.IsMelee(id)) continue;\n                BeginReload(); Fire(Vector3.up);'),('wave <= 7','wave <= 9'),('EIGHT WEAPONS PASS','25 WEAPONS PASS')])
edit(Path('Scripts/ViewWeapon.cs'),[
('Transform magazine, bolt, flash, shell;','Transform magazine, bolt, flash, shell;\n        AnimatedWeaponRig animatedRig;'),
('modelRig = rifle.GetComponent<WeaponModelRig>();','modelRig = rifle.GetComponent<WeaponModelRig>();\n            animatedRig = rifle.GetComponent<AnimatedWeaponRig>();'),
('modelRig != null ? Quaternion.identity','modelRig != null || animatedRig != null ? Quaternion.identity'),('modelRig != null ? 1 : pistol','modelRig != null || animatedRig != null ? 1 : pistol'),
('if (game.firstPersonArms != null)','if (animatedRig == null && game.firstPersonArms != null)'),
('modelRig != null ? modelRig.muzzle : pistol','animatedRig != null ? animatedRig.muzzle : modelRig != null ? modelRig.muzzle : pistol'),
('public void OnShot() {','public void OnShot() { if (animatedRig != null) animatedRig.Shot();'),
('public bool HasReloadParts =>','public bool HasReloadParts => animatedRig != null ? animatedRig.reload != null || WeaponCatalog.IsMelee(tier) :'),
('public bool HasModeledHands => hands','public bool HasModeledHands => animatedRig != null ? animatedRig.HasArms : hands'),('public bool HasConnectedArms => hands','public bool HasConnectedArms => animatedRig != null ? animatedRig.HasArms : hands'),
('bool scoped = tier == 2','bool scoped = WeaponCatalog.IsScoped(tier)'),
('float tilt = reloading ?', '''if (animatedRig != null)
            {
                animatedRig.Pose(progress, reloading, Time.deltaTime);
                transform.localPosition = Vector3.Lerp(animatedRig.hipPosition, animatedRig.aimPosition, aim) + new Vector3(0, Mathf.Sin(Time.time * 10) * movement * .006f, -Mathf.Max(0,recoil)*.02f);
                transform.localRotation = Quaternion.Euler(-Mathf.Max(0,recoil)*3,0,0);
                flashTime -= Time.deltaTime;
                flash.gameObject.SetActive(!reloading && flashTime > 0 && !WeaponCatalog.IsMelee(tier));
                return;
            }
            float tilt = reloading ?''')])
edit(Path('Editor/YardPrototypeEditor.cs'),[('case "inspectarsenal":','case "arsenal": AnimatedArsenalImport.Setup(); break;\n                case "reviewarsenal": AnimatedArsenalVerification.Begin(); break;\n                case "inspectarsenal":')])
