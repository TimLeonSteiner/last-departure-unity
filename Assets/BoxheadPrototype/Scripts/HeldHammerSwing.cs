using System.Linq;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed class HeldHammerSwing : MonoBehaviour
    {
        AnimatedWeaponRig rig;
        Transform arm,elbow,palm;
        Quaternion armRest,elbowRest,palmRest;
        static void Sample(GameObject target,AnimationClip clip,Vector2 range,float progress)
        {
            if(clip==null)return;
            float at=range.y>range.x?Mathf.Lerp(range.x,range.y,progress)/clip.frameRate:Mathf.Clamp01(progress)*clip.length;
            ClipPoseSampler.Sample(target, clip,Mathf.Clamp(at,0,clip.length));
        }
        public void Initialize(AnimatedWeaponRig source)
        {
            rig=source;
            Sample(rig.model,rig.idle!=null?rig.idle:rig.fire,rig.idle!=null?rig.idleFrames:rig.fireFrames,0);
            Transform Find(string name)=>rig.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
            arm=Find("R_arm");elbow=Find("R_elbow");palm=Find("R_palm");
            armRest=arm.localRotation;elbowRest=elbow.localRotation;palmRest=palm.localRotation;
        }
        static float Stroke(float age,float windup,float impact)
        {
            if(age<.08f)return Mathf.Lerp(0,windup,Mathf.SmoothStep(0,1,age/.08f));
            if(age<.18f)return Mathf.Lerp(windup,impact,Mathf.SmoothStep(0,1,(age-.08f)/.1f));
            if(age<.23f)return Mathf.Lerp(impact,impact*.75f,(age-.18f)/.05f);
            return Mathf.Lerp(impact*.75f,0,Mathf.SmoothStep(0,1,(age-.23f)/.32f));
        }
        public void Pose(float age)
        {
            // Start from the gripped idle pose, not the knife stab clip.
            Sample(rig.model,rig.idle!=null?rig.idle:rig.fire,rig.idle!=null?rig.idleFrames:rig.fireFrames,0);
            float upper=Stroke(age,-8,10),lower=Stroke(age,-12,16),wrist=Stroke(age,-4,4);
            arm.localRotation=Quaternion.AngleAxis(upper,arm.parent.InverseTransformDirection(rig.transform.right))*armRest;
            elbow.localRotation=Quaternion.AngleAxis(lower,elbow.parent.InverseTransformDirection(rig.transform.right))*elbowRest;
            palm.localRotation=Quaternion.AngleAxis(wrist,palm.parent.InverseTransformDirection(rig.transform.right))*palmRest;
        }
    }
    public sealed partial class YardSurvival
    {
        HeldHammerSwing heldHammerSwing;
        void UpdateHeldHammer()
        {
            if(hammerRig==null||!hammerRig.gameObject.activeSelf)return;
            if(heldHammerSwing==null||heldHammerSwing.gameObject!=hammerRig.gameObject)
            {heldHammerSwing=hammerRig.gameObject.AddComponent<HeldHammerSwing>();heldHammerSwing.Initialize(hammerRig);}
            heldHammerSwing.Pose(hammerAge);
        }
    }
}
