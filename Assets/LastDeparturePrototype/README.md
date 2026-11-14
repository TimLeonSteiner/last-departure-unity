# Last Departure — first-person survival horror

## Single-player

The game is single-player only. Select **PLAY** from the main menu to start a run. Pause stops the simulation. Co-op hosting, joining, lobbies, and dedicated-server connections are no longer available, and the gameplay update no longer pumps networking.

Legacy multiplayer serialization and shared combat helpers remain in the source to preserve solo gameplay dependencies. Legacy connection entry points explicitly reject calls; they do not initialize Unity Services, create Relay allocations, or open sockets. Old multiplayer verification reports describe historical releases, not this version.

## Meshy roof

The hall loads `MeshyRoofTerminalSet` first, with 28 sections made from the Meshy-generated roof module. Two sections have actual geometry removed for the existing rain shafts; the north collapse remains open. The replacement retains the existing gameplay collision, trusses, lighting, heart suspension and server racks. The former procedural underside lining is skipped when the Meshy roof is present.

The original FBX and texture export and generation prompt are preserved in `ArtSource~/MeshyRoof`. Use **Tools > Yard Survival > Import Meshy roof** to rebuild the roof meshes and replacement terminal prefab from `Resources/Spaceport/MeshyRoof`. `Verification~/MeshyRoofImport.txt` and `MeshyRoofReview.txt` record import and live-scene checks.

## Windows release

Choose **Tools > Yard Survival > Build Windows release** to build the saved `Assets/LastDeparture.unity` scene as a non-development Windows x64 player. This exits Play Mode and selects the Windows build target. The output is `Builds/LastDeparture-Windows/Last Departure.exe` in the project root. The build uses the title Last Departure and a resizable 1280 × 720 window; previous project player settings are restored afterward. Desktop players have a Quit entry on the main menu.

Run `Assets/Verification~/PackageWindowsRelease.ps1` from PowerShell to add the player instructions and sound credits and create `Builds/LastDeparture-Windows.zip`. The script refuses to overwrite an existing ZIP. Share the entire ZIP and extract it before running; the executable requires its accompanying data folder and DLLs. Unity does not need to be installed on the player's computer. Build results are recorded in `Assets/Verification~/WindowsRelease.txt`.

## Browser release

Large Unity data archives are packaged as gzip parts smaller than 100 MB before compression to fit itch.io's 200 MB per-file hosting limit. The release page assembles the decoded parts before starting Unity. Packaging a Brotli release requires Python's `brotli` module; gzip and uncompressed releases use the standard library. Run the packaged page through `ServeBrowser.py` or itch.io so the compressed files receive the correct HTTP headers.

`Tools > Yard Survival > Build browser prototype` builds a production release with gzip compression and restores the previous Unity compression setting afterward. Package the output for itch.io with `python Assets/Verification~/CoopQA/PackageItch.py <build-directory> <output.zip>`. The packager checks itch.io's file limits and excludes development QA builds. Upload the ZIP as an HTML game and mark it as played in the browser.

Open `Assets/LastDeparture.unity` in Unity and press Play. Click the Game view to capture the mouse. The arena is a retrofuturistic depot surrounded by an abandoned highway, ruined buildings, a water tower, burned vehicles, utility poles and distant terrain beneath an overcast dust sky.

## Controls

Press **H**, then open **Tastenbelegung** to customize controls. Click either of an action's two bindings, then press a keyboard key or mouse button (including side buttons). Use × to clear a binding, Esc or Abbrechen to cancel, and Standard wiederherstellen to restore defaults. Conflicts are rejected with the name of the action already using that button. Changes save locally immediately, survive restarts and reopening the game, and appear in HUD prompts. H and Esc stay reserved so the menu and mouse release remain accessible. Gameplay pauses while the menu is open. The controls below are the defaults.

