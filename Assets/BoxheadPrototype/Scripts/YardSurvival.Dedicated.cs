using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        // The existing simulation has two seats: local player fields and `peer`.
        // On a dedicated server both seats are remote; no hidden host player exists.
        PeerPlayer dedicatedFirst;
        bool dedicatedServer, dedicatedConnection, dedicatedMenu;
        string dedicatedEndpoint = "", dedicatedRoom = "";
        float dedicatedStartedAt, dedicatedSnapshotAt, dedicatedLobbyAt;
        readonly Dictionary<ulong, Queue<int>> dedicatedPending = new Dictionary<ulong, Queue<int>>();
        readonly Dictionary<ulong, float> dedicatedInputAt = new Dictionary<ulong, float>();
        const string DedicatedProtocol = "D27-Dedicated-v2-waves";
        const int DedicatedPort = 7777;
        ulong LocalSeatId => dedicatedServer && dedicatedFirst != null ? dedicatedFirst.clientId : NetworkManager.ServerClientId;

        static string StartupArgument(string key, string fallback = "")
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == key) return args[i + 1];
            return fallback;
        }



        void StartDedicatedServer() => throw new NotSupportedException("Dedicated servers are no longer supported; this game is single-player only.");

        public static bool TryDedicatedEndpoint(string value, out Uri endpoint)
        {
            endpoint = null;
            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)) return false;
            bool local = uri.IsLoopback && uri.Scheme == "ws";
            if ((!local && uri.Scheme != "wss") || uri.Port < 1 || uri.Port > 65535 ||
                uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0) return false;
            endpoint = uri; return true;
        }

        public void ConnectDedicatedServer(string endpoint, string roomCode = "") =>
            throw new NotSupportedException("This game is single-player only.");

        void DedicatedConnected(ulong id)
        {
            var seat = new PeerPlayer { clientId = id, connected = true };
            seat.inventory.Add(new InventoryWeapon { weapon = seat.weapon, ammo = seat.ammo, reserve = seat.reserve });
            dedicatedPending[id] = new Queue<int>(); dedicatedInputAt[id] = Time.unscaledTime;
            if (dedicatedFirst == null || !dedicatedFirst.connected)
            {
                dedicatedFirst = seat; seat.position = new Vector3(0, .65f, 0); LoadLocalSeat(seat); coopHostReady = false;
            }
            else { peer = seat; coopGuestReady = false; }
            if (!coopLobby && coopStarted)
            {
                SpawnWaveWeapons(fighting ? wave : wave + 1);
                StoreLocalSeat(dedicatedFirst);
            }
            dedicatedLobbyAt = dedicatedSnapshotAt = 0;
            Debug.Log("Dedicated client connected: " + id);
        }

        void DedicatedDisconnected(ulong id)
        {
            if (dedicatedFirst != null && dedicatedFirst.clientId == id)
            { dedicatedFirst.connected = false; dedicatedFirst.health = 0; LoadLocalSeat(dedicatedFirst); coopHostReady = false; }
            if (peer != null && peer.clientId == id) { peer.connected = false; peer.health = 0; coopGuestReady = false; }
            dedicatedPending.Remove(id); dedicatedInputAt.Remove(id);
            dedicatedLobbyAt = 0;
            if (dedicatedPending.Count == 0)
            {
                coopLobby = true; coopStarted = false;
                coopHostReady = coopGuestReady = false;
                frontScreen = FrontScreen.Lobby;
                coopShots.Clear();
            }
        }

        void StoreLocalSeat(PeerPlayer seat)
        {
            seat.position = player.position; seat.yaw = yaw; seat.pitch = pitch;
            seat.health = health; seat.weapon = weaponId; seat.ammo = ammunition; seat.reserve = reserveAmmo;
            seat.fire = fireTimer; seat.reload = reloading ? reloadTimer : 0; seat.hurt = damageTimer;
            seat.skin = selectedPlayerSkin; seat.slideTime = slideTimer; seat.slideCooldown = slideCooldown;
            seat.jumpHeight = jumpHeight; seat.verticalSpeed = verticalSpeed; seat.slideDirection = slideDirection;
            seat.knockdown = knockdownTime; seat.knockback = knockdownVelocity; seat.dodge = dodge;
            seat.reviveProtectedUntil = coopReviveProtectedUntil;
            seat.unarmed=!HasEquippedWeapon; CaptureInventoryWeapon(); seat.inventory.Clear(); seat.inventory.AddRange(weaponInventory);
        }

        void LoadLocalSeat(PeerPlayer seat)
        {
            player.position = seat.position; yaw = seat.yaw; pitch = seat.pitch;
            health = seat.health; dead = health <= 0; weaponId = seat.weapon; ammunition = seat.ammo; reserveAmmo = seat.reserve;
            fireTimer = seat.fire; reloadTimer = seat.reload; reloading = reloadTimer > 0; damageTimer = seat.hurt;
            selectedPlayerSkin = seat.skin; slideTimer = seat.slideTime; slideCooldown = seat.slideCooldown;
            jumpHeight = seat.jumpHeight; verticalSpeed = seat.verticalSpeed; slideDirection = seat.slideDirection;
            knockdownTime = seat.knockdown; knockdownVelocity = seat.knockback; dodge = seat.dodge;
            coopReviveProtectedUntil = seat.reviveProtectedUntil;
            weaponInventory.Clear(); weaponInventory.AddRange(seat.inventory);
        }

        // Temporarily map the first client to the existing remote-seat handlers.
        // Save both seats in finally so damage, inventory and revival stay isolated.
        void WithDedicatedSeat(ulong id, Action action)
        {
            if (dedicatedFirst == null) return;
            bool first = dedicatedFirst.clientId == id;
            if (first)
            {
                StoreLocalSeat(dedicatedFirst);
                var other = peer ?? new PeerPlayer { health = 0, connected = false };
                peer = dedicatedFirst; dedicatedFirst = other;
                LoadLocalSeat(other);
            }
            try { action(); }
            finally
            {
                if (first)
                {
                    StoreLocalSeat(dedicatedFirst);
                    var originalFirst = peer; peer = dedicatedFirst; dedicatedFirst = originalFirst;
                    LoadLocalSeat(dedicatedFirst);
                }
                else StoreLocalSeat(dedicatedFirst);
            }
        }

        void ReceiveDedicatedInput(ulong id, CoopInput input)
        {
            if (input == null || !dedicatedPending.ContainsKey(id)) return;
            var seat = dedicatedFirst != null && dedicatedFirst.clientId == id ? dedicatedFirst : peer;
            if (seat == null || !seat.connected || input.sequence <= seat.lastSequence) return;
            if (coopLobby)
            {
                seat.lastSequence = input.sequence;
                if (input.action == "lobby-ready")
                {
                    if (seat == dedicatedFirst) coopHostReady = input.type == 1; else coopGuestReady = input.type == 1;
                    if (dedicatedFirst != null && dedicatedFirst.connected && HasCoopPartner && coopHostReady && coopGuestReady) StartDedicatedMatch();
                }
                return;
            }
            if (input.action == "restart")
            {
                if (health <= 0 && (peer == null || peer.health <= 0)) StartDedicatedMatch();
                return;
            }
            WithDedicatedSeat(id, () => ApplyCoopInput(input));
            dedicatedInputAt[id] = Time.unscaledTime;
            SendCoopReceipt(id, seat.lastSequence);
        }

        void StartDedicatedMatch()
        {
            var first = dedicatedFirst; var second = peer;
            ResetRun();
            dedicatedFirst = FreshSeat(first, new Vector3(0, .65f, 0));
            peer = FreshSeat(second, new Vector3(1.5f, .65f, 0));
            foreach (var queue in dedicatedPending.Values) queue.Clear();
            LoadLocalSeat(dedicatedFirst);
            coopLobby = false; coopStarted = true; frontScreen = FrontScreen.Game;
            StartWave(); dedicatedSnapshotAt = 0;
        }

        static PeerPlayer FreshSeat(PeerPlayer old, Vector3 position)
        {
            var seat = new PeerPlayer { clientId = old.clientId, connected = old.connected, skin = old.skin, lastSequence = old.lastSequence, position = position };
            seat.inventory.Add(new InventoryWeapon { weapon = seat.weapon, ammo = seat.ammo, reserve = seat.reserve });
            return seat;
        }

        void DedicatedTick(float dt)
        {
            if (Time.unscaledTime - dedicatedStartedAt >= 3600) { Application.Quit(); return; }
            if (coop == null || !coop.IsListening) return;
            if (coopLobby)
            {
                if (Time.unscaledTime >= dedicatedLobbyAt)
                {
                    dedicatedLobbyAt = Time.unscaledTime + .25f;
                    SendDedicatedLobby(dedicatedFirst, coopGuestReady, coopHostReady, peer != null && peer.connected);
                    SendDedicatedLobby(peer, coopHostReady, coopGuestReady, dedicatedFirst != null && dedicatedFirst.connected);
                }
                return;
            }
            if (dedicatedFirst == null) return;
            foreach (ulong id in new List<ulong>(dedicatedPending.Keys))
                WithDedicatedSeat(id, () => { coopInputAt = dedicatedInputAt[id]; UpdatePeer(dt); });
            UpdateFlashes(dt);
            damageTimer = Mathf.Max(0, damageTimer - dt); impactTimer -= dt;
            TickWaveSimulation(dt); StoreLocalSeat(dedicatedFirst);
            if (Time.unscaledTime < dedicatedSnapshotAt) return;
            dedicatedSnapshotAt = Time.unscaledTime + .05f;
            var shots = coopShots.ToArray();
            foreach (ulong id in new List<ulong>(dedicatedPending.Keys))
            {
                if (dedicatedPending[id].Count >= 4) continue;
                coopShots.Clear(); coopShots.AddRange(shots);
                WithDedicatedSeat(id, SendCoopSnapshot);
            }
            coopShots.Clear();
        }

        void SendDedicatedLobby(PeerPlayer seat, bool otherReady, bool ownReady, bool otherConnected)
        {
            if (seat == null || !seat.connected) return;
            SendCoopJson("D27.Lobby", seat.clientId, JsonUtility.ToJson(new CoopLobbyState {
                hostReady = otherReady, guestReady = ownReady, connected = otherConnected, started = false }));
        }

        void ClearDedicatedSession()
        {
            dedicatedFirst = null; dedicatedServer = dedicatedConnection = false;
            dedicatedPending.Clear(); dedicatedInputAt.Clear();
        }
    }
}
