#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
namespace YardSurvivalPrototype
{
 public sealed partial class YardSurvival
 {
  [Serializable] sealed class CoopQAState
  {public int pendingWorlds,pendingMoves,worldReceived,inputReceived;public long networkBytes;public float snapshotAge;public bool listening,host,guest,busy,fighting,dead,lobby,hostReady,guestReady,started;public string code,status;public ulong client;public int epoch,wave,kills,health,ammo,reserve,weapon,scrap,peerHealth,peerAmmo,peerReserve,chestStage,chestWeapon;public int slotBet,slotStake,slotPayout,slotSerial;public bool slotBusy;public int[] inventoryWeapons,inventoryAmmo,inventoryReserve;public Vector3 player,peer;public Vector3[] enemyPositions,pickupPositions,defensePositions,supplyPositions;public float yaw,pitch,seconds;public int frame;public float fps,weaponPose,partnerPose,zombiePose;public int[] enemyIds,enemyHp,pickupIds,defenseIds,constructionHits,supplyIds;}
#if UNITY_WEBGL && !UNITY_EDITOR
  [DllImport("__Internal")] static extern void D27_CoopQAState(string json);
#endif
  [UnityEngine.Scripting.Preserve] public void CoopQAHost(){if(frontScreen==FrontScreen.Home)HostCoop();}
  [UnityEngine.Scripting.Preserve] public void CoopQAJoin(string code){if(frontScreen==FrontScreen.Home){joinCode=code;JoinCoop();}}

