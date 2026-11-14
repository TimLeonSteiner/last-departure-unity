using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class ImportedZombie
    {
        public AnimationClip idleClip;
        public AnimationClip encounterPoseClip;
        float idleTime;
        bool wasIdle;

        void TickIdlePose(float time)
        {
            SamplePose(idleClip, time % idleClip.length);
            AddArchetypeDetails();
            AnimateCreepyFace(time);
            transform.localPosition = home;
            transform.localRotation = homeRotation;
            transform.localScale = homeScale;
            ApplyArchetypePose(false);
        }
    }
}
