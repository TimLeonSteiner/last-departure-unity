using System.Collections.Generic;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed class ZombieDeathContact:MonoBehaviour
    {public ImportedZombie owner;void OnCollisionEnter(Collision collision){if(owner!=null)owner.RagdollContact(collision);}}
    public sealed partial class ImportedZombie
    {
        readonly List<RagdollLink> ragdollLinks=new List<RagdollLink>();
        sealed class RagdollLink { public Transform bone; public Rigidbody body; }
        public int RagdollBodyCount=>ragdollLinks.Count;
        bool ragdollLanded;
        bool ragdollSettled;
        public void SyncRagdollPose()=>LateUpdate();
        public void RagdollContact(Collision collision)
        {
            if (ragdollLanded || collision.gameObject.layer == 28 || collision.contactCount == 0) return;
            float speed = collision.relativeVelocity.magnitude;
            // Feet may touch immediately on handoff. Save the thud for a real impact.
            if (Time.time - deathAt < .12f || speed < 1.1f) return;
            ragdollLanded = true;
            if (audioOwner != null) audioOwner.PlayZombieSound("Body_Fall", collision.GetContact(0).point,
                Mathf.Lerp(.6f, 1f, Mathf.InverseLerp(1.1f, 4, speed)));
        }
        Transform DeathBone(HumanBodyBones bone)
        {
            if(animationRig!=null && animationRig.isHuman)return animationRig.GetBoneTransform(bone);
            string joint=bone switch {
                HumanBodyBones.LeftUpperArm=>"LeftArm",HumanBodyBones.LeftLowerArm=>"LeftForeArm",
                HumanBodyBones.RightUpperArm=>"RightArm",HumanBodyBones.RightLowerArm=>"RightForeArm",
                HumanBodyBones.LeftUpperLeg=>"LeftUpLeg",HumanBodyBones.LeftLowerLeg=>"LeftLeg",
                HumanBodyBones.RightUpperLeg=>"RightUpLeg",HumanBodyBones.RightLowerLeg=>"RightLeg",
                HumanBodyBones.Chest=>"Spine1",_=>bone.ToString()
            };
            return FindBone("mixamorig:"+joint) ?? FindBone("FatZombie_"+(joint=="Spine1"?"Spine2":joint)) ?? FindBone(joint);
        }
        void BeginRagdoll(Vector3 hitDirection, Vector3 hitPoint, float impactSpeed)
        {
            if(DeathBone(HumanBodyBones.Hips)==null){Debug.LogError("Death ragdoll cannot find hips: "+name);return;}
            var bones=new[]{HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Head,
                HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,
                HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg};
            var ends=new[]{HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Neck,HumanBodyBones.LastBone,
                HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,
                HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};
            var parents=new[]{-1,0,1,2,2,4,2,6,0,8,0,10};
            var regions=new[]{5,5,5,0,1,1,2,2,3,3,4,4};
            var bodies=new Rigidbody[bones.Length];var colliders=new List<Collider>();
            var torsoMesh=BakeRegion(5);float torsoRadius=Mathf.Clamp(torsoMesh.bounds.size.z*.45f,.17f,.34f);Destroy(torsoMesh);
            var container=new GameObject("Articulated death physics");container.transform.SetParent(transform.parent,false);
            container.transform.position=Vector3.zero;container.transform.rotation=Quaternion.identity;container.transform.localScale=Vector3.one;
            for(int i=0;i<bones.Length;i++)
            {
                if(parts[regions[i]]==null||!parts[regions[i]].enabled)continue;
                var bone=DeathBone(bones[i]);if(bone==null)continue;
                var end=ends[i]==HumanBodyBones.LastBone?null:DeathBone(ends[i]);
                Vector3 next=end!=null?end.position:bone.position+transform.up*.20f;
                if(Vector3.Distance(next,bone.position)<.08f)next=bone.position+transform.up*.12f;
                var obj=new GameObject(bones[i]+" physics");obj.transform.SetParent(container.transform,false);
                obj.transform.position=bone.position;obj.transform.rotation=bone.rotation;obj.layer=28;
                var rb=obj.AddComponent<Rigidbody>();rb.mass=i==0?9:i<3?6:i==3?2:2.5f;
                rb.linearDamping=.65f;rb.angularDamping=2;rb.maxAngularVelocity=8;rb.maxDepenetrationVelocity=2;
                rb.solverIterations=10;rb.solverVelocityIterations=4;
                rb.linearVelocity=movementVelocity;
                rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.interpolation=RigidbodyInterpolation.Interpolate;
                var shape=obj.AddComponent<CapsuleCollider>();Vector3 segment=obj.transform.InverseTransformDirection(next-bone.position);
                int axis=Mathf.Abs(segment.x)>Mathf.Abs(segment.y)?0:1;if(Mathf.Abs(segment.z)>Mathf.Abs(segment[axis]))axis=2;
                shape.direction=axis;shape.center=obj.transform.InverseTransformPoint((next+bone.position)*.5f);
                shape.radius=i==0?Mathf.Max(.15f,torsoRadius*.8f):i<3?torsoRadius:i==3?.12f:i>=8?.075f:.06f;
                shape.height=Mathf.Max(shape.radius*2,Vector3.Distance(next,bone.position)+shape.radius);
                obj.AddComponent<ZombieDeathContact>().owner=this;
                colliders.Add(shape);bodies[i]=rb;ragdollLinks.Add(new RagdollLink{bone=bone,body=rb});
                int parent=parents[i];while(parent>=0&&bodies[parent]==null)parent=parents[parent];
                if(parent>=0)
                {
                    var joint=obj.AddComponent<CharacterJoint>();joint.connectedBody=bodies[parent];joint.anchor=Vector3.zero;
                    joint.enableCollision=false;joint.enablePreprocessing=false;joint.enableProjection=true;
                    joint.projectionDistance=.08f;joint.projectionAngle=15;
                    joint.lowTwistLimit=new SoftJointLimit{limit=-25};joint.highTwistLimit=new SoftJointLimit{limit=25};
                    joint.swing1Limit=new SoftJointLimit{limit=i>=4?45:25};joint.swing2Limit=new SoftJointLimit{limit=i>=4?30:20};
                }
            }
            for(int i=0;i<colliders.Count;i++)for(int j=i+1;j<colliders.Count;j++)Physics.IgnoreCollision(colliders[i],colliders[j]);
            ReleasePoseGraph();animationRig.enabled=false;
            // Distribute a restrained impulse, then add a local reaction at the hit.
            // Off-centre hits produce a turn instead of the same forward fall each time.
            Vector3 impulse = hitDirection.normalized * Mathf.Clamp(impactSpeed, 0, 2.4f);
            Rigidbody nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (var link in ragdollLinks)
            {
                link.body.AddForce(impulse * .45f, ForceMode.VelocityChange);
                float distance = (link.body.worldCenterOfMass - hitPoint).sqrMagnitude;
                if (distance < nearestDistance) { nearestDistance = distance; nearest = link.body; }
            }
            if (nearest != null)
            {
                Vector3 contact = nearest.worldCenterOfMass + Vector3.ClampMagnitude(hitPoint - nearest.worldCenterOfMass, .25f);
                nearest.AddForceAtPosition(impulse * .55f, contact, ForceMode.VelocityChange);
            }
            // Non-hit deaths (including replicated removals) still buckle naturally.
            if (impulse.sqrMagnitude < .001f && bodies[0] != null)
                bodies[0].AddForce(transform.forward * .35f, ForceMode.VelocityChange);
        }
        void FixedUpdate()
        {
            if(!dying || ragdollSettled)return;
            // Keep the fallen pose without simulating twelve bodies for the corpse's entire lifetime.
            if (Time.time - deathAt > 4f)
            {
                foreach(var link in ragdollLinks) if(link.body!=null)
                { link.body.collisionDetectionMode=CollisionDetectionMode.Discrete;link.body.isKinematic=true; }
                ragdollSettled=true;return;
            }
            foreach(var link in ragdollLinks)if(link.body!=null){link.body.linearVelocity=Vector3.ClampMagnitude(link.body.linearVelocity,6);link.body.angularVelocity=Vector3.ClampMagnitude(link.body.angularVelocity,8);}
        }
        void LateUpdate()
        {
            if(!dying)return;
            for (int i = 0; i < ragdollLinks.Count; i++)
            {
                var link = ragdollLinks[i];
                if (link.bone == null || link.body == null) continue;
                // Only the root translates. Preserve authored limb lengths even when the
                // physics solver projects a shoulder joint during a heavy landing.
                if (i == 0) link.bone.position = link.body.position;
                link.bone.rotation = link.body.rotation;
            }
        }
    }
}
