using UnityEngine;
using System.Collections.Generic;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public GameObject[] animatedWeaponModels;
        public static readonly int[] ArsenalAudio = {3,3,0,5,5,0,5,2,6,4,0,0,0,0,6,4,2};
        readonly List<ArsenalProjectile> arsenalProjectiles = new List<ArsenalProjectile>();
        sealed class ArsenalProjectile { public ulong shooter; public int netId; public Transform body; public Vector3 position, velocity; public float life; public bool grenade,explosive; public int damage; }
        void LaunchArsenalProjectile(Vector3 direction, bool grenade)
        {
            Vector3 origin = arenaCamera.transform.position + direction * .4f;
            var body = Box(grenade ? "Launched grenade" : "Crossbow bolt", origin, grenade ? Vector3.one * .085f : new Vector3(.018f,.018f,.35f), grenade ? yellowMaterial : brassMaterial, actors);
            body.transform.rotation = Quaternion.LookRotation(direction);
            arsenalProjectiles.Add(new ArsenalProjectile {shooter=coopFiringPlayer,netId=nextCoopEntity++,body=body.transform, position=origin, velocity=direction*(grenade?18:38)+Vector3.up*(grenade?2:0), life=grenade?2.5f:4, grenade=grenade,explosive=weaponId==7,damage=BoostedWeaponDamage});
        }
        void HurtArsenalEnemy(Enemy enemy, int damage, Vector3 direction, AudioClip impact=null)
        {
            if(impact!=null)PlayEffect(impact,.65f);
            ReactZombieHit(enemy,direction,damage,5);
            SpawnBlood(enemy.position + Vector3.up, direction, false); enemy.hp -= damage;
            PlayZombieSound("Flesh_Bullet_Impact", enemy.position, .5f);
            if (enemy.hp > 0) return;
            enemies.Remove(enemy); kills++;ApplySkillKillBonuses();
            DropCaffeine(enemy);
            if (enemy.zombie != null) enemy.zombie.Die(direction, enemy.position + Vector3.up, Mathf.Clamp(.8f + damage * .12f, .8f, 2.4f)); else { SpawnBlood(enemy.position+Vector3.up*.7f,direction,true); Destroy(enemy.body.gameObject); }
        }
        void MeleeAttack(Vector3 direction)
        {
            float range = weaponId == 5 ? 2.7f : 1.8f;
            Vector3 origin=arenaCamera.transform.position;
            float barrelRange=Mathf.Min(range,ShotStopDistance(origin,direction));
            var meleeBarrel=TraceBarrel(origin,direction,ref barrelRange);
            bool teammate=FriendlyTarget(coopFiringPlayer,out bool targetPeer,out var target);
            Vector3 teammateDelta=target+Vector3.up*.6f-origin;
            teammate=teammate&&teammateDelta.magnitude<=(meleeBarrel!=null?barrelRange:range)&&Vector3.Dot(teammateDelta.normalized,direction.normalized)>=.65f&&ShotStopDistance(origin,teammateDelta.normalized)+.1f>=teammateDelta.magnitude;
            float nearest=teammate?teammateDelta.magnitude:(meleeBarrel!=null?barrelRange:range);Enemy hit=null;
            foreach (var enemy in enemies)
            {
                Vector3 delta = enemy.position + Vector3.up - origin;
                if (delta.magnitude > nearest || Vector3.Dot(delta.normalized,direction.normalized) < .65f) continue;
                if (ShotStopDistance(origin,delta.normalized) + .1f < delta.magnitude) continue;
                hit=enemy;nearest=delta.magnitude;
            }
            if(hit!=null){HurtArsenalEnemy(hit,BoostedWeaponDamage,direction,weaponId==5?pitchforkImpact:knifeImpact);return;}
            if(teammate){DamageFriendlyPlayer(targetPeer,ActiveWeapon.damage,target+Vector3.up*.6f,direction);return;}
            if(meleeBarrel!=null)DetonateBarrel(meleeBarrel);
        }
        void UpdateArsenalProjectiles(float dt)
        {
            foreach(var p in arsenalProjectiles.ToArray())
            {
                p.life-=dt; p.velocity+=Vector3.down*(p.grenade?9.8f:2)*dt;
                Vector3 step=p.velocity*dt; float length=step.magnitude;
                Vector3 direction=step.normalized;
                float stop=ShotStopDistance(p.position,direction); Enemy hit=null;
                var barrelHit=TraceBarrel(p.position,direction,ref stop);
                foreach(var e in enemies)
                {
                    var bounds=new Bounds(e.position+Vector3.up*.9f,new Vector3(.9f,1.8f,.9f));
                    if(bounds.IntersectRay(new Ray(p.position,direction),out float entry) && entry<stop) {stop=entry;hit=e;}
                }
                bool friendlyPeer=false;bool friendlyHit=TraceFriendlyPlayer(p.position,direction,p.shooter,ref stop,out friendlyPeer);
                bool impact=stop<=length || p.position.y+step.y<.08f || p.life<=0;
                p.position+=direction*Mathf.Min(length,stop); p.body.position=p.position;
                p.body.rotation=Quaternion.LookRotation(p.velocity);
                if(!impact) continue;
                if(barrelHit!=null&&hit==null&&!friendlyHit&&stop<=length)DetonateBarrel(barrelHit);
                if(p.grenade||p.explosive)
                {
                    float radius=p.grenade?4:2.6f;
                    ExplodeBarrelsInRadius(p.position,radius);
                    foreach(var e in enemies.ToArray())
                    {
                        Vector3 delta=e.position+Vector3.up*.7f-p.position; float distance=delta.magnitude;
                        if(distance<radius && ShotStopDistance(p.position,delta.normalized)+.1f>=distance) HurtArsenalEnemy(e, Mathf.Max(1,Mathf.RoundToInt(p.damage*(1-distance/(radius+1)))),delta.normalized);
                    }
                    if(FriendlyTarget(p.shooter,out bool blastPeer,out var target))
                    {
                        Vector3 delta=target+Vector3.up*.1f-p.position;float distance=delta.magnitude;
                        if(distance<radius&&ShotStopDistance(p.position,delta.normalized)+.1f>=distance)DamageFriendlyPlayer(blastPeer,Mathf.Max(1,Mathf.RoundToInt(p.damage*(1-distance/(radius+1)))),target,delta.normalized);
                    }
                    PlayEffect(grenadeExplosion!=null?grenadeExplosion:scrapSound,.85f);
                    var burst=new GameObject("Explosive bolt detonation");burst.transform.SetParent(actors,false);burst.transform.position=p.position;
                    burst.AddComponent<ArsenalExplosionVisual>().Initialize(muzzleMaterial,dustMaterial);
                }
                else if(friendlyHit&&stop<=length)DamageFriendlyPlayer(friendlyPeer,p.damage,p.position,direction);
                else if(hit!=null) HurtArsenalEnemy(hit,p.damage,direction,crossbowFleshImpact);
                else PlayEffect(crossbowImpact,.6f);
                Destroy(p.body.gameObject); arsenalProjectiles.Remove(p);
            }
        }
        void ClearArsenalProjectiles(){foreach(var p in arsenalProjectiles)if(p.body!=null)Destroy(p.body.gameObject);arsenalProjectiles.Clear();}
    }
}

