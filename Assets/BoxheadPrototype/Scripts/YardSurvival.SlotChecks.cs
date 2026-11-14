#if UNITY_EDITOR
using System;
using System.Text;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunSlotChecks()
        {
            var report=new StringBuilder();Action<bool,string> check=(ok,label)=>report.AppendLine((ok?"PASS ":"FAIL ")+label);
            int wins=0,total=0;for(int roll=0;roll<100;roll++){int m=SlotMultiplier(roll);if(m>0)wins++;total+=m;}
            check(wins==65&&total==160,"Exact 65% win odds and 160% expected return, including stake");
            PlayFromMenu();enabled=false;supplies=12;player.position=SlotApproach+Vector3.up*.65f;
            var sourceMesh=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BoxheadPrototype/ThirdParty/BlenderKitSlots/GamingSlotMachine.fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
            bool importedMesh=false;foreach(var mesh in slotMachine.GetComponentsInChildren<MeshFilter>())if(mesh.sharedMesh==sourceMesh)importedMesh=true;
            check(importedMesh,"Downloaded BlenderKit mesh instantiated in the actual game");
            Ray minus=new Ray(slotPosition+new Vector3(.25f,1.05f,2),Vector3.back);
            Ray plus=new Ray(slotPosition+new Vector3(0,1.05f,2),Vector3.back);
            Ray spin=new Ray(slotPosition+new Vector3(-.25f,1.05f,2),Vector3.back);
            check(SlotTarget(minus)==0&&SlotTarget(plus)==1&&SlotTarget(spin)==2,"Existing machine button hit areas select minus, plus, spin");
            HandleSlotClick(plus);check(slotBet==2,"Clicking model plus button increases stake");HandleSlotClick(minus);check(slotBet==1,"Clicking model minus button decreases stake");
            check(HandleSlotClick(spin)&&slotBusy&&supplies==11,"Clicking model spin button consumes click and starts paid spin");
            UpdateSlots(3);supplies=12;
            check(!HandleSlotClick(new Ray(slotPosition+Vector3.up,Vector3.up)),"Clicks away from machine are not intercepted");
            player.position=Vector3.zero;check(!HandleSlotClick(spin),"Remote clicks cannot operate the machine");player.position=SlotApproach+Vector3.up*.65f;
            slotBet=1;ChangeSlotBet(-100);check(slotBet==1,"Minimum bet is one");ChangeSlotBet(1000);check(slotBet==12,"Bet capped to available scrap");
            check(!TrySlotSpin(player.position,0,true)&&!TrySlotSpin(player.position,101,true)&&!TrySlotSpin(player.position,13,true)&&supplies==12,"Invalid / unaffordable bets rejected without charging");
            check(!TrySlotSpin(Vector3.zero,1,true)&&!TrySlotSpin(player.position,1,false),"Distant and dead players cannot spin");
            check(TrySlotSpin(player.position,4,true)&&supplies==8,"Accepted spin deducts stake exactly once");
            int frozenBet=slotBet;ChangeSlotBet(1);check(slotBet==frozenBet,"Stake buttons cannot change during a spin");
            check(!TrySlotSpin(player.position,4,true)&&supplies==8,"Concurrent spin cannot spend again");
            slotMultiplier=5;slotPayout=20;UpdateSlots(3);check(!slotBusy&&supplies==28,"Jackpot settles once with gross payout");UpdateSlots(3);check(supplies==28,"Repeated update cannot pay twice");
            TrySlotSpin(player.position,4,true);slotMultiplier=0;slotPayout=0;UpdateSlots(3);check(supplies==24,"Losing spin only costs the stake");
            check(!CanPlaceDefense(slotPosition,0,false)&&!Free(slotPosition,.4f,true),"Machine blocks construction and movement");
            ResetRun();check(!slotBusy&&slotSerial==0&&slotBet==1&&supplies==12,"New run resets machine and bet");
            player.position=SlotApproach+Vector3.up*.65f;slotBet=2;SpinSlots();check(slotBusy&&slotStake==2&&supplies==10,"Spin button action starts a real paid spin");ResetRun();
            var prefab=UnityEngine.Object.Instantiate(slotMachine.gameObject);prefab.transform.position=Vector3.zero;
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/BoxheadPrototype/ScrapSlots.prefab");DestroyImmediate(prefab);
            return report.ToString();
        }
        public void PrepareSlotPreview()
        {
            PlayFromMenu();remainingToSpawn=0;nextWaveCountdown=-1;fighting=false;
            player.position=slotPosition+new Vector3(0,.65f,2.7f);yaw=180;pitch=8;
            player.rotation=Quaternion.Euler(0,yaw,0);arenaCamera.transform.localRotation=Quaternion.Euler(pitch,0,0);
            ReleaseMouse();
        }
    }
}
#endif
