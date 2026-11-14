#if UNITY_EDITOR
using System;
using System.Text;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunEncounterGameplayChecks()
        {
            var report=new StringBuilder();bool savedSuppression=suppressRunRecords;suppressRunRecords=true;
            void Check(bool ok,string message){if(!ok)throw new Exception(message);report.AppendLine("PASS "+message);}
            void Setup(){ResetRun();coreVisual=null;health=10000;dead=false;}
            void Simulate(int frames)
            {
                for(int i=0;i<frames;i++){health=10000;dead=false;damageTimer=1;if(i%6==0)RefreshPaths();UpdateEnemies(.05f);}
            }
            try
            {
                Setup();player.position=new Vector3(0,0,12);SpawnEnemy(new Vector3(0,0,8));SpawnEnemy(new Vector3(1,0,8));
                var caller=enemies[0];int screamer=Array.FindIndex(zombieModels,m=>m.name=="Screamer");
                Check(screamer>=0,"Screamer exists in roster");caller.variant=screamer;caller.screamCooldown=0;caller.body.forward=Vector3.forward;
                fighting=true;wave=4;
                Check(TryBeginSpecialAttack(caller,false,player.position,.05f)&&caller.specialAttack==EnemySpecial.Scream,"Screamer begins visible wind-up");
                TickSpecialAttack(caller,.84f);Check(remainingToSpawn==0,"No reinforcements before scream release");
                TickSpecialAttack(caller,.02f);Check(IsScreaming(caller)&&remainingToSpawn==0,"Scream alert starts before any reinforcement batch");
                Check(enemies[1].rallyRemaining>0,"Nearby infected rally to scream");
                TickSpecialAttack(caller,1f);Check(remainingToSpawn==2&&enemies.Count==2,"First full second queues two infected");
                TickSpecialAttack(caller,1f);Check(remainingToSpawn==4,"Second full second queues two more infected");
                TickSpecialAttack(caller,1f);Check(remainingToSpawn==6&&!IsScreaming(caller),"Full scream queues six infected and clears the alert");
                caller.attackTime=-1;caller.attack=0;caller.screamCooldown=0;caller.staggerGuard=0;
                Check(TryBeginSpecialAttack(caller,false,player.position,.05f),"Screamer can begin a later call");
                TickSpecialAttack(caller,.5f);ReactZombieHit(caller,Vector3.forward,3,5);
                Check(caller.attackTime<0&&remainingToSpawn==6,"Heavy hit interrupts the scream wind-up");
                caller.attack=0;caller.screamCooldown=0;caller.staggerGuard=0;
                Check(TryBeginSpecialAttack(caller,false,player.position,.05f),"Screamer can call again");
                TickSpecialAttack(caller,1.9f);
                ReactZombieHit(caller,Vector3.forward,3,5);
                Check(caller.attackTime<0&&!IsScreaming(caller)&&remainingToSpawn==8,"Interrupting a scream keeps only the completed batch");
                spawnTimer=0;TickWaveSimulation(.01f);
                Check(enemies.Count==3&&enemies[2].variant==0&&Mathf.Max(Mathf.Abs(enemies[2].position.x),Mathf.Abs(enemies[2].position.z))>26,"Reinforcements spawn outdoors as ordinary infected");
                Setup();Check(screamReinforcements.Count==0,"Restart clears queued scream reinforcements");
                player.position=new Vector3(0,0,13);
                for(int i=0;i<6;i++)SpawnEnemy(new Vector3(0,0,5+i*.9f));
                RefreshPaths();float minHeading=1,maxHeading=-1;
                foreach(var e in enemies){float x=ZombiePursuit(e,player.position,false).normalized.x;minHeading=Mathf.Min(minHeading,x);maxHeading=Mathf.Max(maxHeading,x);}
                Check(maxHeading-minHeading>.2f,"Straight-line queue receives distinct lateral approach directions");
                Simulate(320);
                float closest=float.PositiveInfinity,minX=float.PositiveInfinity,maxX=float.NegativeInfinity;int arrived=0;
                for(int i=0;i<enemies.Count;i++)
                {
                    var e=enemies[i];minX=Mathf.Min(minX,e.position.x);maxX=Mathf.Max(maxX,e.position.x);
                    if(Vector3.Distance(e.position,player.position)<2.2f)arrived++;
                    Check(Free(e.position,EnemyRadius,true),"Pursuer "+i+" stays outside walls");
                    for(int j=i+1;j<enemies.Count;j++)closest=Mathf.Min(closest,Vector3.Distance(e.position,enemies[j].position));
                }
                Check(closest>=.74f,"Horde retains body separation");Check(maxX-minX>1.5f,"Queue fans out across the target");Check(arrived>=3,"Multiple attackers reach the target ("+arrived+")");
                Setup();player.position=new Vector3(13,0,-5);
                for(int row=0;row<2;row++)for(int col=0;col<3;col++)SpawnEnemy(new Vector3(11.5f+col*.9f,0,-18-row));
                Simulate(600);arrived=0;
                foreach(var e in enemies){if(Vector3.Distance(e.position,player.position)<3)arrived++;Check(Free(e.position,EnemyRadius,true),"Rack routing remains collision safe");}
                Check(arrived>=4,"Group routes around server island ("+arrived+"/6)");
                Setup();player.position=new Vector3(0,0,14);
                for(int i=0;i<4;i++)SpawnEnemy(new Vector3((i%2-.5f)*1.1f,0,27+(i/2)*1.2f));
                Simulate(950);arrived=0;
                foreach(var e in enemies)if(e.position.z<20)arrived++;
                Check(arrived>=3,"Outdoor group breaches entrance and enters hall ("+arrived+"/4)");
                return report.ToString();
            }
            finally{ShowHome();suppressRunRecords=savedSuppression;}
        }
    }
}
#endif