  bool qaCombatArena;
  CoopQAInputDriver qaDriver;
  [UnityEngine.Scripting.Preserve] public void CoopQAAction(string action)
  {
   if(action=="combat-arena"&&IsCoopHost){qaCombatArena=true;foreach(var enemy in enemies.ToArray())Destroy(enemy.body.gameObject);enemies.Clear();remainingToSpawn=0;fighting=false;nextWaveCountdown=-1;return;}
   if(action=="ready"){SetCoopReady(true);return;}if(action=="unready"){SetCoopReady(false);return;}
   if(action=="solo"){if(frontScreen==FrontScreen.Home){StopCoop();Application.runInBackground=true;PlayFromMenu();}return;}
   if(action=="home"){if(qaDriver!=null)qaDriver.forward=qaDriver.back=qaDriver.left=qaDriver.right=qaDriver.fire=qaDriver.aim=false;ShowHome();return;}
   if(action=="restart"){if(dead){if(IsCoopGuest)SendCoopCommand("restart");else if(!HasCoopPartner||peer.health<=0)PlayFromMenu();}return;}
   if(action=="pause"){if(frontScreen==FrontScreen.Game&&!dead)PauseFromGame();return;}
   if(action=="resume"){if(frontScreen==FrontScreen.Pause)ResumeFromMenu();return;}
   if(frontScreen!=FrontScreen.Game||dead)return;
   switch(action){case "fire":Fire(arenaCamera.transform.forward);break;case "reload":BeginReload();break;
    case "slots-near":player.position=SlotApproach+Vector3.up*.65f;if(IsCoopGuest)SendCoopCommand("qa-slot-near");break;
    case "slots-up":ChangeSlotBet(1);break;case "slots-down":ChangeSlotBet(-1);break;case "slots-spin":SpinSlots();break;
    case "revive":InteractCoopPartner();break;
    case "pickup":if(!InteractCoopPartner()&&!InteractLuckyChest())TakePickup(NearbyPickup());break;
    case "hammer":ToggleBuilding();break;case "barricade":SelectDefense(0);break;case "turret":SelectDefense(1);break;case "trap":SelectDefense(2);break;
    case "strike":if(building)StartHammer(new Ray(arenaCamera.transform.position,arenaCamera.transform.forward));break;case "rotate":rotated=!rotated;break;
    case "flashlight":flashlightOn=!flashlightOn;UpdateFlashlight();break;}
  }
  [UnityEngine.Scripting.Preserve] public void CoopQAView(string value)
  {
   var parts=value.Split(',');if(parts.Length!=2)return;
   if(float.TryParse(parts[0],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float y)&&float.TryParse(parts[1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float p)&&Finite(y)&&Finite(p)){yaw=Mathf.Repeat(y+180,360)-180;pitch=Mathf.Clamp(p,-89,75);}
  }
  [UnityEngine.Scripting.Preserve] public void CoopQAEquip(string value){if(frontScreen==FrontScreen.Game&&!IsCoopGuest&&int.TryParse(value,out int id)&&id>=0&&id<WeaponCatalog.All.Length)EquipWeapon(id,WeaponCatalog.All[id].capacity);}
  [UnityEngine.Scripting.Preserve] public void CoopQASwitch(string value){if(int.TryParse(value,out int slot))SwitchInventorySlot(slot-1);}
  [UnityEngine.Scripting.Preserve] public void CoopQAAim(string value){EnsureQADriver();qaDriver.aim=value=="on";if(qaDriver.aim)CaptureMouse();else ReleaseMouse();}
  void EnsureQADriver(){if(qaDriver==null){qaDriver=gameObject.AddComponent<CoopQAInputDriver>();qaDriver.game=this;}}
  [UnityEngine.Scripting.Preserve] public void CoopQAMove(string action)
  {
   EnsureQADriver();
   switch(action){case "forward":qaDriver.forward=!qaDriver.forward;break;case "back":qaDriver.back=!qaDriver.back;break;case "left":qaDriver.left=!qaDriver.left;break;case "right":qaDriver.right=!qaDriver.right;break;case "stop":qaDriver.forward=qaDriver.back=qaDriver.left=qaDriver.right=false;qaDriver.fire=qaDriver.aim=false;break;case "fire":qaDriver.fire=!qaDriver.fire;if(qaDriver.fire)CaptureMouse();else ReleaseMouse();break;}
  }
  [UnityEngine.Scripting.Preserve] public void CoopQAApplyMovement(bool forward,bool back,bool left,bool right,bool fire,bool aim)
  {
   var keyboard=UnityEngine.InputSystem.Keyboard.current;if(keyboard==null)return;
   bool enabled=frontScreen==FrontScreen.Game&&!dead&&!showHelp;
   if(UnityEngine.InputSystem.Mouse.current!=null)
   {
    var mouse=UnityEngine.InputSystem.Mouse.current;
    var state=new UnityEngine.InputSystem.LowLevel.MouseState{position=mouse.position.ReadValue()}
     .WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left,enabled&&fire)
     .WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right,enabled&&aim);
    UnityEngine.InputSystem.LowLevel.InputState.Change(mouse,state);
   }
   var keys=new System.Collections.Generic.List<UnityEngine.InputSystem.Key>();
   foreach(var pair in new[]{(Control.Forward,forward),(Control.Back,back),(Control.Left,left),(Control.Right,right)})
   {int code=bindings[(int)pair.Item1].primary;if(code>0&&enabled&&pair.Item2)keys.Add((UnityEngine.InputSystem.Key)code);}
   UnityEngine.InputSystem.LowLevel.InputState.Change(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(keys.ToArray()));
  }
  static float QAPose(Transform root)
  {
   if(root==null)return 0;float sum=0;int index=0;
   foreach(var bone in root.GetComponentsInChildren<Transform>()){var q=bone.localRotation;var p=bone.localPosition;sum+=(++index)*(q.x+q.y*2+q.z*3+q.w*.5f+p.x*.1f+p.y*.2f+p.z*.3f);}return sum;
  }
  [UnityEngine.Scripting.Preserve] public void CoopQAPublish()
  {
   // Keep the visible development controls accessible while the runtime is tested.
   if(frontScreen==FrontScreen.Game&&(qaDriver==null||(!qaDriver.aim&&!qaDriver.fire)))ReleaseMouse();
   var state=new CoopQAState{lobby=coopLobby,hostReady=coopHostReady,guestReady=coopGuestReady,started=coopStarted,listening=coop!=null&&coop.IsListening,host=IsCoopHost,guest=IsCoopGuest,busy=coopBusy,code=coopCode,status=coopStatus,client=NetworkManagerLocalId(),epoch=coopEpoch,wave=wave,kills=kills,health=health,dead=dead,fighting=fighting,ammo=ammunition,reserve=reserveAmmo,weapon=weaponId,chestStage=chestStage,chestWeapon=chestWeapon,scrap=supplies,seconds=Time.unscaledTime,frame=Time.frameCount,yaw=yaw,pitch=pitch,enemyPositions=enemies.Select(e=>e.position).ToArray(),pickupPositions=pickups.Select(p=>p.position).ToArray(),supplyPositions=supplyBoxes.Select(b=>b.position).ToArray(),player=player!=null?player.position:Vector3.zero,peer=peer!=null?peer.position:replicaPeerPosition,peerHealth=peer!=null?peer.health:replicaPeerHealth,peerAmmo=peer?.ammo??0,peerReserve=peer?.reserve??0,enemyIds=enemies.Select(e=>e.netId).ToArray(),enemyHp=enemies.Select(e=>e.hp).ToArray(),pickupIds=pickups.Select(p=>p.netId).ToArray(),defenseIds=barricades.Select(b=>b.netId).Concat(turrets.Select(b=>b.netId)).Concat(traps.Select(b=>b.netId)).ToArray(),supplyIds=supplyBoxes.Select(b=>b.id).ToArray()};
   state.pendingWorlds=pendingWorlds.Count;state.pendingMoves=pendingMoves.Count;state.worldReceived=receivedWorldSequence;state.inputReceived=receivedInputSequence;state.networkBytes=coopSentBytes;state.snapshotAge=lastWorldAt>0?Time.unscaledTime-lastWorldAt:0;
   state.slotBet=slotBet;state.slotStake=slotStake;state.slotPayout=slotPayout;state.slotSerial=slotSerial;state.slotBusy=slotBusy;
   state.fps=1/Mathf.Max(.001f,Time.unscaledDeltaTime);state.weaponPose=QAPose(viewWeapon!=null?viewWeapon.transform:null);state.partnerPose=QAPose(peerAvatar);state.zombiePose=QAPose(enemies.FirstOrDefault()?.zombie?.transform);
   CaptureInventoryWeapon();state.inventoryWeapons=weaponInventory.Select(w=>w.weapon).ToArray();state.inventoryAmmo=weaponInventory.Select(w=>w.ammo).ToArray();state.inventoryReserve=weaponInventory.Select(w=>w.reserve).ToArray();
   state.defensePositions=state.defenseIds.Select(id=>DefenseById(id,out int _,out float _).position).ToArray();
   state.constructionHits=state.defenseIds.Select(id=>{var body=DefenseById(id,out int _,out float _);var c=body.GetComponent<DefenseConstruction>();return c!=null?c.hits:-1;}).ToArray();
   string json=JsonUtility.ToJson(state);
#if UNITY_WEBGL && !UNITY_EDITOR
   D27_CoopQAState(json);
#else
   Debug.Log(json);
#endif
  }
 }
 [DefaultExecutionOrder(-32000)] public sealed class CoopQAInputDriver:MonoBehaviour {public YardSurvival game;public bool forward,back,left,right,fire,aim;void Update(){if(game!=null)game.CoopQAApplyMovement(forward,back,left,right,fire,aim);}}
}
#endif


