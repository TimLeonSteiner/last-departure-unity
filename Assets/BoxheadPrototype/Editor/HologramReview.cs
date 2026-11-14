using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class HologramReview
{
    const string Root = "Assets/Verification~/";
    static int stage;
    static double next;
    static YardSurvival game;
    static float frozenTime;
    static Vector3 savedCamera;
    static float idleStarted;
    static bool previousBackground, previousTorch, previousSpill;
    static readonly System.Collections.Generic.List<InputDevice> reviewDisabledDevices = new System.Collections.Generic.List<InputDevice>();
    static Keyboard reviewKeyboard;
    static Mouse reviewMouse;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static HologramReview()
    {
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += RestoreReviewInput;
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode) RestoreReviewInput(); };
    }
    static void IsolateReviewInput()
    {
        RestoreReviewInput();
        foreach (var device in InputSystem.devices)
            if ((device is Keyboard || device is Mouse) && device.enabled) { reviewDisabledDevices.Add(device); InputSystem.DisableDevice(device); }
        reviewKeyboard = InputSystem.AddDevice<Keyboard>(); reviewKeyboard.MakeCurrent();
        reviewMouse = InputSystem.AddDevice<Mouse>(); reviewMouse.MakeCurrent();
    }
    static void RestoreReviewInput()
    {
        if (reviewKeyboard != null && reviewKeyboard.added) InputSystem.RemoveDevice(reviewKeyboard);
        if (reviewMouse != null && reviewMouse.added) InputSystem.RemoveDevice(reviewMouse);
        reviewKeyboard = null; reviewMouse = null;
        foreach (var device in reviewDisabledDevices) if (device.added) InputSystem.EnableDevice(device);
        reviewDisabledDevices.Clear();
    }
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Boxhead.unity");
        SessionState.SetBool("HologramReview.Active", true);
        EditorApplication.isPlaying = true;
    }
    static void Set(string name, object value) => typeof(YardSurvival).GetField(name, Private).SetValue(game, value);
    static void CheckTabKeys()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var background = InputSystem.settings.backgroundBehavior;
        var mode = InputSystem.settings.updateMode;
        try
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            var update = typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(InputUpdateType) }, null);
            void KeyFrame(params Key[] keys)
            { update.Invoke(null, new object[] { InputUpdateType.Dynamic }); InputState.Change(keyboard, new KeyboardState(keys), InputUpdateType.Dynamic); }
            _ = keyboard.eKey.wasPressedThisFrame; _ = keyboard.qKey.wasPressedThisFrame;
            var tick = typeof(YardSurvival).GetMethod("TickHologram", Private);
            KeyFrame(Key.E); tick.Invoke(game, null);
            if ((int)typeof(YardSurvival).GetField("hologramTab", Private).GetValue(game) != 1) throw new Exception("E did not advance to Build");
            KeyFrame(); KeyFrame(Key.Q); tick.Invoke(game, null);
            if ((int)typeof(YardSurvival).GetField("hologramTab", Private).GetValue(game) != 0) throw new Exception("Q did not return to Inventory");
            KeyFrame(); KeyFrame(Key.Q); tick.Invoke(game, null);
            if ((int)typeof(YardSurvival).GetField("hologramTab", Private).GetValue(game) != 2) throw new Exception("Q did not wrap to Skills");
            var navigate = typeof(YardSurvival).GetMethod("NavigateHologramSelection", Private);
            int Read(string name) => (int)typeof(YardSurvival).GetField(name, Private).GetValue(game);
            int points = Read("skillPoints");
            _ = keyboard.rightArrowKey.wasPressedThisFrame; _ = keyboard.downArrowKey.wasPressedThisFrame; _ = keyboard.enterKey.wasPressedThisFrame;
            KeyFrame(); KeyFrame(Key.RightArrow); navigate.Invoke(game, new object[] { keyboard });
            KeyFrame(); KeyFrame(Key.DownArrow); navigate.Invoke(game, new object[] { keyboard });
            if (Read("selectedSkill") != 1 || Read("selectedSkillRank") != 1 || Read("skillPoints") != points) throw new Exception("Arrow selection moved incorrectly or spent points");
            KeyFrame(); KeyFrame(Key.Enter); navigate.Invoke(game, new object[] { keyboard });
            if (Read("skillPoints") != points) throw new Exception("Enter skipped a required rank");
            Set("selectedSkill", 0); Set("selectedSkillRank", 0);
            KeyFrame(); Set("hologramTab", 0);
            typeof(YardSurvival).GetMethod("RebuildWristDisplay", Private).Invoke(game, null);
        }
        finally { InputSystem.RemoveDevice(keyboard); InputSystem.settings.backgroundBehavior = background; InputSystem.settings.updateMode = mode; }
    }
    static void Tick()
    {
        if (File.Exists(Root + "HologramReview.request") && !EditorApplication.isCompiling)
        { File.Delete(Root + "HologramReview.request"); stage = 0; next = 0; SessionState.SetBool("HologramReview.Active", true); }
        if (!SessionState.GetBool("HologramReview.Active", false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            if (EditorApplication.timeSinceStartup < next) return;
            if (stage == 0)
            {
                game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
                if (game == null) return;
                IsolateReviewInput();
                previousBackground = Application.runInBackground; Application.runInBackground = true;
                EditorApplication.isPaused = false; game.enabled = true;
                File.WriteAllText(Root + "SkillTreeChecks.txt", game.RunSkillTreeChecks());
                game.PrepareInventoryHudReview(false);
                Set("skillPoints", 3);
                typeof(YardSurvival).GetMethod("RestoreLaunchRendering", Private).Invoke(game, null);
                typeof(YardSurvival).GetMethod("StopLaunchSounds", Private).Invoke(game, null);
                Set("supplies", 24);
                typeof(YardSurvival).GetMethod("SpawnPickup", Private).Invoke(game, new object[] { 3, new Vector3(8, 0, 4), 8, 24 });
                typeof(YardSurvival).GetMethod("SpawnPickup", Private).Invoke(game, new object[] { 0, new Vector3(-5, 0, -8), 60, 120 });
                previousTorch = ((Light)typeof(YardSurvival).GetField("flashlight", Private).GetValue(game)).enabled;
                previousSpill = ((Light)typeof(YardSurvival).GetField("flashlightSpill", Private).GetValue(game)).enabled;
                savedCamera = ((Camera)typeof(YardSurvival).GetField("arenaCamera", Private).GetValue(game)).transform.localPosition;
                typeof(YardSurvival).GetMethod("OpenHologram", Private).Invoke(game, new object[] { 0 });
                CheckTabKeys();
                var avatar = (HumanoidPlayerAvatar)typeof(YardSurvival).GetField("hologramAvatar", Private).GetValue(game);
                if (avatar == null || avatar.idle == null || !AssetDatabase.GetAssetPath(avatar.idle).EndsWith("/InventorySurvivor/InventoryIdle.anim")) throw new Exception("New downloaded Meshy idle is not assigned to the menu");
                idleStarted = (float)typeof(HumanoidPlayerAvatar).GetField("idleTime", Private).GetValue(avatar);
                frozenTime = Time.time;
                EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Show();
            }
            else if (stage == 1)
            {
                if (Time.timeScale != 0 || Mathf.Abs(Time.time - frozenTime) > .001f) throw new Exception("World did not freeze while browsing.");
                var avatar = (HumanoidPlayerAvatar)typeof(YardSurvival).GetField("hologramAvatar", Private).GetValue(game);
                float idleNow = (float)typeof(HumanoidPlayerAvatar).GetField("idleTime", Private).GetValue(avatar);
                if (idleNow - idleStarted < .1f) throw new Exception("Inventory idle stopped while gameplay was paused: " + idleStarted + " -> " + idleNow + "; enabled=" + game.enabled + "; frame=" + Time.frameCount + "; reveal=" + typeof(YardSurvival).GetField("hologramReveal", Private).GetValue(game));
                foreach (float yaw in new[] { -18f, 0f, 18f })
                {
                    Set("hologramOrbitYaw", yaw);
                    typeof(YardSurvival).GetMethod("PoseWristProjection", Private).Invoke(game, null);
                    var camera = (Camera)typeof(YardSurvival).GetField("arenaCamera", Private).GetValue(game);
                    if (Vector3.Dot(camera.transform.position - avatar.transform.position, avatar.transform.forward) >= 0)
                        throw new Exception("Inventory orbit exposed the character front");
                }
                Set("hologramOrbitYaw", 0f);
                typeof(YardSurvival).GetMethod("PoseWristProjection", Private).Invoke(game, null);
                if (((Light)typeof(YardSurvival).GetField("flashlight", Private).GetValue(game)).enabled || ((Light)typeof(YardSurvival).GetField("flashlightSpill", Private).GetValue(game)).enabled)
                    throw new Exception("Camera flashlight is lighting the inventory character");
                ScreenCapture.CaptureScreenshot(Root + "Hologram-Inventory.png");
            }
            else if (stage == 2) Set("hologramTab", 1);
            else if (stage == 3) ScreenCapture.CaptureScreenshot(Root + "Hologram-Build.png");
            else if (stage == 4) Set("hologramTab", 1);
            else if (stage == 5) ScreenCapture.CaptureScreenshot(Root + "Hologram-Build.png");
            else if (stage == 6) { Set("hologramTab", 2); File.WriteAllText(Root + "SkillTreePresentationChecks.txt", game.RunSkillTreePresentationChecks()); }
            else if (stage == 7) ScreenCapture.CaptureScreenshot(Root + "Hologram-Skills.png");
            else
            {
                typeof(YardSurvival).GetMethod("RestoreHologram", Private).Invoke(game, null);
                var camera = (Camera)typeof(YardSurvival).GetField("arenaCamera", Private).GetValue(game);
                if (Time.timeScale != 1 || AudioListener.pause || Vector3.Distance(camera.transform.localPosition, savedCamera) > .001f)
                    throw new Exception("Closing did not restore gameplay time, audio, and camera.");
                if (((Light)typeof(YardSurvival).GetField("flashlight", Private).GetValue(game)).enabled != previousTorch || ((Light)typeof(YardSurvival).GetField("flashlightSpill", Private).GetValue(game)).enabled != previousSpill)
                    throw new Exception("Closing did not restore flashlight state");
                typeof(YardSurvival).GetMethod("OpenHologram", Private).Invoke(game, new object[] { 2 });
                SessionState.SetBool("HologramReview.Active", false); Application.runInBackground = previousBackground; RestoreReviewInput();
                File.WriteAllText(Root + "HologramReview.txt", "PASS: Camera flashlight and spill disabled during inventory and restored on closing; New Meshy idle assigned and advancing on unscaled time while gameplay is paused; rear camera at center and both 18-degree endpoints; Q/E navigation and wraparound; gameplay time frozen across browsing; close restores timescale, audio and camera. Captured all three world-space tabs; Map removed. Left paused on Skills for review."); return;
            }
            stage++; next = EditorApplication.timeSinceStartup + 3;
        }
        catch (Exception ex) { RestoreReviewInput(); SessionState.SetBool("HologramReview.Active", false); File.WriteAllText(Root + "HologramReview.txt", ex.ToString()); Debug.LogException(ex); }
    }
}
