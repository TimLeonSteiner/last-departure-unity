#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunZombieCrowdChecks()
        {
            var report=new System.Text.StringBuilder();
            void Check(bool ok,string message){if(!ok)throw new Exception(message);report.AppendLine("PASS "+message);}
            void Simulate(int frames)
            {
                for(int i=0;i<frames;i++)
                {
                    health=100;dead=false;damageTimer=1;
                    if(i%6==0)RefreshPaths();UpdateEnemies(.05f);
                }
            }
            ResetRun();
            try
            {
                player.position=Vector3.zero;
                for(int row=0;row<3;row++)for(int col=0;col<4;col++)SpawnEnemy(new Vector3((col-1.5f)*.9f,0,5+row*.95f));
                Simulate(300);
                float minimum=float.PositiveInfinity;
                for(int i=0;i<enemies.Count;i++)for(int j=i+1;j<enemies.Count;j++)minimum=Mathf.Min(minimum,Vector3.Distance(enemies[i].position,enemies[j].position));
                Check(minimum>=.74f,"Approaching horde maintains body spacing (minimum "+minimum.ToString("F2")+" m)");
                int attackers=enemies.Count(e=>e.position.magnitude<1.3f);
                Check(attackers>=3,"Several zombies reach attack range instead of queueing behind one ("+attackers+")");
                Check(enemies.Max(e=>e.position.x)-enemies.Min(e=>e.position.x)>2,"Horde spreads its approach across the player");
                Check(enemies.All(e=>Free(e.position,EnemyRadius,true)),"Crowd separation respects the hall collision geometry");
                var lead=enemies[0];var other=enemies[1];lead.position=new Vector3(0,0,10);other.position=new Vector3(0,0,10.8f);
                Vector3 stopped=MoveZombieInCrowd(lead,Vector3.forward*2);
                Check(Vector3.Distance(stopped,other.position)>=.75f,"A long frame cannot push through a stationary zombie");
                ResetRun();player.position=new Vector3(13,0,-5);
                for(int row=0;row<2;row++)for(int col=0;col<3;col++)SpawnEnemy(new Vector3(11.5f+col*.9f,0,-18-row));
                Simulate(600);
                int arrived=enemies.Count(e=>Vector3.Distance(e.position,player.position)<3);
                Check(arrived>=4,"Group routes around a server island and resumes pursuit ("+arrived+"/6 arrived)");
                Check(enemies.All(e=>Free(e.position,EnemyRadius,true)),"Routing around racks never crosses solid geometry");
                ResetRun();player.position=new Vector3(0,0,14);
                for(int row=0;row<4;row++)SpawnEnemy(new Vector3(0,0,18+row*.85f));
                Simulate(240);
                Check(enemies.Count(e=>Vector3.Distance(e.position,player.position)<2)>=3,"Entrance queue opens out after entering the hall");
                return report.ToString();
            }
            finally{ResetRun();}
        }
    }
}
#endif
