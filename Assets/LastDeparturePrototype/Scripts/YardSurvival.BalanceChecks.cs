#if UNITY_EDITOR
using System;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunBalanceChecks()
        {
            void Check(bool ok,string message){if(!ok)throw new Exception(message);}
            var state=new DrowsinessState();state.Reset(.8f);state.Collect(2);
            Check(state.Drink(60)&&Mathf.Approximately(state.Amount,.8f),"Caffeine must not snap awake");
            state.Tick(1.5f,38);Check(Mathf.Approximately(state.Amount,.4f),"Caffeine midpoint fade");
            state.Tick(0,38);Check(Mathf.Approximately(state.Amount,.4f),"Paused fade must not advance");
            state.Tick(1.5f,38);Check(state.Amount==0,"Caffeine fade completes");
            state.Tick(76,38);Check(Mathf.Approximately(state.Amount,.5f),"Protection expires without losing excess tick time");
            state.Collect(1);Check(state.Drink(60),"Repeat dose after expiry");state.Tick(.5f,38);state.Wake(8);
            Check(state.Amount==0,"Thunder cancels an in-progress fade immediately");
            Check(!state.Drink(60),"Active dose cannot be wasted");
            for(int stage=1;stage<=30;stage++)
            {
                Check(Mathf.Abs(CaffeineChanceForWave(stage,.04f)*WaveZombieCount(stage)-1.28f)<.001f,"Drop expectation must not grow with hordes");
                Check(WaveZombieHealth(3,stage)==3,"Normal zombie health stays fixed across waves");
                Check(WaveZombieHealth(9,stage,true)==15,"Charger durability stays fixed across waves");
                if(stage>1)Check(WaveZombieCount(stage)>WaveZombieCount(stage-1),"Later waves increase enemy numbers");
                Check(WaveZombieHealth(8,stage)>=WaveZombieHealth(3,stage),"Archetype health preserved");
            }
            Check(OpeningWaveWeapons[0]==14&&OpeningWaveWeapons[1]==3&&OpeningWaveWeapons[2]==8,"Three distinct non-pistol opening rewards");
            Check(WaveZombieHealth(3,17)==3&&WaveZombieHealth(9,17,true)==15,"Wave 17 keeps normal enemies fragile and chargers durable");
            // Exercise persistence through the actual serializer, restoring every preference afterwards.
            string key=HistoryKey(false);bool existed=PlayerPrefs.HasKey(key);string saved=PlayerPrefs.GetString(key);
            var oldHistory=soloHistory;int oldWave=wave,oldKills=kills;bool oldCoop=runWasCoop;
            try
            {
                soloHistory=new RunHistoryData();runWasCoop=false;
                for(int i=1;i<=7;i++){wave=i;kills=i*10;AppendRunHistory(i*30);}
                soloHistory=null;var restored=ReadRunHistory(false);
                Check(restored.runs.Count==5&&restored.runs[0].wave==7&&restored.runs[4].wave==3,"Five most recent runs persist newest first");
            }
            finally
            {
                wave=oldWave;kills=oldKills;runWasCoop=oldCoop;soloHistory=oldHistory;
                if(existed)PlayerPrefs.SetString(key,saved);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
            }
            return "PASS caffeine fade/repeat/pause/thunder/expiry; wave-scaled drops; 30-wave health progression; opening reward variety; five-run persistence.\n";
        }
    }
}
#endif
