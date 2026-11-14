using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        enum EnemySpecial { None, Charge, Spit, Scream }

        const int ChargerIntroWave = 7;
        const float ChargeWindup = .42f;
        const float ChargeEnd = 1.42f;
        const float SpitRelease = .48f;
        const float KnockdownDuration = 1.2f;
        const float AcidRadius = 1.25f;

        sealed class AcidHazard
        {
            public int id;
            public Transform body;
            public Vector3 velocity;
            public float life, burnTimer;
            public bool pool;
        }

        readonly List<AcidHazard> acidHazards = new List<AcidHazard>();

        float knockdownTime;
        Vector3 knockdownVelocity;

        static bool SelectRunner(int waveNumber, int serial, int variant)
        {
            return waveNumber >= 4 && variant == 0 && (serial + serial / 4) % 4 == 2;
        }

        int SelectEnemyVariant()
        {
            int count = zombieModels?.Length ?? 0;
            if (count == 0) return 0;
            if (IsCoopGuest) return Mathf.Clamp(spawnSerial, 0, count - 1);

            if (spawnSerial % 5 != 0 || wave < 3) return 0;
            int unlocked = wave >= ChargerIntroWave ? 5 : Mathf.Min(4, wave - 2);
            int wanted = (spawnSerial / 5) % unlocked;
            for (int i = 0; i < count; i++)
            {
                int tier = zombieModels[i].name switch
                {
                    "Spitter" => 0,
                    "Screamer" => 1,
                    "ArmoredWorker" => 2,
                    "Charger" => 4,
                    "04-Cargo-Brute" => 3,
                    _ => -1
                };
                if (tier == wanted) return i;
            }
            return 0;
        }

        bool TryBeginSpecialAttack(Enemy enemy, bool targetPeer, Vector3 target, float dt)
        {
            if (enemy.attack > 0 || enemy.zombie == null || enemy.zombie.InjuredLegs > 0) return false;
            string name = zombieModels[enemy.variant].name;
            EnemySpecial special = name == "Charger" ? EnemySpecial.Charge :
                name == "Spitter" ? EnemySpecial.Spit : name == "Screamer" && enemy.screamCooldown<=0 && fighting ? EnemySpecial.Scream : EnemySpecial.None;
            if (special == EnemySpecial.None) return false;

            Vector3 delta = target - enemy.position;
            float range = delta.magnitude;
            float maxRange = special == EnemySpecial.Scream ? 16f : special == EnemySpecial.Charge ? 9f : 9f;
            if ((special != EnemySpecial.Scream && range < 1.4f) || range > maxRange || !PickupLineClear(enemy.position, target)) return false;

            TurnZombie(enemy.body, delta, dt);
            if (Vector3.Dot(enemy.body.forward, delta / range) < .88f)
            {
                if (special != EnemySpecial.Charge && special != EnemySpecial.Scream) return false;
                // Keep core pursuit from undoing the turn later in this frame.
                enemy.velocity = Vector3.zero;
                enemy.zombie.TickMovement(dt, 0, false);
                return true;
            }

            BeginZombieAttack(enemy, targetPeer, null, null);
            enemy.specialAttack = special;
            if (special == EnemySpecial.Scream)
            {
                enemy.screamCooldown = 18;
                screamerPings[enemy] = Time.time;
            }
            enemy.chargeStopped = false;
            return true;
        }

        static float SpecialAttackDuration(EnemySpecial special) =>
            special == EnemySpecial.Scream ? ScreamEnd : special == EnemySpecial.Charge ? 1.85f : special == EnemySpecial.Spit ? 1.25f : ZombieAttackDuration;

        void TickSpecialAttack(Enemy enemy, float dt)
        {
            if (enemy.zombie.InjuredLegs > 0)
            {
                enemy.attackTime = -1;
                enemy.specialAttack = EnemySpecial.None;
                enemy.attack = .5f;
                return;
            }
            float before = enemy.attackTime;
            enemy.attackTime += dt;
            Vector3 target = ZombieAttackTarget(enemy);
            float duration = SpecialAttackDuration(enemy.specialAttack);
            bool targetAlive = enemy.attackPeer ? HasCoopPartner && peer.health > 0 : !dead;

            if(enemy.specialAttack==EnemySpecial.Scream)
            {
                TurnZombie(enemy.body,target-enemy.position,dt);
                AdvanceScream(enemy, before);
            }
            else if (enemy.specialAttack == EnemySpecial.Charge)
            {
                if (before < ChargeWindup)
                    TurnZombie(enemy.body, target - enemy.position, Mathf.Min(dt, ChargeWindup - before));
                float rushTime = Mathf.Min(enemy.attackTime, ChargeEnd) - Mathf.Max(before, ChargeWindup);
                if (rushTime > 0 && !enemy.chargeStopped)
                    AdvanceCharge(enemy, target, targetAlive, rushTime);
            }
            else if (before < SpitRelease && enemy.attackTime >= SpitRelease && targetAlive)
            {
                LaunchAcid(enemy, target);
            }
            else if (enemy.attackTime < SpitRelease)
            {
                TurnZombie(enemy.body, target - enemy.position, dt);
            }

            enemy.zombie.TickMovement(dt, 0, true, Mathf.Clamp01(enemy.attackTime / duration));
            if (enemy.attackTime < duration) return;
            enemy.attack = enemy.specialAttack == EnemySpecial.Charge ? 2f : 2.5f;
            enemy.specialAttack = EnemySpecial.None;
            enemy.attackTime = -1;
        }

        void AdvanceCharge(Enemy enemy, Vector3 target, bool targetAlive, float dt)
        {
            Vector3 direction = enemy.body.forward;
            Vector3 start = enemy.position;
            float stride = 10f * dt;
            int steps = Mathf.CeilToInt(stride / .12f);
            for (int i = 0; i < steps; i++)
            {
                Vector3 next = enemy.position + direction * (stride / steps);
                if (!Free(next, EnemyRadius, true))
                {
                    enemy.chargeStopped = true;
                    break;
                }
                enemy.position = next;
                if (!targetAlive || (target - next).sqrMagnitude > .95f * .95f ||
                    !PickupLineClear(next, target)) continue;

                enemy.chargeStopped = true;
                if (enemy.attackPeer)
                {
                    DamagePeer(24);
                    if (peer.health > 0)
                    {
                        peer.knockdown = KnockdownDuration;
                        peer.knockback = direction * 7f;
                        peer.slideTime = 0;
                    }
                }
                else
                {
                    DamagePlayer(24);
                    if (!dead) BeginKnockdown(direction);
                }
                PlayZombieSound("Zombie_Bite", enemy.position, .6f);
                break;
            }
            enemy.body.position += enemy.position - start;
        }

        void BeginKnockdown(Vector3 direction)
        {
            if (slideTimer > 0) EndSlide();
            dodge.remaining = 0; knockdownTime = KnockdownDuration;
            knockdownVelocity = direction * 7f;
            jumpHeight = verticalSpeed = 0;
        }

        void UpdateKnockdown(float dt)
        {
            Vector3 pos = player.position;
            pos.y = 0;
            player.position = OnGround(Move(pos, knockdownVelocity * dt, PlayerRadius, true),.65f);
            knockdownVelocity = Vector3.MoveTowards(knockdownVelocity, Vector3.zero, dt * 19f);
            float fall = Mathf.SmoothStep(0, 1, Mathf.Clamp01((KnockdownDuration - knockdownTime) / .17f));
            float recover = Mathf.SmoothStep(0, 1, Mathf.Clamp01(knockdownTime / .42f));
            float tilt = fall * recover;
            eyeHeight = Mathf.Lerp(1, .15f, tilt);
            arenaCamera.transform.localPosition = Vector3.up * eyeHeight;
            arenaCamera.transform.localRotation = Quaternion.Euler(pitch - cameraKick + 18f * tilt, recoilSide, -68f * tilt);
            knockdownTime = Mathf.Max(0, knockdownTime - dt);
        }

        Transform CreateAcidVisual(Vector3 position, bool pool, int seed = 0, float life = 3.6f)
        {
            var blob = new GameObject(pool ? "Burning acid puddle" : "Spitter acid stream");
            blob.transform.SetParent(actors, false);
            blob.transform.position = position;
            blob.AddComponent<SpitterAcidVisual>().Initialize(pool, AcidRadius, seed, life);
            return blob.transform;
        }
        void LaunchAcid(Enemy enemy, Vector3 target)
        {
            Vector3 origin = enemy.zombie.RegionCenter(0) + enemy.body.forward * .18f - Vector3.up * .12f;
            Vector3 destination = target + Vector3.up * .02f;
            float flight = Mathf.Clamp(Vector3.Distance(origin, destination) / 11f, .22f, .85f);
            var velocity = (destination - origin) / flight + Vector3.up * (6f * flight);
            acidHazards.Add(new AcidHazard
            {
                id = nextCoopEntity++, body = CreateAcidVisual(origin, false, nextCoopEntity - 1, 1.4f), velocity = velocity, life = 1.4f
            });
        }

        void UpdateAcid(float dt)
        {
            for (int i = acidHazards.Count - 1; i >= 0; i--)
            {
                AcidHazard acid = acidHazards[i];
                acid.life -= dt;
                if (acid.life <= 0)
                {
                    Destroy(acid.body.gameObject);
                    acidHazards.RemoveAt(i);
                    continue;
                }
                if (acid.pool)
                {
                    acid.burnTimer -= dt;
                    if (acid.burnTimer > 0) continue;
                    acid.burnTimer = .55f;
                    if (InAcid(player.position, acid.body.position)) DamagePlayer(5);
                    if (HasCoopPartner && InAcid(peer.position, acid.body.position)) DamagePeer(5);
                    continue;
                }

                Vector3 origin = acid.body.position;
                Vector3 step = acid.velocity * dt + Vector3.down * (6f * dt * dt);
                acid.velocity += Vector3.down * (12f * dt);
                float length = step.magnitude;
                if (length <= .0001f) continue;
                float travel = Mathf.Min(length, ShotStopDistance(origin, step / length));
                Vector3 end = origin + step.normalized * travel;
                bool hitHost = !dead && AcidTouchesPlayer(origin, end, player.position);
                bool hitPeer = HasCoopPartner && peer.health > 0 && AcidTouchesPlayer(origin, end, peer.position);
                if (hitHost) DamagePlayer(12);
                if (hitPeer) DamagePeer(12);
                if (hitHost || hitPeer || travel < length || end.y <= .025f)
                {
                    Vector3 ground = new Vector3(end.x, .035f, end.z);
                    Destroy(acid.body.gameObject);
                    acidHazards.RemoveAt(i);
                    if (Free(new Vector3(ground.x, 0, ground.z), .12f, true))
                        acidHazards.Add(new AcidHazard
                        {
                            id = nextCoopEntity++, body = CreateAcidVisual(ground, true, nextCoopEntity - 1),
                            pool = true, life = 3.6f, burnTimer = .3f
                        });
                    else SpitterAcidVisual.CreateSplash(end, actors, acid.id);
                }
                else acid.body.position = end;
            }
        }

        bool InAcid(Vector3 playerCenter, Vector3 pool)
        {
            if (playerCenter.y > 1.05f) return false;
            Vector3 delta = playerCenter - pool;
            delta.y = 0;
            return delta.sqrMagnitude < AcidRadius * AcidRadius && PickupLineClear(pool, playerCenter);
        }

        bool AcidTouchesPlayer(Vector3 from, Vector3 to, Vector3 playerCenter)
        {
            Vector3 step = to - from;
            Vector3 torso = playerCenter + Vector3.up * .12f;
            float along = Mathf.Clamp01(Vector3.Dot(torso - from, step) / Mathf.Max(.00001f, step.sqrMagnitude));
            Vector3 delta = torso - (from + step * along);
            float distance = delta.magnitude;
            return distance < .49f && (distance <= .01f ||
                ShotStopDistance(torso - delta, delta / distance) >= distance);
        }

        void ClearSpecialAttacks()
        {
            ClearScreamerFeedback();
            foreach (AcidHazard acid in acidHazards)
                if (acid.body != null) Destroy(acid.body.gameObject);
            acidHazards.Clear();
            screamReinforcements.Clear();
            if (actors != null)
                foreach (var visual in actors.GetComponentsInChildren<SpitterAcidVisual>())
                    Destroy(visual.gameObject);
            knockdownTime = 0;
            knockdownVelocity = Vector3.zero;
        }
    }
}

