using System.Collections.Generic;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        void ApplyRayDamage(Enemy hit,int region,Vector3 point,Vector3 direction,int damage)
        {
            ReactZombieHit(hit,direction,damage,region);
            PlayZombieSound("Flesh_Bullet_Impact",point,.5f);
            // Leg shots cripple without killing; the crawler remains a live enemy.
            if(damage > 0 && hit.zombie != null && (region == 3 || region == 4))
            {
                hit.zombie.InjureLeg(region);
                hit.hp = Mathf.Max(1, hit.hp - damage);
            }
            else if(region==0)hit.hp=0;else hit.hp-=damage;
            if(hit.hp<=0){enemies.Remove(hit);DropCaffeine(hit);if(hit.zombie!=null)hit.zombie.Die(direction,point,Mathf.Clamp(.8f+damage*.12f,.8f,2.4f));else {SpawnBlood(hit.position+Vector3.up*.7f,direction,true);PlayZombieSound("Zombie_Death",hit.position,.5f);Destroy(hit.body.gameObject);}kills++;ApplySkillKillBonuses();}
            else PlayZombieSound("Zombie_Pain",hit.position,.4f);
        }
        void TraceSniperShot(Vector3 origin,Vector3 direction)
        {
            float stop=ShotStopDistance(origin,direction);var ray=new Ray(origin,direction);
            var barrelHit=TraceBarrel(origin,direction,ref stop);
            var hits=new List<(Enemy enemy,float distance,int region)>();
            foreach(var e in enemies)
            {
                if(e.zombie!=null){if(e.zombie.Trace(ray,stop,out float entry,out int region))hits.Add((e,entry,region));}
                else if(new Bounds(e.position+Vector3.up*.9f,new Vector3(.95f,1.8f,.95f)).IntersectRay(ray,out float entry)&&entry>=0&&entry<stop)hits.Add((e,entry,5));
            }
            float teammateDistance=stop;
            if(TraceFriendlyPlayer(origin,direction,coopFiringPlayer,ref teammateDistance,out bool targetPeer))DamageFriendlyPlayer(targetPeer,ActiveWeapon.damage,origin+direction*teammateDistance,direction);
            hits.Sort((a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                var point=origin+direction*hit.distance;ApplyRayDamage(hit.enemy,hit.region,point,direction,BoostedWeaponDamage);
            }
            if(barrelHit!=null)DetonateBarrel(barrelHit);
            if(stop<51){var dust=Box("Sniper surface impact",origin+direction*(stop-.03f),Vector3.one*.035f,dustMaterial,actors);flashes.Add(new Flash{body=dust,life=.3f,velocity=-direction*.6f});}
        }
    }
}
