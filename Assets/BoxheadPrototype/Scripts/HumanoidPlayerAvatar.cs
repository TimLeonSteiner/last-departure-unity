using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace YardSurvivalPrototype
{
    public sealed class HumanoidPlayerAvatar:MonoBehaviour
    {
        public Animator rig;
        public AnimationClip idle,walk,run,death;
        PlayableGraph graph;
        AnimationClipPlayable playable;
        AnimationPlayableOutput output;
        AnimationClip current;
        float motionTime,idleTime,deathTime;
        Transform heldWeapon;
        Transform weaponGrip,weaponSupport;
        int heldWeaponId=-1;
        bool authoredWeaponHidden;
        public int VisibleWeaponId => heldWeaponId;
        public void SetWeapon(GameObject prefab,int id,float pitch)
        {
            if(rig==null)return;
            if(id<0){if(heldWeapon!=null){heldWeapon.gameObject.SetActive(false);Destroy(heldWeapon.gameObject);}heldWeapon=null;heldWeaponId=-1;return;}
            if(!authoredWeaponHidden)
            {
                // The source characters include an SMG mesh independent of gameplay equipment.
                foreach(var renderer in rig.GetComponentsInChildren<Renderer>(true))
                    foreach(var material in renderer.sharedMaterials)
                        if(material!=null&&material.name.ToLowerInvariant().Contains("smg")){renderer.enabled=false;break;}
                authoredWeaponHidden=true;
            }
            if(heldWeaponId!=id||heldWeapon==null)
            {
                if(heldWeapon!=null){heldWeapon.gameObject.SetActive(false);Destroy(heldWeapon.gameObject);}
                heldWeaponId=id;
                if(prefab==null)return;
                heldWeapon=Instantiate(prefab,transform).transform;heldWeapon.name="Equipped partner weapon";
                foreach(var animator in heldWeapon.GetComponentsInChildren<Animator>())animator.enabled=false;
                foreach(var shape in heldWeapon.GetComponentsInChildren<Collider>())shape.enabled=false;
                var weaponRig=heldWeapon.GetComponent<AnimatedWeaponRig>();
                weaponRig?.Pose(0,false,1);weaponRig?.GroundPose();
                weaponGrip=weaponSupport=null;
                foreach(var bone in heldWeapon.GetComponentsInChildren<Transform>(true))
                {if(bone.name=="hand.R")weaponGrip=bone;if(bone.name=="hand.L")weaponSupport=bone;}
                foreach(var renderer in heldWeapon.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.gameObject.layer=0;renderer.renderingLayerMask=1;
                    if(renderer.name.ToLowerInvariant().Contains("arm"))renderer.enabled=false;
                }
            }
            var hand=rig!=null&&rig.isHuman?rig.GetBoneTransform(HumanBodyBones.RightHand):null;
            heldWeapon.rotation=transform.rotation*Quaternion.Euler(Mathf.Clamp(pitch,-65,75),0,0);
            heldWeapon.position=(hand!=null?hand.position:transform.position+Vector3.up*1.2f)-heldWeapon.forward*.12f;
            if(hand!=null&&weaponGrip!=null){heldWeapon.position+=hand.position-weaponGrip.position;OrientHand(hand,HumanBodyBones.RightMiddleProximal,weaponGrip);}
            if(weaponSupport!=null&&!WeaponCatalog.IsMelee(id))AlignSupportArm(weaponSupport);
        }
        void OrientHand(Transform hand,HumanBodyBones fingerBone,Transform grip)
        {
            var finger=rig.GetBoneTransform(fingerBone);if(finger==null)return;
            foreach(var bone in grip.GetComponentsInChildren<Transform>())
                if(bone.name.StartsWith("f_middle.01")){hand.rotation=Quaternion.FromToRotation(finger.position-hand.position,bone.position-grip.position)*hand.rotation;break;}
        }
        void AlignSupportArm(Transform grip)
        {
            Vector3 target=grip.position;
            var upper=rig.GetBoneTransform(HumanBodyBones.LeftUpperArm);var lower=rig.GetBoneTransform(HumanBodyBones.LeftLowerArm);var hand=rig.GetBoneTransform(HumanBodyBones.LeftHand);
            if(upper==null||lower==null||hand==null)return;
            float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,hand.position);
            var delta=target-upper.position;float distance=Mathf.Clamp(delta.magnitude,.001f,a+b-.001f);var forward=delta.normalized;
            var bend=Vector3.ProjectOnPlane(lower.position-upper.position,forward).normalized;
            if(bend.sqrMagnitude<.01f)bend=Vector3.ProjectOnPlane(-transform.up,forward).normalized;
            float along=(a*a+distance*distance-b*b)/(2*distance);var elbow=upper.position+forward*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
            OrientHand(hand,HumanBodyBones.LeftMiddleProximal,grip);
        }
        public void Tick(float dt,float speed,float pitch,bool dead)
        {
            motionTime+=Mathf.Min(speed,12)*dt/1.2f;idleTime+=dt;if(dead)deathTime+=dt;else deathTime=0;
            var clip=dead?death:speed>.05f?(speed>7&&run!=null?run:walk):idle;
            if(clip==null||rig==null)return;rig.enabled=true;rig.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            if(!graph.IsValid()){graph=PlayableGraph.Create("Cultist player pose");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);rig.applyRootMotion=false;output=AnimationPlayableOutput.Create(graph,"Body",rig);}
            if(current!=clip){if(playable.IsValid())graph.DestroyPlayable(playable);playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(clip.humanMotion);output.SetSourcePlayable(playable);current=clip;graph.Play();}
            playable.SetTime(dead?Mathf.Min(deathTime,clip.length-.01f):(speed>.05f?motionTime:idleTime)%clip.length);graph.Evaluate(0);
            if(!dead&&rig.isHuman){var chest=rig.GetBoneTransform(HumanBodyBones.Chest);if(chest!=null)chest.localRotation*=Quaternion.Euler(Mathf.Clamp(pitch,-45,45)*.35f,0,0);}
        }
        void OnDestroy(){if(graph.IsValid())graph.Destroy();}
    }
    public sealed partial class YardSurvival {public GameObject[] playerSkinModels;}
}
