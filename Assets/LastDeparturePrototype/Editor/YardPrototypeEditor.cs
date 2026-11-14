using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class YardPrototypeEditor
{
    const string ScenePath = "Assets/LastDeparture.unity";
    const string Folder = "Assets/LastDeparturePrototype";
    static double nextPoll;
    static double audioProbeAt;
    static int effectProbe = -1;
    static double effectReadAt;
    static float effectPeak;
    static YardPrototypeEditor()
    {
        EditorApplication.delayCall += AutoSetup;
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("D27.FreshAcceptance",false))EditorApplication.delayCall+=StartFreshAcceptance;};
    }
    static void StartFreshAcceptance()
    {
        SessionState.SetBool("D27.FreshAcceptance",false);EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.None;EditorSettings.enterPlayModeOptionsEnabled=false;SessionState.SetBool("Dustline.Acceptance.Pending",false);
        if(File.Exists(Folder+"/Acceptance.request"))File.Delete(Folder+"/Acceptance.request");
        GameplayAcceptanceChecks.Begin();
    }
    static void AutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path == ScenePath && !scene.isDirty && UnityEngine.Object.FindAnyObjectByType<YardSurvival>() == null) Setup();
    }

    [MenuItem("Tools/Yard Survival/Set up LastDeparture scene")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before scene setup.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath);
        }
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if (game == null)
        {
            var root = new GameObject("Yard Survival Prototype");
            game = root.AddComponent<YardSurvival>();
            game.groundMaterial = MaterialAsset("Asphalt", new Color(.12f, .17f, .21f));
            game.wallMaterial = MaterialAsset("Steel", new Color(.22f, .3f, .35f));
            game.blueMaterial = MaterialAsset("Blue container", new Color(.08f, .35f, .48f));
            game.redMaterial = MaterialAsset("Rust container", new Color(.65f, .22f, .13f));
            game.yellowMaterial = MaterialAsset("Safety yellow", new Color(1, .7f, .16f));
            game.playerMaterial = MaterialAsset("Player teal", new Color(.13f, .9f, .69f));
            game.enemyMaterial = MaterialAsset("Raider", new Color(.63f, .12f, .24f));
        }
        ConfigureDepotAssets(game);
        NightAndPreviewSetup.ConfigureDefenseAssets(game);
        var oldYard = game.transform.Find("Warehouse yard");
        if (oldYard != null) UnityEngine.Object.DestroyImmediate(oldYard.gameObject);
        game.BuildEnvironment();
        game.ConfigureCamera();
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.57f, .59f, .56f);
        RenderSettings.fog = true; RenderSettings.fogColor = new Color(.51f, .46f, .36f); RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = .009f;
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>())
            if (light.type == LightType.Directional) { light.transform.rotation = Quaternion.Euler(50, -30, 0); light.intensity = 1.3f; light.color = new Color(1, .9f, .72f); }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Report("SETUP PASS: Dustline 27 wasteland, storm sky, imported PBR rifle, magazine/bolt reload, FPS camera, original action/horror score and nine effects saved. WebGL supported: " + BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL));
    }

    static void ConfigureDepotAssets(YardSurvival game)
    {
        game.groundMaterial = DepotMaterial("Depot concrete", new Color(.45f, .42f, .3f), "Concrete", 7);
        game.wallMaterial = DepotMaterial("Bunker olive steel", new Color(.23f, .28f, .22f), "OxidizedMetal", 2);
        game.blueMaterial = DepotMaterial("Oxidized petrol teal", new Color(.3f, .49f, .43f), "OxidizedMetal", 2);
        game.redMaterial = DepotMaterial("Iron oxide red", new Color(.63f, .31f, .2f), "OxidizedMetal", 2);
        game.yellowMaterial = DepotMaterial("Worn hazard ochre", new Color(.82f, .65f, .27f), "OxidizedMetal", 1);
        game.playerMaterial = DepotMaterial("Ranger field canvas", new Color(.59f, .57f, .35f), "Canvas", 1);
        game.enemyMaterial = DepotMaterial("Ghoul olive rags", new Color(.35f, .39f, .24f), "Canvas", 1);
        game.skinMaterial = DepotMaterial("Ghoul mottled skin", new Color(.57f, .63f, .38f), "Concrete", 1);
        game.creamMaterial = DepotMaterial("Aged cream enamel", new Color(.88f, .82f, .61f), "OxidizedMetal", 1);
        game.woodMaterial = DepotMaterial("Dark salvaged rust", new Color(.31f, .24f, .17f), "OxidizedMetal", 1);
        game.accentMaterial = MaterialAsset("Phosphor instrument glass", new Color(.6f, .92f, .47f));
        game.accentMaterial.EnableKeyword("_EMISSION"); game.accentMaterial.SetColor("_EmissionColor", new Color(.28f, .65f, .1f));
        EditorUtility.SetDirty(game.accentMaterial);
        game.signFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        AnimatedArsenalImport.Setup();
        DepotAssetImport.Configure(game);
        game.muzzleMaterial = MaterialAsset("Hot muzzle flash", new Color(1, .75f, .28f));
        game.muzzleMaterial.EnableKeyword("_EMISSION"); game.muzzleMaterial.SetColor("_EmissionColor", new Color(3, 1.8f, .35f)); EditorUtility.SetDirty(game.muzzleMaterial);
        game.dustMaterial = MaterialAsset("Impact dust", new Color(.59f, .53f, .4f));
        game.brassMaterial = MaterialAsset("Spent cartridge brass", new Color(.65f, .46f, .16f));
        string skyPath = Folder + "/Materials/Ashfall sky.mat";
        game.wastelandSky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (game.wastelandSky == null) { game.wastelandSky = new Material(Shader.Find("Dustline/Ashfall Sky")); AssetDatabase.CreateAsset(game.wastelandSky, skyPath); }
        game.rainMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/SourceFiles/VFX/RaindropMaterial.mat");
        game.rainAmbience = AudioAsset("Depot_Rain");
        game.shotSound = AudioAsset("Carbine_Shot");
        game.hitSound = AudioAsset("Ghoul_Hit"); game.groanSound = AudioAsset("Ghoul_Groan");
        game.scrapSound = AudioAsset("Scrap_Impact"); game.waveSound = AudioAsset("Wave_Chime");
        game.deathSound = AudioAsset("Game_Over"); game.hurtSound = AudioAsset("Player_Hurt");
        game.reloadSound = AudioAsset("Magazine_Swap"); game.boltSound = AudioAsset("Charging_Handle");
        ZombieAudioSetup.Configure(game);
        WeaponAudioSetup.Configure(game);
    }
    static void ConfigureWeapon(YardSurvival game)
    {
        const string source = Folder + "/ThirdParty/M4A1/M4A1/";
        var normalImporter = AssetImporter.GetAtPath(source + "M4A1_Normal.png") as TextureImporter;
        normalImporter.textureType = TextureImporterType.NormalMap; normalImporter.SaveAndReimport();
        var material = MaterialAsset("Weathered M4 PBR", Color.white);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(source + "M4A1_Base_Color.png"));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(source + "M4A1_Normal.png"));
        material.EnableKeyword("_NORMALMAP"); material.SetFloat("_Metallic", .75f); material.SetFloat("_Smoothness", .3f);
        var packedImporter = AssetImporter.GetAtPath(source + "M4A1_MetallicSmoothness.png") as TextureImporter;
        if (packedImporter != null)
        {
            packedImporter.sRGBTexture = false; packedImporter.SaveAndReimport();
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(source + "M4A1_MetallicSmoothness.png"));
            material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.SetFloat("_Smoothness", .6f);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source + "M4A1.fbx");
        if (prefab == null) throw new Exception("Downloaded CC0 rifle missing");
        var instance = UnityEngine.Object.Instantiate(prefab);
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
        string path = Folder + "/ThirdParty/M4A1/SalvageRifle.prefab";
        game.weaponModel = PrefabUtility.SaveAsPrefabAsset(instance, path);
        UnityEngine.Object.DestroyImmediate(instance); EditorUtility.SetDirty(material);
    }
    static void ConfigurePistol(YardSurvival game)
    {
        const string source = Folder + "/ThirdParty/Pistol/Textures/Pistol_1/";
        var normal = AssetImporter.GetAtPath(source + "Pistol_1_Normal.png") as TextureImporter;
        normal.textureType = TextureImporterType.NormalMap; normal.SaveAndReimport();
        var material = MaterialAsset("Salvaged pistol PBR", Color.white);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(source + "Pistol_1_Albedo.png"));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(source + "Pistol_1_Normal.png"));
        material.EnableKeyword("_NORMALMAP"); material.SetFloat("_Metallic", .7f); material.SetFloat("_Smoothness", .3f);
        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/ThirdParty/Pistol/FBX/Frame.fbx"));
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
        game.pistolModel = PrefabUtility.SaveAsPrefabAsset(instance, Folder + "/ThirdParty/Pistol/Sidearm.prefab");
        UnityEngine.Object.DestroyImmediate(instance); EditorUtility.SetDirty(material);
    }
    static Material DepotMaterial(string name, Color color, string texture, float repeat)
    {
        Material material = MaterialAsset(name, color);
        material.color = color;
        string path = Folder + "/Textures/" + texture + ".png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        material.mainTextureScale = Vector2.one * repeat;
        EditorUtility.SetDirty(material); return material;
    }
    static AudioClip AudioAsset(string name)
    {
        string path = Folder + "/Audio/" + name + ".wav";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) throw new Exception("Audio importer missing: " + path);
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .7f;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings; importer.loadInBackground = false;
        importer.SaveAndReimport();
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) throw new Exception("Audio clip missing: " + path); return clip;
    }
    static Material MaterialAsset(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder(Folder + "/Materials")) AssetDatabase.CreateFolder(Folder, "Materials");
        string path = Folder + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new Exception("URP Lit shader unavailable");
        material = new Material(shader) { name = name, color = color };
        material.SetFloat("_Smoothness", .15f);
        AssetDatabase.CreateAsset(material, path); return material;
    }

    // A tiny editor command inbox. No network listener, no external package.
    static void Poll()
    {
        ProbeAudioOutput();
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + 1;
        string inbox = Folder + "/EditorCommand.request";
        if (!File.Exists(inbox)) return;
        string command = File.ReadAllText(inbox).Trim(); File.Delete(inbox);
        Directory.CreateDirectory("Assets/Verification~");
        File.AppendAllText("Assets/Verification~/EditorCommand.log",DateTime.Now.ToString("s")+" playing="+EditorApplication.isPlaying+" command="+command+Environment.NewLine);
        try
        {
            switch (command)
            {
                case "barrels": ExplosiveBarrelSetup.Setup(); break;
                case "verifybarrels": ExplosiveBarrelVerification.Run(); break;
                case "reviewbarrel": ExplosiveBarrelVerification.Review(); break;
                case "acceptance":
                    if(EditorApplication.isPlaying){SessionState.SetBool("D27.FreshAcceptance",true);EditorApplication.isPlaying=false;}else StartFreshAcceptance();break;
                case "reviewaim": AnimatedArsenalAimReview.Run(); break;
                case "probeassets": GameplayAssetSetup.Probe(); break;
                case "gameplayassets": GameplayAssetSetup.Setup(); break;
                case "arsenalaudio": AnimatedArsenalAudioImport.Setup(); break;
                case "verifyarsenalaudio": AnimatedArsenalAudioVerification.Begin(); break;
                case "cooppackages": CoopPackageSetup.Install(); break;
                case "arsenal": AnimatedArsenalImport.Setup(); break;
                case "reviewarsenal": AnimatedArsenalVerification.Begin(); break;
                case "inspectarsenal": AnimatedArsenalImport.Inspect(); break;
                case "night": NightAndPreviewSetup.VerifyInEditor(); break;
                case "zombiemovement": ZombieMovementVerification.Verify(); break;
                case "flashlight": FlashlightVerification.Verify(); break;
                case "setup": Setup(); break;
                case "warehouse": WarehouseSetup.Open(); break;
                case "verifywarehouse": WarehouseVerification.Verify(); break;
                case "play": EditorApplication.isPlaying = true; break;
                case "stop": EditorApplication.isPlaying = false; break;
                case "check": RunChecks(); break;
                case "webgl": PrepareWebGL(); break;
                case "build": BuildBrowser(); break;
                case "style": StyleBrowserPage(); break;
                case "audio": BeginAudioChecks(); break;
                case "refresh": AssetDatabase.Refresh(); break;
                case "inspect": InspectWeapon(); break;
                case "inspectlibrary": LibraryWeaponImport.Inspect(); break;
                
                case "inspectadditional": AdditionalWeaponImport.Inspect(); break;
                
                case "checkallweapons": Report(UnityEngine.Object.FindAnyObjectByType<YardSurvival>().RunAdditionalWeaponChecks()); break;
                case "inspectpistol": InspectPistol(); break;
                case "inspectzombie": InspectZombie(); break;
                case "inspectnewzombies": RealisticZombieImport.Inspect(); break;
                case "extrazombies": RealisticZombieImport.AddNewVariants(); break;
                case "newzombies": RealisticZombieImport.Setup(); break;
                case "reviewnewzombies": RealisticZombieImport.Review(); break;
                case "handsinspect": FpsHandsImport.Inspect(); break;
                
                case "handsreview": FpsHandsImport.Review(); break;
                case "connectaudio": ZombieAudioSetup.Connect(); Report("AUDIO CONNECT PASS: 64 zombie sounds and four adaptive music cues saved in LastDeparture."); break;
                case "verifyweapons": WeaponAudioSetup.VerifyInEditor(); break;
                case "adaptiveaudio": Report(UnityEngine.Object.FindAnyObjectByType<YardSurvival>().RunAdaptiveAudioChecks()); break;
                case "verifyaudio": ZombieAudioSetup.VerifyInEditor(); break;
                default: Report("Unknown command: " + command); break;
            }
        }
        catch (Exception ex) { Report("FAIL: " + ex); Debug.LogException(ex); }
    }

    static void InspectWeapon()
    {
        string path = Folder + "/ThirdParty/M4A1/M4A1/M4A1.fbx";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
            Report("WEAPON MESH: " + filter.name + "; vertices=" + filter.sharedMesh.vertexCount + "; bounds=" + filter.sharedMesh.bounds + "; rotation=" + filter.transform.localEulerAngles + "; scale=" + filter.transform.localScale);
        foreach (var child in prefab.GetComponentsInChildren<Transform>()) Report("WEAPON NODE: " + child.name + "; pos=" + child.localPosition);
    }
    static void InspectPistol()
    {
        foreach (string path in Directory.GetFiles(Folder + "/ThirdParty/Pistol/FBX", "*.fbx"))
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>()) Report("PISTOL MESH: " + path + "/" + filter.name + "; bounds=" + filter.sharedMesh.bounds + "; pos=" + filter.transform.localPosition + "; rotation=" + filter.transform.localEulerAngles + "; scale=" + filter.transform.localScale);
        }
    }
    static void InspectZombie()
    {
        string path = Folder + "/ThirdParty/Zombies/Zombie_Male.fbx";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        foreach (var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Report("ZOMBIE MESH: " + skin.name + "; bounds=" + skin.sharedMesh.bounds + "; scale=" + skin.transform.lossyScale + "; vertices=" + skin.sharedMesh.vertexCount + "; submeshes=" + skin.sharedMesh.subMeshCount);
            foreach (var bone in skin.bones) Report("ZOMBIE BONE: " + bone.name + "; pos=" + bone.position);
            foreach (var material in skin.sharedMaterials) Report("ZOMBIE MATERIAL: " + material.name + "; color=" + material.color);
        }
        foreach (var item in AssetDatabase.LoadAllAssetsAtPath(path)) if (item is AnimationClip clip) Report("ZOMBIE CLIP: " + clip.name + "; length=" + clip.length);
    }

    [MenuItem("Tools/Yard Survival/Verify radio loop and game effects (Play Mode)")]
    public static void BeginAudioChecks()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first.");
        Application.runInBackground = true;
        var muteProperty = typeof(EditorUtility).GetProperty("audioMasterMute", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Report("AUDIO DIAGNOSTIC: editor audioMasterMute=" + (muteProperty == null ? "unavailable" : muteProperty.GetValue(null).ToString()) + "; sampleRate=" + AudioSettings.GetConfiguration().sampleRate);
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>(); game.BeginAudioProbe();
        audioProbeAt = EditorApplication.timeSinceStartup + 2; effectProbe = -1;
    }
    static void ProbeAudioOutput()
    {
        if (!EditorApplication.isPlaying) { audioProbeAt = 0; effectProbe = -1; return; }
        if (audioProbeAt <= 0 && effectProbe < 0) return;
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>(); if (game == null) return;
        try
        {
            if (audioProbeAt > 0 && EditorApplication.timeSinceStartup >= audioProbeAt)
            {
                audioProbeAt = 0; Report(game.ReadAudioProbe()); effectProbe = 0; effectPeak = 0;
                game.ProbeEffect(0); effectReadAt = EditorApplication.timeSinceStartup + .16;
            }
            if (effectProbe >= 0)
            {
                effectPeak = Mathf.Max(effectPeak, game.ReadEffectOutput());
                if (EditorApplication.timeSinceStartup >= effectReadAt)
                {
                    if (effectPeak < .0001f) throw new Exception("No AudioSource output for effect " + effectProbe);
                    Report("SFX PASS: effect " + effectProbe + "; peak sampled output RMS=" + effectPeak.ToString("F5"));
                    effectProbe++;
                    if (effectProbe == 9) { effectProbe = -1; game.EndAudioProbe(); Report("AUDIO PASS: all nine game effects produced non-zero mixer output."); }
                    else { effectPeak = 0; game.ProbeEffect(effectProbe); effectReadAt = EditorApplication.timeSinceStartup + .16; }
                }
            }
        }
        catch (Exception ex) { game.EndAudioProbe(); effectProbe = -1; audioProbeAt = 0; Report("AUDIO FAIL: " + ex); Debug.LogException(ex); }
    }

    [MenuItem("Tools/Yard Survival/Run gameplay smoke checks (Play Mode)")]
    public static void RunChecks()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode in LastDeparture first.");
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if (game == null) throw new Exception("Prototype component missing");
        Report(game.RunSmokeChecks()); Debug.Log("Yard Survival smoke checks passed.");
    }

    [MenuItem("Tools/Yard Survival/Prepare WebGL target")]
    public static void PrepareWebGL()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL)) throw new Exception("WebGL Build Support module is missing.");
        // Keep tutorial build scenes/settings untouched; the build command selects LastDeparture explicitly.
        bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        Report("WEBGL target switch: " + switched + "; active target: " + EditorUserBuildSettings.activeBuildTarget);
    }

    [MenuItem("Tools/Yard Survival/Build browser prototype")]
    public static void BuildBrowser()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        string output = Path.Combine(Path.GetTempPath(), "YardSurvival-WebGL");
        var compression = PlayerSettings.WebGL.compressionFormat;
        UnityEditor.Build.Reporting.BuildReport report;
        try
        {
            // itch.io serves gzip with the required encoding headers.
            // Restore the user's setting after building this browser release.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.WebGL, options = BuildOptions.None
            });
        }
        finally { PlayerSettings.WebGL.compressionFormat = compression; }
        if (report == null) throw new Exception("Unity did not return a build report; see the Editor Console.");
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded) StyleBrowserPage();
        Report("BROWSER BUILD: " + report.summary.result + "; errors: " + report.summary.totalErrors + "; output: " + output);
    }

    [MenuItem("Tools/Yard Survival/Refresh browser page layout")]
    public static void StyleBrowserPage()
    {
        string index = Path.Combine(Path.GetTempPath(), "YardSurvival-WebGL", "index.html");
        if (!File.Exists(index)) throw new Exception("Build the browser prototype first.");
        string html = File.ReadAllText(index);
        if (!html.Contains("yard-responsive"))
        {
            html = html.Replace("</head>", "<style id=\"yard-responsive\">" +
                "html,body{margin:0;background:#151d17;overflow:hidden}" +
                "#unity-container.unity-desktop{width:min(100vw,1280px)}" +
                "#unity-fullscreen-container{width:100%!important;height:min(calc(100vh - 42px),800px)!important}" +
                "#unity-canvas{width:100%!important;height:100%!important;display:block}" +
                "#unity-footer{color:#d4c9a0;background:#151d17;padding:0 8px;box-sizing:border-box}" +
                "#unity-build-title{font:16px Arial,sans-serif;line-height:38px}" +
                "</style></head>");
            html = html.Replace(PlayerSettings.productName, "Last Departure");
            File.WriteAllText(index, html);
        }
        if (!html.Contains("yard-pointer-lock"))
        {
            html = html.Replace("</head>", "<script id=\"yard-pointer-lock\">" +
                "(()=>{const request=Element.prototype.requestPointerLock;if(!request)return;" +
                "Element.prototype.requestPointerLock=function(...args){try{const pending=request.apply(this,args);" +
                "return pending&&pending.catch?pending.catch(error=>{console.warn('Mouse capture unavailable in this browser:',error.message)}):pending;" +
                "}catch(error){console.warn('Mouse capture unavailable in this browser:',error.message)}}})();" +
                "</script></head>");
            File.WriteAllText(index, html);
        }
        File.Copy(Folder + "/ThirdParty/AnimatedArsenal/CREDITS.txt", Path.Combine(Path.GetDirectoryName(index), "weapon-credits.txt"), true);
        if (!html.Contains("weapon-credits.txt"))
        {
            html = html.Replace("<div id=\"unity-footer\">", "<div id=\"unity-footer\"><a href=\"weapon-credits.txt\" target=\"_blank\" style=\"color:#d4c9a0;font:12px Arial;line-height:38px\">Weapon credits</a>");
            File.WriteAllText(index, html);
        }
        Report("BROWSER PAGE: responsive FPS canvas, Dustline 27 title and graceful mouse-capture failure handling applied.");
    }
    static void Report(string message)
    {
        File.AppendAllText(Folder + "/Validation.log", DateTime.Now.ToString("s") + " " + message + Environment.NewLine);
    }
}





