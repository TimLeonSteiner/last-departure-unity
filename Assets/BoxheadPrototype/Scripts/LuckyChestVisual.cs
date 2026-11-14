using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed class LuckyChestVisual : MonoBehaviour
    {
        public GameObject model;
        public AnimationClip opening;
        public float startTime,endTime;
        public void Pose(float open)
        {if(opening!=null)ClipPoseSampler.Sample(model,opening,Mathf.Lerp(startTime,endTime,Mathf.SmoothStep(0,1,open)));}
    }
}
