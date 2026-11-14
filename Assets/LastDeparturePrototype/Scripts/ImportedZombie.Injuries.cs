using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class ImportedZombie
    {
        const float CollapseDuration=.7f;
        int legInjuryMask;
        public int LegInjuryMask => legInjuryMask;
        bool LegInjured(int region) => (legInjuryMask & (1 << (region - 3))) != 0 || !parts[region].enabled;
        public void InjureLeg(int region)
        {
            if(IsCharger || dying || (region != 3 && region != 4) || LegInjured(region))return;
            CaptureMotionTransition();
            legInjuryMask |= 1 << (region - 3);
            if(InjuredLegs == 2)BeginCollapse();
        }
        public void SyncLegInjuries(int mask)
        {
            if((mask & 1) != 0)InjureLeg(3);
            if((mask & 2) != 0)InjureLeg(4);
        }
        float collapseRemaining,collapseHeight;
        public bool IsCollapsing=>!dying&&collapseRemaining>0;
        public float CollapseTimeRemaining=>collapseRemaining;
        public void SyncCollapseTime(float remaining){collapseRemaining=InjuredLegs==2?Mathf.Clamp(remaining,0,CollapseDuration):0;}
        void BeginCollapse()
        {
            if(animationRig==null||!animationRig.isHuman)return;
            var hips=animationRig.GetBoneTransform(HumanBodyBones.Hips);
            if(hips==null)return;
            collapseHeight=hips.position.y-(transform.parent!=null?transform.parent.position.y:0);
            collapseRemaining=CollapseDuration;
        }
        void ApplyInjuredPose(float time,bool attacking)
        {
            if(InjuredLegs==0||animationRig==null||!animationRig.isHuman)return;
            var hips=animationRig.GetBoneTransform(HumanBodyBones.Hips);
            if(hips==null)return;
            var frame=transform.parent!=null?transform.parent:transform;
            Vector3 forward=frame.forward,right=frame.right;
            float ground=frame.position.y;
            float cycle=locomotionTime*5.8f;
            if(InjuredLegs==1)
            {
                float side=!LegInjured(3)?1:-1;
                // A short supported step with the weight shifted over the remaining foot.
                hips.rotation=Quaternion.AngleAxis(11, right)*Quaternion.AngleAxis(side*(8+3*Mathf.Sin(cycle)),forward)*hips.rotation;
                var foot=animationRig.GetBoneTransform(!LegInjured(3)?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                if(foot!=null)transform.position+=Vector3.up*(ground+.09f-foot.position.y);
            }
            else
            {
                // Rotate at the pelvis, then place it close to the floor, rather than
                // tilting the entire standing skeleton around its original feet.
                float collapse=1-collapseRemaining/CollapseDuration;
                float settle=Mathf.SmoothStep(0,1,collapse);
                float height=IsCollapsing?Mathf.Lerp(collapseHeight,.39f,settle)-.06f*Mathf.Sin(collapse*Mathf.PI):.39f+.025f*Mathf.Sin(cycle*2);
                hips.rotation=Quaternion.AngleAxis(Mathf.Lerp(12,76,settle),right)*Quaternion.AngleAxis(4*Mathf.Sin(cycle)*settle,forward)*hips.rotation;
                transform.position+=Vector3.up*(ground+height-hips.position.y);
                var head=animationRig.GetBoneTransform(HumanBodyBones.Head);
                if(head!=null)head.rotation=Quaternion.AngleAxis(-35,right)*head.rotation;
                for(int side=0;side<2;side++)
                {
                    if(!parts[side+1].enabled)continue;
                    float phase=cycle+side*Mathf.PI;
                    float reach=attacking?.83f:.55f+.18f*Mathf.Cos(phase);
                    float lift=attacking?.18f:.07f+.1f*Mathf.Max(0,Mathf.Sin(phase));
                    Vector3 target=hips.position+forward*reach+right*(side==0?-.29f:.29f);
                    target.y=ground+lift;
                    if(IsCollapsing)
                    {
                        var hand=animationRig.GetBoneTransform(side==0?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                        if(hand!=null)target=Vector3.Lerp(hand.position,target,Mathf.SmoothStep(0,1,Mathf.Clamp01(collapse/.6f)));
                    }
                    PoseCrawlArm(side==0,target,forward+right*(side==0?-.8f:.8f));
                }
            }
        }
        void PoseCrawlArm(bool left,Vector3 target,Vector3 bendDirection)
        {
            var upper=animationRig.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
            var lower=animationRig.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            var hand=animationRig.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            if(upper==null||lower==null||hand==null)return;
            float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,hand.position);
            if(a<.001f||b<.001f)return;
            Vector3 direction=target-upper.position;
            float distance=Mathf.Clamp(direction.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);
            direction=direction.sqrMagnitude>.0001f?direction.normalized:Vector3.down;
            Vector3 bend=Vector3.ProjectOnPlane(bendDirection,direction).normalized;
            if(bend.sqrMagnitude<.01f)bend=Vector3.Cross(direction,Vector3.up).normalized;
            float along=(a*a-b*b+distance*distance)/(2*distance);
            Vector3 elbow=upper.position+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
        }
    }
}
