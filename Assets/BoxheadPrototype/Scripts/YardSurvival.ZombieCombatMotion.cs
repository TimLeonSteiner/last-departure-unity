using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        const float ZombieStrikeTime=.40f,ZombieAttackDuration=1.05f;
        void BeginZombieAttack(Enemy e,bool targetPeer,Barricade barricade,Turret turret,bool targetCore=false)
        {
            e.specialAttack = EnemySpecial.None;
            e.attackCore=targetCore;e.attackTime=0;e.attackSerial++;e.attackPeer=targetPeer;
            e.attackBarricade=barricade;e.attackTurret=turret;e.velocity=Vector3.zero;
            PlayZombieSound("Zombie_Attack",e.position,.38f);
        }
        Vector3 ZombieAttackTarget(Enemy e)
        {
            if(e.attackCore)return Vector3.zero;
            if(e.attackBarricade!=null||e.attackTurret!=null)
            {
                Rect bounds=e.attackBarricade!=null?e.attackBarricade.bounds:e.attackTurret.bounds;
                return new Vector3(Mathf.Clamp(e.position.x,bounds.xMin,bounds.xMax),0,Mathf.Clamp(e.position.z,bounds.yMin,bounds.yMax));
            }
            Vector3 target=e.attackPeer&&peer!=null?peer.position:player.position;target.y=0;return target;
        }
        void TickZombieAttack(Enemy e,float dt)
        {
            if (e.specialAttack != EnemySpecial.None)
            {
                TickSpecialAttack(e, dt);
                return;
            }
            float before=e.attackTime;e.attackTime+=dt;
            Vector3 target=ZombieAttackTarget(e),delta=target-e.position;delta.y=0;
            // Commit the facing just before contact; the recovery cannot track a dodging player.
            if(before<ZombieStrikeTime*.7f)TurnZombie(e.body,delta,dt);
            if(before<ZombieStrikeTime&&e.attackTime>=ZombieStrikeTime)
            {
                bool inReach=delta.magnitude<=(e.attackCore?2.35f:1.25f)&&(delta.sqrMagnitude<.001f||Vector3.Dot(e.body.forward,delta.normalized)>.35f);
                if(inReach)
                {
                    if(e.attackCore){if(HasCoreObjective && !coreLost)DamageCore(8);}
                    else if(e.attackBarricade!=null)
                    {if(e.attackBarricade.hp>0){e.attackBarricade.hp-=IncomingDamage(e.zombie!=null?e.zombie.BarricadeDamage:18);UpdateConstructionAppearance(e.attackBarricade.body,e.attackBarricade.hp);if(e.attackBarricade.body.GetComponent<EntranceBoards>()==null)PlayEffect(scrapSound,.5f);}}
                    else if(e.attackTurret!=null)
                    {if(e.attackTurret.hp>0){e.attackTurret.hp-=IncomingDamage(18);PlayEffect(scrapSound,.5f);}}
                    else if(PickupLineClear(e.position,target))
                    {
                        int damage=e.zombie!=null?Mathf.Max(2,10-e.zombie.LostArms*4):10;
                        if(e.attackPeer){if(HasCoopPartner&&peer.health>0)DamagePeer(damage);}
                        else if(!dead)DamagePlayer(damage);
                        PlayZombieSound("Zombie_Bite",e.position,.35f);
                    }
                }
            }
            e.zombie?.TickMovement(dt,0,true,Mathf.Clamp01(e.attackTime/ZombieAttackDuration));
            if(e.attackTime>=ZombieAttackDuration)
            {e.attackTime=-1;e.attack=.12f;e.attackBarricade=null;e.attackTurret=null;}
        }
        void ReactZombieHit(Enemy e,Vector3 direction,int damage,int region)
        {
            e.hitSerial++;e.hitDirection=direction;e.hitRegion=region;e.hitDamage=damage;
            e.zombie?.ReactToHit(direction,region,damage);
            // Heavy hits can also cut a Screamer's call short after it starts.
            if(damage>=3&&e.staggerGuard<=0&&(e.attackTime<ZombieStrikeTime||e.specialAttack==EnemySpecial.Scream))
            {
                e.stagger=.24f;e.staggerGuard=1;e.velocity=Vector3.zero;
                e.attackTime=-1;e.attack=.25f;e.attackBarricade=null;e.attackTurret=null;
            }
        }
    }
}
