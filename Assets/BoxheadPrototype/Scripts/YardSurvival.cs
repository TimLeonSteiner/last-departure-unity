using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace YardSurvivalPrototype
{
    // A small arena simulation on the XZ plane; no baked navigation is required.
    public sealed partial class YardSurvival : MonoBehaviour
    {
        public Material groundMaterial, wallMaterial, blueMaterial, redMaterial, yellowMaterial, playerMaterial, enemyMaterial;
        public Material skinMaterial, creamMaterial, woodMaterial, accentMaterial;
        public Font signFont;
        public GameObject weaponModel;
        public GameObject pistolModel;
        public GameObject sniperModel, heavyModel;
        [Tooltip("Optional sentry prefab. Direct child TurretHead aims along local +Z; pivot at 1.2 m. Fit base to a 1 m footprint and total height to 1.5 m.")]
        public GameObject turretModel;
        public GameObject barricadeModel, trapModel;
        public GameObject[] zombieModels;
        public GameObject firstPersonArms;
        public Material muzzleMaterial, dustMaterial, brassMaterial;
        public Material wastelandSky;
        public AudioClip reloadSound, boltSound;
        public AudioClip shotSound, hitSound, groanSound, scrapSound, waveSound, deathSound, hurtSound;
        public const int HalfSize = 23;
        const int GridSize = HalfSize * 2 + 1;
        const float PlayerRadius = .42f, EnemyRadius = .4f;
        // Shared with co-op movement: a 1 m jump with 0.5 s airtime for a heavier feel.
        const float WalkSpeed = 6, JumpSpeed = 8, Gravity = 32;
        const float SlideDuration = .7f, SlideCooldown = .35f;
        float jumpHeight, verticalSpeed, slideTimer, slideCooldown, eyeHeight = 1;
        Vector3 slideDirection;
        readonly List<Rect> obstacles = new List<Rect>();
        readonly List<Rect> perimeter = new List<Rect>();
        readonly List<Bounds> wallVolumes = new List<Bounds>();
        readonly List<Enemy> enemies = new List<Enemy>();
        [SerializeField, HideInInspector] List<Barricade> barricades = new List<Barricade>();
        readonly List<Flash> flashes = new List<Flash>();
        [SerializeField, HideInInspector] List<Turret> turrets = new List<Turret>();
        [SerializeField, HideInInspector] List<Trap> traps = new List<Trap>();
        readonly List<Pickup> pickups = new List<Pickup>();
        readonly Vector3[] entrances = { new Vector3(0, 0, 16), new Vector3(16, 0, 0), new Vector3(0, 0, -16), new Vector3(-16, 0, 0) };
        readonly int[,] distance = new int[GridSize, GridSize];
        Transform actors, player, gun, ghost;
        Camera arenaCamera;
        DepotModels.Rig playerRig;
        ViewWeapon viewWeapon;
        int weaponId;
        WeaponCatalog.Definition ActiveWeapon => WeaponCatalog.All[weaponId];
        int MagazineSize => ActiveWeapon.capacity;
        float ReloadDuration => ActiveWeapon.reload * reloadBoost;
        int BoostedWeaponDamage => ActiveWeapon.damage + weaponBoost + SkillWeaponBonus;
        float cameraKick, weaponKick, aimBlend;
        bool aiming;
        int ammunition, reserveAmmo;
        float reloadTimer;
        int reloadStartAmmo,reloadShellCount;
        bool reloading, reloadBoltPlayed;
        AudioSource radio, effects;
        bool musicEnabled = false, soundEnabled = true, audioUnlocked;
#if UNITY_EDITOR
        bool audioProbeRunning;
        public void EndAudioProbe() { audioProbeRunning = false; }
#endif
        float groanTimer, impactTimer;
        float yaw, pitch = 6;
        int selectedDefense;
        bool buildRayValid;
        Vector3 aim = Vector3.forward, buildPoint;
        int health, wave, kills, supplies, remainingToSpawn, spawnSerial;
        float spawnTimer, fireTimer, damageTimer, pathTimer, bannerTimer;
        bool fighting, dead, building, rotated, validPlacement;
        bool buildMenuVisible;
        string banner;
        GUIStyle titleStyle, textStyle, centerStyle, ammoStyle, roundStyle, buttonStyle, rightStyle;
        bool showHelp;

        sealed class Enemy { public float screamReactionAt = float.PositiveInfinity; public float screamCooldown, rallyRemaining; public Vector3 rallyPoint; public EnemySpecial specialAttack; public bool chargeStopped; public int netId,variant; public Transform body; public DepotModels.Rig rig; public ImportedZombie zombie; public Vector3 position; public int hp = 3; public float attack; public float attackTime=-1,stagger,staggerGuard; public int attackSerial,hitSerial,hitRegion,hitDamage; public bool attackPeer,attackCore; public Barricade attackBarricade; public Turret attackTurret; public Vector3 velocity,hitDirection; public float approachTimer,approachAngle; public float phase; public float vocalTimer, stepTimer; }
        [Serializable] sealed class Barricade { public int netId; public Transform body; public Rect bounds; public float hp = 100; }
        sealed class Flash { public GameObject body; public float life; public Vector3 velocity; public bool casing,settled; public int bounces; }
        sealed class Pickup { public Transform body; public Vector3 position; public int weapon, ammo, reserve, netId; public bool waveDrop; public float readyAt; }
        [Serializable] sealed class Turret { public int netId; public Transform body, head; public Rect bounds; public float hp = 150, cooldown; }
        [Serializable] sealed class Trap { public int netId; public Transform body; public Vector3 position; public float hp = 80; }

        void Awake()
        {
            titleStyle = textStyle = centerStyle = null;
            LoadBindings();
            var roster = new System.Collections.Generic.List<GameObject>();
            var shambler = Resources.Load<GameObject>("MeshyCombat/01-Dockworker-Shambler");
            if (shambler != null) roster.Add(shambler);
            foreach (string name in new[] { "Spitter", "Screamer", "ArmoredWorker", "Charger" })
            {
                var prefab = Resources.Load<GameObject>("MeshyCombat/" + name);
                if (prefab != null) roster.Add(prefab);
            }
            var brute = Resources.Load<GameObject>("MeshyCombat/04-Cargo-Brute")
                ?? Resources.Load<GameObject>("ZombieTypes/04-Cargo-Brute");
            if (brute != null) roster.Add(brute);
            zombieModels = roster.ToArray();
            hasMeshyMonsterRoster = zombieModels.Length > (shambler != null ? 1 : 0);

            RegisterObstacles();
            var existingYard = transform.Find("Warehouse yard");
            if (existingYard == null || existingYard.Find("Warehouse hall") == null)
            {
                if (existingYard != null) { existingYard.gameObject.SetActive(false); Destroy(existingYard.gameObject); }
                BuildEnvironment();
            }
            transform.Find("Warehouse yard").localScale = new Vector3(ArenaScale, 1, ArenaScale);
            DepotModels.EnsureDamagedRoof(this,transform.Find("Warehouse yard/Warehouse hall"));
            SetupDeathPhysicsEnvironment();
            ConfigureCamera();
            SetUpWeather();
            SetupSpaceportExterior();
            actors = new GameObject("Live actors").transform;
            actors.SetParent(transform);
            SetUpAudio();
            ResetRun();
            InitializeFrontEnd();
            InitializeGraphics();
        }

        void RegisterObstacles()
        {
            obstacles.Clear();
            bool compactRacks = transform.Find("Spaceport terminal/Textured server racks") != null;
            float width = compactRacks ? RackBankWidth / ArenaScale : 6f;
            float depth = compactRacks ? RackBankDepth / ArenaScale : 4f;
            foreach (int x in new[] { -10, 10 })
                foreach (int z in new[] { -9, 9 })
                    obstacles.Add(new Rect(x - width / 2, z - depth / 2, width, depth));
            perimeter.Clear();
            wallVolumes.Clear();
            RegisterWarehouseWalls();
            ScaleArenaNavigation();

        }
        // Unity recreates nonserialized lists after a script reload in Play Mode.
        void OnEnable() { RegisterObstacles(); }
        void AddPerimeter(Rect footprint, float height)
        {
            perimeter.Add(footprint);
            wallVolumes.Add(new Bounds(new Vector3(footprint.center.x, height / 2, footprint.center.y), new Vector3(footprint.width, height, footprint.height)));
        }

        public void ConfigureCamera()
        {
            arenaCamera = Camera.main;
            if (arenaCamera == null) { var obj = new GameObject("Main Camera"); obj.tag = "MainCamera"; arenaCamera = obj.AddComponent<Camera>(); obj.AddComponent<AudioListener>(); }
            arenaCamera.transform.position = new Vector3(0, 1.65f, 0);
            arenaCamera.transform.rotation = Quaternion.Euler(6, 0, 0);
            arenaCamera.orthographic = false;
            arenaCamera.fieldOfView = baseFov;
            ConfigureViewmodelCamera();
            arenaCamera.nearClipPlane = .045f;
            arenaCamera.farClipPlane = 240;
            arenaCamera.clearFlags = CameraClearFlags.Skybox;
            if (wastelandSky != null) RenderSettings.skybox = wastelandSky;
            arenaCamera.backgroundColor = new Color(.095f, .115f, .085f);
        }

        public void BuildEnvironment()
        {
            RegisterObstacles();
            var yard = new GameObject("Warehouse yard").transform;
            yard.SetParent(transform, false);
            DepotModels.WarehouseEnvironment(this, yard);
        }
        GameObject Box(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name; obj.transform.SetParent(parent); obj.transform.position = position; obj.transform.localScale = scale;
            obj.GetComponent<Collider>().enabled = false;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }

        public void ResetRun()
        {
            SaveRunRecap();
            ResetPreparationTutorial();
            ResetRunRecap();
            if (deathSting != null) deathSting.Stop();
            StopBarricadeAudio();
            ResetDrowsiness();
            ClearBloodStains();
            ResetWeaponInventory();
            ResetRecoil();
            ResetExplosiveBarrels();
            suppliedWeapons.Clear();supplyBoxes.Clear();nextCoopEntity=1;coopEpoch++;
            nextWaveCountdown=-1;
            RestoreFrontEndEffects();
            ClearArsenalProjectiles();
            ClearSpecialAttacks();
            minigunSpinning=false;
            ResetMovementAudio();
            ResetWalkWeight();
            StopWarehouseAudio();
            ResetPlayerDamageVoice();
            if (gun != null) { gun.gameObject.SetActive(false); Destroy(gun.gameObject); gun = null; }
            RestoreHologram();
            arenaCamera.transform.SetParent(null, true);
            foreach (Transform child in actors) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            enemies.Clear(); barricades.Clear(); flashes.Clear(); turrets.Clear(); traps.Clear(); pickups.Clear(); HideWeaponTrail();
            coopFiringPlayer=0;coopReviveProtectedUntil=0;
            ResetCoreProgression();
            ResetInjuryFeedback();
            health = maxPlayerHealth; wave = kills = spawnSerial = 0; supplies = StartingScrap; remainingToSpawn = 0;
            ResetRampageVoice();
            ResetMonsterVoice();
            fighting = dead = building = rotated = false;
            buildMenuVisible=false;
            RestoreHologram();
            buildFireBlocked = false; selectedDefense = 0; yaw = 0; pitch = 6; showHelp = false;
            bindingAction = -1;
            fireTimer = damageTimer = pathTimer = 0;
            jumpHeight = verticalSpeed = slideTimer = slideCooldown = 0; eyeHeight = 1;
            slideDirection = Vector3.zero;
            dodge = default;
            chargerLessonActive = false;
            pendingPeerDodge = false;
            weaponId = WeaponCatalog.Starter; ammunition = MagazineSize; reloading = reloadBoltPlayed = false; reloadTimer = cameraKick = weaponKick = aimBlend = 0;
            aim = Vector3.forward;
            playerRig = DepotModels.Player(this, actors, new Vector3(0, .65f, HasCoreObjective ? -4 : 0));
            player = playerRig.root; gun = playerRig.gun;
            foreach (var renderer in playerRig.head.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            arenaCamera.transform.SetParent(player, false);
            arenaCamera.transform.localPosition = new Vector3(0, 1, 0);
            arenaCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            gun.SetParent(arenaCamera.transform, false); gun.localPosition = new Vector3(.28f, -.25f, .48f); gun.localScale = Vector3.one * .8f;
            foreach (var renderer in player.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            EquipWeapon(WeaponCatalog.Starter, MagazineSize);
            CreateDefensePreviews();
            SetUpFlashlight();
            if(hammerRig!=null)Destroy(hammerRig.gameObject);SetupHammer();
            ReleaseMouse();
            ShowBanner("DEPOT READY");
            RefreshPaths();
            weaponPickupWave = 0; previousWaveWeapon = -1;
            SpawnWaveSupplies(1);
            // Wave rewards replace the chest and slot machine.
            ResetCoopPeerForRun();
            ResetAdaptiveAudio();
            CreateEntranceBarricades();
        }

        GameObject ModelFor(int id) => animatedWeaponModels[id];
        void EquipWeapon(int id, int rounds, int reserve=-1)
        {
            CaptureInventoryWeapon();
            if(!IsCoopGuest && !weaponInventory.Exists(w=>w.weapon==id) && weaponInventory.Count>=WeaponSlotLimit)
                if(!DropInventoryWeapon(weaponInventory.FindIndex(w=>w.weapon==weaponId)))return;
            ResetRecoil();
            if (gun != null) { gun.gameObject.SetActive(false); Destroy(gun.gameObject); }
            weaponId = id; ammunition = Mathf.Clamp(rounds, 0, MagazineSize);
            reserveAmmo=WeaponCatalog.IsMelee(id)?0:Mathf.Clamp(reserve<0?MagazineSize*3:reserve,0,MagazineSize*5);
            aiming=false;aimBlend=0;arenaCamera.fieldOfView=baseFov;
            reloading = reloadBoltPlayed = false; reloadTimer = fireTimer = cameraKick = weaponKick = 0;
            gun = new GameObject("First person " + ActiveWeapon.name).transform; gun.SetParent(arenaCamera.transform, false);
            viewWeapon = gun.gameObject.AddComponent<ViewWeapon>(); viewWeapon.Initialize(ModelFor(id), this, id);
            RememberEquippedWeapon();
        }
        void Update()
        {
            TickGraphicsResolution();
            TickRunRecap();
            if (titleVoice != null) titleVoice.volume = soundEnabled ? .85f * soundVolume : 0;
            if (deathSting != null) deathSting.volume = soundEnabled ? .55f * soundVolume : 0;
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if ((keyboard != null && keyboard.anyKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame)) UnlockAudio();
            TickCoreVisual();TickCoreAnnouncer();
            TickInjuryFeedback();
            if (FrontEndUpdate()) {UpdateArsenalAudioEvents();return;}
            UpdateArsenalAudioEvents();
            bool hologramWasBlocking = HologramBlocking;
            TickHologram();
            if (HologramBlocking || hologramWasBlocking) return;
            if(upgradePending){ReleaseMouse();PauseMovementAudio(true);TickUpgradeSelection();return;}
            if (bindingAction >= 0) PauseMovementAudio(true);
            if (CaptureBinding()) return;
            if (keyboard != null && keyboard.hKey.wasPressedThisFrame) { showHelp = !showHelp; if (showHelp) ReleaseMouse(); }
            TickPreparationTutorial();
            if (PreparationKeyboardVisible && !showHelp) { PauseMovementAudio(true); return; }
            UpdateWeather();
            float dt = Mathf.Min(Time.deltaTime, .05f);
            bannerTimer -= dt;
            UpdateFlashes(dt);
            UpdateAdaptiveAudio(dt);
            PauseMovementAudio(showHelp || HologramBlocking);
            if (showHelp) { aiming = false; ghost.gameObject.SetActive(false); UpdateFlashlight(); return; }
            if (Pressed(Control.Music)) ToggleMusic();
            if (Pressed(Control.Sound)) ToggleSound();
            if (Pressed(Control.Restart)) {if(IsCoopGuest)SendCoopCommand("restart");else ResetRun();return;}
            if (dead) { UpdateFlashlight(); return; }
            TickDrowsiness(dt);
            TickRampageVoice();
            damageTimer -= dt; fireTimer -= dt;
            bool buildToggle = UpdateBuildingInput();
            if (HologramBlocking) return;
            if (!buildToggle) UpdateWeaponInventoryInput();
            UpdateReload(dt);
            TickRecoil(dt);
            aiming = HasEquippedWeapon && WeaponCatalog.CanAim(weaponId) && knockdownTime <= 0 && !building && !reloading && Held(Control.Aim) && Cursor.lockState == CursorLockMode.Locked;
            aimBlend = Mathf.MoveTowards(aimBlend, aiming ? 1 : 0, dt * 7);
            arenaCamera.fieldOfView = Mathf.Lerp(baseFov, WeaponCatalog.IsScoped(weaponId) ? Mathf.Min(42,baseFov*.55f) : baseFov*.77f, aimBlend);
            if (Pressed(Control.Reload) && !building) BeginReload();
            if (Pressed(Control.Pickup) && !building && !reloading && !InteractCoopPartner()) TakePickup(NearbyPickup());
            groanTimer -= dt; impactTimer -= dt;
            Vector2 input = Vector2.zero;

            {
                input.x = (Held(Control.Right) ? 1 : 0) - (Held(Control.Left) ? 1 : 0);
                input.y = (Held(Control.Forward) ? 1 : 0) - (Held(Control.Back) ? 1 : 0);
                if (!fighting && Pressed(Control.Wave)) StartWave();
            }
            bool capturedThisFrame = false;
            if(building&&buildMenuVisible&&mouse!=null)
            {
                if(mouse.rightButton.wasPressedThisFrame){CaptureMouse();capturedThisFrame=true;}
                if(mouse.rightButton.wasReleasedThisFrame)ReleaseMouse();
            }
            if (!HologramBlocking && !building && mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked && !MouseOverHud(mouse.position.ReadValue()))
            {
                CaptureMouse(); capturedThisFrame = true;
            }
            if (Cursor.lockState == CursorLockMode.Locked && mouse != null)
            {
                Vector2 look = capturedThisFrame ? Vector2.zero : mouse.delta.ReadValue();
                yaw += look.x * .12f * mouseSensitivity; pitch = Mathf.Clamp(pitch - look.y * .12f * mouseSensitivity, -89, 75);
            }
            player.rotation = Quaternion.Euler(0, yaw, 0);
            arenaCamera.transform.localRotation = Quaternion.Euler(pitch - cameraKick, recoilSide, 0);
            bool movementActions = knockdownTime <= 0 && !showHelp && Cursor.lockState == CursorLockMode.Locked;
            UpdatePlayerMovement(input,
                movementActions && Pressed(Control.Jump),
                movementActions && Pressed(Control.Slide), dt,
                movementActions && Pressed(Control.Dodge));
            ApplyDrowsyAim();
            ApplyInjuryCamera();
            TickMonsterVoice();
            Vector3 pos = player.position; pos.y = 0;
            if (mouse != null)
            {
                Ray ray = Cursor.lockState==CursorLockMode.Locked?new Ray(arenaCamera.transform.position,arenaCamera.transform.forward):arenaCamera.ScreenPointToRay(mouse.position.ReadValue());
                buildRayValid = new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float t) && t > 0 && t < DefensePlacementRange + 1;
                if (buildRayValid)
                {
                    Vector3 target = ray.GetPoint(t); Vector3 direction = target - pos;
                    buildPoint = OnGround(new Vector3(Mathf.Round(target.x), 0, Mathf.Round(target.z)));
                }
                bool wantsShot = WeaponCatalog.IsAutomatic(weaponId) ? Held(Control.Fire) : Pressed(Control.Fire);
                if (knockdownTime <= 0 && !building && !buildToggle && !buildFireBlocked && !capturedThisFrame && Cursor.lockState == CursorLockMode.Locked && wantsShot && fireTimer <= 0)
                {
                    float spread = aiming ? .12f : input.magnitude > 0 ? 1.3f : .55f;
                    Fire(Quaternion.Euler(UnityEngine.Random.Range(-spread, spread), UnityEngine.Random.Range(-spread, spread), 0) * arenaCamera.transform.forward);
                }
                if (building && !buildToggle && !buildFireBlocked && Held(Control.Fire) && Cursor.lockState==CursorLockMode.Locked) StartHammer(ray, Pressed(Control.Fire));
            }
            playerRig.Animate(Time.time, input.magnitude > 0 ? 1 : 0, false);
            if (viewWeapon != null) viewWeapon.Tick(reloading ? 1 - reloadTimer / ReloadDuration : 0, reloading, weaponKick, input.magnitude, aimBlend, recoilSide);
            else if(gun!=null)gun.localRotation = Quaternion.Euler(-Mathf.Max(0, fireTimer) * 45, 0, 0);
            TickHammer(dt);
            UpdateHeldHammer();
            UpdateDefensePreview();
            if (HologramBlocking) ghost.gameObject.SetActive(false);
            UpdateFlashlight();
            UpdateSupplyBoxes();
            UpdateWeaponPickups();
            if(IsCoopGuest)return;
            TickWaveSimulation(dt);
        }
        void TickWaveSimulation(float dt)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(qaCombatArena)return;
#endif
            if (wave > 0 && !runRecapSaved && (!dead || PartnerCanContinue)) runSeconds += dt;
            if (!fighting) {TickNextWave(dt);return;}
            spawnTimer -= dt;
            if (remainingToSpawn > 0 && spawnTimer <= 0 && enemies.Count < 80)
            {
                SpawnEnemy(NextZombieEntrance()); remainingToSpawn--;
                spawnTimer = Mathf.Max(.12f, .65f - wave * .035f);
            }
            pathTimer -= dt;
            if (pathTimer <= 0) { RefreshPaths(); pathTimer = .3f; }
            UpdateEnemies(dt);
            UpdateTurrets(dt);
            if ((!dead||(HasCoopPartner&&peer.health>0)) && enemies.Count == 0 && remainingToSpawn == 0)
                CompleteWave();
        }

        void CompleteWave()
        {
            runWavesCleared = wave;
            fighting = false; supplies += WaveScrap;
            if (HasCoreObjective) { AwardWaveSkillPoint(); nextWaveCountdown = 15; RequestAnnouncement(4); }
            else nextWaveCountdown = 5;
            PlayEffect(waveSound, .55f);
            SpawnWaveSupplies(wave+1);
            ShowBanner(HasCoreObjective ? "WAVE COMPLETE! +1 SKILL POINT / TAB > SKILLS / +6 scrap" : "WAVE COMPLETE! +6 scrap / New supply crates in the depot");
            BeginPreparationTutorial();
            // The next weapon appears when the next wave starts.
        }

        public void StartWave()
        {
            if(IsCoopGuest){SendCoopCommand("wave");return;}
            if (dead || fighting || upgradePending || coreLost) return;
            ResetPreparationTutorial();
            nextWaveCountdown=-1;
            fighting = true; building = false; wave++; remainingToSpawn = WaveZombieCount(wave); spawnTimer = 0;
            int openingHorde = Mathf.Min(remainingToSpawn, Mathf.Min(60, 24 + wave * 4));
            for (int i = 0; i < openingHorde; i++) { SpawnEnemy(NextZombieEntrance()); remainingToSpawn--; }
            SpawnWaveWeapons(wave);
            CaptureMouse();
            if (wave > 1) PlayEffect(waveSound, .5f);
            RequestAnnouncement(3);
            ShowWaveLesson();
        }

        void SpawnEnemy(Vector3 position)
        {
            bool summoned = screamReinforcements.Count > 0;
            int modelVariant = summoned ? 0 : SelectEnemyVariant();
            if (zombieModels != null && zombieModels.Length > 0 && zombieModels[0] != null)
            {
                var root = new GameObject("Imported wasteland zombie").transform; root.SetParent(actors, false); root.position = position;
                var model = Instantiate(zombieModels[modelVariant], root);
                var zombie = model.GetComponent<ImportedZombie>(); zombie.Tick(Time.time + enemies.Count * 1.7f, false);
                enemies.Add(new Enemy { body = root, zombie = zombie, position = position, phase = enemies.Count * 1.7f });
            }
            else
            {
                var rig = DepotModels.Ghoul(this, actors, position + Vector3.up * .65f, spawnSerial + enemies.Count);
                enemies.Add(new Enemy { body = rig.root, rig = rig, position = position, phase = enemies.Count * 1.7f });
            }
            var spawned = enemies[enemies.Count - 1];
            spawned.hp = WaveZombieHealth(spawned.zombie != null ? spawned.zombie.ArchetypeHealth : 3, wave, zombieModels != null && modelVariant < zombieModels.Length && zombieModels[modelVariant] != null && zombieModels[modelVariant].name == "Charger");
            spawned.hp = Mathf.Max(1, Mathf.RoundToInt(spawned.hp * HealthScale(runDifficulty)));
            spawned.netId=nextCoopEntity++;spawned.variant=modelVariant;
            if (spawned.zombie != null) spawned.zombie.Runner = !IsCoopGuest && !summoned && SelectRunner(wave, spawnSerial, modelVariant);
            spawned.screamCooldown = 1.5f + .25f * (spawned.netId % 5);
            if(summoned){spawned.rallyPoint=screamReinforcements.Dequeue();spawned.rallyRemaining=28;}
            spawned.vocalTimer = UnityEngine.Random.Range(1f, 4f); spawned.stepTimer = UnityEngine.Random.Range(0f, 1f);
            if (spawned.zombie != null) { spawned.zombie.ConfigureMotion(spawned.netId); spawned.zombie.audioOwner = this; spawned.zombie.DressWounds(woundMaterial); }
        }

        void UpdateEnemies(float dt)
        {
            RebuildCrowdGrid();
            Vector3 hostPos = player.position; hostPos.y = 0;
            foreach (var e in enemies)
            {
                Vector2Int oldCell = CrowdCell(e.position);
                try
                {
                e.screamCooldown=Mathf.Max(0,e.screamCooldown-dt);e.rallyRemaining=Mathf.Max(0,e.rallyRemaining-dt);
                e.approachTimer-=dt; e.attack -= dt; e.staggerGuard=Mathf.Max(0,e.staggerGuard-dt);
                if(e.zombie!=null&&e.zombie.IsCollapsing)
                {
                    e.attackTime=-1;e.attack=.15f;e.attackBarricade=null;e.attackTurret=null;e.velocity=Vector3.zero;
                    e.zombie.TickMovement(dt,0,false);continue;
                }
                if(e.attackTime>=0){TickZombieAttack(e,dt);continue;}
                if(e.stagger>0){e.stagger=Mathf.Max(0,e.stagger-dt);e.velocity=Vector3.zero;e.zombie?.TickMovement(dt,0,false);continue;}
                if (e.zombie == null) e.rig.Animate(Time.time + e.phase, 1, true);
                if (TickExteriorApproach(e, dt)) continue;
                bool targetPeer=HasCoopPartner&&peer.health>0&&(dead||(peer.position-e.position).sqrMagnitude<(hostPos-e.position).sqrMagnitude);
                if(dead&&!targetPeer)continue;
                Vector3 playerPos=targetPeer?new Vector3(peer.position.x,0,peer.position.z):hostPos;
                if ((!dead || targetPeer) && TryBeginSpecialAttack(e, targetPeer, playerPos, dt)) continue;
                bool huntingPlayer = zombieModels != null && zombieModels[e.variant].name == "Charger" && (playerPos-e.position).sqrMagnitude <= 14f*14f;
                bool targetCore=!huntingPlayer && HasCoreObjective && (dead || (playerPos-e.position).sqrMagnitude>9 || !PickupLineClear(e.position,playerPos));
                if(targetCore)playerPos=Vector3.zero;
                bool rallying=!huntingPlayer && e.rallyRemaining>0 && (e.rallyPoint-e.position).sqrMagnitude>9 && ClearPursuitLine(e.position,e.rallyPoint);
                if(rallying){playerPos=e.rallyPoint;targetCore=false;targetPeer=false;}
                Vector3 delta = playerPos - e.position;
                UpdateZombieAudio(e, dt, delta.magnitude);
                if(!rallying && delta.magnitude<(targetCore?2.15f:1.05f) && PickupLineClear(e.position,playerPos))
                {
                    TurnZombie(e.body,delta,dt);e.velocity=Vector3.zero;
                    if(e.attack<=0)BeginZombieAttack(e,targetPeer,null,null,targetCore);
                    e.zombie?.TickMovement(dt,0,e.attackTime>=0,0);continue;
                }
                Vector3 desired = ZombiePursuit(e,playerPos,targetPeer,targetCore);
                float speed = (1.1f + Mathf.Min(wave, 10) * .065f)*(.94f+.12f*(.5f+.5f*Mathf.Sin(e.phase*3.1f)));
                if(e.rallyRemaining>0)speed*=1.15f;
                if(e.zombie != null && e.zombie.IsCharger) speed *= 2.2f;
                if (e.zombie != null) speed *= e.zombie.ArchetypeSpeed(Time.time, e.phase) * e.zombie.RunnerSpeedMultiplier;
                if (e.zombie != null) speed *= e.zombie.InjuredLegs == 2 ? .25f : e.zombie.InjuredLegs == 1 ? .55f : 1;
                foreach (var trap in traps) if (ConstructionReady(trap.body) && (e.position - trap.position).sqrMagnitude < 1.7f * 1.7f) { speed *= .4f; trap.hp -= dt * 5; break; }
                desired=SteerZombieCrowd(e,desired,speed);
                float facing=Vector3.Dot(e.body.forward,desired.normalized);
                float turnSpeed=Mathf.Lerp(.35f,1,Mathf.InverseLerp(-.2f,.8f,facing));
                e.velocity=Vector3.MoveTowards(e.velocity,desired.normalized*speed*turnSpeed,3.4f*dt);
                Vector3 step=Vector3.ClampMagnitude(e.velocity*dt,desired.magnitude);
                Barricade blocking = null;
                foreach (var b in barricades) if (Touches(b.bounds, e.position + step, EnemyRadius + .15f)) { blocking = b; break; }
                Turret blockedTurret = null;
                foreach (var turret in turrets) if (Touches(turret.bounds, e.position + step, EnemyRadius + .15f)) { blockedTurret = turret; break; }
                Vector3 beforeMove = e.position;
                if(blocking!=null||blockedTurret!=null)
                {
                    e.velocity=Vector3.zero;
                    if(e.attack<=0)BeginZombieAttack(e,false,blocking,blockedTurret);
                }
                else
                {
                    e.position = MoveZombieInCrowd(e,step);
                }
                e.body.position = OnGround(e.position,e.zombie != null ? 0 : .65f);
                Vector3 travel = e.position - beforeMove;
                if (e.zombie != null) e.zombie.TickMovement(dt, travel.magnitude, e.attackTime>=0, e.attackTime>=0?e.attackTime/ZombieAttackDuration:-1);
                TurnZombie(e.body, desired, dt);
                }
                finally { UpdateCrowdCell(e, oldCell); }
            }
            crowdGridActive = false;
            for (int i = barricades.Count - 1; i >= 0; i--)
                if (barricades[i].hp <= 0) { Destroy(barricades[i].body.gameObject); barricades.RemoveAt(i); }
            for (int i = turrets.Count - 1; i >= 0; i--)
                if (turrets[i].hp <= 0) { Destroy(turrets[i].body.gameObject); turrets.RemoveAt(i); }
            for (int i = traps.Count - 1; i >= 0; i--)
                if (traps[i].hp <= 0) { Destroy(traps[i].body.gameObject); traps.RemoveAt(i); }
        }
        float deltaForAnimation(Enemy enemy) => (enemy.position - new Vector3(player.position.x, 0, player.position.z)).magnitude;
        static void TurnZombie(Transform body, Vector3 direction, float dt)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > .00001f)
                body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction), 180 * dt);
        }

        public void DamagePlayer(int amount)
        {
            if (dead || damageTimer > 0) return;
            if (amount <= 0) return;
            health = Mathf.Max(0, health - PlayerSkillDamage(IncomingDamage(amount))); damageTimer = .3f;
            RegisterInjuryHit();
            PlayPlayerDamageAudio();
            ApplyPlayerFailsafe();
            if (health == 0) BeginDeathSequence();
        }

        public void Fire(Vector3 direction)
        {
            if (!HasEquippedWeapon || dead || reloading) return;
            if (ammunition <= 0) { BeginReload(); return; }
            if (!WeaponCatalog.IsMelee(weaponId)) ammunition--;
            fireTimer = ActiveWeapon.interval;
            ApplyWeaponRecoil();
            if (viewWeapon != null) viewWeapon.OnShot();
            effects.pitch = 1;
            PlayWeaponShot();
            if(IsCoopGuest)SendCoopCommand("fire",point:direction);else {FireWeaponProjectiles(direction);RecordCoopShot(NetworkManagerLocalId(),weaponId,arenaCamera.transform.position);}
            if (WeaponCatalog.IsMelee(weaponId) || weaponId == 7) return;
            EjectCasing();
        }
        public void BeginReload()
        {
            if (!HasEquippedWeapon || dead || reloading || ammunition == MagazineSize || reserveAmmo<=0 || WeaponCatalog.IsMelee(weaponId)) return;
            if(IsCoopGuest)SendCoopCommand("reload");
            reloadStartAmmo=ammunition;reloadShellCount=Mathf.Min(MagazineSize-ammunition,reserveAmmo);viewWeapon?.SetReloadShellCount(reloadShellCount);
            reloading = true; reloadBoltPlayed = false; reloadTimer = ReloadDuration;
            effects.pitch = 1;
            PlayEffect(WeaponClip(1), .65f);
        }
        void UpdateReload(float dt)
        {
            if (!reloading) return;
            reloadTimer -= dt;
            if(weaponId==3)TransferReloadShells(1-reloadTimer/ReloadDuration,reloadStartAmmo,reloadShellCount,ref ammunition,ref reserveAmmo);
            if (!reloadBoltPlayed && reloadTimer < .45f) { reloadBoltPlayed = true; if (weaponSounds == null || weaponId >= weaponSounds.Length || weaponSounds[weaponId] == null || !weaponSounds[weaponId].reloadIncludesAction) PlayEffect(WeaponClip(2), .65f); }
            if (reloadTimer <= 0) { int transfer=weaponId==3?0:Mathf.Min(MagazineSize-ammunition,reserveAmmo);ammunition+=transfer;reserveAmmo-=transfer;reloading = false; reloadTimer = 0; }
        }
        void TraceShot(Vector3 origin, Vector3 direction, Turret owner)
        {
            if(owner==null&&(WeaponCatalog.IsScoped(weaponId)||weaponId==13)){TraceSniperShot(origin,direction);return;}
            float nearest = ShotStopDistance(origin, direction, owner);
            var barrelHit = TraceBarrel(origin, direction, ref nearest);
            Enemy hit = null;
            int hitRegion = 5;
            foreach (var e in enemies)
            {
                if (e.zombie != null)
                {
                    if (e.zombie.Trace(new Ray(origin, direction), nearest, out float zombieEntry, out int region)) { nearest = zombieEntry; hit = e; hitRegion = region; }
                    continue;
                }
                var box = new Bounds(OnGround(e.position,.9f), new Vector3(.95f, 1.8f, .95f));
                if (box.IntersectRay(new Ray(origin, direction), out float entry) && entry >= 0 && entry < nearest) { nearest = entry; hit = e; }
            }
            bool friendlyPeer=false;bool friendlyHit=owner==null&&TraceFriendlyPlayer(origin,direction,coopFiringPlayer,ref nearest,out friendlyPeer);
            if(friendlyHit){DamageFriendlyPlayer(friendlyPeer,ActiveWeapon.damage,origin+direction*nearest,direction);hit=null;barrelHit=null;}
            else if (hit != null)
            {
                ApplyRayDamage(hit,hitRegion,origin+direction*nearest,direction,owner==null?BoostedWeaponDamage:1);
            }
            else if (barrelHit != null) DetonateBarrel(barrelHit);
            else if (nearest < 17 && impactTimer <= 0) { PlayEffect(scrapSound, .2f); impactTimer = .18f; }
            if (hit == null && !friendlyHit && barrelHit == null && nearest < 51)
                for (int i = 0; i < 5; i++)
                {
                    var puff = Box("Concrete impact", origin + direction * (nearest - .04f), Vector3.one * .035f, dustMaterial, actors);
                    flashes.Add(new Flash { body = puff, life = .23f + i * .025f, velocity = -direction * .7f + new Vector3(UnityEngine.Random.Range(-.4f, .4f), UnityEngine.Random.Range(.1f, .9f), UnityEngine.Random.Range(-.4f, .4f)) });
                }
        }

        static float RayVolume(Vector3 origin, Vector3 direction, Rect rect, float height, float baseHeight = 0)
        {
            float low = 0, high = 52;
            if (!Slab(origin.x, direction.x, rect.xMin, rect.xMax, ref low, ref high) ||
                !Slab(origin.y, direction.y, baseHeight, baseHeight+height, ref low, ref high) ||
                !Slab(origin.z, direction.z, rect.yMin, rect.yMax, ref low, ref high)) return 52;
            return low;
        }
        // Trace the same wall, buttress and gate-post footprints used by the visible models.
        // Open gates stay open, but a ray is clipped at the expanded outer yard edge.
        MeshCollider fullRoofCollider;
        float ShotStopDistance(Vector3 origin, Vector3 direction, Turret owner = null)
        {
            float nearest = 52;
            if(fullRoofCollider!=null && fullRoofCollider.Raycast(new Ray(origin,direction),out var roofHit,nearest)) nearest=roofHit.distance;
            if(HasCoreObjective&&coreVisual.HasMeshyPlatform)nearest=coreVisual.PlatformShotDistance(new Ray(origin,direction),nearest);
            foreach (Rect rect in obstacles) nearest = Mathf.Min(nearest, RayVolume(origin, direction, rect, 2.65f));
            foreach (Bounds bounds in wallVolumes)
                if (bounds.IntersectRay(new Ray(origin, direction), out float distance) && distance >= 0) nearest = Mathf.Min(nearest, distance);
            foreach (var b in barricades) nearest = Mathf.Min(nearest, RayVolume(origin, direction, b.bounds, 1.2f, GroundHeight(b.body.position)));
            foreach (var turret in turrets) if (turret != owner) nearest = Mathf.Min(nearest, RayVolume(origin, direction, turret.bounds, 1.5f, GroundHeight(turret.body.position)));
            if(luckyChest!=null)nearest=Mathf.Min(nearest,RayVolume(origin,direction,ChestBounds,.85f));
            if(slotMachine!=null)nearest=Mathf.Min(nearest,RayVolume(origin,direction,SlotBounds,2.1f));
            if (direction.y < -.0001f) nearest = Mathf.Min(nearest, (.015f - origin.y) / direction.y);
            // Open doors and windows allow shots into the outdoor horde.
            return Mathf.Max(0, nearest);
        }
        static bool Slab(float o, float d, float min, float max, ref float low, ref float high)
        {
            if (Mathf.Abs(d) < .0001f) return o >= min && o <= max;
            float a = (min - o) / d, b = (max - o) / d;
            low = Mathf.Max(low, Mathf.Min(a, b)); high = Mathf.Min(high, Mathf.Max(a, b)); return low <= high;
        }

        Rect BuildBounds(Vector3 pos, bool vertical) => new Rect(pos.x - (vertical ? .325f : 1.4f), pos.z - (vertical ? 1.4f : .325f), vertical ? .65f : 2.8f, vertical ? 2.8f : .65f);
        bool CanPlace(Vector3 pos, bool vertical)
        {
            return CanPlaceDefense(pos, 0, vertical);
        }
        static readonly int[] DefenseCosts = { 2, 6, 3, 2 };
        const float DefensePlacementRange = 14;
        const float BuildAreaHalfExtent = 16;
        Rect DefenseBounds(Vector3 pos, int type, bool vertical) => type == 0 ? BuildBounds(pos, vertical) : type == 3 ? new Rect(pos.x-.4f,pos.z-.4f,.8f,.8f) : new Rect(pos.x - (type == 1 ? .5f : 1.3f), pos.z - (type == 1 ? .5f : 1.3f), type == 1 ? 1 : 2.6f, type == 1 ? 1 : 2.6f);
        bool CanPlaceDefense(Vector3 pos, int type, bool vertical) => DefensePlacementIssue(pos,type,vertical)==null;
        string DefensePlacementIssue(Vector3 pos, int type, bool vertical)
        {
            if (type < 0 || type > 3 || type == 2 || dead) return "Building unavailable";
            if (supplies < DefenseCosts[type]) return "Need "+DefenseCosts[type]+" scrap to place \u00b7 You have "+supplies;
            if (type == 3 && (BarrelTemplate == null || placedBarrels.Count >= 64)) return "No more barrels available";
            Vector3 playerFloor = player.position; playerFloor.y = 0;
            if ((pos - playerFloor).sqrMagnitude > DefensePlacementRange * DefensePlacementRange) return "Move closer \u00b7 Placement range 14 m";
            if(!PickupLineClear(player.position,pos))return "Placement blocked \u00b7 Choose a visible spot";
            Rect rect = DefenseBounds(pos, type, vertical);
            if(HasCoreObjective && coreVisual.HasMeshyPlatform)
            {
                if(Touches(rect,Vector3.zero,1.2f))return "Leave space around the heart";
                foreach(var barrier in coreVisual.BayBarriers)
                    if(Expanded(new Rect(barrier.min.x,barrier.min.z,barrier.size.x,barrier.size.z),.1f).Overlaps(rect))return "Leave space around the platform machinery";
                float floor=GroundHeight(pos);
                foreach(var corner in new[]{new Vector3(rect.xMin,0,rect.yMin),new Vector3(rect.xMax,0,rect.yMin),new Vector3(rect.xMin,0,rect.yMax),new Vector3(rect.xMax,0,rect.yMax)})
                    if(Mathf.Abs(GroundHeight(corner)-floor)>.035f)return "Choose flat ground away from the ramp";
            }
            if(slotMachine!=null&&Expanded(SlotBounds,.2f).Overlaps(rect))return "Leave space around the station";
            if (rect.xMin < -BuildAreaHalfExtent || rect.xMax > BuildAreaHalfExtent || rect.yMin < -BuildAreaHalfExtent || rect.yMax > BuildAreaHalfExtent) return "Outside the building area";
            foreach (Rect obstacle in obstacles) if (Expanded(obstacle, .15f).Overlaps(rect)) return "Too close to a wall or obstacle";
            foreach (var barrel in explosiveBarrels) if (barrel != null && !barrel.Detonated && barrel.isActiveAndEnabled && barrel.Footprint.Overlaps(rect)) return "Too close to a barrel";
            foreach (var b in barricades) if (Expanded(b.bounds, .2f).Overlaps(rect)) return "Too close to an existing barricade";
            foreach (var turret in turrets) if (Expanded(turret.bounds, .2f).Overlaps(rect)) return "Too close to an existing turret";
            foreach (var trap in traps) if (Expanded(DefenseBounds(trap.position, 2, false), .2f).Overlaps(rect)) return "Too close to an existing trap";
            return Touches(rect, player.position, PlayerRadius + .25f)?"Step back from the placement spot":null;
        }
        public bool TryPlaceBarricade(Vector3 pos, bool vertical)
        {
            return TryPlaceDefense(pos, 0, vertical);
        }
        public bool TryPlaceDefense(Vector3 pos, int type, bool vertical)
        {
            string issue=DefensePlacementIssue(pos,type,vertical);
            if(issue!=null){ShowBanner(issue);return false;}
            pos=OnGround(pos);
            if(IsCoopGuest){SendCoopCommand("build",0,type,pos,vertical);return true;}
            if (type == 3) { SpawnPlacedBarrel(nextCoopEntity++, pos); supplies -= DefenseCosts[3]; PlayPlacementSound(type); OnPreparationPlaced(null); return true; }
            Rect rect = DefenseBounds(pos, type, vertical);
            if (type == 0) barricades.Add(new Barricade { netId=nextCoopEntity++,body = DepotModels.Barricade(this, actors, pos + Vector3.up * .6f, vertical), bounds = rect });

            else if (type == 1)
            {
                Transform head; Transform body = DepotModels.Turret(this, actors, pos, out head);
                turrets.Add(new Turret { netId=nextCoopEntity++,body = body, head = head, bounds = rect });
            }
            else traps.Add(new Trap { netId=nextCoopEntity++,body = DepotModels.SlowTrap(this, actors, pos), position = pos });
            if(type==0)DefensePhysics(barricades[barricades.Count-1].body,rect,1.2f);
            else if(type==1)DefensePhysics(turrets[turrets.Count-1].body,rect,1.5f);
            Transform placed=type==0?barricades[barricades.Count-1].body:type==1?turrets[turrets.Count-1].body:traps[traps.Count-1].body;
            InitializeConstruction(placed,type); OnPreparationPlaced(placed);PlayPlacementSound(type); return true;
        }

        Enemy TurretTarget(Turret turret)
        {
            Enemy best = null; float bestDistance = 12;
            foreach (var enemy in enemies)
            {
                Vector3 delta = OnGround(enemy.position,1) - turret.head.position; float length = delta.magnitude;
                if (length < bestDistance && length > .01f && ShotStopDistance(turret.head.position, delta / length, turret) >= length - .02f)
                { best = enemy; bestDistance = length; }
            }
            return best;
        }
        void UpdateTurrets(float dt)
        {
            foreach (var turret in turrets)
            {
                if(!ConstructionReady(turret.body))continue;
                turret.cooldown -= dt;
                Enemy target = TurretTarget(turret);
                if (target == null) { turret.head.Rotate(0, 12 * dt, 0); continue; }
                Vector3 direction = (OnGround(target.position,1) - turret.head.position).normalized;
                turret.head.rotation = Quaternion.LookRotation(direction);
                if (turret.cooldown <= 0)
                {
                    // Use receiver origin for collision, so a barrel poking through cover cannot bypass it.
                    TraceShot(turret.head.position, direction, turret);ShowTurretShot(turret.head.position,direction,OnGround(target.position,1));RecordTurretFeedback(turret.head.position,OnGround(target.position,1));turret.cooldown = skillRanks[8]>=2 ? .5f : .6f; turret.hp=Mathf.Max(0,turret.hp-.08f);
                }
            }
        }
        void SelectDefense(int type)
        {
            if (type < 0 || type >= DefenseCosts.Length || type == 2) return;
            if (!building) PlayEquipmentSwitch();
            reloading = false; reloadTimer = 0;
            OnPreparationBlueprintSelected();
            selectedDefense = type; building = true; buildMenuVisible = false;
            buildFireBlocked = Held(Control.Fire); aiming = false;
            UpdateDefensePreview(); UpdateHeldHammer(); CaptureMouse();
        }
        bool buildFireBlocked;
        bool UpdateBuildingInput()
        {
            if (!HologramBlocking && !Held(Control.Fire)) buildFireBlocked = false;
            // Mode changes own the frame so number keys cannot also switch weapons.
            if (Pressed(Control.EquipHammer)) { ToggleHammerSlot(); return true; }
            if (!building) return false;
            if (Keyboard.current != null && !InventoryKeyConflicts(Key.Digit4) && Keyboard.current.digit4Key.wasPressedThisFrame)
            { SwitchInventorySlot(3); return true; }
            int selection = -1;
            if (Pressed(Control.Barricade)) selection = 0;
            else if (Pressed(Control.Turret)) selection = 1;
            else if (Pressed(Control.Barrel)) selection = 3;
            else if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .01f)
                    selection = AvailableDefenses[(System.Array.IndexOf(AvailableDefenses, selectedDefense) + (scroll > 0 ? -1 : 1) + AvailableDefenses.Length) % AvailableDefenses.Length];
            }
            if (selection >= 0) { SelectDefense(selection); return true; }
            if (selectedDefense == 0 && Pressed(Control.Rotate))
            { rotated = !rotated; buildFireBlocked = Held(Control.Fire); return true; }
            return false;
        }
        void UpdatePlayerMovement(Vector2 input, bool wantsJump, bool wantsSlide, float dt, bool wantsDodge = false)
        {
            if (knockdownTime > 0)
            {
                UpdateKnockdown(dt);
                return;
            }
            Vector3 direction = player.right * input.normalized.x + player.forward * input.normalized.y;
            slideCooldown = Mathf.Max(0, slideCooldown - dt);
            bool grounded = jumpHeight <= 0 && verticalSpeed <= 0;
            bool startedDodge = BeginDodge(ref dodge, wantsDodge && grounded && !wantsJump,
                direction, player.forward, dt);
            if (startedDodge)
            {
                chargerLessonActive = false;
                if (slideTimer > 0) EndSlide();
            }
            // Jump takes priority and can cancel an active slide. Holding a key never repeats it.
            if (wantsJump && grounded)
            {
                verticalSpeed = JumpSpeed;
                if (slideTimer > 0) EndSlide();
                PlayJumpEvent(false);
            }
            else if (wantsSlide && grounded && dodge.remaining <= 0 && slideTimer <= 0 && slideCooldown <= 0 && input.sqrMagnitude > .01f)
            {
                slideDirection = direction;
                slideTimer = SlideDuration;
                BeginSlideAudio();
            }

            bool sliding = slideTimer > 0;
            float speed = sliding ? Mathf.Lerp(WalkSpeed, 12, slideTimer / SlideDuration) : WalkSpeed;
            Vector3 step = (sliding ? slideDirection : direction) * speed * dt;
            Vector3 pos = player.position; pos.y = 0;
            bool dodging = MoveDodge(ref dodge, pos, dt, out Vector3 next);
            if (!dodging) next = Move(pos, step, PlayerRadius, true);
            if (sliding)
            {
                slideTimer = Mathf.Max(0, slideTimer - dt);
                if (slideTimer <= 0 || (next - pos).sqrMagnitude < step.sqrMagnitude * .25f) EndSlide();
            }

            if (jumpHeight > 0 || verticalSpeed > 0)
            {
                jumpHeight += verticalSpeed * dt - .5f * Gravity * dt * dt;
                verticalSpeed -= Gravity * dt;
                if (jumpHeight <= 0) { jumpHeight = verticalSpeed = 0; PlayJumpEvent(true); }
            }
            player.position = OnGround(next,.65f + jumpHeight);
            eyeHeight = Mathf.MoveTowards(eyeHeight, slideTimer > 0 ? .3f : 1, dt * 6);
            UpdateWalkWeight((next-pos).magnitude,grounded&&verticalSpeed<=0&&jumpHeight<=0,sliding,dt);
            arenaCamera.transform.localPosition = new Vector3(0, eyeHeight, 0)+walkCameraOffset;
            float dodgeRoll = -Vector3.Dot(player.right, dodge.direction) * (dodge.remaining / DodgeDuration) * 5f;
            arenaCamera.transform.localRotation=Quaternion.Euler(pitch-cameraKick+walkPitch,recoilSide,walkRoll+dodgeRoll);
            UpdateFootsteps((next - pos).magnitude, grounded && verticalSpeed <= 0 && jumpHeight <= 0, sliding);
        }
        const float DodgeDuration = .21f, DodgeCooldown = .65f, DodgeSpeed = 14f;
        struct DodgeState { public Vector3 direction; public float remaining, cooldown; }
        DodgeState dodge;

        static bool BeginDodge(ref DodgeState state, bool requested, Vector3 direction, Vector3 forward, float dt)
        {
            state.cooldown = Mathf.Max(0, state.cooldown - dt);
            if (!requested || state.remaining > 0 || state.cooldown > 0) return false;
            state.direction = direction.sqrMagnitude > .01f ? direction.normalized : forward;
            state.remaining = DodgeDuration;
            state.cooldown = DodgeCooldown;
            return true;
        }

        bool MoveDodge(ref DodgeState state, Vector3 position, float dt, out Vector3 next)
        {
            next = position;
            if (state.remaining <= 0) return false;
            float activeTime = Mathf.Min(dt, state.remaining);
            Vector3 step = state.direction * (DodgeSpeed * activeTime);
            next = Move(position, step, PlayerRadius, true);
            state.remaining -= activeTime;
            if (Vector3.Dot(next - position, state.direction) < step.magnitude * .5f)
                state.remaining = 0;
            return true;
        }
        void EndSlide() { slideTimer = 0; slideCooldown = SlideCooldown; StopSlideAudio(true); }
        void CaptureMouse() { showHelp = false; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        static void ReleaseMouse() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        bool MouseOverHud(Vector2 point)
        {
            float scale = HudScale;
            Vector2 p = new Vector2(point.x, Screen.height - point.y) / scale;
            float width = Screen.width / scale, height = Screen.height / scale;

            if (showHelp && HelpRect(width, height).Contains(p)) return true;
            if (dead) return new Rect(width / 2 - 100, height / 2 + 38, 200, 36).Contains(p);
            if (InventoryVisible && InventoryBounds(width, height).Contains(p)) return true;
            return building && buildMenuVisible && BuildMenuRect(width, height).Contains(p);
        }

        void OnDisable() { ReleaseEncounterPreview(); StopBarricadeAudio(); ClearDrowsinessPresentation(); StopCoop(); RestoreFrontEndEffects(); ResetMovementAudio(); StopWarehouseAudio(); if (saveAudioAt >= 0) SaveAudioSettings(); ReleaseMouse(); if (radio != null) radio.Stop(); if (stormAudio != null) stormAudio.Stop(); StopAdaptiveAudio(); }
        static Rect Expanded(Rect rect, float margin) => new Rect(rect.x - margin, rect.y - margin, rect.width + 2 * margin, rect.height + 2 * margin);
        static bool Touches(Rect rect, Vector3 pos, float radius)
        {
            float dx = pos.x - Mathf.Clamp(pos.x, rect.xMin, rect.xMax), dz = pos.z - Mathf.Clamp(pos.z, rect.yMin, rect.yMax);
            return dx * dx + dz * dz < radius * radius;
        }
        bool Free(Vector3 pos, float radius, bool includeBarricades)
        {
            if(HasCoreObjective)foreach(var barrier in coreVisual.BayBarriers)
                if(Touches(new Rect(barrier.min.x,barrier.min.z,barrier.size.x,barrier.size.z),pos,radius))return false;
            if(includeBarricades && HasCoreObjective && new Vector2(pos.x,pos.z).magnitude < 1.05f+radius)return false;
            if (BarrelBlocks(pos, radius)) return false;
            if (Mathf.Abs(pos.x) > HalfSize - radius || Mathf.Abs(pos.z) > HalfSize - radius) return false;
            foreach (Rect rect in obstacles) if (Touches(rect, pos, radius)) return false;
            foreach (Rect rect in perimeter) if (Touches(rect, pos, radius)) return false;
            if(luckyChest!=null&&Touches(ChestBounds,pos,radius))return false;
            if(slotMachine!=null&&Touches(SlotBounds,pos,radius))return false;
            if (includeBarricades) foreach (var b in barricades) if (Touches(b.bounds, pos, radius)) return false;
            if (includeBarricades) foreach (var turret in turrets) if (Touches(turret.bounds, pos, radius)) return false;
            return true;
        }
        Vector3 Move(Vector3 pos, Vector3 step, float radius, bool includeBarricades)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(new Vector2(step.x, step.z).magnitude / Mathf.Min(.12f, radius * .5f)));
            Vector3 increment = step / count;
            for (int i = 0; i < count; i++)
            {
                var next = pos + new Vector3(increment.x, 0, 0); if (Free(next, radius, includeBarricades)) pos = next;
                next = pos + new Vector3(0, 0, increment.z); if (Free(next, radius, includeBarricades)) pos = next;
            }
            return pos;
        }

        // Flow field around containers. Barricades stay on the route so raiders break them.
        readonly int[,] peerDistance=new int[GridSize,GridSize];
        void RefreshPaths(){if(HasCoreObjective)RefreshFlow(coreDistance,Vector3.zero);RefreshFlow(distance,player.position);if(HasCoopPartner)RefreshFlow(peerDistance,peer.position);}
        void RefreshFlow(int[,] field,Vector3 target)
        {
            for (int x = 0; x < GridSize; x++) for (int z = 0; z < GridSize; z++) field[x, z] = -1;
            var start = Cell(target); var queue = new Queue<Vector2Int>(); queue.Enqueue(start); field[start.x, start.y] = 0;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var delta in Neighbours)
                {
                    var next = cell + delta;
                    if (next.x < 0 || next.y < 0 || next.x >= GridSize || next.y >= GridSize || field[next.x, next.y] >= 0) continue;
                    if (!Free(World(next), EnemyRadius + .15f, false)) continue;
                    field[next.x, next.y] = field[cell.x, cell.y] + 1; queue.Enqueue(next);
                }
            }
        }
        static readonly Vector2Int[] Neighbours = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        Vector2Int Cell(Vector3 pos) => new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(pos.x) + HalfSize, 0, GridSize - 1), Mathf.Clamp(Mathf.RoundToInt(pos.z) + HalfSize, 0, GridSize - 1));
        Vector3 World(Vector2Int cell) => new Vector3(cell.x - HalfSize, 0, cell.y - HalfSize);
        Vector3 NextPath(Vector3 pos, Vector3 target,bool targetPeer=false,int routeSeed=0,bool targetCore=false)
        {
            // Use a straight pursuit whenever the whole body can pass. The flow field
            // is only necessary around freight, so open aisles no longer zigzag.
            if (ClearPursuitLine(pos, target)) return target;
            var field=targetCore?coreDistance:targetPeer?peerDistance:distance;
            var cell = Cell(pos); int best = field[cell.x, cell.y]; Vector3 result = World(cell);
            for(int choice=0;choice<Neighbours.Length;choice++)
            {
                var delta=Neighbours[(choice+(routeSeed&3))%Neighbours.Length];
                var next = cell + delta;
                if (next.x < 0 || next.y < 0 || next.x >= GridSize || next.y >= GridSize) continue;
                int value = field[next.x, next.y];
                if (value >= 0 && (best < 0 || value < best)) { best = value; result = World(next); }
            }
            return result;
        }
        bool ClearPursuitLine(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from; delta.y = 0;
            float length = delta.magnitude;
            if (length < .001f) return true;
            Vector3 rayOrigin = new Vector3(from.x, .8f, from.z);
            Vector3 direction = delta / length;
            if(HasCoreObjective)foreach(var barrier in coreVisual.BayBarriers)
                if(RayVolume(rayOrigin,direction,Expanded(new Rect(barrier.min.x,barrier.min.z,barrier.size.x,barrier.size.z),EnemyRadius+.12f),3)<length)return false;
            foreach (var obstacle in obstacles)
                if (RayVolume(rayOrigin, direction, Expanded(obstacle, EnemyRadius + .12f), 3) < length) return false;
            foreach (var wall in perimeter)
                if (RayVolume(rayOrigin, direction, Expanded(wall, EnemyRadius + .12f), 3) < length) return false;
            foreach (var barrel in explosiveBarrels)
                if (barrel != null && barrel.isActiveAndEnabled && !barrel.Detonated && RayVolume(rayOrigin, direction, Expanded(barrel.Footprint, EnemyRadius + .12f), 3) < length) return false;
            return true;
        }
        void UpdateFlashes(float dt)
        {
            if(!IsCoopGuest)
            {
                UpdateArsenalProjectiles(dt);
                UpdateAcid(dt);
            }
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var flash = flashes[i]; flash.life -= dt;
                if (flash.life <= 0) { Destroy(flash.body); flashes.RemoveAt(i); continue; }
                if(flash.settled)continue;
                flash.velocity += Vector3.down * (flash.casing ? 9.8f : 1.3f) * dt;
                flash.body.transform.position += flash.velocity * dt;
                if (flash.casing)
                {
                    flash.body.transform.Rotate(400 * dt, 220 * dt, 90 * dt);
                    if (flash.body.transform.position.y < .015f) { var p = flash.body.transform.position; p.y = .015f; flash.body.transform.position = p;flash.bounces++;flash.velocity=new Vector3(flash.velocity.x*.55f,-flash.velocity.y*.25f,flash.velocity.z*.55f);if(flash.bounces>=3||flash.velocity.magnitude<.3f){flash.velocity=Vector3.zero;flash.settled=true;flash.body.transform.rotation=Quaternion.Euler(0,flash.body.transform.eulerAngles.y,0);} }
                }
                else flash.body.transform.localScale += Vector3.one * dt * .1f;
            }
        }
        void ShowBanner(string message) { banner = message; bannerTimer = 5; }

        void SetUpAudio()
        {
            LoadAudioSettings();
            var audioManager = GameAudioManager.GetOrCreate(transform);
            radio = audioManager.Music; radio.clip = openingMusic;
            radio.loop = true; radio.playOnAwake = false; radio.spatialBlend = 0; radio.volume = .42f * musicVolume;
            SetUpAdaptiveAudio();
            effects = audioManager.Effects; effects.volume = .7f * soundVolume;
            SetUpMovementAudio();
            SetUpWarehouseAudio();
            SetUpPlayerDamageVoice();
            SetUpTitleVoice();
            if (Application.platform != RuntimePlatform.WebGLPlayer) UnlockAudio();
        }
        public void UnlockAudio()
        {
            audioUnlocked = true;
            ResumeAdaptiveAudio();
        }
        public void ToggleMusic()
        {
#if UNITY_EDITOR
            if (audioProbeRunning) return;
#endif
            musicEnabled = !musicEnabled;
            saveAudioAt = Time.unscaledTime + .4f;
            if (musicEnabled) UnlockAudio(); else { if (radio != null) radio.Pause(); if (musicB != null) musicB.Pause(); }
        }
        public void ToggleSound()
        {
#if UNITY_EDITOR
            if (audioProbeRunning) return;
#endif
            soundEnabled = !soundEnabled; if (!soundEnabled && effects != null) effects.Stop();
            if (!soundEnabled) StopSlideAudio(false);
            if (!soundEnabled) { StopWarehouseAudio(); ResetPlayerDamageVoice(); StopBarricadeAudio(); }
            saveAudioAt = Time.unscaledTime + .4f;
            if (!soundEnabled) foreach (var voice in zombieVoices) voice.Stop();
        }
        void PlayEffect(AudioClip clip, float gain)
        {
            if (soundEnabled && audioUnlocked && effects != null && GameAudioManager.Instance != null) GameAudioManager.Instance.PlayEffect(clip, gain);
        }

        static readonly Color HudWhite = new Color(.91f, .9f, .85f);
        static readonly Color HudRed = new Color(.72f, .17f, .12f);
        static float HudScale => Mathf.Max(.1f, Mathf.Min(Screen.width / 1000f, Screen.height / 720f));
        static Rect BuildMenuRect(float width, float height) => new Rect(width / 2 - 320, height - 112, 640, 44);
        static Rect HelpRect(float width, float height) => new Rect(width / 2 - 290, height / 2 - 195, 580, 390);
        static void Fill(Rect rectangle, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color; GUI.DrawTexture(rectangle, Texture2D.whiteTexture); GUI.color = previous;
        }
        void HudLabel(Rect rectangle, string words, GUIStyle style, Color color)
        {
            // A small offset shadow keeps unboxed text legible against bright scenery.
            Color previous = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, .8f);
            GUI.Label(new Rect(rectangle.x + 1, rectangle.y + 2, rectangle.width, rectangle.height), words, style);
            style.normal.textColor = color; GUI.Label(rectangle, words, style);
            style.normal.textColor = previous;
        }
        void OnGUI()
        {
            if (dedicatedServer) return;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
                centerStyle = new GUIStyle(textStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
                ammoStyle = new GUIStyle(titleStyle) { fontSize = 36, alignment = TextAnchor.MiddleRight };
                roundStyle = new GUIStyle(titleStyle) { fontSize = 46 };
                rightStyle = new GUIStyle(textStyle) { alignment = TextAnchor.MiddleRight };
                buttonStyle = new GUIStyle(centerStyle) { fontSize = 15, fontStyle = FontStyle.Bold };
            }
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            float scale = HudScale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            GUI.color = Color.white;
            float width = Screen.width / scale, height = Screen.height / scale;
            if (DrawFrontEnd(width, height))
            {
                if (frontScreen != FrontScreen.Launch && !(dead && !PartnerCanContinue && deathTime >= .8f))
                {
                    if (frontScreen != FrontScreen.Pause) DrawCoreAnnouncer(width, height);
                    DrawAudioCredits(width, height);
                }
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                return;
            }

            EnsureDreadHud();
            if(upgradePending){DrawCoreUpgrades(width,height);GUI.matrix=previousMatrix;GUI.color=previousColor;return;}
            if (HologramBlocking && !dead && !showHelp) { DrawHologram(width, height); GUI.matrix = previousMatrix; GUI.color = previousColor; return; }
            if (PreparationKeyboardVisible && !showHelp && !dead) { DrawPreparationKeyboard(width, height); GUI.matrix = previousMatrix; GUI.color = previousColor; return; }
            DrawPreparationHint(width, height);
            DrawInjuryFeedback(width, height);
            DrawCoreHud(width,height);
            DrawCoreAnnouncer(width,height);
            DrawMonsterSubtitle(width,height);
            DrawConstructionHud(width,height);
            DrawSniperScope(width, height);
            DrawWeaponInventory(width, height);
            DrawDrowsinessHud(width, height);
            DrawScreamerPings(width,height);
            DrawMinimalVitals(width, height);

            if (!dead && !building)
            {
                DrawMinimalAmmo(width,height);
                if (!reloading)
                {
                    var nearby = NearbyPickup();
                    if(CanRevivePartner())HudLabel(new Rect(width/2-350,height/2+58,700,32),ControlHint(Control.Pickup)+" REVIVE PARTNER \u00b7 50 HP",centerStyle,HudWhite);
                    else if (nearby != null) HudLabel(new Rect(width / 2 - 300, height / 2 + 58, 600, 32), ControlHint(Control.Pickup) + " " + WeaponCatalog.All[nearby.weapon].name + (weaponInventory.Count >= WeaponSlotLimit && !weaponInventory.Exists(w=>w.weapon==nearby.weapon) ? " \u00b7 swap equipped weapon" : " pick up"), centerStyle, HudWhite);
                }
                if ((!aiming || weaponId == 9) && !showHelp)
                {
                    Fill(new Rect(width / 2 - 8, height / 2 - 1, 5, 2), HudWhite);
                    Fill(new Rect(width / 2 + 3, height / 2 - 1, 5, 2), HudWhite);
                    Fill(new Rect(width / 2 - 1, height / 2 - 8, 2, 5), HudWhite);
                    Fill(new Rect(width / 2 - 1, height / 2 + 3, 2, 5), HudWhite);
                }
            }
            if (!dead && !showHelp && !fighting)
            {
                if (!building) HudLabel(new Rect(width / 2 - 320, 82, 640, 28), (PreparationActive?"Preparation tutorial":nextWaveCountdown>=0?"Next wave in "+Mathf.CeilToInt(nextWaveCountdown)+" s":"Ready") + "  \u00b7  " + ControlHint(Control.Hologram) + " Menu", centerStyle, HudWhite);
                if (building&&buildMenuVisible)
                {
                    Rect menu = BuildMenuRect(width, height);
                    string[] choices = { ControlHint(Control.Barricade) + " Barricade \u00b7 2", ControlHint(Control.Turret) + " Turret \u00b7 6", ControlHint(Control.Trap) + " Trap \u00b7 3" };
                    for (int i = 0; i < choices.Length; i++)
                    {
                        Rect choice = new Rect(menu.x + i * 216, menu.y, 208, menu.height);
                        Fill(choice, new Color(0, 0, 0, AvailableDefenses[i] == selectedDefense ? .72f : .4f));
                        if (AvailableDefenses[i] == selectedDefense) Fill(new Rect(choice.x, choice.yMax - 3, choice.width, 3), HudWhite);
                        Color labelColor = supplies >= DefenseCosts[i] ? HudWhite : new Color(.6f, .58f, .54f);
                        buttonStyle.normal.textColor = labelColor;
                        if (GUI.Button(choice, choices[i], buttonStyle)) SelectDefense(AvailableDefenses[i]);
                    }
                    string hint = validPlacement ? ControlHint(Control.Fire) + " Place" : "Choose a clear spot within 14 m";
                    if (selectedDefense == 0) hint += "  \u00b7  " + ControlHint(Control.Rotate) + " Rotate";
                    HudLabel(new Rect(width / 2 - 380, height - 154, 760, 30), hint+"  \u00b7  Hold right mouse to look around", centerStyle, HudWhite);
                }
            }
            if (dead && !showHelp)
            {
                Fill(new Rect(0, 0, width, height), new Color(0, 0, 0, .65f));
                HudLabel(new Rect(width / 2 - 300, height / 2 - 84, 600, 40), "DEPOT LOST", centerStyle, HudRed);
                HudLabel(new Rect(width / 2 - 300, height / 2 - 35, 600, 40), $"Wave {wave}  \u00b7  {kills} Ghouls", centerStyle, HudWhite);
                buttonStyle.normal.textColor = HudWhite;
                Rect retry = new Rect(width / 2 - 100, height / 2 + 38, 200, 36);
                Fill(retry, new Color(0, 0, 0, .65f));
                if (GUI.Button(retry, ControlHint(Control.Restart) + " Try again", buttonStyle)) ResetRun();
            }
            if (showHelp)
            {
                Rect help = HelpRect(width, height);
                Fill(help, new Color(.025f, .025f, .025f, .92f));
                HudLabel(new Rect(help.x + 24, help.y + 18, 532, 32), "CONTROLS  \u00b7  [H] Close", titleStyle, HudWhite);
                bool previousEnabled = GUI.enabled;
                GUI.enabled = bindingAction < 0;
                if (GUI.Button(new Rect(help.x + 24, help.y + 62, 254, 30), "Key bindings")) showBindings = true;
                if (GUI.Button(new Rect(help.x + 302, help.y + 62, 254, 30), "Help & volume")) showBindings = false;
                GUI.enabled = previousEnabled;
                if (showBindings) DrawBindings(help);
                else
                {
                    HudLabel(new Rect(help.x + 24, help.y + 104, 532, 92), "Mouse: Look around \u00b7 Click the game to capture mouse\nJump " + ControlHint(Control.Jump) + " \u00b7 Dodge " + ControlHint(Control.Dodge) + " \u00b7 Slide " + ControlHint(Control.Slide) + " while moving\nFlashlight " + ControlHint(Control.Flashlight) + " \u00b7 H: Menu \u00b7 Esc: Release mouse\nGame pauses while this menu is open.", textStyle, HudWhite);
                    DrawVolumeControl(new Rect(help.x + 24, help.y + 202, 532, 28), "Music", ref musicVolume, musicEnabled, ToggleMusic);
                    DrawVolumeControl(new Rect(help.x + 24, help.y + 240, 532, 28), "Effects / Zombies", ref soundVolume, soundEnabled, ToggleSound);
                    DrawVolumeControl(new Rect(help.x + 24, help.y + 278, 532, 28), "Rain", ref rainVolume, rainEnabled, ToggleRain);
                    HudLabel(new Rect(help.x + 24, help.y + 312, 532, 26), "Settings saved \u00b7 " + ControlHint(Control.Music) + " Music \u00b7 " + ControlHint(Control.Sound) + " Effects", textStyle, HudWhite);
                }
                if (GUI.Button(new Rect(help.x + 332, help.y + 342, 224, 30), bindingAction < 0 ? "[H] Close" : "Cancel"))
                { if (bindingAction >= 0) { bindingAction = -1; bindingMessage = "Cancelled."; } else showHelp = false; }
            }
            else if (!dead)
            {
                if (bannerTimer > 0) HudLabel(new Rect(width / 2 - 350, 118, 700, 36), banner, centerStyle, HudWhite);
                if (Cursor.lockState != CursorLockMode.Locked && !building)
                    HudLabel(new Rect(width / 2 - 260, height - 160, 520, 28), "Click the game  \u00b7  [H] Controls & volume", centerStyle, HudWhite);
            }
            if (chargerLessonActive && fighting && !dead && !showHelp && !HologramBlocking &&
                Time.unscaledTime - chargerLessonStarted < 8f)
                DrawPreparationKeyboard(width, height, dodgeLesson: true);
            // Defense condition is only useful when building, and only for visible damaged cover.
            if (!dead && building && !showHelp && arenaCamera != null)
                foreach (var b in barricades)
                {
                    Vector3 point = arenaCamera.WorldToViewportPoint(b.body.position + Vector3.up);
                    if (b.hp >= 100 || point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) continue;
                    Fill(new Rect(point.x * width - 22, (1 - point.y) * height, 44 * b.hp / 100, 3), HudWhite);
                }
            GUI.color = previousColor; GUI.matrix = previousMatrix;
        }

