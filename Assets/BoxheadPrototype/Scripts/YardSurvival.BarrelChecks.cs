#if UNITY_EDITOR
using System;
using System.Text;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunBarrelChecks(GameObject prefab)
        {
            if (IsCoopGuest || IsCoopHost) throw new Exception("Run barrel checks outside a co-op session.");
            var report = new StringBuilder();
            void Check(bool value, string label) { report.AppendLine((value ? "PASS " : "FAIL ") + label); if (!value) throw new Exception(report.ToString()); }
            ResetRun();
            var stage = new GameObject("Barrel checks");
            ExplosiveBarrel Make(Vector3 position)
            { var b = Instantiate(prefab, position, Quaternion.identity, stage.transform).GetComponent<ExplosiveBarrel>(); RegisterExplosiveBarrel(b); return b; }
            var barrel = Make(new Vector3(-3, 0, 4)); var chain = Make(new Vector3(-1, 0, 4)); var far = Make(new Vector3(6, 0, 4));
            Vector3 start = new Vector3(-3, .45f, 1);
            void Rearm() { barrel.SetDetonated(false); chain.SetDetonated(false); far.SetDetonated(false); }
            Enemy Target(Vector3 position, int hp)
            { var body = new GameObject("Barrel target").transform; body.SetParent(actors); body.position = position; var e = new Enemy { body = body, position = position, hp = hp }; enemies.Add(e); return e; }
            try
            {
                Check(barrel.Trace(start, Vector3.forward, 4, out float distance) && distance > 2 && distance < 3, "imported barrel hit bounds");
                Check(!Free(barrel.transform.position, .4f, true), "intact barrel blocks movement");
                var wall = new Bounds(new Vector3(-3, .5f, 2), new Vector3(2, 2, .2f)); wallVolumes.Add(wall);
                weaponId = WeaponCatalog.Starter; TraceShot(start, Vector3.forward, null);
                Check(!barrel.Detonated, "wall prevents shooting barrel"); wallVolumes.Remove(wall);
                var blocker = Target(new Vector3(-3, 0, 2.5f), 50); TraceShot(start, Vector3.forward, null);
                Check(!barrel.Detonated && blocker.hp < 50, "nearer zombie intercepts ordinary bullet"); enemies.Remove(blocker); Destroy(blocker.body.gameObject);
                var victim = Target(new Vector3(-3, 0, 5), 3); var outside = Target(new Vector3(-3, 0, 12), 3);
                int beforeKills = kills; TraceShot(start, Vector3.forward, null);
                Check(barrel.Detonated && chain.Detonated && !far.Detonated, "one bullet detonates barrel and nearby chain only");
                Check(!enemies.Contains(victim) && enemies.Contains(outside) && kills == beforeKills + 1, "blast kills nearby zombie and awards one kill");
                Check(Free(barrel.transform.position, .4f, true), "exploded barrel releases movement blocker");
                DetonateBarrel(barrel); Check(kills == beforeKills + 1, "detonation cannot apply damage twice");
                enemies.Remove(outside); Destroy(outside.body.gameObject);
                Rearm(); var protectedTarget = Target(new Vector3(-3, 0, 5.5f), 50);
                wall = new Bounds(new Vector3(-3, .6f, 4.8f), new Vector3(10, 3, .2f)); wallVolumes.Add(wall);
                DetonateBarrel(barrel); Check(protectedTarget.hp == 50, "solid wall protects zombie from blast");
                wallVolumes.Remove(wall); enemies.Remove(protectedTarget); Destroy(protectedTarget.body.gameObject);
                Rearm(); health = 100; dead = false; damageTimer = 0; player.position = new Vector3(-3, .65f, 5);
                DetonateBarrel(barrel); Check(health < 100 && health > 0, "nearby player receives blast damage"); player.position = new Vector3(0, .65f, -5);
                foreach (int id in new[] { 0, 1, 2, 3, 6, 8, 9, 11, 12, 13, 14, 15, 16 })
                { Rearm(); weaponId = id; TraceShot(start, Vector3.forward, null); Check(barrel.Detonated, WeaponCatalog.All[id].name + " detonates barrel"); }
                foreach (int id in new[] { 4, 7 })
                {
                    Rearm(); weaponId = id; arenaCamera.transform.position = new Vector3(-3, .6f, 1);
                    LaunchArsenalProjectile(Vector3.forward, id == 4);
                    for (int i = 0; i < 180 && arsenalProjectiles.Count > 0; i++) UpdateArsenalProjectiles(1f / 60);
                    Check(barrel.Detonated, WeaponCatalog.All[id].name + " projectile detonates barrel");
                }
                Rearm(); weaponId = 10; arenaCamera.transform.position = new Vector3(-3, .45f, 3); MeleeAttack(Vector3.forward);
                Check(barrel.Detonated, "melee contact detonates barrel");
                string[] spent = SpentBarrelKeys(); Rearm(); ApplySpentBarrels(spent);
                Check(barrel.Detonated && chain.Detonated && !far.Detonated, "co-op spent-state replay hides correct barrels");
                int effectCount = actors.GetComponentsInChildren<ArsenalExplosionVisual>().Length; ApplySpentBarrels(spent);
                Check(actors.GetComponentsInChildren<ArsenalExplosionVisual>().Length == effectCount, "repeated snapshot does not replay explosion");
                ResetExplosiveBarrels(); Check(!barrel.Detonated && barrel.GetComponent<Collider>().enabled, "restart restores placed barrel and collider");
                barrel.transform.rotation = Quaternion.Euler(0, 37, 0); barrel.transform.localScale = Vector3.one * 1.5f;
                Check(barrel.Trace(barrel.Center - Vector3.forward * 3, Vector3.forward, 4, out distance), "rotated scaled placement remains hittable");
                report.AppendLine("ALL EXPLOSIVE BARREL CHECKS PASSED");
            }
            finally { stage.SetActive(false); Destroy(stage); RegisterObstacles(); ResetRun(); }
            return report.ToString();
        }
    }
}
#endif
