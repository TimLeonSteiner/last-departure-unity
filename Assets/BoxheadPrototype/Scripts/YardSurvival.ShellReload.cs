using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        // Same opening/insert/closing phases as AnimatedWeaponRig; credit each shell as it enters the tube.
        static void TransferReloadShells(float progress,int initial,int count,ref int ammo,ref int reserve)
        {
            int inserted=Mathf.Clamp(Mathf.FloorToInt((progress-.12f)/.73f*count+.28f),0,count);
            int transfer=Mathf.Min(Mathf.Max(0,initial+inserted-ammo),reserve);
            ammo+=transfer;reserve-=transfer;
        }
        void BeginPeerReload()
        {
            if(peer==null||peer.reload>0||peer.reserve<=0)return;
            var weapon=WeaponCatalog.All[peer.weapon];if(peer.ammo>=weapon.capacity||WeaponCatalog.IsMelee(peer.weapon))return;
            peer.reloadStartAmmo=peer.ammo;peer.reloadShellCount=Mathf.Min(weapon.capacity-peer.ammo,peer.reserve);peer.reload=weapon.reload;
        }
    }
}
