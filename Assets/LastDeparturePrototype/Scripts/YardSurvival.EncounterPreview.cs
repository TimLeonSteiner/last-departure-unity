using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        const int EncounterPreviewLayer = 29;
        GameObject encounterPreviewStage;
        Transform encounterPreviewPivot;
        RenderTexture encounterPreviewTarget;
        Material encounterPreviewFloor;
        Material encounterPreviewRim;
        Material encounterPreviewMarking;
        Material encounterPreviewLamp;
        Texture2D encounterPreviewShadow;
        Material encounterPreviewDeck;
        int encounterPreviewIndex = -1;
        int encounterDragControl;

        bool DrawEncounterPreview(Rect area, int index)
        {
            int control = GUIUtility.GetControlID(FocusType.Passive, area);
            var current = Event.current;
            if (encounterPreviewIndex != index && current.type == EventType.Repaint)
                BuildEncounterPreview(index);
            if (encounterPreviewTarget == null || encounterPreviewIndex != index)
                return false;

            if (GUI.enabled && !audioCreditsVisible && current.button == 0)
            {
                if (current.type == EventType.MouseDown && area.Contains(current.mousePosition))
                {
                    encounterDragControl = GUIUtility.hotControl = control;
                    current.Use();
                }
                else if (GUIUtility.hotControl == control && current.type == EventType.MouseDrag)
                {
                    encounterPreviewPivot.Rotate(0, -current.delta.x * .65f, 0, Space.World);
                    current.Use();
                }
                else if (GUIUtility.hotControl == control && current.type == EventType.MouseUp)
                {
                    GUIUtility.hotControl = encounterDragControl = 0;
                    current.Use();
                }
            }

            if (current.type == EventType.Repaint)
                DrawEncounterComposite(area);
            var previousAlignment = archiveSmall.alignment;
            archiveSmall.alignment = TextAnchor.MiddleCenter;
            PauseLabel(new Rect(area.x, area.yMax - 39, area.width, 30),
                "DRAG TO ROTATE", archiveSmall, PauseInk);
            archiveSmall.alignment = previousAlignment;
            return true;
        }

        void DrawEncounterComposite(Rect area)
        {
            // The contact shadow is composited under the transparent 3D render.
            if (encounterPreviewShadow == null)
            {
                const int size = 64;
                encounterPreviewShadow = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "Encounters contact shadow",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var point = new Vector2((x + .5f) * 2 / size - 1, (y + .5f) * 2 / size - 1);
                    float opacity = 1 - Mathf.SmoothStep(0, 1, point.magnitude);
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)(opacity * 220));
                }
                encounterPreviewShadow.SetPixels32(pixels);
                encounterPreviewShadow.Apply(false, true);
            }

            var shadow = new Rect(area.x + area.width * .025f, area.y + area.height * .865f,
                area.width * .95f, area.height * .10f);
            GUI.DrawTexture(shadow, encounterPreviewShadow);
            GUI.DrawTexture(area, encounterPreviewTarget, ScaleMode.StretchToFill, true);
        }

        void BuildEncounterPreview(int index)
        {
            ReleaseEncounterPreview();
            encounterPreviewIndex = index;
            var prefab = zombieModels[index];
            if (prefab == null)
                return;

            encounterPreviewStage = new GameObject("Encounters inspection stage");
            var stage = encounterPreviewStage.transform;
            stage.position = new Vector3(0, -200, 0);
            encounterPreviewPivot = new GameObject("Specimen turntable").transform;
            encounterPreviewPivot.SetParent(stage, false);
            var model = Instantiate(prefab, encounterPreviewPivot);
            var zombie = model.GetComponent<ImportedZombie>();
            if (zombie != null)
            {
                var stanceClip = zombie.encounterPoseClip != null ? zombie.encounterPoseClip : zombie.walkClip;
                if (stanceClip != null) SampleEncounterStance(zombie, stanceClip);
            }

            foreach (var animator in model.GetComponentsInChildren<Animator>())
                animator.enabled = false;
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>())
                behaviour.enabled = false;
            foreach (var collider in model.GetComponentsInChildren<Collider>())
                collider.enabled = false;
            foreach (var body in model.GetComponentsInChildren<Rigidbody>())
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            foreach (var node in model.GetComponentsInChildren<Transform>(true))
                node.gameObject.layer = EncounterPreviewLayer;

            var bounds = new Bounds();
            bool hasBounds = false;
            void IncludePoint(Vector3 point)
            {
                if (hasBounds) bounds.Encapsulate(point);
                else { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
            }

            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || !filter.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled)
                    continue;
                var vertices = filter.sharedMesh.vertices;
                foreach (int vertex in filter.sharedMesh.triangles)
                {
                    IncludePoint(encounterPreviewPivot.InverseTransformPoint(filter.transform.TransformPoint(vertices[vertex])));
                }
            }
            var baked = new Mesh();
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!skin.enabled || skin.sharedMesh == null) continue;
                skin.updateWhenOffscreen = true;
                skin.BakeMesh(baked);
                // Split body meshes retain unused vertices from the source model.
                // Only visible triangles should determine the display height.
                var vertices = baked.vertices;
                foreach (int vertex in baked.triangles)
                {
                    var point = encounterPreviewPivot.InverseTransformPoint(skin.transform.TransformPoint(vertices[vertex]));
                    IncludePoint(point);
                }
            }
            Destroy(baked);
            if (!hasBounds || bounds.size.y < .01f)
            {
                ReleaseEncounterPreview();
                encounterPreviewIndex = index;
                return;
            }

            // The Brute's crouched display pose reads shorter than the upright
            // specimens. Calibrate only this inspection copy to their head height.
            float displayHeight = EncounterId(index) == "04-Cargo-Brute" ? 2.5f : 2f;
            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            encounterPreviewPivot.localScale = Vector3.one * (displayHeight / bounds.size.y);
            encounterPreviewPivot.localRotation = Quaternion.Euler(0, 165, 0);

            encounterPreviewFloor = new Material(RuntimeShaders.Find("Universal Render Pipeline/Lit"));
            encounterPreviewFloor.color = new Color(.16f, .17f, .18f);
            encounterPreviewFloor.SetFloat("_Metallic", .35f);
            DetailEncounterPodium(stage);

            AddEncounterLight(stage, new Vector3(-2.1f, 3.7f, -2.8f), new Color(1f, .85f, .71f), 32, true);
            AddEncounterLight(stage, new Vector3(2.5f, 2.3f, 1.2f), new Color(1f, .22f, .12f), 22, false);
            AddEncounterLight(stage, new Vector3(1.8f, 1.5f, -2.7f), new Color(.59f, .74f, 1f), 12, false);

            encounterPreviewTarget = new RenderTexture(990, 1452, 24, RenderTextureFormat.ARGB32) { name = "Encounters specimen preview", filterMode = FilterMode.Bilinear };
            UpdateGraphicsResolution();
            var camera = new GameObject("Specimen camera").AddComponent<Camera>();
            camera.transform.SetParent(stage, false);
            camera.transform.localPosition = new Vector3(0, 2.38f, -5);
            camera.transform.LookAt(stage.position + Vector3.up * 1.02f);
            camera.orthographic = true;
            // A shared camera keeps apparent size consistent across specimens.
            camera.orthographicSize = 1.42f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << EncounterPreviewLayer;
            camera.farClipPlane = 24;
            camera.allowHDR = false;
            camera.useOcclusionCulling = false;
            camera.targetTexture = encounterPreviewTarget;
        }

        static void SampleEncounterStance(ImportedZombie zombie, AnimationClip clip)
        {
            var rig = zombie.animationRig;
            var left = rig != null && rig.isHuman ? rig.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
            var right = rig != null && rig.isHuman ? rig.GetBoneTransform(HumanBodyBones.RightFoot) : null;
            foreach (var bone in zombie.GetComponentsInChildren<Transform>())
            {
                if (left == null && bone.name.EndsWith("LeftFoot", System.StringComparison.Ordinal)) left = bone;
                if (right == null && bone.name.EndsWith("RightFoot", System.StringComparison.Ordinal)) right = bone;
            }

            var head = rig != null && rig.isHuman ? rig.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null)
                foreach (var bone in zombie.GetComponentsInChildren<Transform>())
                    if (bone.name.EndsWith(":Head", System.StringComparison.Ordinal)) { head = bone; break; }
            float bestTime = 0;
            float bestScore = float.PositiveInfinity;
            if (left != null && right != null)
            {
                for (int i = 0; i < 32; i++)
                {
                    float time = clip.length * i / 32f;
                    zombie.SamplePose(clip, time);
                    Vector3 spacing = left.position - right.position;
                    float score = Mathf.Abs(spacing.y) * 5f + Mathf.Abs(Vector3.Dot(spacing, zombie.transform.forward)) * .3f;
                    if (clip == zombie.encounterPoseClip)
                    {
                        // Prefer a planted frame for the display pose.
                        score += Mathf.Abs(spacing.y) * 15f;
                    }
                    if (head != null) score -= (head.position.y - Mathf.Min(left.position.y, right.position.y)) * 2f;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    bestTime = time;
                }
            }
            zombie.SamplePose(clip, bestTime);
        }

        void DetailEncounterPodium(Transform stage)
        {
            encounterPreviewRim = new Material(encounterPreviewFloor);
            encounterPreviewRim.color = new Color(.65f, .62f, .58f);
            encounterPreviewRim.mainTexture = Resources.Load<Texture2D>("MenuArtwork/InspectionSteel");
            encounterPreviewRim.SetFloat("_Smoothness", .54f);
            encounterPreviewMarking = new Material(encounterPreviewFloor);
            encounterPreviewMarking.color = new Color(.46f, .30f, .075f);
            encounterPreviewMarking.SetFloat("_Metallic", .05f);

            encounterPreviewLamp = new Material(encounterPreviewMarking);
            encounterPreviewLamp.EnableKeyword("_EMISSION");
            encounterPreviewLamp.SetColor("_EmissionColor", new Color(1.4f, .48f, .055f));
            encounterPreviewDeck = new Material(encounterPreviewFloor);
            encounterPreviewDeck.color = new Color(.87f, .86f, .84f);
            encounterPreviewDeck.mainTexture = Resources.Load<Texture2D>("MenuArtwork/InspectionSteel");
            encounterPreviewDeck.mainTextureScale = Vector2.one;
            encounterPreviewDeck.SetFloat("_Smoothness", .32f);

            var podiumPrefab = Resources.Load<GameObject>("MenuArtwork/InspectionPodium");
            if (podiumPrefab == null)
            {
                MakeEncounterFloor(stage, PrimitiveType.Cylinder, new Vector3(0, -.079f, 0),
                    new Vector3(1.8f, .079f, 1.8f), encounterPreviewDeck);
                return;
            }

            var podium = Instantiate(podiumPrefab, stage, false);
            foreach (var node in podium.GetComponentsInChildren<Transform>(true))
                node.gameObject.layer = EncounterPreviewLayer;
            foreach (var part in podium.GetComponentsInChildren<MeshRenderer>())
            {
                string name = part.gameObject.name;
                part.sharedMaterial = name.StartsWith("Deck") ? encounterPreviewDeck :
                    name.StartsWith("Steel") ? encounterPreviewRim :
                    name.StartsWith("Paint") ? encounterPreviewMarking :
                    name.StartsWith("Amber") ? encounterPreviewLamp : encounterPreviewFloor;
            }
        }

        Transform MakeEncounterFloor(Transform stage, PrimitiveType type, Vector3 position, Vector3 scale,
            Material material = null)
        {
            var floor = GameObject.CreatePrimitive(type);
            floor.layer = EncounterPreviewLayer;
            floor.transform.SetParent(stage, false);
            floor.transform.localPosition = position;
            floor.transform.localScale = scale;
            floor.GetComponent<Collider>().enabled = false;
            floor.GetComponent<Renderer>().sharedMaterial = material != null ? material : encounterPreviewFloor;
            return floor.transform;
        }

        static void AddEncounterLight(Transform stage, Vector3 position, Color color, float intensity, bool shadows)
        {
            var light = new GameObject("Specimen light").AddComponent<Light>();
            light.transform.SetParent(stage, false);
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = 9;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.cullingMask = 1 << EncounterPreviewLayer;
        }

        void ReleaseEncounterPreview()
        {
            if (encounterDragControl != 0 && GUIUtility.hotControl == encounterDragControl)
                GUIUtility.hotControl = 0;
            encounterDragControl = 0;
            if (encounterPreviewStage != null)
            {
                encounterPreviewStage.SetActive(false);
                Destroy(encounterPreviewStage);
            }
            if (encounterPreviewTarget != null) Destroy(encounterPreviewTarget);
            if (encounterPreviewFloor != null) Destroy(encounterPreviewFloor);
            if (encounterPreviewRim != null) Destroy(encounterPreviewRim);
            if (encounterPreviewMarking != null) Destroy(encounterPreviewMarking);
            if (encounterPreviewLamp != null) Destroy(encounterPreviewLamp);
            if (encounterPreviewShadow != null) Destroy(encounterPreviewShadow);
            if (encounterPreviewDeck != null) Destroy(encounterPreviewDeck);
            encounterPreviewStage = null;
            encounterPreviewTarget = null;
            encounterPreviewFloor = null;
            encounterPreviewRim = null;
            encounterPreviewMarking = null;
            encounterPreviewLamp = null;
            encounterPreviewShadow = null;
            encounterPreviewDeck = null;
            encounterPreviewIndex = -1;
            UpdateGraphicsResolution();
        }
    }
}