- WASD / arrows: move. Mouse: look. Left click: fire (hold for automatic rifles). Right click: aim down sights. Esc: release the mouse.
- Space: jump from the ground (also cancels a slide). Ctrl or C while moving: slide in your current movement direction for 0.7 seconds, slowing from a burst of speed with a lowered camera. Slides have a short cooldown and cannot start in midair. Jumping and sliding require the mouse captured and the help/build menus closed. Walls, containers and defenses still block movement while jumping or sliding.
- R: reload, with magazine and slide/charging-handle animation. Reload duration depends on the weapon and blocks firing. Empty magazines reload automatically. Spare magazines are unlimited in this prototype.
- E: collect a nearby weapon you are facing. Your previous gun drops with its remaining ammunition.
- T: restart. The death screen also has a restart button.
- 1 / 2 / 3: select barricade / automatic turret / induction slow trap during the build phase.
- B: open the construction menu; press B again to close it. While placing a selected blueprint, B switches back to the weapon. Q: rotate a barricade. Click a valid green placement within eight metres.
- Enter: start the next ghoul wave.
- H: show/hide controls and volume settings. Music, effects/zombie sounds and rain have separate 0–100% sliders and mute buttons. Rain starts at 60%; the N effects toggle and gun volume leave rain unchanged. Levels and mute choices are saved locally and restored after reopening the game. The compact HUD keeps round, scrap and health at bottom left and weapon/ammo at bottom right. Build choices appear only in build mode.
- M: toggle music. N: toggle sound effects. In the browser, the first click or key unlocks audio.

Start with a 12-round pistol and 12 scrap. All eight guns use distinct imported models. Ground pickups refresh between waves and retain each unlocked choice: sidearm at wave 1; M4 and Grease Gun at wave 2; Suomi at wave 3; Scout sniper and pump shotgun at wave 4; AK-47 at wave 5; M60 at wave 7. The strongest newly available gun occupies the nearest tray. The shotgun fires eight pellets per shell and uses shell insertion plus the original moving pump. Barricades cost 2, turrets 6, traps 3. Turrets automatically shoot visible zombies within 12 metres. Walls and barricades stop bullets. Zombies navigate around depot modules, destroy blocking barricades and turrets, and wear down traps. Traps remain walkable and slow nearby enemies. Each cleared wave awards 6 scrap, restores 20 health and repairs surviving defenses. The first wave has eight zombies; each subsequent wave adds three.

The active zombies use Pxltiger's Zombie and Artalasky's Modern Zombie Free, with walk, attack and death animations. Their skinned meshes are split into head, torso, arms and legs. Limb shots detach geometry; lost legs slow movement and lost arms reduce attack damage. Headshots detach the head and kill. Torso damage takes three pistol/service-rifle hits, two M60 hits, or one sniper hit. Detached parts tumble and expire after five seconds.

## Turret appearance

The built-in sentry is original geometry assembled at runtime in `Scripts/DepotModels.cs`, rather than an imported turret mesh or prefab. It uses the existing weathered olive steel and rust materials, with a compact tripod, six-barrel cluster, ammunition can, feed belt and small optic. The HUD and sentry take a restrained military direction inspired by Black Ops 1. Combat costs, fire rate, range and collision bounds are unchanged.

To use another model, assign `Turret Model` on the scene's `YardSurvival` component. Its root sits on the ground; it needs a direct child named `TurretHead`, aimed along local +Z with its pivot at about 1.2 metres. Keep the base within a one-metre footprint and total height around 1.5 metres to match existing collision bounds. All visual parts that should aim belong under `TurretHead`; leave legs under the root. Prefab colliders are disabled because gameplay uses simulation bounds. An unassigned or invalid prefab falls back to the built-in model. Materials on a replacement prefab can be edited independently.

## Assets and sound

The rifle uses the downloaded CC0 M4A1 by nisu / 3DModelsCC0 with its supplied base-color, normal, metallic and roughness texture maps. The original separated magazine and charging handle are animated alongside gloved hands, weapon tilt, recoil and muzzle flash. Source and license are preserved in `ThirdParty/M4A1/LICENSE.txt`.

