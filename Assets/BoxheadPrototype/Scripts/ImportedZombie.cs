using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class ImportedZombie : MonoBehaviour
    {
        public SkinnedMeshRenderer[] parts;
        public AnimationClip walkClip, attackClip, deathClip, crawlClip, runClip;
        public AnimationClip[] deathClips;
        public AnimationClip ActiveDeathClip { get; private set; }
        public bool Runner { get; set; }
        public bool IsRunning => Runner && runClip != null && InjuredLegs == 0 && !IsCollapsing;
        public float RunnerSpeedMultiplier => IsRunning ? 1.8f : 1f;
        public bool IsCharger => name.StartsWith("Charger", System.StringComparison.Ordinal);
        [System.NonSerialized] public YardSurvival audioOwner;
        readonly List<Transform>[] joints = { new List<Transform>(), new List<Transform>(), new List<Transform>(), new List<Transform>(), new List<Transform>(), new List<Transform>() };
        bool dying;
        float deathAt;
        Vector3 home;
        Quaternion homeRotation;
        Vector3 homeScale;
        float locomotionTime, attackPoseTime;
        bool wasAttacking;
        Vector3 movementVelocity;
        public void TickMovement(float dt, float distance, bool attacking, float attackProgress=-1)
        {
            if (dying) return;
            gaitClock += dt;
            if(IsCollapsing){collapseRemaining=Mathf.Max(0,collapseRemaining-dt);distance=0;attacking=false;}
            bool idle = idleClip != null && !attacking && distance <= .00001f && InjuredLegs == 0 && !IsCollapsing;
            if(attacking!=wasAttacking || idle!=wasIdle)CaptureMotionTransition();
            movementVelocity = !attacking && dt > .0001f
                ? transform.forward * Mathf.Clamp(distance / dt, 0, 3) : Vector3.zero;
            if (attacking)
            {
                if (!wasAttacking) attackPoseTime = 0;
                attackPoseTime = attackProgress>=0 && attackClip!=null ? Mathf.Clamp01(attackProgress)*Mathf.Max(.01f,attackClip.length-.001f) : attackPoseTime+dt;
                Tick(attackPoseTime, true);
            }
            else if (idle)
            {
                idleTime += dt;
                TickIdlePose(idleTime);
            }
            else
            {
                // The in-place shamble covers roughly 1.1 metres per clip second.
                // Use actual travel so traps and obstructions also slow the feet.
                locomotionTime += Mathf.Max(0, distance) / ((IsRunning ? 2.4f : 1.1f) * (archetype != null ? archetype.Stride : 1));
                Tick(locomotionTime / (InjuredLegs > 0 ? .55f : 1), false);
            }
            BlendMotionTransition(dt);ApplyHitReaction(dt);
            wasAttacking = attacking;
            wasIdle = idle;
        }
        Material woundMaterial;
        readonly List<GameObject>[] attachedDetails = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>(), new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };
        public int LostLegs => (!parts[3].enabled ? 1 : 0) + (!parts[4].enabled ? 1 : 0);
        public int InjuredLegs => (LegInjured(3) ? 1 : 0) + (LegInjured(4) ? 1 : 0);
        public int LostArms => (!parts[1].enabled ? 1 : 0) + (!parts[2].enabled ? 1 : 0);
        public static int Classify(string name)
        {
            string n = name.ToLowerInvariant(); bool left = n.EndsWith(".l");
            if (n == "head") return 0;
            if (n.Contains("arm") || n.Contains("fist") || n.Contains("hand")) return left ? 1 : 2;
            if (n.Contains("leg") || n.Contains("foot")) return left ? 3 : 4;
            return 5;
        }
        void Awake()
        {
            SetupArchetype();
            home = transform.localPosition; homeRotation = transform.localRotation; homeScale = transform.localScale;
            if (hitBones != null && hitBones.Length > 0)
            {
                for (int i = 0; i < hitBones.Length; i++) if (hitBones[i] != null) joints[hitBoneRegions[i]].Add(hitBones[i]);
                return;
            }
            foreach (var bone in parts[0].bones)
            {
                int part = Classify(bone.name);
                if (part == 5 && (bone.name == "Bone" || bone.name == "Neck" || bone.name.Contains("Target"))) continue;
                joints[part].Add(bone);
            }
        }
        public void Tick(float time, bool attacking)
        {
            if (dying) return;
            var clip = attacking && attackClip != null ? attackClip
                : InjuredLegs == 2 && crawlClip != null && !IsCollapsing ? crawlClip : IsRunning ? runClip : walkClip;
            if (clip != null) SamplePose(clip, (time * (!attacking && InjuredLegs > 0 ? .55f : 1)) % clip.length);
            AddArchetypeDetails();
            AnimateCreepyFace(time);
            transform.localPosition = home;
            transform.localRotation = homeRotation;
            transform.localScale = homeScale;
            ApplyInjuredPose(time,attacking);
            ApplyArchetypePose(attacking);
        }
        Bounds Region(int part)
        {
            var bones = joints[part];
            if (bones.Count == 0) return parts[part].bounds;
            Bounds bounds = new Bounds(bones[0].position, Vector3.zero);
            foreach (var bone in bones) bounds.Encapsulate(bone.position);
            if (part == 0) bounds.center += transform.up * .09f;
            bounds.Expand(part == 0 ? new Vector3(.34f, .38f, .34f) : part == 5 ? new Vector3(.43f, .2f, .32f) : new Vector3(.2f, .2f, .2f));
            return bounds;
        }
        public bool Trace(Ray ray, float limit, out float distance, out int region)
        {
            distance = limit; region = -1;
            for (int part = 0; part < parts.Length; part++)
                if (parts[part] != null && parts[part].enabled && Region(part).IntersectRay(ray, out float entry) && entry >= 0 && entry < distance) { distance = entry; region = part; }
            return region >= 0;
        }
        public Vector3 RegionCenter(int part) => Region(part).center;
        public void DressWounds(Material material)
        {
            woundMaterial = material;
            if (!preserveSourceAppearance) ApplyCreepyAppearance();
            if (preserveSourceAppearance || !YardSurvival.GoreEnabled) return;
            if (material == null) return;
            var chest = FindBone("Body");
            if (chest == null) return;
            // Small raised wounds follow the sampled skeleton, rather than floating in world space.
            attachedDetails[5].Add(Wound(chest, chest.position + transform.forward * .16f + transform.up * .06f,
                new Vector3(.13f, .2f, .035f), "Bloodied chest wound"));
            attachedDetails[5].Add(Wound(chest, chest.position + transform.forward * .17f - transform.right * .1f - transform.up * .12f,
                new Vector3(.075f, .16f, .025f), "Torn bloody clothing"));
        }
        Transform FindBone(string name)
        {
            foreach (var bone in parts[0].bones) if (bone != null && bone.name == name) return bone;
            return null;
        }
        GameObject Wound(Transform bone, Vector3 position, Vector3 size, string name)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere); obj.name = name;
            obj.transform.position = position; obj.transform.rotation = transform.rotation;
            obj.transform.localScale = size; obj.transform.SetParent(bone, true);
            obj.GetComponent<Collider>().enabled = false;
            obj.GetComponent<Renderer>().sharedMaterial = woundMaterial;
            return obj;
        }
        public bool Sever(int part, Vector3 direction, Transform debrisParent)
        {
            if (IsCharger && part >= 3) return false;
            if (!YardSurvival.GoreEnabled || part < 0 || part >= 5 || !parts[part].enabled) return false;
            CaptureMotionTransition();
            Vector3 woundPosition = RegionCenter(part);
            var skin = parts[part]; var mesh = BakeRegion(part);
            var obj = new GameObject(part == 0 ? "Severed head" : part < 3 ? "Severed arm" : "Severed leg");
            obj.transform.SetParent(debrisParent, false); obj.transform.position = skin.transform.position; obj.transform.rotation = skin.transform.rotation; obj.transform.localScale = Vector3.one;
            obj.AddComponent<MeshFilter>().sharedMesh = mesh; obj.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
            obj.AddComponent<ZombieDebris>().Initialize(mesh, Vector3.ClampMagnitude(direction,1) * 2.3f + Vector3.up * 1.2f, audioOwner);
            if (audioOwner != null) audioOwner.PlayZombieSound(part == 0 ? "Head_Sever" : part < 3 ? "Arm_Sever" : "Leg_Sever", RegionCenter(part), .6f);
            skin.enabled = false;
            if(part>=3&&InjuredLegs==2)BeginCollapse();
            foreach (var detail in attachedDetails[part]) if (detail != null) detail.transform.SetParent(obj.transform, true);
            if (woundMaterial != null)
            {
                string boneName = new[] { "Neck", "UpperArm.L", "UpperArm.R", "UpperLeg.L", "UpperLeg.R" }[part];
                Transform bone = severJoints != null && severJoints.Length > part ? severJoints[part] : FindBone(boneName);
                if (bone != null) SeverSeams(bone,obj.transform,part);
            }
            if (audioOwner != null) audioOwner.SpawnBlood(woundPosition, direction, true);
            return true;
        }
        public void Die() => Die(Vector3.zero, transform.position + Vector3.up, 0);

        public void Die(Vector3 hitDirection, Vector3 hitPoint, float impactSpeed = 1.2f)
        {
            if (dying) return;
            if (audioOwner != null) audioOwner.PrepareNightmareDeath(this);
            dying = true;
            if (audioOwner != null) audioOwner.SpawnBlood(transform.position + Vector3.up * .7f, hitDirection, true);
            deathAt = Time.time;
            ActiveDeathClip = deathClip;
            if (deathClips != null && deathClips.Length > 0)
            {
                int start = Random.Range(0, deathClips.Length);
                for (int i = 0; i < deathClips.Length; i++)
                {
                    var candidate = deathClips[(start + i) % deathClips.Length];
                    if (candidate == null || candidate.length < .25f) continue;
                    ActiveDeathClip = candidate;
                    break;
                }
            }
            // The replacement Brute mesh cannot follow the source death clips' extreme arm poses.
            bool useAuthoredDeath = !name.StartsWith("04-Cargo-Brute", System.StringComparison.Ordinal) && preserveSourceAppearance && animationRig != null &&
                ActiveDeathClip != null && ActiveDeathClip.length >= .25f && parts[0].enabled &&
                InjuredLegs == 0 && LostArms == 0;
            if (useAuthoredDeath)
            {
                CaptureMotionTransition();
                if (ActiveDeathClip.legacy) { ReleasePoseGraph(); animationRig.enabled = false; }
            }
            else if (animationRig != null)
                BeginRagdoll(hitDirection, hitPoint, impactSpeed);
            if (audioOwner != null) { audioOwner.PlayZombieSound("Zombie_Death", transform.position, .5f);  }
            Destroy(transform.parent.gameObject, 8);
        }
        void Update()
        {
            if (!dying || ragdollLinks.Count>0) return;
            if (ActiveDeathClip != null)
            {
                float time = Mathf.Clamp(Time.time - deathAt, 0, Mathf.Max(0, ActiveDeathClip.length - .01f));
                if (ActiveDeathClip.legacy) ActiveDeathClip.SampleAnimation(gameObject, time);
                else SamplePose(ActiveDeathClip, time);
            }
            transform.localPosition = home;
            transform.localRotation = homeRotation;
            transform.localScale = homeScale;
            BlendMotionTransition(Time.deltaTime);
        }
    }

    public sealed class ZombieDebris : MonoBehaviour
    {
        Mesh mesh;
        Rigidbody body;
        YardSurvival audioOwner;
        bool landed;
        public void Initialize(Mesh baked, Vector3 impulse, YardSurvival owner = null)
        {
            mesh=baked;audioOwner=owner;gameObject.layer=28;
            var shape=gameObject.AddComponent<BoxCollider>();shape.center=baked.bounds.center;
            Vector3 size=baked.bounds.size;shape.size=new Vector3(Mathf.Max(.04f,size.x),Mathf.Max(.04f,size.y),Mathf.Max(.04f,size.z));
            body=gameObject.AddComponent<Rigidbody>();body.mass=1.5f;body.linearDamping=.55f;body.angularDamping=1.5f;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.interpolation=RigidbodyInterpolation.Interpolate;
            body.maxAngularVelocity=7;body.maxDepenetrationVelocity=2;
            body.linearVelocity=Vector3.ClampMagnitude(impulse,3);body.angularVelocity=new Vector3(2,3,1);
            Destroy(gameObject,6);
        }
        void FixedUpdate()
        {
            if(body==null)return;
            body.linearVelocity=Vector3.ClampMagnitude(body.linearVelocity,6);body.angularVelocity=Vector3.ClampMagnitude(body.angularVelocity,7);
        }
        void OnCollisionEnter(Collision collision)
        {
            if(landed||collision.gameObject.layer==28)return;
            landed=true;if(audioOwner!=null)audioOwner.PlayZombieSound("Limb_Land",transform.position,.6f);
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
