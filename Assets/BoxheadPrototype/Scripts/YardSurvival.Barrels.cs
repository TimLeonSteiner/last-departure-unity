using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        readonly List<ExplosiveBarrel> explosiveBarrels = new List<ExplosiveBarrel>();
        readonly Dictionary<int,ExplosiveBarrel> placedBarrels = new Dictionary<int,ExplosiveBarrel>();
        ExplosiveBarrel BarrelTemplate => explosiveBarrels.FirstOrDefault(b=>b!=null&&b.placedId==0&&b.enabled);
        [System.Serializable] sealed class PlacedBarrelState { public int id; public Vector3 position; }
        ExplosiveBarrel SpawnPlacedBarrel(int id, Vector3 position)
        {
            var source=BarrelTemplate;if(source==null)return null;
            var barrel=Instantiate(source,position,Quaternion.identity,actors);
            barrel.name="Placed explosive barrel";barrel.placedId=id;barrel.enabled=true;barrel.SetDetonated(false);
            placedBarrels[id]=barrel;RegisterExplosiveBarrel(barrel);return barrel;
        }
        void ApplyPlacedBarrels(PlacedBarrelState[] states)
        {
            if(states==null)return;
            foreach(var state in states)if(!placedBarrels.ContainsKey(state.id))SpawnPlacedBarrel(state.id,state.position);
        }
        public void RegisterExplosiveBarrel(ExplosiveBarrel barrel)
        { if (barrel != null && !explosiveBarrels.Contains(barrel)) explosiveBarrels.Add(barrel); }
        void ResetExplosiveBarrels()
        {
            foreach(var barrel in placedBarrels.Values)if(barrel!=null){barrel.gameObject.SetActive(false);Destroy(barrel.gameObject);}
            placedBarrels.Clear();
            explosiveBarrels.Clear();
            foreach (var barrel in FindObjectsByType<ExplosiveBarrel>(FindObjectsSortMode.None))
            { if(barrel.enabled&&barrel.placedId==0){RegisterExplosiveBarrel(barrel); barrel.SetDetonated(false);} }
        }
        ExplosiveBarrel TraceBarrel(Vector3 origin, Vector3 direction, ref float nearest)
        {
            ExplosiveBarrel hit = null;
            foreach (var barrel in explosiveBarrels)
                if (barrel != null && barrel.Trace(origin, direction, nearest, out float entry)) { nearest = entry; hit = barrel; }
            return hit;
        }
        bool BarrelBlocks(Vector3 point, float radius)
        { return explosiveBarrels.Any(b => b != null && b.isActiveAndEnabled && !b.Detonated && Touches(b.Footprint, point, radius)); }
        bool BlastVisible(Vector3 origin, Vector3 target)
        { var delta = target - origin; return delta.sqrMagnitude < .0001f || ShotStopDistance(origin, delta.normalized) + .03f >= delta.magnitude; }
        void BarrelEffect(ExplosiveBarrel barrel)
        {
            var burst = new GameObject("Fuel barrel explosion"); burst.transform.SetParent(actors, false); burst.transform.position = barrel.Center;
            burst.AddComponent<ArsenalExplosionVisual>().Initialize(muzzleMaterial, dustMaterial);
            PlayEffect(grenadeExplosion != null ? grenadeExplosion : scrapSound, .85f);
        }
        public void DetonateBarrel(ExplosiveBarrel first)
        {
            if (IsCoopGuest || first == null || first.Detonated) return;
            var pending = new Queue<ExplosiveBarrel>(); pending.Enqueue(first); first.SetDetonated(true);
            while (pending.Count > 0)
            {
                var barrel = pending.Dequeue(); Vector3 origin = barrel.Center; float radius = barrel.blastRadius;
                BarrelEffect(barrel);
                foreach (var e in enemies.ToArray())
                {
                    Vector3 target = e.position + Vector3.up * .7f; float distance = Vector3.Distance(origin, target);
                    if (distance < radius && BlastVisible(origin, target))
                        HurtArsenalEnemy(e, Mathf.Max(1, Mathf.CeilToInt(barrel.zombieDamage * (1 - distance / radius))), (target - origin).normalized);
                }
                if (player != null)
                {
                    float distance = Vector3.Distance(origin, player.position);
                    if (distance < radius && BlastVisible(origin, player.position)) DamagePlayer(Mathf.CeilToInt(barrel.playerDamage * (1 - distance / radius)));
                }
                if (HasCoopPartner && peer.health > 0)
                {
                    float distance = Vector3.Distance(origin, peer.position);
                    if (distance < radius && BlastVisible(origin, peer.position)) DamagePeer(Mathf.CeilToInt(barrel.playerDamage * (1 - distance / radius)));
                }
                foreach (var other in explosiveBarrels)
                    if (other != null && other.isActiveAndEnabled && !other.Detonated && Vector3.Distance(origin, other.Center) < radius && BlastVisible(origin, other.Center))
                    { other.SetDetonated(true); pending.Enqueue(other); }
            }
            pathTimer = 0;
        }
        void ExplodeBarrelsInRadius(Vector3 origin, float radius)
        {
            foreach (var barrel in explosiveBarrels.ToArray())
                if (barrel != null && !barrel.Detonated && Vector3.Distance(origin, barrel.Center) < radius && BlastVisible(origin, barrel.Center)) DetonateBarrel(barrel);
        }
        string[] SpentBarrelKeys() => explosiveBarrels.Where(b => b != null && b.Detonated).Select(b => b.SceneKey).ToArray();
        void ApplySpentBarrels(string[] keys)
        {
            if (keys == null) return;
            var spent = new HashSet<string>(keys);
            foreach (var barrel in explosiveBarrels)
                if (barrel != null && !barrel.Detonated && spent.Contains(barrel.SceneKey)) { barrel.SetDetonated(true); BarrelEffect(barrel); }
        }
    }
}
