#if UNITY_EDITOR
using System;
using System.Text;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunProgressionChecks()
        {
            var report = new StringBuilder();
            Action<bool, string> check = (ok, label) => {
                if (!ok) throw new Exception(label);
                report.AppendLine("PASS " + label);
            };
            enabled = false;
            ResetRun();
            check(typeof(YardSurvival).GetField("luckyChestModel") == null, "Retired chest asset is no longer serialized by the game");
            check(Resources.Load<GameObject>("DustlineSlotMachine") == null, "Retired slot machine is not shipped in Resources");
            check(!weaponInventory.Exists(w => w.weapon == 1), "Minigun locked at start");
            check(weaponInventory.Count == 1 && pickups.Count == 0, "Start with pistol only and no pre-wave drop");
            frontScreen = FrontScreen.Game; wave = 1; fighting = true;
            SpawnWaveWeapons(1);
            check(pickups.Count == 1 && pickups[0].weapon == 14, "Wave one supplies a Persuader");
            var first = pickups[0];
            check(first.marker != null && first.marker.parent == first.body && first.marker.localPosition.y > 0,
                "Wave weapon has a holographic arrow above it");
            check(first.body.GetComponent<Light>() == null, "Wave weapon has no extra dynamic light");
            var markerMaterial = first.marker.GetComponent<MeshRenderer>().sharedMaterial;
            foreach (var renderer in first.body.GetComponentsInChildren<Renderer>())
                if (renderer.transform != first.marker)
                    check(Array.IndexOf(renderer.sharedMaterials, markerMaterial) < 0, "Weapon keeps its own materials");
            SpawnWaveWeapons(1);
            check(pickups.Count == 1 && pickups[0] == first, "Repeated wave calls do not duplicate drops");
            check(weaponInventory.Count == 1, "Weapon remains on the map until collected");
            player.position = first.position + Vector3.up * .65f;
            check(NearbyPickup() == first && TakePickup(first), "Nearby interaction collects the weapon");
            check(pickups.Count == 0 && weaponId == 14 && weaponInventory.Count == 2, "Wave-one reward adds a distinct weapon");
            check(!TakePickup(first), "Collected pickup cannot be claimed twice");
            SpawnWaveWeapons(1); check(pickups.Count == 0, "Collected weapon does not respawn in same wave");
            wave = 2; SpawnWaveWeapons(2); var second = pickups[0];
            check(pickups.Count == 1 && second.weapon == 3, "Wave two supplies a Boomstick");
            player.position = second.position + Vector3.right * 8 + Vector3.up * .65f;
            check(!TakePickup(second), "Distant player cannot collect a weapon");
            peer = new PeerPlayer { position = second.position + Vector3.right * 8, health = 100 };
            PeerPickup(second.netId); check(pickups.Count == 1, "Server rejects distant peer pickup");
            peer.position = second.position; PeerPickup(second.netId);
            check(pickups.Count == 0 && peer.weapon == 3, "Server grants nearby pickup to requesting peer");
            peer = null;
            for (int stage = 3; stage <= 12; stage++)
            {
                wave = stage; SpawnWaveWeapons(stage);
                check(pickups.Count == 1, "Exactly one unclaimed drop at wave " + stage);
                int tier = WeaponCatalog.All[pickups[0].weapon].unlockWave;
                check(stage == 3 ? pickups[0].weapon == 8 : tier <= stage && tier >= (stage >= 7 ? Mathf.Min(stage,9)-2 : stage), "Appropriate weapon tier at wave " + stage);
            }
            check(WaveZombieCount(1) == 32 && WaveZombieCount(2) == 46 && WaveZombieCount(5) == 112 && WaveZombieCount(10) == 302, "Waves grow beyond the former 80-zombie total cap");
            ResetRun(); frontScreen = FrontScreen.Game; StartWave();
            check(enemies.Count + remainingToSpawn == WaveZombieCount(1) && pickups.Count == 1, "Starting a wave schedules its entire horde and one drop");
            check(transform.Find("Warehouse yard").localScale.x == ArenaScale, "Rendered hall uses expanded arena scale");
            var floor = transform.Find("Warehouse yard/Warehouse hall/Concrete warehouse floor").GetComponent<Renderer>().bounds;
            check(Mathf.Abs(floor.size.x - 36 * ArenaScale) < .05f, "Hall floor is approximately 48.7 metres wide");
            check(Free(new Vector3(0, 0, 20), PlayerRadius, true), "Expanded floor is playable up to the entrance barricades");
            check(!Free(new Vector3(0, 0, 24), PlayerRadius, true), "Expanded outer boundary still blocks escape");
            check(ShotStopDistance(new Vector3(0, 1.65f, 21), Vector3.back) > 35, "Shots from the expanded floor cross the old boundary");
            check(ShotStopDistance(new Vector3(0, 1.65f, 21), Vector3.forward) > 20, "Shots can reach the outdoor horde");
            RefreshPaths(); spawnSerial = 0;
            for (int i = 0; i < 12; i++)
            {
                Vector3 spawn = NextZombieEntrance();
                check(Mathf.Max(Mathf.Abs(spawn.x), Mathf.Abs(spawn.z)) >= 27 && Mathf.Min(Mathf.Abs(spawn.x), Mathf.Abs(spawn.z)) <= 1.15f,
                    "Spawn lane " + i + " begins outside and aligns with a loading bay");
            }
            ResetRun();
            check(!weaponInventory.Exists(w => w.weapon == 1), "New run resets wave unlocks");
            return report.ToString();
        }
    }
}
#endif

