using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace YardSurvivalPrototype
{
    public sealed partial class ImportedZombie
    {
        public bool preserveSourceAppearance;
        public Animator animationRig;
        public Transform[] hitBones, severJoints;
        public int[] hitBoneRegions;
        PlayableGraph poseGraph;
        AnimationClipPlayable posePlayable;
        AnimationPlayableOutput poseOutput;
        AnimationClip currentPose;

        public void SamplePose(AnimationClip clip, float time)
        {
            if (animationRig == null) { ClipPoseSampler.Sample(gameObject, clip, time); return; }
            if (!poseGraph.IsValid())
            {
                poseGraph = PlayableGraph.Create("Zombie pose");
                poseGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                animationRig.applyRootMotion = false;
                animationRig.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                poseOutput = AnimationPlayableOutput.Create(poseGraph, "Body", animationRig);
            }
            if (currentPose != clip || !posePlayable.IsValid())
            {
                if (posePlayable.IsValid()) poseGraph.DestroyPlayable(posePlayable);
                posePlayable = AnimationClipPlayable.Create(poseGraph, clip);
                posePlayable.SetApplyFootIK(clip.humanMotion);
                poseOutput.SetSourcePlayable(posePlayable);
                currentPose = clip;
                poseGraph.Play();
            }
            posePlayable.SetTime(time);
            poseGraph.Evaluate(0);
        }

        Mesh BakeRegion(int part)
        {
            var skin = parts[part];
            var mesh = new Mesh { name = "Detached zombie part" };
            skin.BakeMesh(mesh, false);
            mesh.RecalculateBounds();
            // BakeMesh(false) already carries the rendered scale. The detached
            // object uses unit scale to avoid shrinking the geometry twice.
            return mesh;
        }
        void ReleasePoseGraph() { if (poseGraph.IsValid()) poseGraph.Destroy(); }
    }
}
