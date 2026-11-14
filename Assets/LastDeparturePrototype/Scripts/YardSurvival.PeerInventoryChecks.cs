#if UNITY_EDITOR
using System;using System.Text;using UnityEngine;
namespace YardSurvivalPrototype {public sealed partial class YardSurvival {
        void GrantPeerTestWeapon(int id)
        {
            MakePeerWeaponRoom(id);peer.unarmed=false;peer.weapon=id;peer.ammo=WeaponCatalog.All[id].capacity;
            peer.reserve=peer.ammo*3;peer.reload=0;peer.fire=.18f;CapturePeerInventory();
        }
public string RunPeerInventoryChecks(){var old=peer;var log=new StringBuilder();Action<bool,string>check=(ok,label)=>log.AppendLine((ok?"PASS ":"FAIL ")+label);try{
peer=new PeerPlayer();CapturePeerInventory();check(peer.inventory.Count==1&&peer.inventory[0].weapon==WeaponCatalog.Starter,"Peer begins with starter only");
peer.ammo=8;peer.reserve=17;peer.fire=.6f;GrantPeerTestWeapon(13);
check(peer.inventory.Count==2&&peer.weapon==13&&peer.ammo==WeaponCatalog.All[13].capacity&&peer.reserve==peer.ammo*3,"Test grant adds firearm and full finite ammunition");
peer.ammo=7;peer.reserve=40;peer.fire=.8f;peer.reload=1;
check(SwitchPeerWeapon(WeaponCatalog.Starter)&&peer.ammo==8&&peer.reserve==17&&peer.fire>=.59f&&peer.reload==0,"Peer switch restores starter ammo and preserves shot cooldown, cancels reload");
check(SwitchPeerWeapon(13)&&peer.ammo==7&&peer.reserve==40&&peer.fire>=.79f,"Peer switch restores second weapon ammunition and cooldown");
check(!SwitchPeerWeapon(16)&&peer.weapon==13,"Host rejects unowned peer weapon switch");
GrantPeerTestWeapon(13);check(peer.inventory.Count==2,"Repeated test grant cannot duplicate peer slot");
int before=peer.weapon;PeerPickup(99);check(peer.weapon==before,"Stale peer ground-pickup command is inert");
GrantPeerTestWeapon(0);GrantPeerTestWeapon(3);peer.ammo=2;peer.reserve=7;GrantPeerTestWeapon(14);
check(peer.inventory.Count==4&&peer.weapon==14&&!peer.inventory.Exists(w=>w.weapon==3),"Peer full inventory swaps equipped weapon");
check(pickups.Exists(w=>w.weapon==3&&w.ammo==2&&w.reserve==7),"Peer swap preserves dropped ammunition");
while(peer.inventory.Count>0)if(!DropPeerWeapon(peer.inventory[0].weapon))throw new Exception("Peer drop failed");
CapturePeerInventory();check(peer.unarmed&&peer.inventory.Count==0,"Snapshot capture does not recreate a dropped last weapon");
GrantPeerTestWeapon(12);check(!peer.unarmed&&peer.inventory.Count==1,"Unarmed peer can acquire a weapon again");
return log+"COMPLETE peer inventory helpers "+DateTime.Now.ToString("s")+". Actual two-browser synchronization test remains required.";
}finally{peer=old;}}
}}
#endif
