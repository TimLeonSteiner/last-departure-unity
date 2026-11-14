using UnityEngine;
namespace YardSurvivalPrototype
{
    public enum ZombieKind { Shambler, Limper, Twitcher, Brute }
    public sealed class ZombieArchetype : MonoBehaviour
    {
        public ZombieKind kind;
        public int Health => kind == ZombieKind.Brute ? 9 : kind == ZombieKind.Twitcher ? 2 : 3;
        public float Stride => kind == ZombieKind.Brute ? 1.25f : kind == ZombieKind.Limper ? .72f : kind == ZombieKind.Twitcher ? .82f : 1;
        public int BarricadeDamage => kind == ZombieKind.Brute ? 30 : 18;
        public float Speed(float time, float phase)
        {
            switch (kind)
            {
                case ZombieKind.Limper: return .72f * Mathf.Lerp(.45f, 1.25f, .5f + .5f * Mathf.Sin(time * 4 + phase));
                case ZombieKind.Twitcher: return Mathf.Sin(time * 2.6f + phase) > .3f ? 1.8f : .55f;
                case ZombieKind.Brute: return .72f;
                default: return .9f + .12f * Mathf.Sin(time * 1.8f + phase);
            }
        }
    }
    public sealed partial class ImportedZombie
    {
        ZombieArchetype archetype;
        Transform gaitChest, gaitHead;
        float gaitClock;
        public float ArchetypeSpeed(float time, float phase) => archetype != null ? archetype.Speed(time, phase) : 1;
        public int ArchetypeHealth => archetype != null ? archetype.Health : 3;
        public int BarricadeDamage => archetype != null ? archetype.BarricadeDamage : 18;
        void SetupArchetype()
        {
            archetype = GetComponent<ZombieArchetype>();
            if (archetype == null) return;
            if (animationRig != null && animationRig.isHuman)
            {
                gaitChest = animationRig.GetBoneTransform(HumanBodyBones.Spine);
                gaitHead = animationRig.GetBoneTransform(HumanBodyBones.Head);
            }
            else { gaitChest = FindBone("Body"); gaitHead = FindBone("Head"); }
        }
        void ApplyArchetypePose(bool attacking)
        {
            if (archetype == null || dying || IsCollapsing) return;
            var frame = transform.parent != null ? transform.parent : transform;
            float sway = Mathf.Sin(gaitClock * 4.5f), weight = attacking ? .25f : 1;
            float lean = 0, roll = 0, twitch = 0;
            switch (archetype.kind)
            {
                case ZombieKind.Shambler: lean = 8; roll = sway * 3; break;
                case ZombieKind.Limper: lean = 12; roll = -9 + sway * 4; break;
                case ZombieKind.Twitcher: lean = 19; roll = sway * 6; twitch = Mathf.Pow(Mathf.Max(0, Mathf.Sin(gaitClock * 13)), 8) * 22; break;
                case ZombieKind.Brute: lean = 5; roll = sway * 5; break;
            }
            if (gaitChest != null) gaitChest.rotation = Quaternion.AngleAxis(lean * weight, frame.right) * Quaternion.AngleAxis(roll * weight, frame.forward) * gaitChest.rotation;
            if (gaitHead != null) gaitHead.rotation = Quaternion.AngleAxis(twitch * weight, frame.up) * gaitHead.rotation;
        }
    }
}
