using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        int nextCoopEntity=1,coopEpoch,clientEpoch=-1,coopSequence,lastGuestSequence;
        PeerPlayer peer;
        Transform peerAvatar;
        HumanoidPlayerAvatar peerAnimation;
        Vector3 replicaPeerPosition;
        float replicaPeerYaw,replicaPeerPitch;
        int replicaPeerHealth=100,replicaPeerWeapon=2,selectedPlayerSkin,replicaPeerSkin,spawnedPeerSkin=-1;
        bool pendingPeerJump,pendingPeerSlide,pendingPeerDodge;
        readonly Dictionary<int,Vector3> predictedCoopPositions=new Dictionary<int,Vector3>();
        readonly Queue<int> predictedCoopSequences=new Queue<int>();
        void RememberCoopPosition(int sequence)
        {
            predictedCoopPositions[sequence]=player.position;predictedCoopSequences.Enqueue(sequence);
            while(predictedCoopSequences.Count>256)predictedCoopPositions.Remove(predictedCoopSequences.Dequeue());
        }
        void ReconcileCoopPosition(int ack,Vector3 authoritative)
        {
            if(dead||!predictedCoopPositions.TryGetValue(ack,out var predicted))return;
            // Replay the displacement since the acknowledged input instead of
            // dragging a moving guest back to a delayed host position.
            Vector3 error=authoritative-predicted;
            error.y=0;
            if(error.magnitude<.45f){coopCorrection=Vector3.zero;return;}
            if(error.magnitude<=3){coopCorrection=error;return;}
            Vector3 correction=error.magnitude>3?error:error*.25f;
            Vector3 corrected=Move(player.position,correction,PlayerRadius,true);corrected.y=player.position.y;
            Vector3 applied=corrected-player.position;player.position=corrected;
            foreach(var key in predictedCoopSequences.ToArray())predictedCoopPositions[key]+=applied;
        }
        [Serializable] sealed class CoopInput
        {public string action;public int sequence,entity,type,skin;public Vector2 movement;public float yaw,pitch;public bool jump,slide,dodge,aim,rotate;public Vector3 point;}
        sealed class PeerPlayer
        {
            public ulong clientId;public bool connected,jump,slide,aim,unarmed; public bool wantsDodge; public DodgeState dodge;public int health=100,weapon=WeaponCatalog.Starter,ammo=15,reserve=45,lastSequence,skin;
            public Vector3 position=new Vector3(1.5f,.65f,0),slideDirection;public Vector2 movement;
            public int reloadStartAmmo,reloadShellCount;
            public float knockdown; public Vector3 knockback; public float reviveProtectedUntil;public float yaw,pitch,fire,reload,hurt,jumpHeight,verticalSpeed,slideTime,slideCooldown;
            public readonly List<InventoryWeapon> inventory=new List<InventoryWeapon>();
        }
        ulong NetworkManagerLocalId()=>coop!=null?coop.LocalClientId:0;
        void InitializePeer(ulong id){peer=new PeerPlayer{clientId=id,connected=true};ResetCoopTraffic();CapturePeerInventory();DestroyPeerAvatar();coopSnapshotAt=0;}
        void ResetCoopPeerForRun()
        {
            if (dedicatedServer) return;
            if(IsCoopHost&&peer!=null){ulong id=peer.clientId;bool connected=peer.connected;peer=new PeerPlayer{clientId=id,connected=connected};CapturePeerInventory();DestroyPeerAvatar();}
            else if(IsCoopGuest){DestroyPeerAvatar();}
        }
        void CaptureCoopInput()
        {
            if(frontScreen!=FrontScreen.Game||dead||building||showHelp)return;
            pendingPeerJump|=Pressed(Control.Jump);pendingPeerSlide|=Pressed(Control.Slide);pendingPeerDodge|=Pressed(Control.Dodge);
        }
        void SendCoopCommand(string action,int entity=0,int type=0,Vector3 point=default,bool rotate=false)
        {
            if(!IsCoopGuest)return;
            if(action=="input"&&pendingMoves.Count>=MovementWindow)return;
            bool control=frontScreen==FrontScreen.Game&&!dead&&!showHelp;
            var input=new CoopInput{action=action,sequence=++coopSequence,entity=entity,type=type,point=point,rotate=rotate,yaw=yaw,pitch=pitch,aim=aiming,skin=selectedPlayerSkin,
                movement=control?new Vector2((Held(Control.Right)?1:0)-(Held(Control.Left)?1:0),(Held(Control.Forward)?1:0)-(Held(Control.Back)?1:0)):Vector2.zero,jump=pendingPeerJump,slide=pendingPeerSlide,dodge=pendingPeerDodge};
            RememberCoopPosition(input.sequence);
            pendingPeerJump=pendingPeerSlide=pendingPeerDodge=false;
            if(action!="input")lastGuestSequence=input.sequence;
            if(action=="input")SendCoopMovement(input);
            else SendCoopJson("D27.Input",NetworkManager.ServerClientId,JsonUtility.ToJson(input));
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        void ReceiveCoopInput(ulong id,FastBufferReader reader)
        {
            if (dedicatedServer)
            {
                if (reader.Length > 8192 || !dedicatedPending.ContainsKey(id)) return;
                try { reader.ReadValueSafe(out string payload); ReceiveDedicatedInput(id,JsonUtility.FromJson<CoopInput>(payload)); }
                catch (Exception e) { Debug.LogWarning("Rejected dedicated input: " + e.Message); }
                return;
            }
            if(!IsCoopHost||peer==null||!peer.connected||id!=peer.clientId||reader.Length>8192)return;
            try
            {
                reader.ReadValueSafe(out string json);var input=JsonUtility.FromJson<CoopInput>(json);
                ApplyCoopInput(input);
            }
            catch(Exception e){Debug.LogWarning("Rejected malformed coop input: "+e.Message);}
        }
        void ApplyCoopInput(CoopInput input)
        {
                if(input==null||input.sequence<=peer.lastSequence||!Finite(input.yaw)||!Finite(input.pitch)||!Finite(input.movement.x)||!Finite(input.movement.y))return;
                peer.skin=Mathf.Clamp(input.skin,0,Mathf.Max(0,(playerSkinModels?.Length??1)-1));peer.lastSequence=input.sequence;peer.yaw=Mathf.Repeat(input.yaw+180,360)-180;peer.pitch=Mathf.Clamp(input.pitch,-89,75);
                if(coopLobby){if(input.action=="lobby-ready"){coopGuestReady=input.type==1;TryStartCoopLobby();SendCoopLobby();}return;}
                peer.movement=Vector2.ClampMagnitude(input.movement,1);peer.jump|=input.jump;peer.slide|=input.slide;peer.wantsDodge|=input.dodge;peer.aim=input.aim;
                if(peer.health<=0){if(input.action=="restart"&&dead)PlayFromMenu();return;}
                if (peer.knockdown > 0 && input.action == "fire") return;
                switch(input.action)
                {
                    case "fire":PeerFire(input.point);break;
                    case "revive":ReviveCoopPartner(true);break;
                    
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    case "qa-slot-near":peer.position=SlotApproach+Vector3.up*.65f;break;
#endif
                    case "reload":if(!peer.unarmed&&peer.reload<=0&&peer.reserve>0&&peer.ammo<WeaponCatalog.All[peer.weapon].capacity&&!WeaponCatalog.IsMelee(peer.weapon))BeginPeerReload();break;
                    case "pickup":PeerPickup(input.entity);break;
                    case "switch":SwitchPeerWeapon(input.type);break;
                    case "drop":DropPeerWeapon(input.type);break;
                    case "supply":var box=supplyBoxes.FirstOrDefault(b=>b.id==input.entity);if(box!=null)ConsumeSupply(box,peer.position,ref peer.health,peer.weapon,ref peer.reserve);break;
                    case "build":
                        if(!Finite(input.point.x)||!Finite(input.point.z))break;
                        Vector3 old=player.position;try{player.position=peer.position;TryPlaceDefense(new Vector3(Mathf.Round(input.point.x),0,Mathf.Round(input.point.z)),input.type,input.rotate);}finally{player.position=old;}break;
                    case "hammer":if(peer.fire<=0){ApplyHammerHit(input.entity,peer.position);peer.fire=.55f;}break;
                    case "wave":StartWave();break;
                }
                coopInputAt=Time.unscaledTime;
        }
        void CapturePeerInventory()
        {
            if(peer==null||peer.unarmed)return;
            var slot=peer.inventory.Find(w=>w.weapon==peer.weapon);
            if(slot==null){slot=new InventoryWeapon{weapon=peer.weapon};peer.inventory.Add(slot);}
            slot.ammo=peer.ammo;slot.reserve=peer.reserve;slot.readyAt=Time.time+Mathf.Max(0,peer.fire);
        }
        void GrantPeerChestWeapon(int id)
        {
            MakePeerWeaponRoom(id);peer.unarmed=false;peer.weapon=id;peer.ammo=WeaponCatalog.All[id].capacity;
            peer.reserve=peer.ammo*3;peer.reload=0;peer.fire=.18f;CapturePeerInventory();
        }
        void MakePeerWeaponRoom(int id)
        {
            CapturePeerInventory();
            if(!peer.inventory.Exists(w=>w.weapon==id)&&peer.inventory.Count>=WeaponSlotLimit)DropPeerWeapon(peer.weapon);
        }
        bool DropPeerWeapon(int id)
        {
            if(peer==null||peer.health<=0||upgradePending)return false;
            CapturePeerInventory();var slot=peer.inventory.Find(w=>w.weapon==id);
            if(slot==null)return false;
            var pickup=SpawnPickup(id,WeaponDropPosition(peer.position,Quaternion.Euler(0,peer.yaw,0)*Vector3.forward),slot.ammo,slot.reserve);
            if(pickup==null)return false;
            pickup.readyAt=slot.readyAt;peer.inventory.Remove(slot);
            if(peer.weapon==id)
            {
                peer.reload=0;peer.unarmed=peer.inventory.Count==0;
                if(peer.unarmed){peer.ammo=peer.reserve=0;}
                else {var next=peer.inventory[0];peer.weapon=next.weapon;peer.ammo=next.ammo;peer.reserve=next.reserve;peer.fire=Mathf.Max(.18f,next.readyAt-Time.time);}
            }
            return true;
        }
        bool SwitchPeerWeapon(int id)
        {
            if(peer==null||peer.health<=0)return false;
            var slot=peer.inventory.Find(w=>w.weapon==id);
            if(slot==null)return false;
            if(id==peer.weapon)return true;
            CapturePeerInventory();peer.weapon=id;peer.ammo=slot.ammo;peer.reserve=slot.reserve;
            peer.reload=0;peer.fire=Mathf.Max(.18f,slot.readyAt-Time.time);return true;
        }
        void PeerPickup(int id)
        {
            if (peer == null || peer.health <= 0 || peer.reload > 0 || upgradePending) return;
            var pickup = pickups.Find(p => p.netId == id);
            if (pickup == null || !InWeaponPickupRange(peer.position,pickup)) return;
            MakePeerWeaponRoom(pickup.weapon); peer.unarmed=false; peer.weapon = pickup.weapon; peer.ammo = pickup.ammo;
            peer.reserve = pickup.reserve; peer.reload = 0; peer.fire = Mathf.Max(.18f,pickup.readyAt-Time.time);
            CapturePeerInventory(); RemoveWeaponPickup(pickup);
        }
        void PeerFire(Vector3 shotDirection)
        {
            if(peer.unarmed)return;
            var weapon=WeaponCatalog.All[peer.weapon];
            if(peer.fire>0||peer.reload>0)return;
            if(peer.ammo<=0){if(peer.reserve>0)BeginPeerReload();return;}
            if(!WeaponCatalog.IsMelee(peer.weapon))peer.ammo--;peer.fire=weapon.interval;
            Vector3 origin=peer.position+Vector3.up*(peer.slideTime>0?.3f:1);
            Vector3 direction=Quaternion.Euler(peer.pitch,peer.yaw,0)*Vector3.forward;
            if(Finite(shotDirection.x)&&Finite(shotDirection.y)&&Finite(shotDirection.z)&&shotDirection.sqrMagnitude>.5f&&shotDirection.sqrMagnitude<1.5f&&Vector3.Dot(direction,shotDirection.normalized)>.9f)direction=shotDirection.normalized;
            ulong previousShooter=coopFiringPlayer;coopFiringPlayer=peer.clientId;
            int oldWeapon=weaponId;bool oldAim=aiming;Vector3 oldPosition=arenaCamera.transform.position;Quaternion oldRotation=arenaCamera.transform.rotation;
            try
            {
                weaponId=peer.weapon;aiming=peer.aim;arenaCamera.transform.position=origin;arenaCamera.transform.rotation=Quaternion.LookRotation(direction);
                FireWeaponProjectiles(direction);PlayEffect(WeaponClip(0),.5f);
            }
            finally{weaponId=oldWeapon;aiming=oldAim;arenaCamera.transform.position=oldPosition;arenaCamera.transform.rotation=oldRotation;coopFiringPlayer=previousShooter;if(health<=0&&!dead)BeginDeathSequence();}
            RecordCoopShot(peer.clientId,peer.weapon,origin);
        }
        void UpdatePeer(float dt)
        {
            if(!HasCoopPartner)return;
            peer.fire=Mathf.Max(0,peer.fire-dt);peer.hurt=Mathf.Max(0,peer.hurt-dt);peer.slideCooldown=Mathf.Max(0,peer.slideCooldown-dt);
            if(peer.reload>0){peer.reload-=dt;if(peer.weapon==3)TransferReloadShells(1-peer.reload/WeaponCatalog.All[peer.weapon].reload,peer.reloadStartAmmo,peer.reloadShellCount,ref peer.ammo,ref peer.reserve);if(peer.reload<=0){int transfer=peer.weapon==3?0:Mathf.Min(WeaponCatalog.All[peer.weapon].capacity-peer.ammo,peer.reserve);peer.ammo+=transfer;peer.reserve-=transfer;}}
            if(peer.health<=0){peer.movement=Vector2.zero;return;}
            if (peer.knockdown > 0)
            {
                Vector3 ground = new Vector3(peer.position.x, 0, peer.position.z);
                peer.position = OnGround(Move(ground, peer.knockback * dt, PlayerRadius, true),.65f);
                peer.knockback = Vector3.MoveTowards(peer.knockback, Vector3.zero, 19f * dt);
                peer.knockdown = Mathf.Max(0, peer.knockdown - dt);
                peer.jump = peer.slide = peer.wantsDodge = false; peer.dodge.remaining = 0;
                peer.jumpHeight = peer.verticalSpeed = 0;
                replicaPeerPosition = peer.position;
                replicaPeerHealth = peer.health;
                return;
            }
            if(Time.unscaledTime-coopInputAt>1)peer.movement=Vector2.zero;
            Quaternion facing = Quaternion.Euler(0, peer.yaw, 0);
            Vector3 direction = facing * new Vector3(peer.movement.x, 0, peer.movement.y);
            bool grounded=peer.jumpHeight<=0&&peer.verticalSpeed<=0;
            if (BeginDodge(ref peer.dodge, peer.wantsDodge && grounded && !peer.jump, direction, facing * Vector3.forward, dt))
            {
                if (peer.slideTime > 0) peer.slideCooldown = SlideCooldown;
                peer.slideTime = 0;
            }
            if(peer.jump&&grounded){peer.verticalSpeed=JumpSpeed;peer.slideTime=0;}
            else if(peer.slide&&grounded&&peer.dodge.remaining<=0&&peer.slideTime<=0&&peer.slideCooldown<=0&&direction.sqrMagnitude>.01f){peer.slideDirection=direction.normalized;peer.slideTime=SlideDuration;}
            peer.jump=peer.slide=peer.wantsDodge=false;
            bool sliding=peer.slideTime>0;float speed=sliding?Mathf.Lerp(WalkSpeed,12,peer.slideTime/SlideDuration):WalkSpeed;
            Vector3 step=(sliding?peer.slideDirection:direction)*speed*dt;
            Vector3 before=peer.position;before.y=0;if (!MoveDodge(ref peer.dodge, before, dt, out Vector3 next)) next = Move(before, step, PlayerRadius, true);
            if(sliding){peer.slideTime=Mathf.Max(0,peer.slideTime-dt);if(peer.slideTime<=0||(next-before).sqrMagnitude<step.sqrMagnitude*.25f){peer.slideTime=0;peer.slideCooldown=SlideCooldown;}}
            if(peer.jumpHeight>0||peer.verticalSpeed>0){peer.jumpHeight+=peer.verticalSpeed*dt-.5f*Gravity*dt*dt;peer.verticalSpeed-=Gravity*dt;if(peer.jumpHeight<=0)peer.jumpHeight=peer.verticalSpeed=0;}
            peer.position=OnGround(next,.65f+peer.jumpHeight);
            foreach(var box in supplyBoxes.ToArray())ConsumeSupply(box,peer.position,ref peer.health,peer.weapon,ref peer.reserve);
            replicaPeerPosition=peer.position;replicaPeerYaw=peer.yaw;replicaPeerPitch=peer.pitch;replicaPeerHealth=peer.health;replicaPeerWeapon=peer.unarmed?-1:peer.weapon;replicaPeerSkin=peer.skin;
        }
        void DamagePeer(int amount)
        {if(!HasCoopPartner||peer.health<=0||peer.hurt>0)return;peer.health=Mathf.Max(0,peer.health-IncomingDamage(amount));peer.hurt=.3f;}
        void DestroyPeerAvatar(){if(peerAvatar!=null){peerAvatar.gameObject.SetActive(false);Destroy(peerAvatar.gameObject);}peerAvatar=null;peerAnimation=null;spawnedPeerSkin=-1;}
        void UpdatePeerAvatar(float dt)
        {
            bool present=HasCoopPartner||IsCoopGuest;if(!present)return;
            if(peerAvatar!=null&&spawnedPeerSkin!=replicaPeerSkin)DestroyPeerAvatar();
            if(peerAvatar==null)
            {
                peerAvatar=new GameObject("Coop cultist partner").transform;peerAvatar.SetParent(actors,false);
                if(playerSkinModels!=null&&playerSkinModels.Length>0){spawnedPeerSkin=Mathf.Clamp(replicaPeerSkin,0,playerSkinModels.Length-1);var skin=Instantiate(playerSkinModels[spawnedPeerSkin],peerAvatar);peerAnimation=skin.GetComponent<HumanoidPlayerAvatar>();}
                else {var rig=DepotModels.Player(this,peerAvatar,Vector3.zero);foreach(var r in rig.root.GetComponentsInChildren<Renderer>())r.enabled=true;}
            }
            Vector3 before=peerAvatar.position;Vector3 target=replicaPeerPosition-Vector3.up*.65f;
            float blend=1-Mathf.Exp(-18*dt);
            peerAvatar.position=Vector3.Distance(before,target)>3?target:Vector3.Lerp(before,target,blend);
            peerAvatar.rotation=Quaternion.Slerp(peerAvatar.rotation,Quaternion.Euler(0,replicaPeerYaw,0),blend);
            peerAnimation?.Tick(dt,Vector3.Distance(before,peerAvatar.position)/Mathf.Max(.001f,dt),replicaPeerPitch,replicaPeerHealth<=0);
            if(replicaPeerWeapon>=0&&replicaPeerWeapon<animatedWeaponModels.Length)
                peerAnimation?.SetWeapon(ModelFor(replicaPeerWeapon),replicaPeerWeapon,replicaPeerPitch);
            else peerAnimation?.SetWeapon(null,-1,0);
        }
    }
}

