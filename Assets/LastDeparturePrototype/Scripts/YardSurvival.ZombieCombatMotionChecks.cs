#if UNITY_EDITOR
using System;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public void ReviewZombieMotionPoses(Action capture)
        {
            ResetRun();
            try
            {
                float[] progress={.15f,ZombieStrikeTime/ZombieAttackDuration,.8f};
                for(int i=0;i<3;i++)
                {
                    SpawnEnemy(new Vector3((i-1)*2,0,4));var e=enemies[enemies.Count-1];
                    e.body.rotation=Quaternion.Euler(0,180,0);
                    e.zombie?.TickMovement(.2f,0,true,progress[i]);
                }
                capture();
            }
            finally{ResetRun();}
        }
        public string RunZombieCombatMotionChecks()
        {
            var report=new System.Text.StringBuilder();
            void Check(bool value,string message){if(!value)throw new Exception(message);report.AppendLine("PASS "+message);}
            ResetRun();
            try
            {
                SpawnEnemy(new Vector3(0,0,4));var e=enemies[enemies.Count-1];
                void Prepare(){player.position=new Vector3(0,0,4.9f);e.body.rotation=Quaternion.identity;health=100;damageTimer=0;dead=false;e.stagger=0;e.staggerGuard=0;BeginZombieAttack(e,false,null,null);}
                Prepare();TickZombieAttack(e,.39f);Check(health==100,"Wind-up causes no damage");
                TickZombieAttack(e,.02f);Check(health==90,"Strike causes one hit at contact");
                damageTimer=0;TickZombieAttack(e,.5f);Check(health==90,"Recovery cannot cause repeated damage");
                TickZombieAttack(e,.2f);Check(e.attackTime<0&&e.attack>0,"Completed swing returns to movement with a cooldown");
                Prepare();TickZombieAttack(e,.3f);player.position=new Vector3(0,0,6);TickZombieAttack(e,.15f);Check(health==100,"Backing out during anticipation avoids damage");
                Prepare();TickZombieAttack(e,.3f);player.position=new Vector3(0,0,3.1f);TickZombieAttack(e,.15f);Check(health==100,"Dodging behind the committed swing avoids damage");
                Prepare();TickZombieAttack(e,.8f);Check(health==90,"A long frame crossing contact still delivers exactly one hit");
                Prepare();var b=new Barricade{bounds=new Rect(-.5f,4.6f,1,1),hp=100};BeginZombieAttack(e,false,b,null);
                TickZombieAttack(e,.3f);Check(b.hp==100,"Barricades receive no damage before contact");
                TickZombieAttack(e,.15f);Check(b.hp==82,"Barricade damage uses the same strike timing");
                Prepare();ReactZombieHit(e,Vector3.forward,1,5);Check(e.attackTime==0&&e.stagger==0,"Light hits preserve the attack");
                ReactZombieHit(e,Vector3.right,3,0);Check(e.attackTime<0&&e.stagger>0,"Heavy hits interrupt anticipation");
                e.stagger=0;BeginZombieAttack(e,false,null,null);ReactZombieHit(e,Vector3.right,3,5);Check(e.attackTime==0&&e.stagger==0,"Stagger guard prevents repeated interruptions");
                Prepare();TickZombieAttack(e,.45f);ReactZombieHit(e,Vector3.right,3,5);Check(e.attackTime>ZombieStrikeTime&&e.stagger==0,"Heavy hits cannot cancel completed contact");
                var state=new CoopEnemyState{id=e.netId,variant=e.variant,hp=e.hp,mask=63,position=e.position,attackTime=.25f,attackSerial=3,hitSerial=2,hitRegion=0,hitDirection=Vector3.right};
                var copy=JsonUtility.FromJson<CoopEnemyState>(JsonUtility.ToJson(state));
                ApplyCoopEnemies(new[]{copy});Check(e.attackTime==.25f&&e.attackSerial==3&&e.hitSerial==2,"Network snapshot applies authoritative attack and hit states");
                Prepare();Vector3 beforeCollapse=e.position;
                e.zombie.Sever(3,Vector3.forward,actors);e.zombie.Sever(4,Vector3.forward,actors);
                UpdateEnemies(.2f);UpdateEnemies(.3f);
                Check(health==100&&e.attackTime<0&&e.position==beforeCollapse,"Collapsing zombie cancels its strike and cannot slide toward the player");
                var fallingState=new CoopEnemyState{id=e.netId,variant=e.variant,hp=e.hp,mask=39,position=e.position,attackTime=-1,collapseRemaining=.12f,hitDamage=8};
                var fallingCopy=JsonUtility.FromJson<CoopEnemyState>(JsonUtility.ToJson(fallingState));ApplyCoopEnemies(new[]{fallingCopy});
                Check(Mathf.Abs(e.zombie.CollapseTimeRemaining-.12f)<.001f&&fallingCopy.hitDamage==8,"Snapshot preserves collapse progress and shot strength");
                report.AppendLine(RunZombieMovementChecks());
                return report.ToString();
            }
            finally{ResetRun();}
        }
    }
}
#endif
