#if UNITY_EDITOR
using System;
using System.Text;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunChargerChecks()
        {
            var report = new StringBuilder();
            bool saved = suppressRunRecords; suppressRunRecords = true;
            void Check(bool ok, string label) { if (!ok) throw new Exception(label); report.AppendLine("PASS " + label); }
            void Setup()
            {
                ResetRun(); runDifficulty=RunDifficulty.Normal; wave = 17; fighting = true; spawnSerial = 15;
                player.position = new Vector3(0, .65f, 13);
                SpawnEnemy(new Vector3(0, 0, 8));
                Check(zombieModels[enemies[0].variant].name == "Charger", "Actual charger prefab selected");
                Check(enemies[0].zombie.attackClip != null && enemies[0].zombie.attackClip != enemies[0].zombie.walkClip, "Distinct charge animation assigned");
                enemies[0].body.forward = Vector3.back;
                RefreshPaths();
            }
            try
            {
                Setup(); var e = enemies[0];
                for (int i = 0; i < 45 && e.specialAttack != EnemySpecial.Charge; i++) UpdateEnemies(.05f);
                Check(e.specialAttack == EnemySpecial.Charge, "Initially backward-facing charger commits to player instead of core");
                Vector3 before = e.position;
                for(int i=0;i<8;i++)UpdateEnemies(.05f);
                Check(Vector3.Distance(before,e.position)<.01f,"Windup holds position");
                for(int i=0;i<25 && knockdownTime<=0;i++)UpdateEnemies(.05f);
                Check(Vector3.Distance(before,e.position)>3f,"Rush closes the distance to the player");
                Check(health==76 && knockdownTime>0 && e.chargeStopped,"Impact deals 24 damage and begins knockdown (health="+health+", knockdown="+knockdownTime+")");
                UpdateKnockdown(.2f); UpdateKnockdown(.05f);
                Check(eyeHeight<.4f,"Knockdown lowers the player camera to the floor");
                for(int i=0;i<30;i++)UpdateKnockdown(.05f);
                Check(knockdownTime==0,"Knockdown recovers");
                for(int i=0;i<40;i++)UpdateEnemies(.05f);
                Check(health==76,"One rush cannot repeatedly damage the player");
                Setup(); e=enemies[0];e.body.forward=Vector3.forward;
                Check(TryBeginSpecialAttack(e,false,new Vector3(0,0,13),.05f),"Second rush starts");
                for(int i=0;i<9;i++)UpdateEnemies(.05f);
                player.position=new Vector3(3,.65f,13);
                for(int i=0;i<30;i++)UpdateEnemies(.05f);
                Check(health==100 && knockdownTime==0,"Sidestepping after commitment avoids impact");
                e.zombie.InjureLeg(3);e.zombie.InjureLeg(4);
                Check(e.zombie.InjuredLegs==0 && !e.zombie.IsCollapsing,"Chargers never enter the leg-injury crawl state");
                Check(!e.zombie.Sever(3,Vector3.forward,actors),"Chargers retain their supporting legs");
                e.zombie.Die(Vector3.forward,e.body.position+Vector3.up);
                Check(e.zombie.RagdollBodyCount>0,"Charger death starts physics from its current pose");
                Check(NightmareAppearance.DarknessAmount(.19f)==0 && NightmareAppearance.DarknessAmount(.2f)==0,"Zombie darkening stays off through 20 percent sleepiness");
                Check(NightmareAppearance.DarknessAmount(.3f)>0 && NightmareAppearance.DarknessAmount(1)==1,"Zombie darkening ramps progressively after 20 percent");
                Check(Resources.Load<AudioClip>("EnemyFeedback/ScreamerCall")!=null,"Dedicated screamer shriek imports successfully");
                UnlockAudio();PlayScreamerCall(e);
                Check(screamerCall!=null && screamerCall.clip!=null && screamerCall.isPlaying && screamerPings.ContainsKey(e),"Dedicated screamer audio starts and location ping is registered");
                ClearScreamerFeedback();
                Check(screamerPings.Count==0 && !screamerCall.isPlaying,"Reset clears screamer feedback");
                report.Append(RunBalanceChecks());
                return report.ToString();
            }
            finally { ShowHome(); suppressRunRecords=saved; }
        }
    }
}
#endif
