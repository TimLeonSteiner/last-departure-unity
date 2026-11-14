using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        [Serializable] sealed class CoopPlayerState {public Vector3 position;public float yaw,pitch,reload,knockdown;public int health,weapon,ammo,reserve,skin,reloadStartAmmo,reloadShellCount;public InventoryWeapon[] inventory;}
        [Serializable] sealed class CoopEnemyState {public bool runner;public int id,variant,hp,mask,legInjuries,special,attackSerial,hitSerial,hitRegion,hitDamage;public Vector3 position,hitDirection;public float yaw,collapseRemaining,attackTime=-1;}
        [Serializable] sealed class CoopDefenseState {public int id,type,hits,paid;public Vector3 position;public bool vertical;public float hp,yaw;}
        [Serializable] sealed class CoopPickupState {public int id,weapon,ammo,reserve;public Vector3 position;public bool waveDrop;}
        [Serializable] sealed class CoopSupplyState {public int id;public bool health;public Vector3 position;}
        [Serializable] sealed class CoopProjectileState {public int id;public Vector3 position;public bool grenade,acid,pool;public float life;}
        [Serializable] sealed class CoopShot {public ulong player;public int weapon;public Vector3 origin,target;public bool turret;}
        [Serializable] sealed class CoopWorldState
        {public float runSeconds;public int wavesCleared,difficulty;public BloodState[] blood;public int sequence,epoch,ack,wave,kills,scrap,remaining,chestStage,chestWeapon;public bool fighting,slotBusy;public int slotStake,slotPayout,slotSerial,slotMultiplier;public float slotTimer;public float countdown,chestTimer;public CoopPlayerState host,guest;public CoopEnemyState[] enemies;public CoopDefenseState[] defenses;public CoopPickupState[] pickups;public CoopSupplyState[] boxes;public CoopProjectileState[] projectiles;public CoopShot[] shots;public PlacedBarrelState[] placedBarrels;public string[] spentBarrels;}
        readonly Dictionary<int,float> coopEnemyYaw=new Dictionary<int,float>();
        void UpdateCoopEnemyVisuals(float dt)
        {
            float blend=1-Mathf.Exp(-18*dt);
            foreach(var e in enemies)
            {
                Vector3 before=e.body.position;
                var floorPosition=OnGround(e.position,e.zombie==null?.65f:0);
                e.body.position=Vector3.Distance(before,floorPosition)>3?floorPosition:Vector3.Lerp(before,floorPosition,blend);
                if(coopEnemyYaw.TryGetValue(e.netId,out float angle))e.body.rotation=Quaternion.Slerp(e.body.rotation,Quaternion.Euler(0,angle,0),blend);
                float duration = SpecialAttackDuration(e.specialAttack);
                bool attacking=e.attackTime>=0 && e.attackTime<duration;
                if(attacking)e.attackTime=Mathf.Min(duration,e.attackTime+dt);
                e.zombie?.TickMovement(dt,Vector3.Distance(before,e.body.position),attacking,attacking?e.attackTime/duration:-1);
            }
        }
        readonly List<CoopShot> coopShots=new List<CoopShot>();
        readonly Dictionary<int,Transform> coopProjectileVisuals=new Dictionary<int,Transform>();
        void RecordCoopShot(ulong source,int weapon,Vector3 origin)
        {if(IsCoopHost&&HasCoopPartner&&coopShots.Count<32)coopShots.Add(new CoopShot{player=source,weapon=weapon,origin=origin});}
        void RecordTurretFeedback(Vector3 origin,Vector3 target)
        {if(IsCoopHost&&HasCoopPartner&&coopShots.Count<32)coopShots.Add(new CoopShot{turret=true,origin=origin,target=target});}
        void SendCoopSnapshot()
        {
            CapturePeerInventory();CaptureInventoryWeapon();
            var defs=new List<CoopDefenseState>();
            foreach(var b in barricades)defs.Add(new CoopDefenseState{id=b.netId,type=0,position=new Vector3(b.bounds.center.x,0,b.bounds.center.y),vertical=b.bounds.height>b.bounds.width,hp=b.hp});
            foreach(var b in turrets)defs.Add(new CoopDefenseState{id=b.netId,type=1,position=new Vector3(b.bounds.center.x,0,b.bounds.center.y),hp=b.hp,yaw=b.head.eulerAngles.y});
            foreach(var b in traps)defs.Add(new CoopDefenseState{id=b.netId,type=2,position=b.position,hp=b.hp});
            foreach(var state in defs){var body=DefenseById(state.id,out int _,out float _);var c=body.GetComponent<DefenseConstruction>();state.hits=c!=null?c.hits:DefenseConstruction.RequiredHits[state.type];state.paid=c!=null?c.paid:DefenseCosts[state.type];}
            var world=new CoopWorldState{difficulty=(int)runDifficulty,runSeconds=runSeconds,wavesCleared=runWavesCleared,blood=BloodSnapshot(peer.clientId),sequence=worldSequence+1,epoch=coopEpoch,ack=peer.lastSequence,wave=wave,kills=kills,scrap=supplies,remaining=remainingToSpawn,fighting=fighting,countdown=nextWaveCountdown,
                chestStage=chestStage,chestWeapon=chestWeapon,chestTimer=chestTimer,slotBusy=slotBusy,slotStake=slotStake,slotPayout=slotPayout,slotSerial=slotSerial,slotMultiplier=slotMultiplier,slotTimer=slotTimer,
                host=new CoopPlayerState{position=player.position,yaw=yaw,pitch=pitch,health=health,weapon=weaponId,ammo=ammunition,reserve=reserveAmmo,skin=selectedPlayerSkin,reload=reloading?reloadTimer:0,reloadStartAmmo=reloadStartAmmo,reloadShellCount=reloadShellCount,inventory=weaponInventory.ToArray()},
                guest=new CoopPlayerState{position=peer.position,knockdown=peer.knockdown,yaw=peer.yaw,pitch=peer.pitch,health=peer.health,weapon=peer.weapon,ammo=peer.ammo,reserve=peer.reserve,skin=peer.skin,reload=peer.reload,reloadStartAmmo=peer.reloadStartAmmo,reloadShellCount=peer.reloadShellCount,inventory=peer.inventory.ToArray()},
                enemies=enemies.Select(e=>new CoopEnemyState{id=e.netId,variant=e.variant,runner=e.zombie!=null&&e.zombie.Runner,hp=e.hp,attackTime=e.attackTime,special=(int)e.specialAttack,attackSerial=e.attackSerial,hitSerial=e.hitSerial,hitDirection=e.hitDirection,hitRegion=e.hitRegion,hitDamage=e.hitDamage,legInjuries=e.zombie!=null?e.zombie.LegInjuryMask:0,collapseRemaining=e.zombie!=null?e.zombie.CollapseTimeRemaining:0,position=e.position,yaw=e.body.eulerAngles.y,mask=e.zombie==null?63:e.zombie.parts.Select((p,i)=>p.enabled?1<<i:0).Sum()}).ToArray(),defenses=defs.ToArray(),
                pickups=pickups.Select(p=>new CoopPickupState{id=p.netId,weapon=p.weapon,ammo=p.ammo,reserve=p.reserve,position=p.position,waveDrop=p.waveDrop}).ToArray(),boxes=supplyBoxes.Select(b=>new CoopSupplyState{id=b.id,health=b.health,position=b.position}).ToArray(),
                projectiles=arsenalProjectiles.Select(p=>new CoopProjectileState{id=p.netId,position=p.position,grenade=p.grenade}).Concat(acidHazards.Select(a=>new CoopProjectileState{id=a.id,position=a.body.position,acid=true,pool=a.pool,life=a.life})).ToArray(),shots=coopShots.ToArray(),placedBarrels=placedBarrels.Select(p=>new PlacedBarrelState{id=p.Key,position=p.Value.transform.position}).ToArray(),spentBarrels=SpentBarrelKeys()};
            string json=JsonUtility.ToJson(world);if(json.Length>50000)return;
            SendCoopJson("D27.World",peer.clientId,json);worldSequence=world.sequence;sentBloodRevision[peer.clientId]=bloodRevision;
            if(dedicatedServer)dedicatedPending[peer.clientId].Enqueue(world.sequence);else pendingWorlds.Enqueue(world.sequence);
            coopShots.Clear();
        }
        void ReceiveCoopWorld(ulong id,FastBufferReader reader)
        {
            if(!IsCoopGuest||id!=NetworkManager.ServerClientId||reader.Length>131072)return;
            try
            {
                reader.ReadValueSafe(out int length);if(length<0||length!=RemainingCoopBytes(reader))return;
                byte[] bytes=new byte[length];reader.ReadBytesSafe(ref bytes,length);string json=System.Text.Encoding.UTF8.GetString(bytes);var world=JsonUtility.FromJson<CoopWorldState>(json);
                if(world==null||world.guest==null||world.host==null||world.sequence<=receivedWorldSequence)return;
                coopLobby=false;coopStarted=true;
                if(world.epoch!=clientEpoch||frontScreen==FrontScreen.Lobby){ResetRun();predictedCoopPositions.Clear();predictedCoopSequences.Clear();player.position=world.guest.position;coopCorrection=Vector3.zero;clientEpoch=world.epoch;frontScreen=FrontScreen.Game;CaptureMouse();}
                runDifficulty=(RunDifficulty)Mathf.Clamp(world.difficulty,0,2);
                runSeconds=world.runSeconds;runWavesCleared=world.wavesCleared;
                wave=world.wave;kills=world.kills;supplies=world.scrap;remainingToSpawn=world.remaining;fighting=world.fighting;nextWaveCountdown=world.countdown;
                chestStage=world.chestStage;chestWeapon=world.chestWeapon;chestTimer=world.chestTimer;
                slotBusy=world.slotBusy;slotStake=world.slotStake;slotPayout=world.slotPayout;slotSerial=world.slotSerial;slotMultiplier=world.slotMultiplier;slotTimer=world.slotTimer;
                int previousInventoryCount=weaponInventory.Count,previousWeaponId=weaponId;
                var own=world.guest;
                knockdownTime = Mathf.Clamp(own.knockdown, 0, KnockdownDuration);
                if (knockdownTime > 0) { slideTimer = jumpHeight = verticalSpeed = dodge.remaining = 0; knockdownVelocity = Vector3.zero; }
                ReconcileCoopPosition(world.ack,own.position);
                if(world.ack>=lastGuestSequence){
                    if((weaponId!=own.weapon||!HasEquippedWeapon)&&(own.inventory==null||own.inventory.Length>0))EquipWeapon(own.weapon,own.ammo,own.reserve);
                    ammunition=own.ammo;reserveAmmo=own.reserve;reloading=own.reload>0;reloadTimer=Mathf.Max(0,own.reload);reloadStartAmmo=own.reloadStartAmmo;reloadShellCount=own.reloadShellCount;viewWeapon?.SetReloadShellCount(reloadShellCount);
                    weaponInventory.Clear();
                    foreach(var slot in own.inventory??Array.Empty<InventoryWeapon>())
                        if(weaponInventory.Count<WeaponSlotLimit&&slot.weapon>=0&&slot.weapon<WeaponCatalog.All.Length&&!weaponInventory.Any(w=>w.weapon==slot.weapon))
                            weaponInventory.Add(new InventoryWeapon{weapon=slot.weapon,ammo=slot.ammo,reserve=slot.reserve});
                }
                if(gun!=null)gun.gameObject.SetActive(HasEquippedWeapon&&!building&&!HologramBlocking);
                if(previousInventoryCount!=weaponInventory.Count||previousWeaponId!=weaponId)hologramRenderedTab=-1;
                int previousHealth=health;health=own.health;if(health<=0&&!dead)BeginDeathSequence();else if(health>0&&dead){RestoreCoopPlayer();DestroyPeerAvatar();}else if(health<previousHealth)PlayPlayerDamageAudio();
                replicaPeerPosition=world.host.position;replicaPeerYaw=world.host.yaw;replicaPeerPitch=world.host.pitch;replicaPeerHealth=world.host.health;replicaPeerWeapon=world.host.inventory!=null&&world.host.inventory.Length==0?-1:world.host.weapon;replicaPeerSkin=world.host.skin;
                ApplyBloodSnapshot(world.blood);ApplyPlacedBarrels(world.placedBarrels);ApplySpentBarrels(world.spentBarrels);ApplyCoopEnemies(world.enemies??Array.Empty<CoopEnemyState>());ApplyCoopDefenses(world.defenses??Array.Empty<CoopDefenseState>());ApplyCoopPickups(world.pickups??Array.Empty<CoopPickupState>());ApplyCoopSupplies(world.boxes??Array.Empty<CoopSupplyState>());ApplyCoopProjectiles(world.projectiles??Array.Empty<CoopProjectileState>());
                foreach(var shot in world.shots??Array.Empty<CoopShot>())
                    if(shot.turret)ShowTurretShot(shot.origin,(shot.target-shot.origin).normalized,shot.target);
                    else if(shot.player!=coop.LocalClientId&&shot.weapon>=0&&shot.weapon<17)PlayEffect(weaponSounds[shot.weapon].shot,.5f);
                receivedWorldSequence=world.sequence;lastWorldAt=Time.unscaledTime;SendCoopReceipt(NetworkManager.ServerClientId,receivedWorldSequence);
            }
            catch(Exception ex){Debug.LogWarning("Invalid world snapshot: "+ex.Message);}
        }
        void ApplyCoopEnemies(CoopEnemyState[] states)
        {
            var ids=new HashSet<int>(states.Select(s=>s.id));foreach(var e in enemies.ToArray())if(!ids.Contains(e.netId)){enemies.Remove(e);coopEnemyYaw.Remove(e.netId);if(e.zombie!=null)e.zombie.Die();else Destroy(e.body.gameObject);}
            var existing=enemies.ToDictionary(e=>e.netId);
            foreach(var state in states)
            {
                bool alreadyPresent=existing.TryGetValue(state.id,out var e);
                if(e==null){int saved=spawnSerial;spawnSerial=state.variant;SpawnEnemy(state.position);spawnSerial=saved;e=enemies[enemies.Count-1];e.netId=state.id;e.zombie?.ConfigureMotion(state.id);}
                if(e.zombie!=null)e.zombie.Runner=state.runner;
                if(alreadyPresent&&e.hitSerial!=state.hitSerial&&e.zombie!=null)e.zombie.ReactToHit(state.hitDirection,state.hitRegion,state.hitDamage);
                bool newAttack = e.attackSerial != state.attackSerial;
                e.attackTime=state.attackTime;e.specialAttack=(EnemySpecial)state.special;e.attackSerial=state.attackSerial;e.hitSerial=state.hitSerial;
                e.position=state.position;e.hp=state.hp;coopEnemyYaw[e.netId]=state.yaw;
                if (newAttack) screamerPings.Remove(e);
                if (IsScreaming(e) && !screamerPings.ContainsKey(e)) PlayScreamerCall(e);
                if(e.zombie!=null){for(int part=0;part<6;part++)if((state.mask&(1<<part))==0&&e.zombie.parts[part].enabled){if(part<5)e.zombie.Sever(part,Vector3.forward,actors);else e.zombie.parts[part].enabled=false;}e.zombie.SyncLegInjuries(state.legInjuries);e.zombie.SyncCollapseTime(state.collapseRemaining);}
            }
        }
        readonly Dictionary<int,float> coopDefenseHp=new Dictionary<int,float>();
        void ApplyCoopDefenses(CoopDefenseState[] states)
        {
            var ids=new HashSet<int>(states.Select(s=>s.id));
            foreach(var b in barricades.ToArray())if(!ids.Contains(b.netId)){b.body.GetComponent<EntranceBoards>()?.SetHealth(0);barricades.Remove(b);b.body.gameObject.SetActive(false);Destroy(b.body.gameObject);}
            foreach(var b in turrets.ToArray())if(!ids.Contains(b.netId)){turrets.Remove(b);b.body.gameObject.SetActive(false);Destroy(b.body.gameObject);}
            foreach(var b in traps.ToArray())if(!ids.Contains(b.netId)){traps.Remove(b);b.body.gameObject.SetActive(false);Destroy(b.body.gameObject);}
            foreach(var s in states)
            {
                if(s.type==0){var b=barricades.FirstOrDefault(x=>x.netId==s.id);if(b==null){b=new Barricade{netId=s.id,bounds=DefenseBounds(s.position,0,s.vertical),body=DepotModels.Barricade(this,actors,s.position+Vector3.up*.6f,s.vertical)};barricades.Add(b);}b.hp=s.hp;}
                else if(s.type==1){var b=turrets.FirstOrDefault(x=>x.netId==s.id);if(b==null){Transform head;var body=DepotModels.Turret(this,actors,s.position,out head);b=new Turret{netId=s.id,bounds=DefenseBounds(s.position,1,false),body=body,head=head};turrets.Add(b);}b.hp=s.hp;b.head.rotation=Quaternion.Euler(0,s.yaw,0);}
                else if(s.type==2){var b=traps.FirstOrDefault(x=>x.netId==s.id);if(b==null){b=new Trap{netId=s.id,position=s.position,body=DepotModels.SlowTrap(this,actors,s.position)};traps.Add(b);}b.hp=s.hp;}
                var target=DefenseById(s.id,out int _,out float _);if(target!=null){var c=target.GetComponent<DefenseConstruction>()??target.gameObject.AddComponent<DefenseConstruction>();bool changed=c.hits!=s.hits||c.paid!=s.paid||!coopDefenseHp.TryGetValue(s.id,out float oldHp)||oldHp!=s.hp;c.type=s.type;c.hits=s.hits;c.paid=s.paid;if(s.type<2)DefensePhysics(target,DefenseBounds(s.position,s.type,s.vertical),s.type==0?1.2f:1.5f);if(changed){UpdateConstructionAppearance(target,s.hp);coopDefenseHp[s.id]=s.hp;}}
            }
        }
        void ApplyCoopPickups(CoopPickupState[] states)
        {
            var ids=new HashSet<int>(states.Select(s=>s.id));foreach(var p in pickups.ToArray())if(!ids.Contains(p.netId))RemoveWeaponPickup(p);
            foreach(var s in states)if(!pickups.Any(p=>p.netId==s.id)){var p=SpawnPickup(s.weapon,s.position,s.ammo,s.reserve,s.waveDrop);if(p!=null)p.netId=s.id;}
        }
        void ApplyCoopSupplies(CoopSupplyState[] states)
        {
            var ids=new HashSet<int>(states.Select(s=>s.id));foreach(var b in supplyBoxes.ToArray())if(!ids.Contains(b.id)){supplyBoxes.Remove(b);b.body.gameObject.SetActive(false);Destroy(b.body.gameObject);}
            foreach(var s in states)if(!supplyBoxes.Any(b=>b.id==s.id))CreateSupplyBox(s.position,s.health,s.id);
        }
        void ApplyCoopProjectiles(CoopProjectileState[] states)
        {
            var ids=new HashSet<int>(states.Select(s=>s.id));foreach(var id in coopProjectileVisuals.Keys.ToArray())if(!ids.Contains(id)){if(coopProjectileVisuals[id]!=null)Destroy(coopProjectileVisuals[id].gameObject);coopProjectileVisuals.Remove(id);}
            foreach(var s in states){if(!coopProjectileVisuals.TryGetValue(s.id,out var body)||body==null){body=s.acid ? CreateAcidVisual(s.position,s.pool,s.id,s.life) : Box(s.grenade?"Network grenade":"Network bolt",s.position,s.grenade?Vector3.one*.085f:new Vector3(.018f,.018f,.35f),s.grenade?yellowMaterial:brassMaterial,actors).transform;coopProjectileVisuals[s.id]=body;}body.position=s.position;if(s.acid)body.GetComponent<SpitterAcidVisual>().SyncLifetime(s.life);}
        }
    }
}

