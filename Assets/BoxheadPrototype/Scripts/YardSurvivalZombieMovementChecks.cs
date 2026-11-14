#if UNITY_EDITOR
using System;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunZombieMovementChecks()
        {
            Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); };
            foreach (var prefab in zombieModels)
            {
                var root = new GameObject("Locomotion verification");
                try
                {
                    var a = Instantiate(prefab, root.transform).GetComponent<ImportedZombie>();
                    var b = Instantiate(prefab, root.transform).GetComponent<ImportedZombie>();
                    check(a.animationRig != null && a.animationRig.isHuman, "Humanoid rig required");
                    var footA = a.animationRig.GetBoneTransform(HumanBodyBones.LeftFoot);
                    var footB = b.animationRig.GetBoneTransform(HumanBodyBones.LeftFoot);
                    a.TickMovement(.2f, .22f, false);
                    b.TickMovement(.8f, .22f, false);
                    check(Vector3.Distance(footA.position, footB.position) < .002f, "Equal travel produces equal gait regardless of frame time");
                    Vector3 stopped = footA.position;
                    a.TickMovement(.8f, 0, false);
                    check(Vector3.Distance(stopped, footA.position) < .002f, "Stopped feet do not walk in place");
                    a.TickMovement(.2f, .22f, false);
                    check(Vector3.Distance(stopped, footA.position) > .005f, "Travelling advances the walking pose");
                    a.TickMovement(.2f, 0, true);
                    a.TickMovement(.2f, .22f, false);
                    check(float.IsFinite(footA.position.y), "Attack-to-walk transition remains valid");
                    root.transform.rotation = Quaternion.identity;
                    TurnZombie(root.transform, Vector3.right, .1f);
                    check(Mathf.Abs(Quaternion.Angle(Quaternion.identity, root.transform.rotation) - 18) < .01f, "Turns are limited to 180 degrees per second");
                    TurnZombie(root.transform, Vector3.right, 1);
                    check(Vector3.Dot(root.transform.forward, Vector3.right) > .99f, "Facing follows movement direction");
                }
                finally { DestroyImmediate(root); }
            }
            return "ZOMBIE MOVEMENT PASS: distance-driven gait, stationary feet, animated travel, attack transition, smooth route-facing turns on both humanoid rigs.";
        }
    }
}
#endif
