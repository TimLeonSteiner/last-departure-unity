using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        bool hologramOpen, hologramOwnsPause;
        float hologramReveal, hologramPreviousScale, hologramPreviousFov;
        bool hologramPreviousAudio, hologramPreviousFlashlight, hologramPreviousSpill;
        float hologramOrbitYaw, hologramOrbitPitch, hologramOrbitDistance = 1.85f;
        Vector2 hologramPan;
        int hologramTab, hologramRenderedTab = -1, hologramSelection, hologramPage;
        Transform hologramRoot, hologramBody, hologramWrist, hologramEmitter;
        HumanoidPlayerAvatar hologramAvatar;
        RectTransform hologramCanvas;
        CanvasGroup hologramOpacity;
        Material hologramLensMaterial;
        Light hologramLight;
        Vector3 hologramCameraPosition;
        Quaternion hologramCameraRotation;
        readonly List<HologramHit> hologramHits = new List<HologramHit>();
        readonly List<GameObject> hologramWidgets = new List<GameObject>();
        struct HologramHit { public Rect rect; public Action action; }
        static readonly Color HoloInk = new Color(.38f, .88f, 1f, 1);
        static readonly Color HoloMuted = new Color(.3f, .58f, .66f, 1);
        bool HologramBlocking => hologramOwnsPause;

        void OpenHologram(int tab)
        {
            if (dead || showHelp || upgradePending || bindingAction >= 0 || player == null) return;
            if (!hologramOwnsPause)
            {
                hologramPreviousScale = Time.timeScale; hologramPreviousAudio = AudioListener.pause;
                hologramCameraPosition = arenaCamera.transform.localPosition;
                hologramCameraRotation = arenaCamera.transform.localRotation;
                hologramPreviousFov = arenaCamera.fieldOfView;
                hologramPreviousFlashlight = flashlight != null && flashlight.enabled;
                hologramPreviousSpill = flashlightSpill != null && flashlightSpill.enabled;
                if (flashlight != null) flashlight.enabled = false;
                if (flashlightSpill != null) flashlightSpill.enabled = false;
                hologramOwnsPause = true; Time.timeScale = 0; AudioListener.pause = true;
                UpdateGraphicsResolution();
                hologramOrbitYaw = 0; hologramOrbitPitch = 0; hologramOrbitDistance = 1.85f; hologramPan = Vector2.zero;
                CaptureInventoryWeapon(); CreateWristProjection();
            }
            hologramTab = Mathf.Clamp(tab, 0, 2); hologramOpen = true;
            hologramRenderedTab = -1; hologramSelection = 0; hologramPage = 0;
            OnPreparationMenuOpened();
            aiming = false; PauseMovementAudio(true); ReleaseMouse();
            PoseWristProjection();PlayMenuFeedback(true);
        }
        void CloseHologram(bool feedback=true) { if(hologramOpen&&feedback)PlayMenuFeedback(false);hologramOpen = false; buildFireBlocked = true; }
        void RestoreHologram()
        {
            if (!hologramOwnsPause) return;
            hologramOpen = false; hologramReveal = 0; hologramOwnsPause = false;
            UpdateGraphicsResolution();
            Time.timeScale = hologramPreviousScale; AudioListener.pause = hologramPreviousAudio;
            if (flashlight != null) flashlight.enabled = hologramPreviousFlashlight && !dead;
            if (flashlightSpill != null) flashlightSpill.enabled = hologramPreviousSpill && !dead;
            if (arenaCamera != null)
            {
                arenaCamera.transform.localPosition = hologramCameraPosition;
                arenaCamera.transform.localRotation = hologramCameraRotation;
                arenaCamera.fieldOfView = hologramPreviousFov;
            }
            if (viewmodelCamera != null) viewmodelCamera.enabled = true;
            if (hologramRoot != null) { hologramRoot.gameObject.SetActive(false); Destroy(hologramRoot.gameObject); }
            if (hologramLensMaterial != null) Destroy(hologramLensMaterial);
            if (hologramOverlayMaterial != null) Destroy(hologramOverlayMaterial);
            if (hologramBackplateMaterial != null) Destroy(hologramBackplateMaterial);
            hologramRoot = hologramBody = hologramWrist = null; hologramCanvas = null;
            hologramAvatar = null; hologramHits.Clear(); hologramWidgets.Clear(); hologramRenderedTab = -1;
        }
        void TickHologram()
        {
            if (dead || showHelp || upgradePending || frontScreen != FrontScreen.Game)
            { RestoreHologram(); return; }
            if (bindingAction < 0 && Pressed(Control.Hologram))
            { if (hologramOpen) CloseHologram(); else { OpenHologram(0); return; } }
            if (!HologramBlocking) return;
            var keyboard = Keyboard.current;
            if (hologramOpen && keyboard != null)
            {
                int direction = (keyboard.eKey.wasPressedThisFrame ? 1 : 0) - (keyboard.qKey.wasPressedThisFrame ? 1 : 0);
                if (direction != 0) SelectHologramTab((hologramTab + direction + 3) % 3);
                if (hologramReveal > .98f) NavigateHologramSelection(keyboard);
            }
            hologramReveal = Mathf.MoveTowards(hologramReveal, hologramOpen ? 1 : 0, Time.unscaledDeltaTime / .65f);
            if (!hologramOpen && hologramReveal <= 0) { RestoreHologram(); CaptureMouse(); return; }
            ReleaseMouse();
            UpdateInventoryCameraInput();
            PoseWristProjection();
            if (hologramRenderedTab != hologramTab) RebuildWristDisplay();
            var mouse = Mouse.current;
            if (hologramOpen && hologramReveal > .98f && mouse != null && !mouse.rightButton.isPressed && !mouse.middleButton.isPressed && mouse.leftButton.wasPressedThisFrame &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(hologramCanvas, mouse.position.ReadValue(), arenaCamera, out Vector2 local))
            {
                Vector2 point = new Vector2(local.x + 400, 310 - local.y);
                for (int i = 0; i < hologramHits.Count; i++)
                    if (hologramHits[i].rect.Contains(point)) { hologramHits[i].action(); break; }
            }
        }
        void SelectHologramTab(int tab)
        {
            if(hologramTab==tab)return;
            hologramTab=tab;hologramSelection=hologramPage=0;hologramRenderedTab=-1;PlayMenuFeedback(false);
        }
        void SelectHologramItem(int index)
        {
            if(hologramSelection==index)return;
            hologramSelection=index;hologramRenderedTab=-1;PlayMenuFeedback(false);
        }
        void EquipHologramWeapon(int index){if(SwitchInventorySlot(index)){PlayMenuFeedback(true);hologramRenderedTab=-1;}}
        void DropHologramWeapon(int index){if(DropInventoryWeapon(index))PlayMenuFeedback(true);}
        void SelectHologramBlueprint(int type){SelectDefense(type);PlayMenuFeedback(true);CloseHologram(false);ReleaseMouse();}
        void NavigateHologramSelection(Keyboard keyboard)
        {
            int x = (keyboard.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (keyboard.leftArrowKey.wasPressedThisFrame ? 1 : 0);
            int y = (keyboard.downArrowKey.wasPressedThisFrame ? 1 : 0) - (keyboard.upArrowKey.wasPressedThisFrame ? 1 : 0);
            if (hologramTab == 2)
            {
                if (x != 0 || y != 0)
                {
                    Vector2 origin = CircuitNodePosition(selectedSkill,selectedSkillRank), direction = new Vector2(x,y).normalized;
                    float best = float.PositiveInfinity;
                    int chosen = selectedSkill, level = selectedSkillRank;
                    for (int id = selectedSkill / 3 * 3; id < selectedSkill / 3 * 3 + 3; id++)
                    for (int rank = 0; rank < SkillMaxRank + (id % 3 == 0 ? 1 : 0); rank++)
                    {
                        Vector2 delta = CircuitNodePosition(id,rank) - origin;
                        float forward = Vector2.Dot(delta,direction);
                        if (forward <= 1) continue;
                        float sideways = Mathf.Abs(delta.x*direction.y-delta.y*direction.x);
                        float score = delta.magnitude + sideways*3;
                        if (score < best) { best=score; chosen=id; level=rank; }
                    }
                    SelectSkillNode(chosen,level);
                }
                if (keyboard.enterKey.wasPressedThisFrame && selectedSkillRank < SkillMaxRank && selectedSkillRank == skillRanks[selectedSkill]) BuySkill(selectedSkill);
            }
            else if (hologramTab == 1)
            {
                SelectHologramItem(Mathf.Clamp(hologramSelection + x, 0, AvailableDefenses.Length - 1));
                if (keyboard.enterKey.wasPressedThisFrame && supplies >= DefenseCosts[AvailableDefenses[hologramSelection]])
                { SelectHologramBlueprint(AvailableDefenses[hologramSelection]); }
            }
            else if (hologramTab == 0 && weaponInventory.Count > 0)
            {
                SelectHologramItem(Mathf.Clamp(hologramSelection + x + y * 4, 0, weaponInventory.Count - 1));
                hologramPage = hologramSelection / 8;
                if (Pressed(Control.DropWeapon)) { DropHologramWeapon(hologramSelection); return; }
                if (keyboard.enterKey.wasPressedThisFrame) EquipHologramWeapon(hologramSelection);
            }
            if (x != 0 || y != 0 || keyboard.enterKey.wasPressedThisFrame) hologramRenderedTab = -1;
        }
        void UpdateInventoryCameraInput()
        {
            if (!hologramOpen || hologramReveal < .98f) return;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (mouse.rightButton.isPressed)
                {
                    hologramOrbitYaw = Mathf.Clamp(hologramOrbitYaw + delta.x * .12f, -18, 18);
                    hologramOrbitPitch = Mathf.Clamp(hologramOrbitPitch - delta.y * .1f, -8, 18);
                }
                if (mouse.middleButton.isPressed)
                {
                    hologramPan.x = Mathf.Clamp(hologramPan.x - delta.x * .0015f, -.35f, .35f);
                    hologramPan.y = Mathf.Clamp(hologramPan.y - delta.y * .0015f, -.3f, .3f);
                }
                hologramOrbitDistance = Mathf.Clamp(hologramOrbitDistance - mouse.scroll.ReadValue().y * .0015f, 1.65f, 3.6f);
            }
            if (Keyboard.current?.rKey.wasPressedThisFrame == true)
            { hologramOrbitYaw = 0; hologramOrbitPitch = 0; hologramOrbitDistance = 1.85f; hologramPan = Vector2.zero; }
        }
        void CreateWristProjection()
        {
            hologramRoot = new GameObject("Wrist inventory presentation").transform;
            hologramRoot.SetParent(player, false);
            var inventoryModel = Resources.Load<GameObject>("Inventory/Survivor");
            if (inventoryModel == null && playerSkinModels != null && playerSkinModels.Length > 0)
                inventoryModel = playerSkinModels[Mathf.Clamp(selectedPlayerSkin, 0, playerSkinModels.Length - 1)];
            if (inventoryModel != null)
            {
                hologramBody = Instantiate(inventoryModel, hologramRoot).transform;
                hologramBody.localPosition = Vector3.down * .65f;
                hologramAvatar = hologramBody.GetComponent<HumanoidPlayerAvatar>();
                foreach (var collider in hologramBody.GetComponentsInChildren<Collider>()) collider.enabled = false;
                foreach (var renderer in hologramBody.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.gameObject.layer = 0;
                    foreach (var material in renderer.sharedMaterials)
                        if (material != null && material.name.ToLowerInvariant().Contains("smg")) renderer.enabled = false;
                }
            }
            else
            {
                var fallback = DepotModels.Player(this, hologramRoot, Vector3.zero);
                hologramBody = fallback.root; hologramWrist = fallback.rightArm;
                fallback.rightArm.localRotation = Quaternion.Euler(-65, 0, -25);
            }
            var go = new GameObject("Projected equipment plane", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            hologramCanvas = (RectTransform)go.transform; hologramCanvas.SetParent(hologramRoot, false);
            hologramCanvas.sizeDelta = new Vector2(800, 620);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = arenaCamera;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            hologramOpacity = go.GetComponent<CanvasGroup>();
            hologramLensMaterial = new Material(RuntimeShaders.Find("Universal Render Pipeline/Unlit"));
            hologramLensMaterial.SetColor("_BaseColor", new Color(.1f, .75f, 1, 1));
            hologramEmitter = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            hologramEmitter.name = "Wrist projector housing"; hologramEmitter.SetParent(hologramRoot, false);
            hologramEmitter.localScale = new Vector3(.105f, .035f, .085f);
            hologramEmitter.GetComponent<Collider>().enabled = false;
            hologramEmitter.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            var lens = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            lens.name = "Projector aperture"; lens.SetParent(hologramEmitter, false);
            lens.localPosition = new Vector3(0, .55f, 0); lens.localScale = new Vector3(.7f, .12f, .65f);
            lens.GetComponent<Collider>().enabled = false; lens.GetComponent<Renderer>().sharedMaterial = hologramLensMaterial;
            hologramLight = new GameObject("Projection bounce").AddComponent<Light>(); hologramLight.transform.SetParent(hologramRoot, false);
            hologramLight.color = HoloInk; hologramLight.range = 1.1f; hologramLight.intensity = .25f; hologramLight.shadows = LightShadows.None;
        }
        void PoseWristProjection()
        {
            float blend = Mathf.SmoothStep(0, 1, hologramReveal);
            if (hologramAvatar != null)
            {
                hologramAvatar.Tick(Time.unscaledDeltaTime, 0, 0, false);
                var rig = hologramAvatar.rig;
                if (rig != null && !rig.isHuman)
                {
                    // Preserve the authored Meshy pose and follow its left wrist device.
                    if (hologramWrist == null)
                        foreach (var bone in rig.GetComponentsInChildren<Transform>())
                            if (bone.name.Replace("mixamorig:", "").Replace("mixamorig", "") == "LeftHand") { hologramWrist = bone; break; }
                }
                if (rig != null && rig.isHuman)
                {
                    var upper = rig.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    var lower = rig.GetBoneTransform(HumanBodyBones.RightLowerArm);
                    var hand = rig.GetBoneTransform(HumanBodyBones.RightHand);
                    if (upper != null && lower != null && hand != null)
                    {
                        Vector3 elbow = hologramBody.TransformPoint(new Vector3(.28f, 1.12f, .1f));
                        Vector3 target = hologramBody.TransformPoint(new Vector3(.42f, 1.34f, .34f));
                        upper.rotation = Quaternion.Slerp(upper.rotation, Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation, blend);
                        lower.rotation = Quaternion.Slerp(lower.rotation, Quaternion.FromToRotation(hand.position - lower.position, target - lower.position) * lower.rotation, blend);
                        hologramWrist = hand;
                    }
                }
            }
            Vector3 wrist = hologramWrist != null ? hologramWrist.position : player.TransformPoint(new Vector3(.4f, .65f, .25f));
            // Shoulder-height rear view: the survivor fills the left foreground,
            // with the wrist projection ahead and to his right.
            Vector3 focus = new Vector3(.78f + hologramPan.x, .78f + hologramPan.y, .5f);
            Quaternion orbit = Quaternion.Euler(hologramOrbitPitch, hologramOrbitYaw, 0);
            Vector3 cameraTarget = focus + orbit * Vector3.back * hologramOrbitDistance;
            Quaternion cameraRotation = Quaternion.LookRotation(focus - cameraTarget);
            arenaCamera.transform.localPosition = Vector3.Lerp(hologramCameraPosition, cameraTarget, blend);
            arenaCamera.transform.localRotation = Quaternion.Slerp(hologramCameraRotation, cameraRotation, blend);
            arenaCamera.fieldOfView = Mathf.Lerp(hologramPreviousFov, 52, blend);
            if (viewmodelCamera != null) viewmodelCamera.enabled = false;
            hologramBody.gameObject.SetActive(hologramReveal > .12f);
            float unfold = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.3f, 1, hologramReveal));
            hologramCanvas.position = Vector3.Lerp(wrist, player.TransformPoint(new Vector3(1.05f, .88f, 1f)), unfold);
            hologramCanvas.localRotation = Quaternion.Euler(0, 8, 0);
            hologramCanvas.localScale = new Vector3(.0023f * Mathf.Lerp(.03f, 1, unfold), .0023f * Mathf.Lerp(.015f, 1, unfold), .0023f);
            hologramOpacity.alpha = unfold;
            hologramEmitter.position = wrist - player.forward * .05f; hologramEmitter.rotation = player.rotation;
            hologramLight.transform.position = wrist; hologramLight.intensity = .25f * unfold;
            if (hologramBackplateMaterial != null)
                hologramBackplateMaterial.SetFloat("_SweepPhase", Mathf.Repeat(Time.unscaledTime / 4f, 1f));
        }
        // The menu itself is world-space. This small cue is the only screen-space element.
        void DrawHologram(float width, float height)
        {
            DrawPreparationHint(width, height);
        }
        Material hologramOverlayMaterial;
        Material hologramBackplateMaterial;
        Material HologramOverlayMaterial
        {
            get
            {
                if (hologramOverlayMaterial == null) hologramOverlayMaterial = new Material(Resources.Load<Shader>("Inventory/HologramOverlay"));
                return hologramOverlayMaterial;
            }
        }
        RectTransform HoloRect(string name, Rect rect)
        {
            var go = new GameObject(name, typeof(RectTransform)); hologramWidgets.Add(go);
            var rt = (RectTransform)go.transform; rt.SetParent(hologramCanvas, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            // UI sibling order supplies layering. Depth offsets separate connected
            // shapes in perspective and make their borders appear clipped.
            rt.anchoredPosition3D = new Vector3(rect.x, -rect.y, 0); rt.sizeDelta = rect.size;
            return rt;
        }
        void HoloBox(Rect rect, Color color) { var image = HoloRect("Projection line", rect).gameObject.AddComponent<Image>(); image.material = HologramOverlayMaterial; image.color = color; image.raycastTarget = false; }
        void DrawHologramBackplate()
        {
            if (hologramBackplateMaterial == null)
            {
                hologramBackplateMaterial = new Material(HologramOverlayMaterial);
                hologramBackplateMaterial.EnableKeyword("HOLOGRAM_BACKPLATE");
            }
            // Extra space around the panel gives the edge glow room to fade out.
            var backplate = HoloRect("Hologram backplate", new Rect(-16, -16, 832, 652)).gameObject.AddComponent<RawImage>();
            backplate.material = hologramBackplateMaterial;
            backplate.color = new Color(1, 1, 1, .95f);
            backplate.raycastTarget = false;
        }
        Text HoloText(Rect rect, string text, int size = 18, Color? color = null)
        {
            var label = HoloRect(text, rect).gameObject.AddComponent<Text>();
            label.material = HologramOverlayMaterial;
            label.font = Resources.Load<Font>("PauseMenu/InterQuiet") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text; label.fontSize = size; label.color = color ?? HoloInk;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false; return label;
        }
        void HoloFrame(Rect r, Color color)
        {
            HoloBox(new Rect(r.x, r.y, r.width, 2), color); HoloBox(new Rect(r.x, r.yMax, r.width, 2), color);
            HoloBox(new Rect(r.x, r.y, 2, r.height), color); HoloBox(new Rect(r.xMax, r.y, 2, r.height), color);
        }
        void HoloSelection(Rect r, bool tintInterior = true)
        {
            if (tintInterior) HoloBox(r, new Color(.55f, .9f, 1f, .16f));
            r = new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8);
            float cut = Mathf.Min(7, r.width * .2f);
            Vector2[] corners = {
                new Vector2(r.x + cut,r.y), new Vector2(r.xMax - cut,r.y),
                new Vector2(r.xMax,r.y + cut), new Vector2(r.xMax,r.yMax - cut),
                new Vector2(r.xMax - cut,r.yMax), new Vector2(r.x + cut,r.yMax),
                new Vector2(r.x,r.yMax - cut), new Vector2(r.x,r.y + cut)
            };
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 delta = corners[(i + 1) % corners.Length] - corners[i];
                var edge = HoloRect("Selection outline", new Rect(corners[i].x,corners[i].y,delta.magnitude,2));
                edge.localRotation = Quaternion.Euler(0,0,-Mathf.Atan2(delta.y,delta.x) * Mathf.Rad2Deg);
                var ink = edge.gameObject.AddComponent<Image>(); ink.material = HologramOverlayMaterial; ink.color = new Color(.8f,.96f,1f); ink.raycastTarget = false;
            }
        }
        void HoloButton(Rect r, string label, Action action, bool active = true)
        {
            HoloBox(r, active ? new Color(.08f, .36f, .43f, .38f) : new Color(.05f, .1f, .12f, .3f));
            HoloFrame(r, active ? HoloMuted : new Color(.15f, .22f, .24f));
            HoloText(new Rect(r.x + 12, r.y + 8, r.width - 16, r.height - 8), label, 18, active ? HoloInk : HoloMuted);
            if (active) hologramHits.Add(new HologramHit { rect = r, action = action });
        }
        void HoloWeapon(Rect r, int id)
        {
            var texture = Resources.Load<Texture2D>("HudWeapons/" + id.ToString("00"));
            if (texture == null) return;
            float aspect = texture.width / (float)texture.height;
            float w = Mathf.Min(r.width, r.height * aspect), h = w / aspect;
            var image = HoloRect("Weapon projection", new Rect(r.center.x - w / 2, r.center.y - h / 2, w, h)).gameObject.AddComponent<RawImage>();
            image.material = HologramOverlayMaterial; image.texture = texture; image.color = new Color(.65f, 1, 1, .95f); image.raycastTarget = false;
        }
        void RebuildWristDisplay()
        {
            foreach (var widget in hologramWidgets) if (widget != null) { widget.SetActive(false); Destroy(widget); }
            hologramWidgets.Clear(); hologramHits.Clear(); hologramRenderedTab = hologramTab;
            DrawHologramBackplate();
            HoloText(new Rect(22, 47, 620, 50), hologramTab == 0 ? "INVENTORY" : hologramTab == 1 ? "CONSTRUCTION" : "UPGRADE BENCH", 36);
            HoloText(new Rect(660, 54, 130, 28), "II  PAUSED", 18);
            string[] tabs = { "INVENTORY", "BUILD", "SKILLS" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i; var rect = new Rect(22 + i * 254, 108, 246, 38);
                if (i == hologramTab) HoloBox(rect, new Color(.2f, .65f, .8f, .3f));
                HoloText(new Rect(rect.x + 12, rect.y + 7, 166, 30), tabs[i], 18, i == hologramTab ? HoloInk : HoloMuted);
                hologramHits.Add(new HologramHit { rect = rect, action = () => { SelectHologramTab(tab); } });
            }
            if (hologramTab == 0) BuildWristInventory();
            else if (hologramTab == 1) BuildWristConstruction();
            else BuildWristSkills();
            HoloBox(new Rect(22, 569, 756, 1), HoloMuted);
            HoloText(new Rect(22, 586, 756, 25), hologramTab == 1 ? "[Q/E] TABS     [CLICK] SELECT BLUEPRINT     " + ControlHint(Control.Hologram) + " EXIT" : "[Q/E] TABS     [ARROWS] SELECT     [ENTER] CONFIRM     " + ControlHint(Control.Hologram) + " EXIT", 15, HoloMuted);
        }
        void BuildWristInventory()
        {
            int count = weaponInventory.Count;
            hologramSelection = Mathf.Clamp(hologramSelection, 0, Mathf.Max(0, count - 1));
            for (int cell = 0; cell < WeaponSlotLimit; cell++)
            {
                int index = cell;
                Rect r = new Rect(22 + cell % 4 * 116, 174 + cell / 4 * 138, 106, 124);
                bool occupied = index < count, selected = occupied && hologramSelection == index;
                HoloBox(r, new Color(.03f, .18f, .23f, selected ? .8f : .3f)); HoloFrame(r, selected ? HoloInk : HoloMuted * .65f);
                if (selected) HoloSelection(r);
                HoloText(new Rect(r.x + 7, r.y + 5, 90, 20), (index + 1).ToString("00"), 12, HoloMuted);
                if (!occupied) { HoloText(new Rect(r.x + 34, r.y + 44, 60, 35), "+", 25, HoloMuted); continue; }
                var slot = weaponInventory[index]; HoloWeapon(new Rect(r.x + 5, r.y + 24, 96, 65), slot.weapon);
                HoloText(new Rect(r.x + 8, r.y + 99, 94, 22), slot.ammo + " / " + slot.reserve, 14);
                hologramHits.Add(new HologramHit { rect = r, action = () => { SelectHologramItem(index); } });
            }
            if (count > 0)
            {
                var slot = weaponInventory[hologramSelection];
                HoloText(new Rect(508, 174, 267, 65), WeaponCatalog.All[slot.weapon].name.ToUpperInvariant(), 24);
                HoloWeapon(new Rect(510, 246, 260, 118), slot.weapon);
                HoloText(new Rect(510, 380, 260, 64), slot.ammo + "  LOADED\n" + slot.reserve + "  RESERVE", 19);
                int index = hologramSelection;
                HoloButton(new Rect(508, 455, 260, 43), slot.weapon == weaponId ? "EQUIPPED" : "EQUIP WEAPON", () => { EquipHologramWeapon(index); });
            }
            if(count>0) { int dropIndex=hologramSelection; HoloButton(new Rect(508, 510, 260, 43), ControlHint(Control.DropWeapon)+" DROP WEAPON", () => DropHologramWeapon(dropIndex)); }
            HoloText(new Rect(22, 470, 440, 28), count + " / 4 WEAPONS     /     " + supplies + " SCRAP", 18);
            if (count > 8)
                HoloButton(new Rect(22, 515, 440, 35), "NEXT SLOTS  >", () => { hologramPage = (hologramPage + 1) % Mathf.CeilToInt(count / 8f); hologramRenderedTab = -1;PlayMenuFeedback(false); });
        }
        void BuildWristConstruction()
        {
            HoloText(new Rect(22, 166, 740, 30), supplies + " scrap available", 18, HoloMuted);
            for (int i = 0; i < AvailableDefenses.Length; i++)
            {
                int type = AvailableDefenses[i]; bool selected = hologramSelection == i;
                Rect r = new Rect(130 + i * 184, 228, 172, 212);
                HoloBox(r, new Color(.035f, .038f, .04f, selected ? .94f : .82f));
                HoloFrame(r, new Color(.55f, .55f, .52f, .3f));
                if (selected) HoloBox(new Rect(r.x, r.yMax - 3, r.width, 3), HudRed);
                HoloText(new Rect(r.x + 12, r.y + 10, 70, 26), BindingName(bindings[(int)BuildKeys[type]].primary), 16, HudWhite);
                var texture = BuildImage(type);
                if (texture != null)
                {
                    float w = r.width - 34, h = w * texture.height / texture.width;
                    var image = HoloRect("Build model image " + BuildImageNames[type], new Rect(r.center.x - w / 2, r.y + 26 + (110 - h) / 2, w, h)).gameObject.AddComponent<RawImage>();
                    image.texture = texture; image.material = HologramOverlayMaterial;
                    image.color = Color.white; image.raycastTarget = false;
                }
                HoloText(new Rect(r.x + 4, r.y + 140, r.width - 8, 30), BuildNames[type], 15, HudWhite).alignment = TextAnchor.MiddleCenter;
                bool affordable = supplies >= DefenseCosts[type];
                HoloText(new Rect(r.x + 4, r.y + 174, r.width - 8, 26), DefenseCosts[type] + " scrap", 16, affordable ? DreadMuted : HudRed).alignment = TextAnchor.MiddleCenter;
                if (affordable) hologramHits.Add(new HologramHit { rect = r, action = () => SelectHologramBlueprint(type) });
            }

        }
    }
}
