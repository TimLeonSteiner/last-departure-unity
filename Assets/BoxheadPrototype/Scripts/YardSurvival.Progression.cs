using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        int weaponPickupWave;
        int previousWaveWeapon = -1;
        static int WaveZombieCount(int stage)
        {
            int n = Mathf.Clamp(stage - 1, 0, 100);
            return 32 + 12 * n + 2 * n * n;
        }
        static int WaveZombieHealth(int baseHealth, int stage, bool charger = false)
        {
            // Later waves add bodies, not hit points. Chargers are the durable threat.
            return charger ? 15 : Mathf.Max(1, baseHealth);
        }
        static readonly int[] OpeningWaveWeapons = {14, 3, 8};
        void SpawnWaveWeapons(int upcomingWave)
        {
            if (IsCoopGuest || !fighting || upcomingWave != wave || upcomingWave <= weaponPickupWave) return;
            foreach (var old in pickups.ToArray()) if (old.waveDrop) RemoveWeaponPickup(old);
            HideWeaponTrail();
            var available = new List<int>();
            int tier = Mathf.Min(upcomingWave, 9);
            // Later drops stay in the heavy-weapon pool, instead of reverting to pistols.
            for (int id = 0; id < WeaponCatalog.All.Length; id++)
            {
                if (!WeaponCatalog.IsAvailable(id) || WeaponCatalog.IsMelee(id) || id == WeaponCatalog.Starter) continue;
                int unlock = WeaponCatalog.All[id].unlockWave;
                if (unlock <= tier && unlock >= (tier >= 7 ? tier - 2 : tier)) available.Add(id);
            }
            if (available.Count == 0) available.Add(12);
            if (available.Count > 1) available.Remove(previousWaveWeapon);
            int chosen = upcomingWave <= OpeningWaveWeapons.Length
                ? OpeningWaveWeapons[upcomingWave-1] : available[Random.Range(0, available.Count)];
            Vector3 position = FindWaveWeaponPosition();
            SpawnPickup(chosen, position, WeaponCatalog.All[chosen].capacity, waveDrop: true);
            previousWaveWeapon = chosen; weaponPickupWave = upcomingWave;
        }
        Vector3 FindWaveWeaponPosition()
        {
            // Flood only walkable cells, including current player-built defenses.
            var visited = new bool[GridSize, GridSize];
            var queue = new Queue<Vector2Int>();
            var start = Cell(player.position); queue.Enqueue(start); visited[start.x,start.y] = true;
            var candidates = new List<Vector3>();
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue(); var point = World(cell);
                if (Free(point, .8f, true) && Vector3.Distance(point, player.position) > 5 &&
                    Mathf.Abs(point.x) < 20 && Mathf.Abs(point.z) < 20) candidates.Add(point);
                foreach (var step in Neighbours)
                {
                    var next = cell + step;
                    if (next.x < 0 || next.y < 0 || next.x >= GridSize || next.y >= GridSize || visited[next.x,next.y]) continue;
                    if (!Free(World(next), PlayerRadius + .1f, true)) continue;
                    visited[next.x,next.y] = true; queue.Enqueue(next);
                }
            }
            return candidates.Count > 0 ? candidates[Random.Range(0,candidates.Count)] : new Vector3(player.position.x,0,player.position.z);
        }
        static InventoryWeapon NewWaveWeapon(int id)
        {
            int capacity = WeaponCatalog.All[id].capacity;
            return new InventoryWeapon { weapon = id, ammo = capacity, reserve = WeaponCatalog.IsMelee(id) ? 0 : capacity * 3 };
        }
        string NextWeaponUnlock() => "ONE WEAPON DROP EACH WAVE - PRESS " + ControlHint(Control.Pickup) + " TO PICK UP";

        public const float ArenaScale = 23f / 17f;
        static Vector3 ArenaPosition(Vector3 position) =>
            new Vector3(position.x * ArenaScale, position.y, position.z * ArenaScale);

        void ScaleArenaNavigation()
        {
            for (int i = 0; i < obstacles.Count; i++) obstacles[i] = ScaleArenaRect(obstacles[i]);
            for (int i = 0; i < perimeter.Count; i++) perimeter[i] = ScaleArenaRect(perimeter[i]);
            for (int i = 0; i < wallVolumes.Count; i++)
                wallVolumes[i] = new Bounds(ArenaPosition(wallVolumes[i].center), ArenaPosition(wallVolumes[i].size));
        }
        static Rect ScaleArenaRect(Rect rect) => new Rect(rect.x * ArenaScale,
            rect.y * ArenaScale, rect.width * ArenaScale, rect.height * ArenaScale);

        Vector3 NextZombieEntrance()
        {
            int serial = spawnSerial++;
            int side = serial % entrances.Length;
            Vector3 lane = side % 2 == 0 ? Vector3.right : Vector3.forward;
            // Three lanes per loading bay prevent a single-file pileup at the door.
            int rank = (serial / 4) % 20;
            return entrances[side].normalized * (27 + (rank / 3) * 1.8f)
                + lane * ((rank % 3 - 1) * 1.15f);
        }
    }
}
