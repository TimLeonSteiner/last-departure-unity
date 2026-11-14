#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public void PreviewCoreUpgrades()
        {
            ResetRun();frontScreen=FrontScreen.Game;wave=1;OfferCoreUpgrades();
            coreOffers[0]=0;coreOffers[1]=1;coreOffers[2]=3;
        }
        public string RunCoreObjectiveChecks()
        {
            var report=new System.Text.StringBuilder();
            void Check(bool ok,string label){if(!ok)throw new Exception(label);report.AppendLine("PASS "+label);}
            ResetRun();
            try
            {
                Check(HasCoreObjective&&coreHealth==500,"Run creates a full-health suspended core");
                Check(Free(player.position,PlayerRadius,true)&&!Free(Vector3.zero,PlayerRadius,true),"Player spawns clear of the protected core footprint");
                player.position=new Vector3(-8,.65f,-8);SpawnEnemy(new Vector3(0,0,5));var e=enemies.Last();
                for(int i=0;i<180;i++){if(i%6==0)RefreshPaths();UpdateEnemies(.05f);}
                Check(coreHealth<500&&health==100,"Distant zombie reaches and damages core instead of chasing player");
                e.attackTime=-1;e.attack=0;player.position=e.position+e.body.forward*.8f;player.position+=Vector3.up*.65f;
                UpdateEnemies(.05f);
                Check(e.attackTime>=0&&!e.attackCore,"Zombie switches to a nearby player");
                int coreBefore=coreHealth;for(int i=0;i<12;i++)UpdateEnemies(.05f);
                Check(health<100&&coreHealth==coreBefore,"Committed player swing damages player rather than core");
                ResetRun();player.position=new Vector3(-18,.65f,-18);SpawnEnemy(new Vector3(13,0,-18));
                for(int i=0;i<800;i++){if(i%6==0)RefreshPaths();UpdateEnemies(.05f);}
                Check(coreHealth<500&&enemies.All(enemy=>Free(enemy.position,EnemyRadius,true)),"Core-bound zombie routes around server racks without crossing walls");
                ResetRun();StartWave();int cleared=wave;CompleteWave();
                Check(!upgradePending && skillPoints == 1 && nextWaveCountdown == 15,"Wave completion grants a banked skill point and starts preparation");
                Check(BuySkill(0) && weaponBoost == 1 && !BuySkill(0),"One earned point buys exactly one rank");
                frontScreen = FrontScreen.Game; TickNextWave(16);
                Check(wave == cleared + 1 && fighting,"Next wave starts without forced upgrade selection");
                DamageCore(10000);Check(coreLost&&dead&&!fighting&&nextWaveCountdown<0,"Destroyed core ends the run");
                ResetRun();Check(!coreLost&&!upgradePending&&weaponBoost==0&&maxPlayerHealth==100&&coreHealth==500&&reloadBoost==1,"Restart clears upgrades, defeat and core damage");
                return report.ToString();
            }
            finally{ResetRun();frontScreen=FrontScreen.Game;}
        }
        public string RunBayCollisionChecks()
        {
            var report=new System.Text.StringBuilder();
            void Check(bool ok,string label){if(!ok)throw new Exception(label);report.AppendLine("PASS "+label);}
            try
            {
                ResetRun();
                foreach(var barrier in coreVisual.BayBarriers)
                {
                    Check(!Free(barrier.center,EnemyRadius,false),"Bay barrier blocks navigation and movement at "+barrier.center);
                    var start=barrier.center+Vector3.back*2;start.y=0;
                    if(Free(start,EnemyRadius,true))Check(Free(Move(start,Vector3.forward*4,EnemyRadius,true),EnemyRadius,true),"Large movement step cannot tunnel through bay barrier");
                }
                foreach(var entry in new[]{new Vector3(0,0,-2.2f),new Vector3(0,0,2.2f),new Vector3(-2.7f,0,0),new Vector3(2.7f,0,0)})
                    Check(Free(entry,EnemyRadius+.15f,false),"Bay entrance admits navigation clearance: "+entry);
                Check(Move(new Vector3(1.8f,0,-4),Vector3.forward*4,EnemyRadius,true).z < -2.6f,"Fast forward movement stops before the front railing");
                Check(Move(new Vector3(4,0,-1.3f),Vector3.left*4,EnemyRadius,true).x > 3.1f,"Fast lateral movement stops before the side railing");
                Check(!ClearPursuitLine(new Vector3(1.8f,0,-4),new Vector3(1.8f,0,0)),"Path shortcuts cannot cross the front railing");
                foreach(var start in new[]{new Vector3(4,0,4),new Vector3(-4,0,4),new Vector3(4,0,-4),new Vector3(-4,0,-4)})
                {
                    ResetRun();player.position=new Vector3(-18,.65f,-18);SpawnEnemy(start);var enemy=enemies.Last();
                    for(int i=0;i<500;i++){if(i%6==0)RefreshPaths();UpdateEnemies(.05f);Check(Free(enemy.position,EnemyRadius,true),"Zombie remains outside barriers");}
                    Check(coreHealth<500,"Zombie routes from "+start+" to attack the heart");
                }
                return "PASS all bay barriers, four entrances, swept movement, and four zombie approach simulations\n";
            }
            finally{ResetRun();}
        }
    }
}
#endif
