using System;
using System.Text;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunDifficultyChecks()
        {
            var report=new StringBuilder();
            void Check(bool value,string label){if(!value)throw new Exception(label);report.AppendLine("PASS "+label);}
            var savedRun=runDifficulty;var savedSelected=selectedDifficulty;
            try
            {
                Check(BestKey(false,RunDifficulty.Normal)=="LastDeparture.Best.v1.Solo.","Existing Normal best records retain their key");
                Check(HistoryKey(false,RunDifficulty.Normal)=="LastDeparture.Recent.v1.Solo","Existing Normal recent runs retain their key");
                var keys=new System.Collections.Generic.HashSet<string>();
                foreach(var mode in DifficultyOrder)
                {
                    runDifficulty=mode;
                    Check(keys.Add(BestKey(false,mode))&&keys.Add(BestKey(true,mode)),mode+" solo and co-op records are isolated");
                    Check(keys.Add(HistoryKey(false,mode))&&keys.Add(HistoryKey(true,mode)),mode+" recent runs are isolated");
                    Check(IncomingDamage(0)==0,mode+" zero damage stays zero");
                    Check(IncomingDamage(10)==(mode==RunDifficulty.Easy?7:mode==RunDifficulty.Hard?13:10),mode+" incoming damage balance");
                    Check(StartingScrap==(mode==RunDifficulty.Easy?18:mode==RunDifficulty.Hard?8:12),mode+" starting scrap");
                    Check(WaveScrap==(mode==RunDifficulty.Easy?8:mode==RunDifficulty.Hard?4:6),mode+" wave rewards");
                    var snapshot=JsonUtility.FromJson<CoopWorldState>(JsonUtility.ToJson(new CoopWorldState{difficulty=(int)mode}));
                    Check(snapshot.difficulty==(int)mode,mode+" survives co-op snapshot serialization");
                }
                for(int hp=1;hp<=30;hp++)
                {
                    int easy=Mathf.Max(1,Mathf.RoundToInt(hp*HealthScale(RunDifficulty.Easy)));
                    int normal=Mathf.Max(1,Mathf.RoundToInt(hp*HealthScale(RunDifficulty.Normal)));
                    int hard=Mathf.Max(1,Mathf.RoundToInt(hp*HealthScale(RunDifficulty.Hard)));
                    Check(easy<=normal&&normal==hp&&hard>=normal,"Health ordering for base HP "+hp);
                }
                runDifficulty=RunDifficulty.Hard;selectedDifficulty=RunDifficulty.Easy;
                Check(IncomingDamage(10)==13&&StartingScrap==8,"Menu selection cannot rebalance an active run");
                foreach(string name in new[]{"Turret","Barrel","Barricade"})
                {
                    var clip=Resources.Load<AudioClip>("PlacementAudio/"+name);
                    Check(clip!=null&&clip.samples>0&&clip.frequency==48000,name+" approved placement audio loads");
                    if(name=="Barricade")Check(clip.length<1,"Barricade is a single short placement clip");
                }
            }
            finally{runDifficulty=savedRun;selectedDifficulty=savedSelected;}
            return report.ToString();
        }
    }
}
