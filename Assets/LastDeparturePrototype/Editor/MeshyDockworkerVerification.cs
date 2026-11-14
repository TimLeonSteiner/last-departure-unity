using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class MeshyDockworkerVerification
{
    const string Request = "Library/VerifyMeshyDockworker.request";
    const string Pending = "MeshyDockworkerVerification.Pending";
    const string Report = "Assets/Verification~/MeshyCharacters/DockworkerVerification.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static MeshyDockworkerVerification()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnPlay;
    }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(Request);
        if (EditorApplication.isPlaying) Run();
        else { SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true; }
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        EditorApplication.delayCall += Run;
    }
    static void Run()
    {
        var report = new StringBuilder();
        GameObject holder = null;
        void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
            report.AppendLine("PASS: " + message);
        }
        try
        {
            var game = UnityEngine.Object.FindFirstObjectByType<YardSurvival>();
            Check(game != null, "LastDeparture scene has a live YardSurvival instance.");
            var prefab = Resources.Load<GameObject>("MeshyCombat/01-Dockworker-Shambler");
            Check(prefab != null && game.zombieModels[0] == prefab, "Normal roster slot 0 loads the new Meshy Dockworker.");
            Check(prefab.name == "01-Dockworker-Shambler", "Existing encounter ID is preserved.");
            Check(game.zombieModels.Select(p => p.name).SequenceEqual(new[] { "01-Dockworker-Shambler", "Spitter", "Screamer", "ArmoredWorker", "Charger" }), "All five roster entries remain in their existing order.");
            var wave = typeof(YardSurvival).GetField("wave", Private);
            var serial = typeof(YardSurvival).GetField("spawnSerial", Private);
            var select = typeof(YardSurvival).GetMethod("SelectEnemyVariant", Private);
            object previousWave = wave.GetValue(game), previousSerial = serial.GetValue(game);
            try
            {
                wave.SetValue(game, 1);
                for (int i = 0; i < 25; i++)
                {
                    serial.SetValue(game, i);
                    if ((int)select.Invoke(game, null) != 0) throw new Exception("Wave 1 selected another roster entry.");
                }
                report.AppendLine("PASS: 25 wave-1 spawn selections resolve to the new Shambler.");
            }
            finally { wave.SetValue(game, previousWave); serial.SetValue(game, previousSerial); }

            var runnerSelector = typeof(YardSurvival).GetMethod("SelectRunner", BindingFlags.Static | BindingFlags.NonPublic);
            for (int w = 1; w <= 8; w++)
            {
                int runners = 0;
                for (int s = 0; s < 40; s++)
                {
                    if ((bool)runnerSelector.Invoke(null, new object[] { w, s, 0 })) runners++;
                    for (int variant = 1; variant <= 4; variant++)
                        if ((bool)runnerSelector.Invoke(null, new object[] { w, s, variant })) throw new Exception("Special enemy selected as runner.");
                }
                Check(runners == (w < 4 ? 0 : 10), "Wave " + w + " selects " + runners + " runners per 40 normal spawn candidates.");
            }
            var snapshotType = typeof(YardSurvival).GetNestedType("CoopEnemyState", BindingFlags.NonPublic);
            var snapshot = Activator.CreateInstance(snapshotType);
            snapshotType.GetField("runner").SetValue(snapshot, true);
            var decoded = JsonUtility.FromJson(JsonUtility.ToJson(snapshot), snapshotType);
            Check((bool)snapshotType.GetField("runner").GetValue(decoded), "Runner flag survives co-op snapshot JSON round-trip.");

            holder = new GameObject("Dockworker verification only");
            holder.transform.position = new Vector3(1000, 0, 1000);
            var z = UnityEngine.Object.Instantiate(prefab, holder.transform).GetComponent<ImportedZombie>();
            Check(z.GetComponent<ZombieArchetype>().kind == ZombieKind.Shambler, "Normal Shambler archetype is assigned.");
            Check(z.animationRig.avatar.isValid && !z.animationRig.isHuman, "Native Generic Meshy skeleton is valid, without retargeting.");
            Check(z.parts.Length == 6 && z.parts.All(p => p != null && p.sharedMesh.vertexCount > 0 && p.bones.All(b => b != null)), "All six damage regions have geometry and valid skin bones.");
            Check(z.parts.Sum(p => p.sharedMesh.triangles.Length / 3) == 309738, "Combat regions preserve all 309,738 source triangles.");
            Check(z.parts.All(p => p.sharedMaterials.All(m => m.GetTexture("_BaseMap") != null && m.GetTexture("_BumpMap") != null)), "Every combat region has base-color and normal textures.");
            var clips = new[] { z.idleClip, z.walkClip, z.attackClip, z.deathClip, z.crawlClip };
            Check(clips.All(c => c != null && c.length > 2.9f) && clips.Distinct().Count() == 5, "Five distinct custom Meshy motions are assigned.");
            Check(z.idleClip.isLooping && z.walkClip.isLooping && z.crawlClip.isLooping && !z.attackClip.isLooping && !z.deathClip.isLooping, "Idle, shamble and crawl loop; attack and death are one-shot.");
            var bones = z.hitBones.Distinct().ToArray();
            for (int i = 0; i < clips.Length; i++)
            {
                z.SamplePose(clips[i], clips[i].length * .1f);
                var rotations = bones.Select(b => b.localRotation).ToArray();
                z.SamplePose(clips[i], clips[i].length * .55f);
                float angle = bones.Select((b, index) => Quaternion.Angle(rotations[index], b.localRotation)).Max();
                Check(angle > .1f && bones.All(b => float.IsFinite(b.position.x) && float.IsFinite(b.position.y) && float.IsFinite(b.position.z)), MeshyDockworkerMotions.Labels[i] + " animates the native bones with finite poses.");
            }
            var current = typeof(ImportedZombie).GetField("currentPose", Private);
            z.TickMovement(.2f, 0, false);
            Check(current.GetValue(z) == z.idleClip, "Stationary Shambler selects its authored idle.");
            z.TickMovement(.2f, .2f, false);
            Check(current.GetValue(z) == z.walkClip, "Movement selects its authored shamble.");
            Check(z.runClip != null && z.runClip.name == "Running" && z.runClip.isLooping, "Native Meshy Running clip is assigned and loops.");
            z.Runner = true;
            z.TickMovement(.2f, .5f, false);
            Check(current.GetValue(z) == z.runClip && z.RunnerSpeedMultiplier == 1.8f, "Runner moves 80% faster and selects the running animation.");
            z.TickMovement(.2f, 0, false);
            Check(current.GetValue(z) == z.idleClip, "Stopped runner uses idle.");
            z.TickMovement(.2f, 0, true, .36f);
            Check(current.GetValue(z) == z.attackClip, "Melee uses the authored swipe with gameplay attack progress.");
            z.InjureLeg(3); z.TickMovement(.2f, .1f, false);
            Check(!z.IsRunning && z.RunnerSpeedMultiplier == 1 && current.GetValue(z) == z.walkClip, "One leg injury removes running and its speed bonus.");
            z.InjureLeg(4); z.TickMovement(.8f, .1f, false);
            Check(z.InjuredLegs == 2 && current.GetValue(z) == z.crawlClip, "Two leg injuries select the authored forearm crawl.");
            if (!YardSurvival.GoreEnabled) report.AppendLine("INFO: Global gore is disabled; preserved that setting. Verified six-region geometry without enabling dismemberment.");
            var death = UnityEngine.Object.Instantiate(prefab, holder.transform).GetComponent<ImportedZombie>();
            death.Die();
            death.SendMessage("Update");
            Check(death.RagdollBodyCount == 0 && death.ActiveDeathClip != null && death.deathClips.Contains(death.ActiveDeathClip), "Intact death selects one of its three authored death variations.");
            report.AppendLine("DOCKWORKER VERIFICATION PASS");
        }
        catch (Exception error) { report.AppendLine("FAIL: " + error); Debug.LogException(error); }
        finally
        {
            if (holder != null) UnityEngine.Object.DestroyImmediate(holder);
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, report.ToString());
            EditorApplication.isPlaying = false;
        }
    }
}
