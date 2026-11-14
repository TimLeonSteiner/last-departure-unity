using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    // Finger posing and arm IK for the imported CC0 hand mesh.
    public sealed class ModeledHands : MonoBehaviour
    {
        sealed class Finger { public Transform bone; public Quaternion rest; public Vector3 axis; public float curl; }
        sealed class Arm
        {
            public Transform upper, forearm, hand;
            public float upperLength, lowerLength;
            public Quaternion upperOffset, lowerOffset, handOffset;
            public readonly List<Finger> fingers = new List<Finger>();
        }
        Arm right, left;
        public int VertexCount { get; private set; }
        public bool HasContinuousArms => Connected(right) && Connected(left);
        static bool Connected(Arm arm) => arm != null && Mathf.Abs(Vector3.Distance(arm.upper.position, arm.forearm.position) - arm.upperLength) < .005f && Mathf.Abs(Vector3.Distance(arm.forearm.position, arm.hand.position) - arm.lowerLength) < .005f;
        void Awake()
        {
            right = ReadArm("R"); left = ReadArm("L");
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.updateWhenOffscreen = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                VertexCount += renderer.sharedMesh.vertexCount;
            }
        }
        Transform Bone(string name)
        {
            foreach (var node in GetComponentsInChildren<Transform>()) if (node.name == name) return node;
            throw new System.InvalidOperationException("Missing imported arm joint: " + name);
        }
        Arm ReadArm(string side)
        {
            var a = new Arm { upper = Bone("upper_arm." + side), forearm = Bone("forearm." + side), hand = Bone("hand." + side) };
            Vector3 up = (a.forearm.position - a.upper.position).normalized, low = (a.hand.position - a.forearm.position).normalized;
            a.upperLength = Vector3.Distance(a.forearm.position, a.upper.position); a.lowerLength = Vector3.Distance(a.hand.position, a.forearm.position);
            Vector3 finger = (Bone("f_middle.01." + side).position - a.hand.position).normalized;
            Vector3 width = Bone("f_index.01." + side).position - Bone("f_pinky.01." + side).position;
            Vector3 palm = Vector3.Cross(finger, width).normalized * (side == "L" ? -1 : 1);
            a.handOffset = Quaternion.Inverse(Quaternion.LookRotation(palm, finger)) * a.hand.rotation;
            a.upperOffset = Quaternion.Inverse(Quaternion.LookRotation(up, palm)) * a.upper.rotation;
            a.lowerOffset = Quaternion.Inverse(Quaternion.LookRotation(low, palm)) * a.forearm.rotation;
            foreach (var node in a.hand.GetComponentsInChildren<Transform>())
            {
                if (!(node.name.StartsWith("f_") || node.name.StartsWith("thumb.")) || node.name.EndsWith("_end")) continue;
                float curl = node.name.Contains(".01.") ? 58 : node.name.Contains(".02.") ? 72 : 42;
                if (node.name.StartsWith("thumb")) curl = node.name.Contains(".01.") ? 20 : 26;
                a.fingers.Add(new Finger { bone = node, rest = node.localRotation, axis = node.InverseTransformDirection(Vector3.Cross(node.up, palm).normalized), curl = curl });
            }
            return a;
        }
        void PoseArm(Arm a, Vector3 wrist, Vector3 shoulder, Vector3 elbowHint, Vector3 fingers, Vector3 palm, bool trigger, float openness, float triggerCurl = .45f)
        {
            wrist = transform.parent.TransformPoint(wrist); shoulder = transform.parent.TransformPoint(shoulder);
            elbowHint = transform.parent.TransformPoint(elbowHint);
            fingers = transform.parent.TransformDirection(fingers); palm = transform.parent.TransformDirection(palm);
            Vector3 delta = wrist - shoulder;
            float reach = a.upperLength + a.lowerLength - .001f;
            if (delta.magnitude > reach) { shoulder += delta.normalized * (delta.magnitude - reach); delta = wrist - shoulder; }
            float distance = Mathf.Max(.05f, delta.magnitude);
            Vector3 direction = delta.normalized;
            float along = (a.upperLength * a.upperLength - a.lowerLength * a.lowerLength + distance * distance) / (2 * distance);
            float height = Mathf.Sqrt(Mathf.Max(0, a.upperLength * a.upperLength - along * along));
            Vector3 bend = Vector3.ProjectOnPlane(elbowHint - shoulder, direction).normalized;
            Vector3 elbow = shoulder + direction * along + bend * height;
            a.upper.position = shoulder; a.upper.rotation = Quaternion.LookRotation(elbow - shoulder, palm) * a.upperOffset;
            a.forearm.position = elbow; a.forearm.rotation = Quaternion.LookRotation(wrist - elbow, palm) * a.lowerOffset;
            a.hand.position = wrist; a.hand.rotation = Quaternion.LookRotation(palm, fingers) * a.handOffset;
            foreach (var f in a.fingers)
            {
                float curl = f.curl;
                if (trigger && f.bone.name.StartsWith("f_index")) curl *= triggerCurl;
                f.bone.localRotation = f.rest * Quaternion.AngleAxis(curl * (1 - openness), f.axis);
            }
            if (!trigger && Vector3.Dot(palm, transform.parent.up) > .5f)
                foreach (var f in a.fingers)
                    if (f.bone.name.StartsWith("thumb.01."))
                        f.bone.rotation = Quaternion.AngleAxis(-28, Vector3.Cross(fingers, palm).normalized) * f.bone.rotation;
        }
        public void Pose(bool pistol, Vector3 supportWrist, Quaternion supportRotation, float openness, Vector3? rightWrist = null)
        {
            PoseArm(right, rightWrist ?? (pistol ? new Vector3(.005f, -.038f, -.12f) : new Vector3(.043f, -.083f, -.18f)), new Vector3(.27f, -.35f, -.22f), new Vector3(.48f, -.5f, .03f), pistol ? new Vector3(0, .342f, .94f) : Vector3.forward, Vector3.left, true, 0, pistol ? .85f : .45f);
            // Keep a bend at the elbow. The old shoulder was beyond the arm's reach,
            // which straightened both bones into a rod and forced a sharp wrist angle.
            PoseArm(left, supportWrist, new Vector3(-.34f, -.38f, -.02f), new Vector3(-.48f, -.38f, .05f), supportRotation * Vector3.right, supportRotation * Vector3.up, false, openness);
            if (pistol)
            {
                // Thumbs follow the slide's side instead of pointing across the gun.
                foreach (var finger in right.fingers)
                    if (finger.bone.name.StartsWith("thumb.01.")) finger.bone.rotation = Quaternion.AngleAxis(55, transform.parent.up) * finger.bone.rotation;
                float grip = 1 - Mathf.Clamp01(Vector3.Distance(supportWrist, new Vector3(-.025f, -.042f, -.12f)) / .06f);
                foreach (var finger in left.fingers)
                    if (finger.bone.name.StartsWith("thumb.01.")) finger.bone.rotation = Quaternion.AngleAxis(-55 * grip, transform.parent.up) * finger.bone.rotation;
            }
        }
    }
}
