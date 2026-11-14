#if UNITY_EDITOR
using System;
using System.Text;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunDedicatedChecks()
        {
            var report = new StringBuilder();
            Action<bool,string> check = (ok,label) => { if (!ok) throw new Exception(label); report.AppendLine("PASS " + label); };
            enabled = false;
            try
            {
                check(TryDedicatedEndpoint("wss://example.com:12345",out var endpoint) && endpoint.Port == 12345,"Secure endpoint preserves proxy external port");
                check(TryDedicatedEndpoint("ws://127.0.0.1:7777",out _),"Local unencrypted test endpoint");
                foreach (var invalid in new[] {"http://example.com", "ws://example.com:7777", "wss://user:secret@example.com", "wss://example.com/path", "wss://example.com?token=secret"})
                    check(!TryDedicatedEndpoint(invalid,out _),"Reject unsupported endpoint " + invalid);
                StartDedicatedServer();
                check(coop.IsServer && !coop.IsClient && !coop.IsHost,"Server has no local client or hidden host player");
                DedicatedConnected(101); DedicatedConnected(202);
                check(dedicatedFirst.clientId == 101 && peer.clientId == 202,"Two distinct remote seats");
                StartDedicatedMatch();
                check(coopStarted && !coopLobby && wave == 1,"Match starts authoritative wave");
                var before = player.position;
                WithDedicatedSeat(101, () => { peer.movement = Vector2.up; coopInputAt = Time.unscaledTime; UpdatePeer(.05f); });
                check(player.position.z > before.z && peer.position.z == 0,"First remote player movement isolated from second");
                WithDedicatedSeat(202, () => { peer.movement = Vector2.right; coopInputAt = Time.unscaledTime; UpdatePeer(.05f); });
                check(peer.position.x > 1.5f && player.position.x == before.x,"Second remote player movement isolated from first");
                int firstAmmo = ammunition;
                WithDedicatedSeat(101, () => { peer.ammo -= 2; peer.health = 64; });
                check(ammunition == firstAmmo - 2 && health == 64 && peer.health == 100,"First seat ammo and health preserved");
                WithDedicatedSeat(202, () => peer.ammo -= 1);
                check(peer.ammo == firstAmmo - 1 && ammunition == firstAmmo - 2,"Second seat ammo does not overwrite first");
                WithDedicatedSeat(101, () => {
                    check(FriendlyTarget(101,out var targetPeer,out _) && !targetPeer,"First shooter targets other remote seat");
                    DamageFriendlyPlayer(false,1,player.position,Vector3.forward);
                });
                check(peer.health == 90 && health == 64,"Friendly damage applied to correct player");
                WithDedicatedSeat(202, () => DamageFriendlyPlayer(false,1,player.position,Vector3.forward));
                check(health == 54 && peer.health == 90,"Reverse friendly damage applied to correct player");
                health = 0; dead = true; StoreLocalSeat(dedicatedFirst);
                peer.position = player.position + Vector3.right * .5f;
                WithDedicatedSeat(202, () => check(ReviveCoopPartner(true),"Second client revives first"));
                check(health == 50 && !dead,"Revival persisted in first seat");
                peer.health = 0;
                WithDedicatedSeat(101, () => check(ReviveCoopPartner(true),"First client revives second"));
                check(peer.health == 50 && health == 50,"Reverse revival preserves both seats");
                var drop = SpawnPickup(16,dedicatedFirst.position - Vector3.up * .65f,5);
                WithDedicatedSeat(101, () => PeerPickup(drop.netId));
                check(weaponId == 16 && peer.weapon == WeaponCatalog.Starter,"Weapon pickup belongs only to requesting dedicated client");
                var first = dedicatedFirst;
                try { WithDedicatedSeat(101, () => throw new InvalidOperationException("test")); } catch (InvalidOperationException) { }
                check(dedicatedFirst == first && peer.clientId == 202 && weaponId == 16,"Seat context restored after exception");
                DedicatedDisconnected(101);
                check(dead && HasCoopPartner && peer.health > 0 && coop.IsListening,"First disconnect leaves server and second player alive");
                DedicatedConnected(303);
                check(dedicatedFirst.clientId == 303 && peer.clientId == 202,"Vacant seat can be joined without replacing survivor");
                DedicatedDisconnected(303); DedicatedDisconnected(202);
                check(coopLobby && !coopStarted && dedicatedPending.Count == 0 && coop.IsListening,"Empty server returns to lobby for a fresh match");
                return report + "ALL DEDICATED STATE CHECKS PASSED\n";
            }
            finally { StopCoop(); }
        }
    }
}
#endif