#if UNITY_EDITOR
        // Integration checks run inside the real editor, including collision and combat.
        public string RunSmokeChecks()
        {
            RunControlChecks();
            Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); };
            bool previousSound = soundEnabled; soundEnabled = false;
            ResetRun();
            playerRig.Animate(0,1,false);
            check(weaponId==WeaponCatalog.Starter&&ammunition==15,"Animated starter pistol");
            check(weaponModel==null&&pistolModel==null&&sniperModel==null&&heavyModel==null&&firstPersonArms==null&&additionalWeaponModels.Length==0,"Retired weapon references removed");
            check(pickups.Count==0,"No ground weapons at start");
            check(!TakePickup(null),"Empty pickup interaction is harmless");
            check(!TakePickup(new Pickup { weapon=15,ammo=17,position=player.position })&&weaponId==WeaponCatalog.Starter,"Legacy pickup cannot award a weapon");
            Fire(Vector3.up);int spent=ammunition;BeginReload();Fire(Vector3.up);check(reloading&&ammunition==spent,"Reload blocks firing");UpdateReload(ReloadDuration+.1f);check(!reloading&&ammunition==MagazineSize,"Reload completes");
            ResetRun();
            check(obstacles.Count == 4 && entrances.Length == 4, "Arena layout: obstacles="+obstacles.Count+" entrances="+entrances.Length);
            check(skinMaterial != null && creamMaterial != null && playerRig.head.childCount >= 8, "Detailed ranger materials and model");
            for (int side = 0; side < 4; side++)
            {
                Quaternion turn = Quaternion.Euler(0, side * 90, 0);
                Vector3 forward = turn * Vector3.forward;
                check(Mathf.Abs(ShotStopDistance(turn * new Vector3(4, 0, 0), forward) - 17.3f) < .002f, "Solid outer wall stops shot on side " + side);
                check(Mathf.Abs(ShotStopDistance(turn * new Vector3(4, 0, 0) + Vector3.up * 1.65f, forward) - 18) < .002f, "Open warehouse window at FPS eye height " + side);
                check(Mathf.Abs(ShotStopDistance(turn * new Vector3(4, 0, 0) + Vector3.up * 1.2f, forward) - 18) < .002f, "Open warehouse window at turret height " + side);
                check(Mathf.Abs(ShotStopDistance(Vector3.zero, forward) - 18) < .002f, "Open gate clips shot at yard edge " + side);
                check(Mathf.Abs(ShotStopDistance(turn * new Vector3(2.3f, 0, 0), forward) - 15.8f) < .002f, "Gate post stops shot " + side);
                check(Mathf.Abs(ShotStopDistance(turn * new Vector3(2.501f, 0, 0), forward) - 17.3f) < .002f, "Gate/wall seam stops shot " + side);
                float corner = ShotStopDistance(turn * new Vector3(14, 0, 14), turn * new Vector3(1, 0, 1).normalized);
                check(Mathf.Abs(corner - 3.3f * Mathf.Sqrt(2)) < .002f, "Outer corner stops diagonal shot " + side);
            }
            check(Mathf.Abs(ShotStopDistance(new Vector3(16.8f, 0, 4), Vector3.right) - .5f) < .002f, "Near-wall shot");
            check(Mathf.Abs(ShotStopDistance(new Vector3(4, 3.05f, 0), Vector3.forward) - 18) < .002f, "Upper window opening matches mesh");
            check(Mathf.Abs(ShotStopDistance(new Vector3(4, 3.3f, 0), Vector3.forward) - 18) < .002f, "Shot over wall clips at yard edge");
            check(!Free(ArenaPosition(new Vector3(-10, 0, -9)), PlayerRadius, false), "Rack bank collision");
            check(Move(Vector3.zero, Vector3.right, PlayerRadius, true).x == 1, "Player movement");
            check(Move(new Vector3(16, 0, 0), Vector3.right * 2, PlayerRadius, true).x <= HalfSize - PlayerRadius, "Arena boundary");
            UpdatePlayerMovement(Vector2.zero, true, false, .05f);
            check(jumpHeight > 0 && player.position.y > .65f, "Jump raises player and camera");
            float fallingVelocity = verticalSpeed;
            UpdatePlayerMovement(Vector2.up, true, true, .05f);
            check(verticalSpeed < fallingVelocity && slideTimer == 0, "No double jump or airborne slide");
            for (int i = 0; i < 20; i++) UpdatePlayerMovement(Vector2.zero, false, false, .05f);
            check(jumpHeight == 0 && verticalSpeed == 0 && Mathf.Approximately(player.position.y, .65f), "Jump lands at ground height");
            ResetRun();
            UpdatePlayerMovement(Vector2.zero, false, true, .05f);
            check(slideTimer == 0, "Slide requires movement");
            UpdatePlayerMovement(Vector2.up, false, true, .05f);
            check(slideTimer > 0 && player.position.z > WalkSpeed * .05f && eyeHeight < 1, "Slide gains speed and lowers camera");
            UpdatePlayerMovement(Vector2.right, false, false, .05f);
            check(Mathf.Approximately(player.position.x, 0), "Slide preserves initial direction");
            UpdatePlayerMovement(Vector2.up, true, false, .05f);
            check(jumpHeight > 0 && slideTimer == 0 && slideCooldown > 0, "Jump cancels slide");
            ResetRun(); player.position = new Vector3(0, .65f, HalfSize - PlayerRadius - .01f);
            UpdatePlayerMovement(Vector2.up, false, true, .05f);
            check(player.position.z <= HalfSize - PlayerRadius && slideTimer == 0, "Slide stops at arena boundary");
            ResetRun();
            check(jumpHeight == 0 && slideTimer == 0 && slideCooldown == 0 && eyeHeight == 1, "Restart resets jump, slide and camera");
            check(TryPlaceBarricade(new Vector3(3, 0, 0), false), "Barricade placement");
            check(!TryPlaceBarricade(new Vector3(3, 0, 0), false), "Overlapping barricade rejected");
            check(!Free(new Vector3(3, 0, 0), PlayerRadius, true), "Barricade blocks movement");
            StartWave(); check(wave == 1 && enemies.Count + remainingToSpawn == WaveZombieCount(1), "Wave starts");
            check(!TryPlaceBarricade(new Vector3(5, 0, 0), false), "Building disabled during combat");
            remainingToSpawn = 0;
            SpawnEnemy(new Vector3(0, 0, 4));
            check(enemies[0].zombie != null && enemies[0].zombie.parts.Length == 6 && enemies[0].zombie.walkClip != null, "Imported animated zombie with six damage regions");
            check(enemies[0].zombie.RegionCenter(0).y > 1.4f && enemies[0].zombie.RegionCenter(0).y < 2.1f, "Zombie head matches human height after animation sampling");
            Vector3 torsoAim = (enemies[0].zombie.RegionCenter(5) - arenaCamera.transform.position).normalized;
            for (int i = 0; i < 3; i++) Fire(torsoAim);
            check(enemies.Count == 0 && kills == 1, "Hitscan kills enemy");
            SpawnEnemy(new Vector3(6, 0, 0)); var victim = enemies[0];
            for (int i = 0; i < 3; i++) Fire((victim.position + Vector3.up * .9f - arenaCamera.transform.position).normalized);
            check(victim.hp == 3, "Barricade stops bullets");
            victim.position = new Vector3(5, 0, 0);
            for (int i = 0; i < 220; i++) { RefreshPaths(); UpdateEnemies(.05f); }
            check(barricades.Count == 0, "Enemy breaches barricade");
            int beforeReward = supplies; health = 70; enemies.Clear(); remainingToSpawn = 0;
            CompleteWave();
            check(!fighting && supplies == beforeReward + 6 && health == 70, "Wave completion supplies; health remains finite");
            StartWave(); check(wave == 2 && enemies.Count + remainingToSpawn == WaveZombieCount(2), "Second wave progression");
            enemies.Clear(); fighting = false; health = 100; damageTimer = 0;
            DamagePlayer(100); check(dead, "Death state");
            ResetRun(); check(!dead && health == 100 && wave == 0 && enemies.Count == 0 && supplies == 12, "Restart resets run");
            foreach (Vector3 gate in entrances) check(distance[Cell(gate).x, Cell(gate).y] >= 0, "All gates have a route");
            foreach (Vector3 target in new[] { Vector3.zero, new Vector3(14, 0, 13), new Vector3(-14, 0, -13) })
                foreach (Vector3 gate in entrances)
                {
                    player.position = target + Vector3.up * .65f; RefreshPaths();
                    Vector3 seeker = gate;
                    for (int i = 0; i < 1600 && (seeker - target).sqrMagnitude > 1; i++)
                    {
                        Vector3 direction = NextPath(seeker, target) - seeker;
                        seeker = Move(seeker, direction.normalized * Mathf.Min(direction.magnitude, .1f), EnemyRadius, false);
                    }
                    check((seeker - target).sqrMagnitude <= 1.1f, "Navigation from " + gate + " to " + target + " stuck at " + seeker);
                }
            ResetRun();
            check(!arenaCamera.orthographic && arenaCamera.transform.parent == player && gun.parent == arenaCamera.transform, "FPS camera and visible weapon");
            check(TryPlaceDefense(new Vector3(0, 0, 3), 1, false),"Turret blueprint placed");
            check(!ConstructionReady(turrets[0].body),"Unfinished turret is inactive");
            for(int hit=0;hit<6;hit++)check(ApplyHammerHit(turrets[0].netId,player.position),"Turret hammer contact "+hit);
            check(supplies==6&&ConstructionReady(turrets[0].body),"Turret completed for six scrap");
            check(!TryPlaceDefense(new Vector3(0, 0, 3), 1, false) && supplies == 6, "Rejected placement does not spend scrap");
            SpawnEnemy(new Vector3(0, 0, 6));
            for (int i = 0; i < 3; i++) UpdateTurrets(.7f);
            check(enemies.Count == 0 && kills == 1, "Turret selects and kills visible ghoul");
            check(TryPlaceBarricade(new Vector3(0, 0, 5), false),"Barricade blueprint placed");
            for(int hit=0;hit<3;hit++)check(ApplyHammerHit(barricades[0].netId,new Vector3(2.2f,.65f,3)),"Barricade hammer contact "+hit);
            check(supplies==4,"Barricade completed for two scrap");
            SpawnEnemy(new Vector3(0, 0, 7));
            check(TurretTarget(turrets[0]) == null, "Turret sightline blocked by barricade");
            for (int i = 0; i < 10; i++) UpdateTurrets(.7f);
            check(enemies[0].hp == 3, "Turret cannot shoot through barricade");
            check(!TryPlaceDefense(new Vector3(5, 0, 3), 1, false), "Insufficient resources rejected");
            ResetRun(); player.position = new Vector3(2.3f, .65f, 10);
            check(TryPlaceDefense(new Vector3(2.3f, 0, 13), 1, false), "Turret near perimeter");
            SpawnEnemy(new Vector3(2.3f, 0, 18));
            check(TurretTarget(turrets[0]) == null, "Turret cannot target through outer wall");
            for (int i = 0; i < 3; i++) { UpdateTurrets(.7f); Fire(Vector3.forward); }
            check(enemies[0].hp == 3, "Player and turret shots stopped by outer wall");
            ResetRun();
            check(!TryPlaceDefense(new Vector3(0, 0, 4), 2, false) && traps.Count == 0, "Removed slow trap cannot be placed");
            ResetRun(); check(TryPlaceDefense(new Vector3(0, 0, 3), 1, false), "Destructible turret placement");
            SpawnEnemy(new Vector3(0, 0, 4.7f));
            for (int i = 0; i < 220; i++) { RefreshPaths(); UpdateEnemies(.05f); }
            check(turrets.Count == 0, "Ghoul destroys blocking turret");
            ResetRun(); SpawnEnemy(new Vector3(0, 0, 4)); Fire(new Vector3(0, 1, 1).normalized);
            check(enemies[0].hp == 3, "FPS vertical aim misses ghoul when shooting over it");
            ResetRun();
            soundEnabled = previousSound;
            soundEnabled = false;
            SpawnEnemy(new Vector3(0, 0, 4)); var amputee = enemies[0];
            var zombieModel = amputee.zombie;
            check(zombieModel.parts[3].sharedMesh.vertexCount > 0 && zombieModel.Sever(3, Vector3.right, actors) && !zombieModel.parts[3].enabled && zombieModel.InjuredLegs == 1, "Leg geometry detaches and disables");
            float legHeight = actors.Find("Severed leg").GetComponent<Renderer>().bounds.size.y;
            check(legHeight > .2f && legHeight < 1.5f, "Detached leg retains world scale: " + legHeight);
            check(!zombieModel.Sever(3, Vector3.right, actors), "Cannot detach the same limb twice");
            Vector3 prior = amputee.position; UpdateEnemies(.1f);
            check((amputee.position - prior).magnitude < .13f, "Leg loss slows zombie pursuit");
            check(zombieModel.Sever(1, Vector3.right, actors) && zombieModel.LostArms == 1, "Arm detaches and weakens attacks");
            Vector3 head = zombieModel.RegionCenter(0);
            TraceShot(head - Vector3.forward * 2, Vector3.forward, null);
            check(enemies.Count == 0 && !zombieModel.parts[0].enabled, "Head shot severs head and kills zombie");
            ResetRun(); soundEnabled = previousSound;
            return "PASS: jumping/landing/no double jump, grounded directional slide/camera/speed/wall stop/jump cancellation/restart, imported pistol/rifle, wave weapon pickups, explicit pickup interaction, reload firing lockout/timing/auto-reload/restart, imported animated zombie regions, leg and arm detachment, limb-loss movement and headshot decapitation, FPS camera/vertical aim, wall occlusion, turret combat and destruction, traps, build costs, 12 pursuit routes, waves, rewards, death and restart.";
        }

        public void BeginAudioProbe()
        {
            audioProbeRunning = true;
            foreach (AudioClip clip in new[] { shotSound, hitSound, groanSound, scrapSound, waveSound, deathSound, hurtSound, reloadSound, boltSound })
            {
                if (clip == null || clip.loadState != AudioDataLoadState.Loaded) throw new Exception("Missing/unloaded audio asset");
                float[] samples = new float[Mathf.Min(clip.samples * clip.channels, 16384)];
                if (!clip.GetData(samples, 0)) throw new Exception("Cannot read audio PCM: " + clip.name);
                float peak = 0; foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
                if (peak < .001f) throw new Exception("Silent audio asset: " + clip.name);
            }
            musicEnabled = true; soundEnabled = true; UnlockAudio(); radio.Stop(); radio.loop = true; radio.Play();
            foreach (var listener in FindObjectsByType<AudioListener>()) Debug.Log("AUDIO LISTENER: enabled=" + listener.enabled + "; active=" + listener.gameObject.activeInHierarchy + "; camera=" + listener.name);
            Debug.Log("AUDIO SOURCE: playing=" + radio.isPlaying + "; loop=" + radio.loop + "; focus=" + Application.isFocused + "; enabled=" + radio.enabled + "; active=" + radio.gameObject.activeInHierarchy + "; mute=" + radio.mute + "; volume=" + radio.volume + "; clip=" + radio.clip.name);
            radio.timeSamples = radio.clip.samples - radio.clip.frequency / 2;
            // Unity allocates its history buffer on the first query; warm it before measuring.
            radio.GetOutputData(new float[1024], 0);
            effects.GetOutputData(new float[1024], 0);
        }
        public string ReadAudioProbe()
        {
            float[] samples = new float[1024]; radio.GetOutputData(samples, 0);
            float sum = 0; foreach (float sample in samples) sum += sample * sample;
            float rms = Mathf.Sqrt(sum / samples.Length);
            if (!radio.isPlaying || !radio.loop || radio.timeSamples > radio.clip.samples - radio.clip.frequency || rms < .0001f)
                throw new Exception("Radio loop/output not verified: musicEnabled=" + musicEnabled + "; focus=" + Application.isFocused + "; loop=" + radio.loop + "; samples=" + radio.timeSamples + "; rms=" + rms + "; playing=" + radio.isPlaying + "; virtual=" + radio.isVirtual + "; listenerVolume=" + AudioListener.volume + "; listenerPause=" + AudioListener.pause + "; listeners=" + FindObjectsByType<AudioListener>().Length);
            return "AUDIO PASS: nine non-silent loaded effects, music AudioSource playing, loop wrap observed, soundtrack output RMS=" + rms.ToString("F5") + "; clip=" + radio.clip.name;
        }
        public void ProbeEffect(int index)
        {
            AudioClip[] clips = { shotSound, hitSound, groanSound, scrapSound, waveSound, deathSound, hurtSound, reloadSound, boltSound };
            effects.Stop(); effects.PlayOneShot(clips[index], .5f);
        }
        public float ReadEffectOutput()
        {
            float[] samples = new float[1024]; effects.GetOutputData(samples, 0);
            float sum = 0; foreach (float sample in samples) sum += sample * sample;
            return Mathf.Sqrt(sum / samples.Length);
        }
#endif
    }
}
