using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

// Integration checks execute the real gameplay methods and let Unity physics advance.
// The report deliberately separates automated coverage from manual play testing.
[InitializeOnLoad]
public static class GameplayAcceptanceChecks
{
    const string Root = "Assets/LastDeparturePrototype/AcceptanceReview";
    const string Request = "Assets/LastDeparturePrototype/Acceptance.request";
    const string Pending = "Dustline.Acceptance.Pending";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly List<string> report = new List<string>();
    static YardSurvival game;
    static IEnumerator routine;
    static double next;
    static bool oldEnabled;

    static GameplayAcceptanceChecks()
    {
        EditorApplication.update += Poll;
        AssemblyReloadEvents.beforeAssemblyReload += () =>
        {
            if (routine == null) return;
            report.Add("ABORT Assembly reload interrupted acceptance; rerun against a stable build.");
            routine = null; SessionState.SetBool(Pending, false); Save();
        };
        EditorApplication.playModeStateChanged += state =>
        {
            if (routine != null && state == PlayModeStateChange.ExitingPlayMode)
            {
                report.Add("ABORT External Play Mode exit interrupted acceptance at " + DateTime.Now.ToString("HH:mm:ss"));
                routine = null; SessionState.SetBool(Pending, false); Save();
            }
        };
    }
    [MenuItem("Tools/Yard Survival/Run independent gameplay acceptance")]
    public static void Begin()
    {
        if (routine != null) throw new InvalidOperationException("Acceptance is already running.");
        Directory.CreateDirectory(Root);
        SessionState.SetBool(Pending, true);
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        next = EditorApplication.timeSinceStartup + 2;
    }
    static object Read(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    static T Get<T>(string name) => (T)Read(game, name);
    static void Set(string name, object value) => game.GetType().GetField(name, Flags).SetValue(game, value);
    static object Call(string name, params object[] args)
    {
        var method = game.GetType().GetMethods(Flags).First(m => m.Name == name && m.GetParameters().Length >= args.Length && m.GetParameters().Skip(args.Length).All(p => p.IsOptional));
        var parameters = method.GetParameters();
        var values = parameters.Select((p, i) => i < args.Length ? args[i] : p.DefaultValue).ToArray();
        return method.Invoke(game, values);
    }
    static void Check(bool condition, string label) { report.Add((condition ? "PASS " : "FAIL ") + label); }
    static void Case(string name, Action test)
    {
        try { test(); }
        catch (Exception ex) { report.Add("FAIL " + name + ": " + (ex.InnerException ?? ex).Message); }
    }
    static void Save() { File.WriteAllLines(Root + "/Results.txt", report); }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Request) && routine == null && !SessionState.GetBool(Pending, false))
        {
            try { File.Delete(Request); } catch (IOException) { return; }
            Begin();
        }
        if (EditorApplication.timeSinceStartup < next) return;
        if (routine == null && SessionState.GetBool(Pending, false) && EditorApplication.isPlaying)
        {
            game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            if (game == null) return;
            report.Clear(); report.Add("Independent Play Mode acceptance " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            report.Add("Automated integration checks; these do not certify subjective audio quality, ADS appearance or two-client Relay.");
            oldEnabled = game.enabled; game.enabled = false; routine = Run();
        }
        if (routine == null) return;
        if (!EditorApplication.isPlaying || game == null)
        {
            report.Add("ABORT Play Mode/game instance is no longer valid.");
            routine = null; SessionState.SetBool(Pending, false); Save(); return;
        }
        try
        {
            if (!routine.MoveNext()) { Finish(); return; }
            next = EditorApplication.timeSinceStartup + (routine.Current is float seconds ? seconds : .02f);
        }
        catch (Exception ex) { report.Add("FAIL runner: " + (ex.InnerException ?? ex)); Finish(); }
    }
    static void Finish()
    {
        routine = null; SessionState.SetBool(Pending, false);
        if (game != null) { Case("Restore menu", () => Call("ShowHome")); game.enabled = oldEnabled; }
        report.Add("COMPLETE " + DateTime.Now.ToString("HH:mm:ss"));
        Save(); Debug.Log("Acceptance finished: " + report.Count(l => l.StartsWith("FAIL")) + " failed checks. " + Root);
        EditorApplication.isPlaying = false;
    }
    static void Frame(string name)
    {
        var camera = Get<Camera>("arenaCamera");
        var target = new RenderTexture(960, 600, 24);
        var pixels = new Texture2D(960, 600, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            target.Create();
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 960, 600), 0, 0); pixels.Apply();
            File.WriteAllBytes(Root + "/" + name + ".png", pixels.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels); }
    }
    static IEnumerator Run()
    {
        Case("Menu lifecycle", () =>
        {
            Call("ShowHome"); Check(!Get<bool>("fighting") && Get<int>("wave") == 0, "Home does not run a wave");
            Call("PlayFromMenu"); Check(Get<bool>("fighting") && Get<int>("wave") == 1, "Play starts first wave");
            Call("PauseFromGame"); Check(Time.timeScale == 0 && AudioListener.pause, "Solo pause stops simulation and audio");
            Call("ResumeFromMenu"); Check(Time.timeScale == 1 && !AudioListener.pause, "Resume restores simulation and audio");
        });
        yield return null;
        foreach (int id in Enumerable.Range(0, WeaponCatalog.All.Length).Where(i => !WeaponCatalog.IsMelee(i)))
        {
            Case("Ammo " + id, () =>
            {
                Call("ResetRun"); var weapon = WeaponCatalog.All[id];
                Call("EquipWeapon", id, weapon.capacity, 2);
                game.Fire(Vector3.up); Check(Get<int>("ammunition") == weapon.capacity - 1, weapon.name + " shot consumes one round");
                game.BeginReload(); int before = Get<int>("ammunition"); game.Fire(Vector3.up);
                Check(Get<int>("ammunition") == before, weapon.name + " cannot fire while reloading");
                Call("UpdateReload", weapon.reload + 1);
                Check(Get<int>("ammunition") == weapon.capacity && Get<int>("reserveAmmo") == 1, weapon.name + " reload transfers finite reserve");
                Set("ammunition", 0); Set("reserveAmmo", 0); game.Fire(Vector3.up); Call("UpdateReload", weapon.reload + 1);
                Check(Get<int>("ammunition") == 0 && Get<int>("reserveAmmo") == 0, weapon.name + " empty reserve does not refill");
            });
            yield return null;
        }
        Case("Supply collection", () =>
        {
            Call("ResetRun"); Set("health", 60);
            var point = Get<Transform>("player").position; point.y = 0;
            Call("CreateSupplyBox", point, true, 0); Call("UpdateSupplyBoxes");
            Check(Get<int>("health") == 95, "Health crate heals 35 on contact");
            Set("health", 100); Call("CreateSupplyBox", point, true, 0);
            int count = Get<IList>("supplyBoxes").Count; Call("UpdateSupplyBoxes");
            Check(Get<IList>("supplyBoxes").Count == count, "Full health preserves crate");
            Call("EquipWeapon", WeaponCatalog.Starter, WeaponCatalog.All[WeaponCatalog.Starter].capacity, 0);
            Call("CreateSupplyBox", point, false, 0); Call("UpdateSupplyBoxes");
            Check(Get<int>("reserveAmmo") == WeaponCatalog.All[WeaponCatalog.Starter].capacity * 2, "Ammo crate grants two magazines");
        });
        yield return null;
        Case("Automatic wave", () =>
        {
            Call("PlayFromMenu"); Set("remainingToSpawn", 0); Call("TickWaveSimulation", .02f);
            Check(!Get<bool>("fighting"), "Cleared wave enters intermission");
            for (int i = 0; i < 110; i++) Call("TickWaveSimulation", .05f);
            Check(Get<bool>("fighting") && Get<int>("wave") == 2, "Next wave starts once within 5.5 seconds without input");
        });
        yield return null;
        foreach (int type in new[] { 0, 1 })
        {
            Case("Build collision " + type, () =>
            {
                Call("ResetRun"); Set("supplies", 100);
                Check(game.TryPlaceDefense(new Vector3(0, 0, 3), type, false), "Defense placement " + type);
                var moved = (Vector3)Call("Move", Vector3.zero, Vector3.forward * 6, .35f, true);
                Check(moved.z < 2.5f, "Built defense blocks movement sweep " + type);
                if (type == 1)
                {
                    var defense = Get<IList>("turrets")[0]; var body = (Transform)Read(defense, "body");
                    var meshes = body.GetComponentsInChildren<MeshFilter>();
                    Check(meshes.Length > 0 && meshes.All(m => m.sharedMesh != null && m.sharedMesh.vertexCount > 0), "Turret has live mesh references");
                    Check(body.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null)), "Turret materials have supported shader and color texture");
                    Frame("Turret");
                }
            });
            yield return null;
        }
        foreach (int type in new[] { 0, 1, 2 })
        {
            Case("Hammer " + type, () =>
            {
                Call("ResetRun"); Set("supplies", 100);
                Check(game.TryPlaceDefense(new Vector3(0, 0, 2), type, false), "Hammer blueprint placement " + type);
                var defense = Get<IList>(new[] { "barricades", "turrets", "traps" }[type])[0];
                int id = (int)Read(defense, "netId"); var body = (Transform)Read(defense, "body");
                var state = body.GetComponents<MonoBehaviour>().FirstOrDefault(c => c.GetType().Name == "DefenseConstruction");
                Check(state != null, "Defense begins with construction state " + type);
                if (state == null) return;
                var ready = state.GetType().GetProperty("Ready");
                Check(!(bool)ready.GetValue(state), "Blueprint is initially unfinished " + type);
                int before = Get<int>("supplies");
                Check(!(bool)Call("ApplyHammerHit", id, new Vector3(10, .65f, 10)) && Get<int>("supplies") == before, "Distant hammer hit rejected without cost " + type);
                for (int hit = 0; hit < 8 && !(bool)ready.GetValue(state); hit++) Call("ApplyHammerHit", id, Get<Transform>("player").position);
                Check((bool)ready.GetValue(state), "Repeated nearby hammer hits complete structure " + type);
                var costs = (int[])game.GetType().GetField("DefenseCosts", BindingFlags.Static | Flags).GetValue(null);
                Check(Get<int>("supplies") == 100 - costs[type], "Construction charges original total cost once " + type);
                before = Get<int>("supplies");
                Check(!(bool)Call("ApplyHammerHit", id, Get<Transform>("player").position) && Get<int>("supplies") == before, "Full-health repair consumes nothing " + type);
                float max = type == 0 ? 100 : type == 1 ? 150 : 80;
                defense.GetType().GetField("hp", Flags).SetValue(defense, max - 10);
                Call("ApplyHammerHit", id, Get<Transform>("player").position);
                Check((float)Read(defense, "hp") == max && Get<int>("supplies") == before - 1, "Repair restores capped health for one scrap " + type);
                defense.GetType().GetField("hp", Flags).SetValue(defense, max - 20);
                Call("CompleteWave");
                Check((float)Read(defense, "hp") == max - 20, "Wave reward does not repair structure for free " + type);
            });
            yield return null;
        }
        for (int variant = 0; variant < game.zombieModels.Length; variant++)
        {
            Call("ResetRun"); yield return null;
            ImportedZombie zombie = null; Vector3 start = new Vector3(0, 0, 4);
            Case("Spawn zombie " + variant, () =>
            {
                Set("spawnSerial", variant); Call("SpawnEnemy", start);
                zombie = (ImportedZombie)Read(Get<IList>("enemies")[0], "zombie"); zombie.Tick(0, false);
                var animator = zombie.animationRig;
                report.Add("INFO variant " + variant + " model=" + zombie.name + " animator=" + (animator == null ? "null" : animator.name + " human=" + animator.isHuman + " enabled=" + animator.enabled + " avatar=" + (animator.avatar == null ? "null" : animator.avatar.name)));
                zombie.Sever(1, Vector3.right, Get<Transform>("actors")); zombie.Die();
                var counter = zombie.GetType().GetProperty("RagdollBodyCount", Flags);
                report.Add("INFO ragdoll links=" + (counter == null ? "property absent in loaded assembly" : counter.GetValue(zombie).ToString()) + " character root=" + zombie.transform.parent.name);
                Check(zombie.transform.parent.GetComponentsInChildren<Rigidbody>().Count(b => !b.isKinematic) >= 6, "Variant " + variant + " articulated dynamic ragdoll");
                Check(zombie.transform.parent.GetComponentsInChildren<Joint>().Length >= 5, "Variant " + variant + " connected body joints");
            });
            bool stable = true, debrisStable = true; int observed = 0, debrisObserved = 0;
            for (int frame = 0; frame < 60; frame++)
            {
                yield return .05f;
                if (zombie == null) continue;
                foreach (var body in zombie.transform.parent.GetComponentsInChildren<Rigidbody>())
                {
                    observed++; var p = body.worldCenterOfMass;
                    if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z) || p.y < -.5f || Vector3.Distance(p, start) > 12 || body.linearVelocity.magnitude > 25) stable = false;
                }
                foreach (var debris in Get<Transform>("actors").GetComponentsInChildren<ZombieDebris>())
                {
                    var body = debris.GetComponent<Rigidbody>(); var collider = debris.GetComponent<Collider>();
                    debrisObserved++;
                    if (body == null || collider == null || !collider.enabled || collider.isTrigger) { debrisStable = false; continue; }
                    if (!float.IsFinite(body.position.y) || body.worldCenterOfMass.y < -.5f || body.linearVelocity.magnitude > 12 || Vector3.Distance(body.worldCenterOfMass, start) > 12) debrisStable = false;
                }
            }
            Check(stable && observed > 0, "Variant " + variant + " remains finite and above floor for three seconds");
            Check(debrisStable && debrisObserved > 0, "Variant " + variant + " severed limb has collision and remains above floor");
            if (zombie != null)
            {
                var bodies = zombie.transform.parent.GetComponentsInChildren<Rigidbody>();
                Check(bodies.Length >= 6 && bodies.Max(b => b.worldCenterOfMass.y) < 1.4f, "Variant " + variant + " actually collapses below standing height");
            }
            Case("Ragdoll screenshot " + variant, () => Frame("Ragdoll-" + variant));
            Save();
        }
    }
}
