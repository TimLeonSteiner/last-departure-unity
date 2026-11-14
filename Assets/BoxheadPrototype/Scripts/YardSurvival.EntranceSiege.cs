using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        void CreateEntranceBarricades()
        {
            if (IsCoopGuest) return;
            for (int side = 0; side < 4; side++)
            {
                Vector3 outward = entrances[side].normalized;
                Vector3 lateral = side % 2 == 0 ? Vector3.right : Vector3.forward;
                for (int panel = -1; panel <= 1; panel += 2)
                {
                    Vector3 position = outward * EntranceBoards.DoorDepth + lateral * (panel * 1.4f);
                    bool vertical = side % 2 != 0;
                    var cover = new Barricade { netId = nextCoopEntity++, bounds = BuildBounds(position, vertical),
                        body = DepotModels.Barricade(this, actors, position + Vector3.up * .6f, vertical) };
                    barricades.Add(cover);
                    DefensePhysics(cover.body, cover.bounds, 1.2f);
                }
            }
        }

        // Outside the indoor flow field, funnel attackers through the loading bays.
        // Never expand the player's movement bounds or allow enemies through walls.
        bool TickExteriorApproach(Enemy enemy, float dt)
        {
            if (Mathf.Max(Mathf.Abs(enemy.position.x), Mathf.Abs(enemy.position.z)) <= 22.3f) return false;
            bool northSouth = Mathf.Abs(enemy.position.z) >= Mathf.Abs(enemy.position.x);
            Vector3 outward = northSouth ? Vector3.forward * Mathf.Sign(enemy.position.z) : Vector3.right * Mathf.Sign(enemy.position.x);
            Vector3 lateral = northSouth ? Vector3.right : Vector3.forward;
            float lane = ((enemy.netId*7%9)-4)*.4f;
            Vector3 target = outward * 21 + lateral * lane;
            Vector3 desired = target - enemy.position;
            float speed = 1.1f + Mathf.Min(wave, 10) * .065f;
            if (enemy.zombie != null) speed *= enemy.zombie.ArchetypeSpeed(Time.time, enemy.phase);
            if (enemy.zombie != null && enemy.zombie.IsCharger) speed *= 2.2f;
            if (enemy.zombie != null) speed *= enemy.zombie.InjuredLegs == 2 ? .25f : enemy.zombie.InjuredLegs == 1 ? .55f : 1;
            desired=SteerZombieCrowd(enemy,desired,speed);
            Vector3 step = Vector3.ClampMagnitude(desired, speed * dt);
            Vector3 next = enemy.position + step;
            Barricade blocking = null;
            foreach (var cover in barricades)
                if (cover.hp > 0 && Touches(cover.bounds, next, EnemyRadius + .15f)) { blocking = cover; break; }
            TurnZombie(enemy.body, desired, dt);
            UpdateZombieAudio(enemy, dt, Vector3.Distance(enemy.position, player.position));
            if (blocking != null)
            {
                if (enemy.attack <= 0) BeginZombieAttack(enemy, false, blocking, null);
                enemy.zombie?.TickMovement(dt, 0, enemy.attackTime >= 0, 0);
                return true;
            }
            bool blocked = false;
            foreach (var wall in perimeter) if (Touches(wall, next, EnemyRadius)) blocked = true;
            foreach (var other in enemies)
                if (other != enemy && (next - other.position).sqrMagnitude < .76f * .76f
                    && (next - other.position).sqrMagnitude < (enemy.position - other.position).sqrMagnitude) blocked = true;
            if (!blocked) enemy.position = next;
            enemy.velocity = blocked ? Vector3.zero : step / Mathf.Max(dt, .001f);
            enemy.body.position = OnGround(enemy.position,enemy.zombie == null ? .65f : 0);
            enemy.zombie?.TickMovement(dt, blocked ? 0 : step.magnitude, false);
            return true;
        }

#if UNITY_EDITOR
        public string RunEntranceSiegeChecks()
        {
            ResetRun();
            if (barricades.Count != 8) throw new System.Exception("All four entrances must start sealed by two panels.");
            StartWave();
            if (enemies.Count != 32 || remainingToSpawn != 0) throw new System.Exception("First wave must assemble 32 real outdoor zombies.");
            foreach (var enemy in enemies)
                if (Mathf.Max(Mathf.Abs(enemy.position.x), Mathf.Abs(enemy.position.z)) < 26) throw new System.Exception("Enemy spawned inside.");
            if (ShotStopDistance(new Vector3(0, 1.65f, 20), Vector3.forward) < 15)
                throw new System.Exception("Invisible arena boundary blocks shooting the outdoor horde.");
            // Isolate one attacker: verify contact-timed strikes, breach and indoor navigation.
            for (int i = enemies.Count - 1; i > 0; i--) { Destroy(enemies[i].body.gameObject); enemies.RemoveAt(i); }
            var attacker = enemies[0];
            attacker.position = new Vector3(-1.15f, 0, 27); attacker.body.position = attacker.position;
            player.position = new Vector3(0, .65f, 12); RefreshPaths();
            for (int i = 0; i < 650; i++) UpdateEnemies(.05f);
            if (barricades.Count != 7) throw new System.Exception("Attacker failed to destroy its entrance panel.");
            if (attacker.position.z >= 20) throw new System.Exception("Attacker failed to enter through the breach.");
            ResetRun();
            if (barricades.Count != 8 || enemies.Count != 0) throw new System.Exception("Restart did not restore barricades and clear the horde.");
            StartWave();
            health = 10000;
            for (int i = 0; i < 800; i++) UpdateEnemies(.05f);
            for (int side = 0; side < 4; side++)
            {
                Vector3 outward = entrances[side].normalized;
                bool breached = false;
                foreach (var enemy in enemies)
                    if (Vector3.Dot(enemy.position, outward) > 0 && Vector3.Dot(enemy.position, outward) < 20) breached = true;
                if (!breached) throw new System.Exception("Crowd failed to breach direction " + side);
            }
            ResetRun();
            return "PASS: eight starting panels; 32 real outdoor attackers; animated attacks destroy a panel; zombie enters the breach; restart restores all entrances.";
        }
#endif
    }
}
