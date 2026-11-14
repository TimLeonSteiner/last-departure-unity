using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace YardSurvivalPrototype
{
    // Evaluate imported generic clips through an Animator in browser players too.
    public sealed class ClipPoseSampler : MonoBehaviour
    {
        PlayableGraph graph;
        AnimationClipPlayable playable;
        AnimationPlayableOutput output;
        AnimationClip current;
        public static void Sample(GameObject target, AnimationClip clip, float time)
        {
            if (target == null || clip == null) return;
            if (clip.legacy) { clip.SampleAnimation(target, time); return; }
            var sampler = target.GetComponent<ClipPoseSampler>() ?? target.AddComponent<ClipPoseSampler>();
            sampler.Evaluate(clip, time);
        }
        void Evaluate(AnimationClip clip, float time)
        {
            if (!graph.IsValid())
            {
                var animator = GetComponent<Animator>();
                if (animator == null) animator = gameObject.AddComponent<Animator>();
                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("Imported clip pose");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                output = AnimationPlayableOutput.Create(graph, "Pose", animator);
            }
            if (current != clip)
            {
                if (playable.IsValid()) graph.DestroyPlayable(playable);
                playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                output.SetSourcePlayable(playable);
                current = clip;
                graph.Play();
            }
            playable.SetTime(time);
            graph.Evaluate(0);
        }
        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }
    }
}
