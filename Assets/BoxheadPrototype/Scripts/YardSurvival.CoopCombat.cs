using Unity.Netcode;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        ulong coopFiringPlayer;
        float coopReviveProtectedUntil;
        const float CoopReviveRange=2.5f;
        bool FriendlyTarget(ulong shooter,out bool targetPeer,out Vector3 position)
        {
            targetPeer=shooter==LocalSeatId;
            position=targetPeer&&peer!=null?peer.position:player.position;
            return HasCoopPartner&&(targetPeer?peer.health>0:health>0);
        }
        bool TraceFriendlyPlayer(Vector3 origin,Vector3 direction,ulong shooter,ref float nearest,out bool targetPeer)
        {
            if(!FriendlyTarget(shooter,out targetPeer,out var position))return false;
            float height=targetPeer&&peer.slideTime>0?.9f:!targetPeer&&slideTimer>0?.9f:1.8f;
            var bounds=new Bounds(position+Vector3.up*(height*.5f-.65f),new Vector3(.65f,height,.65f));
            if(!bounds.IntersectRay(new Ray(origin,direction),out float entry)||entry<0||entry>=nearest)return false;
            nearest=entry;return true;
        }
        void DamageFriendlyPlayer(bool targetPeer,int damage,Vector3 point,Vector3 direction)
        {
            if(!HasCoopPartner)return;
            if(targetPeer)
            {
                if(peer.health<=0||Time.unscaledTime<peer.reviveProtectedUntil)return;
                peer.health=Mathf.Max(0,peer.health-damage*10);
                if(peer.health==0){peer.movement=Vector2.zero;peer.jump=peer.slide=false;peer.reload=peer.slideTime=0;ShowBanner("Partner down · Move closer and press "+ControlHint(Control.Pickup)+".");}
            }
            else
            {
                if(health<=0||Time.unscaledTime<coopReviveProtectedUntil)return;
                health=Mathf.Max(0,health-damage*10);PlayPlayerDamageAudio();
                // PeerFire temporarily uses the guest's camera. Finish death after
                // its finally block restores the host camera.
                if(health==0&&coopFiringPlayer==NetworkManager.ServerClientId)BeginDeathSequence();
            }
            SpawnBlood(point,direction,false);
        }
        bool NearFallenPartner(Vector3 rescuer,Vector3 fallen)
        {
            return Vector3.Distance(rescuer,fallen)<=CoopReviveRange&&PickupLineClear(rescuer,fallen);
        }
        bool CanRevivePartner()
        {
            if(dead||coopLobby)return false;
            return IsCoopHost?HasCoopPartner&&peer.health<=0&&NearFallenPartner(player.position,peer.position):
                IsCoopGuest&&replicaPeerHealth<=0&&NearFallenPartner(player.position,replicaPeerPosition);
        }
        bool InteractCoopPartner()
        {
            if(!CanRevivePartner())return false;
            if(IsCoopGuest)SendCoopCommand("revive");else ReviveCoopPartner(false);
            return true;
        }
        bool ReviveCoopPartner(bool guestRescuer)
        {
            if(!HasCoopPartner||coopLobby)return false;
            if(guestRescuer)
            {
                if(peer.health<=0||!dead||!NearFallenPartner(peer.position,player.position))return false;
                health=50;RestoreCoopPlayer();
            }
            else
            {
                if(dead||peer.health>0||!NearFallenPartner(player.position,peer.position))return false;
                peer.health=50;peer.hurt=1;peer.reviveProtectedUntil=Time.unscaledTime+1;
                peer.jumpHeight=peer.verticalSpeed=peer.slideTime=0;peer.position=OnGround(peer.position,.65f);
                peer.movement=Vector2.zero;peer.jump=peer.slide=false;
            }
            DestroyPeerAvatar();coopSnapshotAt=0;ShowBanner("Partner revived.");return true;
        }
        void RestoreCoopPlayer()
        {
            ResetPlayerDamageVoice();
            ResetRecoil();
            dead=false;deathTime=0;frontScreen=FrontScreen.Game;showHelp=false;building=false;
            reloading=reloadBoltPlayed=false;reloadTimer=cameraKick=weaponKick=aimBlend=0;
            jumpHeight=verticalSpeed=slideTimer=slideCooldown=0;eyeHeight=1;player.position=OnGround(player.position,.65f);
            damageTimer=1;coopReviveProtectedUntil=Time.unscaledTime+1;
            RestoreFrontEndEffects();ResetMovementAudio();ResetWalkWeight();
            arenaCamera.transform.SetParent(player,false);arenaCamera.transform.localPosition=Vector3.up;
            arenaCamera.transform.localRotation=Quaternion.Euler(pitch,0,0);
            if(gun!=null)gun.gameObject.SetActive(HasEquippedWeapon);CaptureMouse();ShowBanner("Revived · 50 HP");
        }
    }
}
