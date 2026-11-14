using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed class AnimatedWeaponRig : MonoBehaviour
    {
        public GameObject model;
        public AnimationClip fire, reload, idle, ready, cycle, reloadStart, reloadEnd;
        public Vector2 fireFrames, reloadFrames, idleFrames, readyFrames, cycleFrames;
        public float fireDuration=.25f;
        [Range(0,1)] public float aimFireMotion=1;
        public Vector3 hipPosition = new Vector3(.18f, -.18f, .48f);
        public Vector3 aimPosition = new Vector3(0, -.075f, .4f);
        public Vector3 rearSight,frontSight,aimEuler;
        public bool calibratedSights;
        public Vector3 muzzle = new Vector3(0, .08f, .7f);
        public Transform muzzleAnchor;
        public Vector3 muzzleAnchorOffset;
        public bool shellReload;
        [System.NonSerialized] public int reloadShellCount=6;
        float shotAge = 100, readyAge, transitionAge;
        int poseState=-1;
        Transform[] poseBones;
        Vector3[] fromPositions,hipPositions;
        Quaternion[] fromRotations,hipRotations;
        void PrepareBones(){if(poseBones!=null)return;poseBones=model.GetComponentsInChildren<Transform>(true);fromPositions=new Vector3[poseBones.Length];hipPositions=new Vector3[poseBones.Length];fromRotations=new Quaternion[poseBones.Length];hipRotations=new Quaternion[poseBones.Length];}
        public bool HasArms => model != null && model.GetComponentsInChildren<SkinnedMeshRenderer>().Length > 0;
        public void Shot() { shotAge = 0; }
        static void Sample(GameObject target, AnimationClip clip, Vector2 frames, float progress)
        {
            if (clip == null) return;
            float at = frames.y > frames.x ? Mathf.Lerp(frames.x, frames.y, Mathf.Clamp01(progress)) / clip.frameRate : Mathf.Clamp01(progress) * clip.length;
            ClipPoseSampler.Sample(target, clip, Mathf.Clamp(at, 0, clip.length));
        }
        readonly Transform[] fittedBones=new Transform[6];
        readonly Vector3[] fittedPositions=new Vector3[6];
        readonly Quaternion[] fittedRotations=new Quaternion[6];
        bool shouldersFitted;
#if UNITY_EDITOR
        public float ReviewArmLengthError {get;private set;}
#endif
        void RestoreAimShoulders()
        {
            if(!shouldersFitted)return;
            for(int i=0;i<6;i++)if(fittedBones[i]!=null){fittedBones[i].localPosition=fittedPositions[i];fittedBones[i].localRotation=fittedRotations[i];}
            shouldersFitted=false;
        }
        public void Pose(float progress, bool reloading, float dt, float aim=0)
        {
            RestoreAimShoulders();
            shotAge += dt; readyAge += dt;PrepareBones();
            int state=reloading?4:shotAge<fireDuration?1:cycle!=null&&shotAge<fireDuration+.65f?2:readyAge<.5f&&ready!=null?3:0;
            if(state!=poseState){for(int i=0;i<poseBones.Length;i++){fromPositions[i]=poseBones[i].localPosition;fromRotations[i]=poseBones[i].localRotation;}transitionAge=poseState<0?1:0;poseState=state;}
            transitionAge+=dt;
            if (reloading && shellReload)
            {
                if (progress < .12f) Sample(model, reloadStart, Vector2.zero, progress / .12f);
                else if (progress > .85f) Sample(model, reloadEnd, Vector2.zero, (progress - .85f) / .15f);
                else Sample(model, reload, Vector2.zero, ((progress - .12f) / .73f * Mathf.Max(1,reloadShellCount)) % 1);
            }
            else if (reloading) Sample(model, reload, reloadFrames, progress);
            else if (shotAge < fireDuration) Sample(model, fire, fireFrames, shotAge / fireDuration);
            else if (cycle != null && shotAge < fireDuration+.65f) Sample(model, cycle, cycleFrames, (shotAge - fireDuration) / .65f);
            else if (readyAge < .5f && ready != null) Sample(model, ready, readyFrames, readyAge / .5f);
            else
            {
                Sample(model,idle!=null?idle:fire,idle!=null?idleFrames:fireFrames,idle!=null?Mathf.PingPong(Time.time*.3f,1):0);
                if(aim>0&&idle!=null)
                {
                    for(int i=0;i<poseBones.Length;i++){hipPositions[i]=poseBones[i].localPosition;hipRotations[i]=poseBones[i].localRotation;}
                    Sample(model,idle,idleFrames,0);
                    for(int i=0;i<poseBones.Length;i++){poseBones[i].localPosition=Vector3.Lerp(hipPositions[i],poseBones[i].localPosition,aim);poseBones[i].localRotation=Quaternion.Slerp(hipRotations[i],poseBones[i].localRotation,aim);}
                }
            }
            if(!reloading&&aim>0&&aimFireMotion<1&&(state==1||state==2)&&idle!=null)
            {
                for(int i=0;i<poseBones.Length;i++){hipPositions[i]=poseBones[i].localPosition;hipRotations[i]=poseBones[i].localRotation;}
                Sample(model,idle,idleFrames,0);
                float motion=Mathf.Lerp(1,aimFireMotion,aim);
                for(int i=0;i<poseBones.Length;i++){poseBones[i].localPosition=Vector3.Lerp(poseBones[i].localPosition,hipPositions[i],motion);poseBones[i].localRotation=Quaternion.Slerp(poseBones[i].localRotation,hipRotations[i],motion);}
            }
            float blend=Mathf.SmoothStep(0,1,transitionAge/(reloading?.12f:.08f));
            if(blend<1)for(int i=0;i<poseBones.Length;i++){poseBones[i].localPosition=Vector3.Lerp(fromPositions[i],poseBones[i].localPosition,blend);poseBones[i].localRotation=Quaternion.Slerp(fromRotations[i],poseBones[i].localRotation,blend);}
        }
        public void FitAimShoulders(Transform camera,float amount,int weaponId)
        {
#if UNITY_EDITOR
            ReviewArmLengthError=0;
#endif
            RestoreAimShoulders();
            // Keep the authored grip and limb lengths. Independent shoulder/elbow
            // offsets stretched the forearms and exposed sleeve ends during ADS.
            if(amount<=0||camera==null||(weaponId!=0&&weaponId!=2&&weaponId!=4&&weaponId!=7&&weaponId!=9&&weaponId!=15))return;
            PrepareBones();
            for(int side=0;side<2;side++)
            {
                string upperName=weaponId==2?(side==0?"arm.L":"arm.R"):(side==0?"L_arm":"R_arm");
                string lowerName=weaponId==2?(side==0?"forearm.L":"forearm.R"):(side==0?"L_elbow":"R_elbow");
                string handName=weaponId==2?(side==0?"hand.L":"hand.R"):(side==0?"L_wrist":"R_wrist");
                Transform upper=null,lower=null,hand=null;
                foreach(var bone in poseBones){if(bone.name==upperName)upper=bone;if(bone.name==lowerName)lower=bone;if(bone.name==handName)hand=bone;}
                if(upper==null||lower==null||hand==null)continue;
                int index=side*3;fittedBones[index]=upper;fittedBones[index+1]=lower;fittedBones[index+2]=hand;
                for(int i=index;i<index+3;i++){fittedPositions[i]=fittedBones[i].localPosition;fittedRotations[i]=fittedBones[i].localRotation;}
                Vector3 wrist=hand.position,oldElbow=lower.position,upperDirection=oldElbow-upper.position;
                Quaternion wristRotation=hand.rotation,lowerRotation=lower.rotation;
                float sign=side==0?-1:1;
                float upperLength=upperDirection.magnitude,lowerLength=Vector3.Distance(oldElbow,wrist);
                if(upperLength<.001f||lowerLength<.001f)continue;
                Vector3 shoulder=Vector3.Lerp(upper.position,camera.TransformPoint(new Vector3(sign*.32f,-.42f,-.12f)),amount);
                Vector3 reach=shoulder-wrist;
                float distance=Mathf.Clamp(reach.magnitude,Mathf.Abs(upperLength-lowerLength)+.001f,(upperLength+lowerLength)*.995f);
                shoulder=wrist+reach.normalized*distance;
                Vector3 axis=(wrist-shoulder)/distance;
                Vector3 pole=Vector3.Lerp(oldElbow,camera.TransformPoint(new Vector3(sign*.45f,-.45f,.12f)),amount);
                Vector3 bend=Vector3.ProjectOnPlane(pole-shoulder,axis).normalized;
                if(bend.sqrMagnitude<.5f)bend=Vector3.ProjectOnPlane(-camera.up,axis).normalized;
                float along=(upperLength*upperLength-lowerLength*lowerLength+distance*distance)/(2*distance);
                Vector3 elbow=shoulder+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,upperLength*upperLength-along*along));
                upper.position=shoulder;
                if(upperDirection.sqrMagnitude>.0001f)upper.rotation=Quaternion.FromToRotation(upperDirection,elbow-shoulder)*upper.rotation;
                lower.position=elbow;
                if((wrist-oldElbow).sqrMagnitude>.0001f)lower.rotation=Quaternion.FromToRotation(wrist-oldElbow,wrist-elbow)*lowerRotation;
                // Anchor the animated grip while moving sleeves below the sight line.
                hand.SetPositionAndRotation(wrist,wristRotation);
#if UNITY_EDITOR
                ReviewArmLengthError=Mathf.Max(ReviewArmLengthError,Mathf.Abs(Vector3.Distance(upper.position,lower.position)-upperLength),Mathf.Abs(Vector3.Distance(lower.position,hand.position)-lowerLength));
#endif
                shouldersFitted=true;
            }
        }
        public void GroundPose()
        {
            Pose(0, false, 0);
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (skin.name.ToLowerInvariant().Contains("arm")) skin.enabled = false;
        }
    }
}
