using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class ImportedZombie
    {
        Transform[] blendBones;
        Vector3[] blendPositions;
        Quaternion[] blendRotations;
        float transitionRemaining,hitRemaining,hitSide;
        Transform reactionBone;
        float hitDuration,hitStrength,hitForward;
        int reactionRegion;
        Vector3 blendRootPosition;
        Quaternion blendRootRotation;
        public void ConfigureMotion(int seed){locomotionTime=(seed*0.6180339f)%1.3f;gaitClock=(seed*.754877f)%6;}
        void CaptureMotionTransition()
        {
            blendRootPosition=transform.localPosition;blendRootRotation=transform.localRotation;
            if(blendBones==null)
            {
                var unique=new HashSet<Transform>();
                foreach(var skin in parts)foreach(var bone in skin.bones)if(bone!=null)unique.Add(bone);
                blendBones=new Transform[unique.Count];unique.CopyTo(blendBones);
                blendPositions=new Vector3[blendBones.Length];blendRotations=new Quaternion[blendBones.Length];
            }
            for(int i=0;i<blendBones.Length;i++)
            {blendPositions[i]=blendBones[i].localPosition;blendRotations[i]=blendBones[i].localRotation;}
            transitionRemaining=.14f;
        }
        void BlendMotionTransition(float dt)
        {
            if(transitionRemaining<=0||blendBones==null)return;
            transitionRemaining=Mathf.Max(0,transitionRemaining-dt);
            float blend=Mathf.SmoothStep(0,1,1-transitionRemaining/.14f);
            transform.localPosition=Vector3.Lerp(blendRootPosition,transform.localPosition,blend);
            transform.localRotation=Quaternion.Slerp(blendRootRotation,transform.localRotation,blend);
            for(int i=0;i<blendBones.Length;i++)
            {
                var bone=blendBones[i];if(bone==null)continue;
                bone.localPosition=Vector3.Lerp(blendPositions[i],bone.localPosition,blend);
                bone.localRotation=Quaternion.Slerp(blendRotations[i],bone.localRotation,blend);
            }
        }
        public void ReactToHit(Vector3 direction,int region,int damage=1)
        {
            if(dying)return;
            var frame=transform.parent!=null?transform.parent:transform;
            direction=direction.sqrMagnitude>.001f?direction.normalized:frame.forward;
            hitStrength=Mathf.Lerp(.65f,1.65f,Mathf.InverseLerp(1,8,damage));
            hitDuration=damage>=3?.34f:.22f;hitRemaining=hitDuration;reactionRegion=region;
            hitSide=Mathf.Clamp(Vector3.Dot(frame.right,direction),-1,1);
            hitForward=Mathf.Clamp(Vector3.Dot(frame.forward,direction),-1,1);
            if(animationRig!=null&&animationRig.isHuman)
                reactionBone=animationRig.GetBoneTransform(region==0?HumanBodyBones.Head:HumanBodyBones.Chest);
            else reactionBone=FindBone("Body");
            if(reactionBone==null&&animationRig!=null&&animationRig.isHuman)reactionBone=animationRig.GetBoneTransform(HumanBodyBones.Spine);
        }
        void ApplyHitReaction(float dt)
        {
            if(hitRemaining<=0||reactionBone==null)return;
            hitRemaining=Mathf.Max(0,hitRemaining-dt);
            float progress=1-hitRemaining/hitDuration;
            float weight=(progress<.2f?Mathf.Sin(progress/.2f*Mathf.PI*.5f):1-Mathf.SmoothStep(0,1,(progress-.2f)/.8f))*hitStrength;
            var frame=transform.parent!=null?transform.parent:transform;
            float twist=reactionRegion==1?-18:reactionRegion==2?18:hitSide*8;
            float pitch=(reactionRegion==0?24:12)*hitForward;
            // Rotate in the actor's frame so differently oriented imported bones
            // react consistently. Shoulder hits twist the surviving upper body.
            reactionBone.rotation=Quaternion.AngleAxis(twist*weight,frame.up)*Quaternion.AngleAxis(pitch*weight,frame.right)*Quaternion.AngleAxis(-hitSide*12*weight,frame.forward)*reactionBone.rotation;
        }
    }
}