The CC0 pistol by loafbrr_1 supplies separate slide and magazine meshes; its source and license are in `ThirdParty/Pistol/LICENSE.txt`. The active zombie models are [Zombie by Pxltiger](https://assetstore.unity.com/packages/3d/characters/humanoids/zombie-30232) and [Modern Zombie Free by Artalasky](https://assetstore.unity.com/packages/3d/characters/humanoids/modern-zombie-free-58134), acquired through the project owner's Unity Asset Store account. Their source packages remain in `ThirdParty/PxltigerZombie` and `ThirdParty/ModernZombie`; provenance and license notes are in `ThirdParty/PlayableZombies/SOURCE.txt`. These packages retain their Unity Asset Store licenses and are not CC0. The earlier Quaternius models remain in `ThirdParty/Zombies` with their original CC0 license, but are not the active scene models. Reload and damage behavior is implemented in this project. Shooting uses hitscan with movement-dependent spread, tighter aimed shots, camera/weapon recoil, brief amber muzzle flashes, brass ejection and impact particles.

First-person arms use para's CC0 rigged FPS arm mesh, based on MakeHuman (about 4,600 imported vertices), with an adapted glove/sleeve albedo atlas. Original sources and license remain in `ThirdParty/FPSArms`. `ModeledHands` poses articulated fingers and solves each arm to its wrist target. Eight weapon-specific rigs locate grips and moving parts. The support elbow remains bent below the view, and the wrist follows the handguard at a diagonal rather than a right angle. The pistol uses a two-handed grip. Reloads follow each magazine, ammunition box, slide or charging handle; the pump shotgun loads shells and cycles its fore-stock. View weapons use layer 30 and soft fill lighting; the flashlight excludes this layer to prevent close-range overexposure. `FpsHandsImport.Review` captures idle, fire, six reload phases, ADS and side views for all eight guns. Structural continuity checks supplement visual review. The poses are procedural; the source asset does not supply production reload clips.

`CreateAshfall.py` produces the original 60-second, 128 BPM action/horror loop: industrial percussion, bass ostinato, detuned strings, eerie bells and wind. It also produces magazine and charging-handle effects. Music becomes louder during combat. `CreateAudio.py` creates the other seven gameplay effects and retains the earlier radio-jazz asset; the active scene uses Ashfall. `CreateTextures.py` creates the depot's weathered surface textures. Generated WAV sample levels are recorded in the Audio validation JSON files.

The Scout uses the separate sniper mesh, scope, stock, magazine and bolt from [the 3dmodelscc0 library](https://3dmodelscc0.itch.io/free-cc0-guns-explosives-pack), under CC0. The [M60 by mightydinosaurcol](https://opengameart.org/content/m60-machine-gun) is used under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Adaptations include normalized meshes, Unity materials, folded bipod and procedural reload rigging. Its ammunition box/belt and charging handle are separated from the original mesh components. Original sources and licenses remain in `ThirdParty/LibraryGuns` and `ThirdParty/M60`; browser builds include M60 attribution through the Weapon credits link. `WeaponModelRig` defines model-specific wrist, ammunition, action and muzzle contacts.

## Editor and browser

Weapon audio selects a named bank for each of the eight guns. P12 and M4 retain their dedicated recordings and impact variants. Scout, M60, AK-47, shotgun, Suomi and Grease Gun each use a newly generated ElevenLabs shot, reload and action recording, with three processed shot variations. The 18 originals are archived under `Audio/ElevenLabs_Weapons`; `PrepareAdditionalAudio.py` delegates to the dedicated-source preparation. `Audio/ElevenLabs_Weapons/DedicatedWeaponChecks.txt` records assignment, waveform and timing checks. Reload clips finish before the action sound at the final 0.45 seconds. Music, effects and rain volume settings remain independent.

`PrepareWeaponAudio.py` prepares the downloaded WAVs as mono 48 kHz files, removes leading silence, fits mechanical sequences to reload timing and balances peaks with short fades. `Audio/ElevenLabs_Weapons/WeaponAudioValidation.json` records source filenames, hashes, durations and levels. `Tools > Yard Survival > Connect weapon sounds` reconnects these clips without rebuilding the arena. Scene regeneration also reconnects them. `WeaponAudioSetup.VerifyInEditor` reconnects the banks and runs gameplay, eight-weapon progression/reload, weapon PCM/timing and independent rain checks in Play Mode, then restores the prior editor state. `VerifyBatch` is only for a separate batch invocation because it exits Unity.

The ElevenLabs zombie library contains 16 categories with four variations each in `Audio/ElevenLabs_Zombies`. Hisses are excluded from playback. Approaching zombies mix 24 groans, choking calls and snarls, including 12 new ElevenLabs Sound Effects v3 recordings in `Resources/HordeVoices`, with a stable vocal style and pitch range per individual. Calls are spatialized out to 42 metres, with irregular overlapping calls that grow denser as the horde approaches. Eight reserved crowd voices keep 24 separate attack, damage and foley slots available. Each category cycles through a shuffled bag before repeating a recording. Existing music/effects volume controls also apply to these sources; restarting clears the crowd state. Bullet impacts, blood splatters, severed heads/arms/legs, limb landings and corpse falls still follow their gameplay events.

`Audio/ElevenLabs_Music` contains four instrumental cues: Abandoned Terminal for opening/preparation, Stormbound Terminal for waves 1–3, Stormbound Terminal — Rising Pressure from wave 4, and Stormbound Terminal — Overrun from wave 7. These are the new one-minute recordings, prepared as 58.53-second seamless loops. Nearby hordes and low-health danger can raise the stage early. Music crossfades over 2.5 seconds with an eight-second minimum stage hold during combat, returns to preparation between waves, softens on death and resets on restart. `PrepareMusic.py` prepares the downloaded WAVs with a wrap crossfade and consistent headroom; the original downloads remain intact.

Use `Tools > Yard Survival > Connect zombie sounds and adaptive music` to wire these assets into the existing LastDeparture scene without rebuilding its environment. The normal scene setup also connects them. Audio source checks are in `YardSurvivalAudio.RunAdaptiveAudioChecks`.

Use `Tools > Yard Survival > Set up LastDeparture scene` to regenerate the depot and wire assets. Stop Play Mode first. Tutorial scenes and their build list are preserved.

Use `Run gameplay smoke checks (Play Mode)` to verify imported reload parts, ammunition use, reload timing and firing lockout, auto-reload and restart, collision, wall occlusion, defenses, costs, waves, enemy navigation, damage, rewards and death. This resets the current run. `Verify radio loop and game effects (Play Mode)` checks loaded audio PCM, looping playback and real mixer output. Results are appended to `Validation.log`; historical failures remain in the log.

`Build browser prototype` builds only LastDeparture for WebGL into the editor's `%TEMP%/YardSurvival-WebGL`. Run `ServeBrowser.py --directory <that build directory>` with Python 3 and open `http://127.0.0.1:8765/`. The server supplies required compressed-file headers. The output is temporary and can be rebuilt from the menu. The desktop browser needs a keyboard and mouse; touch controls are not implemented.



## Rain and battered depot

The scene now starts with wind-driven rain, small ground splashes, irregular dark puddles, scattered low scrap, buckled rooftop metal and rust runoff. Cold fog and moving storm clouds replace the warm dust lighting. Weather dressing is added at runtime even when the saved depot already exists, so scene regeneration is unnecessary. Tall wreckage stays inside existing blocked module footprints; ground litter has no colliders. Rain is emitted over five open strips that exclude depot roofs.

`Storm atmosphere` on the survival component exposes the existing rain material, the `Depot_Rain` ambience and rain intensity. Change intensity before or during Play to adjust rainfall density (0 disables rain and splashes). Rain ambience unlocks with browser audio and has its own saved Regen slider and mute button in H > Hilfe & Lautstärke. Muting rain preserves its position in the loop; gun/effects volume and N do not affect it.

`PrepareRainAudio.py <download.wav>` prepares an ElevenLabs rain loop with a one-second wrap crossfade, RMS 0.12 target and peak limit 0.65, preserving the scene's existing audio GUID. It keeps the source in `Audio/ElevenLabs_Rain`, saves levels and the source hash in `Audio/RainAudioValidation.json`, and preserves the earlier ambience as `Depot_Rain_Procedural.wav`. `CreateRain.py` regenerates only that procedural alternative. `RainAudioVerification.VerifyBatch` checks the assigned loop, audio levels, independent rain/effects controls, intensity zero and browser unlock gating in Unity Play Mode.

## Zombie models and dismemberment

Pxltiger Zombie and Modern Zombie Free retain their supplied skin, clothing and face textures through converted URP materials. `RealisticZombieImport.Setup` generates playable prefabs, six skinned damage regions per character and humanoid animation bindings in `ThirdParty/PlayableZombies`. The Pxltiger walk, attack and death clips retarget to both avatars. Normal scene setup preserves these prepared models. The older procedural face and chest alterations are skipped for these characters so their source appearance is preserved.

Severed heads, arms and legs retain their animated pose, textures and world scale. Wound details remain on the body, with a short mesh-particle blood spray and a temporary ground stain. Torso hits produce a smaller spray. At most 32 stains are retained, each lasting eight seconds. Debris expires after five seconds; loss of legs slows pursuit, loss of arms weakens attacks, and headshots kill. Region cuts follow mesh triangles and use procedural wound details.

`RealisticZombieImport.Review` runs in Play Mode and records model scale, walking movement, head-hit regions, URP materials, all five detachments, repeated-sever prevention, debris pose/scale, limb-loss counts and death poses in `ZombieReview/Validation.txt`. That folder also contains rendered walk, sever and death captures for both characters. The combined gameplay smoke checks additionally exercise these models in the active scene.

## M4 automatic fire audio

The M1911-style sidearm report uses the existing P12 firing bank, with a deeper recorded low-mid body, a sharp initial crack, a subtle recorded slide detail, and a dry 0.34-second tail. Three variations preserve its asset references. Regenerate just the pistol with `python PrepareImpactAudio.py --profile P12`; levels and rapid-fire overlap checks are in `Audio/Weapon_Impact/P12_Validation.json`, with `P12_Preview.wav` for listening.

Player movement has four concrete bootstep variations, a gear rustle on jumping, a boot impact on landing, and a 0.7-second concrete slide scrape with a stop sound. `CreateMovementAudio.py` generates this original procedural foley in `Audio/Player_Movement`. Footsteps follow actual grounded travel and stay silent while blocked, airborne or sliding. The slide scrape slows in volume and stops on collision, jumping, death or restart; it pauses with the controls menu. All movement sounds follow the existing effects volume and mute, independently of rain and music. Scene setup and Connect weapon sounds reconnect these assets too.

Gunshots use impact versions in `Audio/Weapon_Impact`, built from the preserved ElevenLabs recordings by `PrepareImpactAudio.py`. They add a sharper attack, compressed report, low-mid blast and a compact yard reflection. Service and heavy M4 tails are shortened to 0.25 seconds to keep automatic fire clear. Each player weapon cycles through three subtle variations, once per real bullet at its existing cadence. Player shots have a stronger playback gain; reloads, charging handles and other effects keep their previous levels. The turret has a heavier report too. PCM import preserves the short attacks. Scene setup reconnects these impact assets and the existing ElevenLabs reload/action banks.

`Audio/Weapon_Impact/Validation.json` records sample levels, source hashes and eight-shot overlap peaks at gameplay gain. The folder includes isolated weapon and automatic-fire previews. Releasing the trigger stops new shots immediately; reloads and empty magazines stop firing through the existing ammunition logic. The earlier synthesized clips and previews in `Audio/M4_Automatic` remain unused alternatives.

## Night, flashlight and visible defenses

The depot uses dark blue-grey night lighting and storm fog. The camera-mounted flashlight starts on; F toggles it, and the binding can be changed in H > Tastenbelegung. It follows your view and points at the chosen placement in build mode. A warm 24 m hotspot and a softer 12 m peripheral spill illuminate actual ground, walls, props and characters with distance and cone falloff. The main beam casts real-time shadows where the graphics preset enables them. The peripheral spill is an unshadowed fill light. `Flashlight Intensity` controls beam strength (default 55). Both beams switch off together on toggle or death.

`Tools > Yard Survival > Verify flashlight environment lighting` renders the actual yard with the flashlight on/off and a separate receiver with an opaque shadow blocker. Results are saved in `FlashlightChecks.txt`, with comparison captures in `FlashlightReview`.

`Prefabs/SteelBarricade.prefab`, `TripodSentry.prefab` and `InductionTrap.prefab` are reusable defense assets. Placement previews instantiate these same models at their actual size and show green/red validity tinting without physics collisions or spending scrap. Barricades rotate with Q. Placed models keep their original materials.

The flashlight illuminates the active Pxltiger and Modern zombie models with their original textured materials. `CreateCorpseSkin.py` and the earlier gaunt-face alterations remain available for the legacy Quaternius characters; they do not replace the new models' skin or face textures.

`Tools > Yard Survival > Apply night and defense assets` rebuilds the three local prefabs and saves the scene lighting. Scene setup also preserves the defense slots. Unity validation is recorded in `NightAndPreviewChecks.txt`; `NightReview` contains actual camera renders of each placement preview and an illuminated zombie.

## Placeable explosive barrels

Drag `Prefabs/ExplosiveBarrel.prefab` into the scene, or use `GameObject > Yard Survival > Explosive barrel`. Move, rotate and duplicate instances normally; the root pivot rests on the ground. `Tools > Yard Survival > Create explosive barrel asset` prepares the prefab and adds two sample instances under `Explosive barrels` if that group does not already exist.

The barrel uses Jorge Camacho's CC0 [Barrel_01 model from Poly Haven](https://polyhaven.com/a/Barrel_01), with its original 2K rusted red paint, explosive warning label and PBR texture maps. Original downloads, checksums and license links are preserved in `ThirdParty/ExplosiveBarrel`.

One bullet or melee contact detonates a barrel. Grenades and explosive bolts can trigger it too. The blast produces fire, smoke, sparks and an explosion sound, damages nearby zombies and players with distance falloff, and triggers visible nearby barrels. Solid arena walls and defenses block shots and blast damage. Intact barrels block movement and defense placement; detonated barrels stop blocking. They remain spent until the run restarts. The Inspector exposes blast radius (default 4 metres), maximum zombie damage (12) and maximum player damage (45); selecting the barrel shows its radius.

Co-op damage runs on the host, with spent barrel state and explosion effects replicated to the guest. Scene-authored barrel instances must be present in the same scene hierarchy on both clients. `ExplosiveBarrelChecks.txt` records combat, occlusion, chain reaction, movement, restart and snapshot replay checks; `ExplosiveBarrelReview.jpg` shows the imported prefab rendered in Unity. Placement is through the Unity scene, not the in-game defense menu.

## End-of-run recap and hammer feedback

The final death screen shows waves cleared, wave reached, kills, and active survival time, with new personal bests highlighted. Solo and co-op records are stored separately on this device/browser; co-op kills are the team total. A downed player sees the revive message until neither player can continue. The host supplies the co-op run timer, including for rejoining guests. Solo pause and upgrade selection do not advance survival time. Restart clears run statistics while retaining personal bests.

With the hammer equipped, click once to place a blueprint and hold Fire while aiming at it to finish construction or repair. Holding never places additional blueprints. The focused readout shows build progress or HP, the next strike cost, remaining construction cost, and repair amount (up to 25 HP for one scrap). Fully repaired, unaffordable, and out-of-range targets do not start another swing. Placement feedback explains missing scrap, blocked sight, overlap, arena boundaries, and proximity to the player. Placement still reaches eight metres; hammer work requires approaching within approximately three metres.

## Death-screen presentation

The death overlay uses deterministic, prebuilt RGBA edge splashes and four individual drip textures in `Resources/DeathScreen`. Drips reveal through cropped UVs at a fixed scale over staggered intervals; they never stretch vertically during the animation. A soft edge vignette keeps the fallen-camera scene visible, with a clear center for the recap. The treatment is reduced while a co-op partner can revive the player. The title now uses the menu display font with a restrained fade and short settling motion.

Regenerate the assets with `python Assets/LastDeparturePrototype/ArtSource~/DeathScreen/CreateOverlay.py` from the project root (Pillow and NumPy required). Existing texture GUIDs are preserved. `Verification~/DeathScreen/DesignPreview.jpg` is an illustrative composite, not an in-game capture.


## Guided first-wave preparation

After clearing wave one, a translucent cyan keyboard highlights the current Field Link binding (Tab by default). The next-wave countdown waits during this lesson. Opening the link leads to BUILD; selecting a blueprint advances to placement, then holding Fire near the placed defense completes construction. Failed placement and repairs to unrelated defenses do not complete the lesson. Placing an explosive barrel completes its direct-placement path. Completing or skipping the lesson restores a full 15-second preparation timer. Restarting a run resets the lesson; later waves do not repeat it.

The keyboard uses reusable outline, fill and glow textures in `Resources/Tutorial` with live key labels and a pulsing selection; remapped mouse/numpad bindings receive a separate highlighted label. The original full-screen field guide has been replaced. Inventory and Skills remain in the wrist link; B opens Construction. In Construction, arrows select and Enter confirms, and mouse selection remains available.

`Verification~/PreparationReview.request` runs the editor verification and writes `PreparationChecks.txt`, `Preparation-Keyboard.png` and `Preparation-Select.png`. It starts a disposable Play Mode run and exits after success.

## Meshy barricades

Placed defenses and entrance barricades now use `Resources/Construction/Barricade.prefab`. The generated source archive and optimization script are preserved in `ArtSource~/MeshyBarricade`. The source was reduced from 375,516 to 12,000 triangles with 2K PBR textures. Five damage sections preserve break-away feedback and repair restoration. The prefab fits the existing 2.8 x 1.2 x 0.65 metre gameplay bounds. Use **Tools > Yard Survival > Import Meshy barricade** to rebuild it.

## Biomechanical heart platform

The core now loads `Resources/HeartPlatform/Platform.prefab`, a complete Meshy assembly with a low perimeter ramp, vessel hoses, twin pumps, and an overhead suspension frame. Its private source archive and approved reference are preserved in `ArtSource~/HeartPlatform`. `Optimize.py` reduces the original 3,997,932 faces to 65,000 triangles; the runtime uses a 4K color map and 2K normal, metallic, and roughness maps. The imported assembly measures 8.4 x 4.7 x 8.4 metres.

The heart shader deforms organic tissue while leaving metal hardware rigid. The original platform railings, procedural bay, and amber side lamps are replaced. A smooth ramp collider and shared ground-height sampling support player movement, jumping, enemy visuals, and defense placement; pumps and arch feet retain navigation and shot collision. Placement requires a flat footprint clear of the heart and machinery.

Use **Tools > Yard Survival > Import heart platform** to rebuild the prefab from its optimized FBX and maps. `Verification~/HeartPlatformReview.request` starts a disposable Play Mode review, records ramp access, jump/landing, heartbeat, defense elevation, and enemy approach checks in `HeartPlatformChecks.txt`, captures `HeartPlatform-Overview.png` and `HeartPlatform-Player.png`, then exits Play Mode.
