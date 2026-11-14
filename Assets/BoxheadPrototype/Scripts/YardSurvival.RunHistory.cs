using System;
using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        [Serializable] sealed class RunHistoryEntry
        {
            public int wave,kills,seconds;
            public string date;
        }
        [Serializable] sealed class RunHistoryData { public List<RunHistoryEntry> runs=new List<RunHistoryEntry>(); }
        bool historyCoop;
        RunHistoryData soloHistory,coopHistory;
        RunDifficulty historyCacheDifficulty;
        internal static bool suppressRunRecords;
        string HistoryKey(bool coop)=>HistoryKey(coop,runDifficulty);
        static string HistoryKey(bool coop,RunDifficulty mode)=>"LastDeparture.Recent.v1."+(coop?"Coop":"Solo")+ModeSuffix(mode);
        RunHistoryData ReadRunHistory(bool coop) => ReadRunHistory(coop,runDifficulty);
        RunHistoryData ReadRunHistory(bool coop,RunDifficulty mode)
        {
            if(historyCacheDifficulty!=mode){soloHistory=coopHistory=null;historyCacheDifficulty=mode;}
            var cached=coop?coopHistory:soloHistory;if(cached!=null)return cached;
            try{cached=JsonUtility.FromJson<RunHistoryData>(PlayerPrefs.GetString(HistoryKey(coop,mode),""));}catch(ArgumentException){cached=null;}
            if(cached==null||cached.runs==null)cached=new RunHistoryData();
            if(coop)coopHistory=cached;else soloHistory=cached;return cached;
        }
        void AppendRunHistory(int seconds)
        {
            var history=ReadRunHistory(runWasCoop);
            history.runs.Insert(0,new RunHistoryEntry{wave=wave,kills=kills,seconds=seconds,date=DateTime.Now.ToString("dd MMM",System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant()});
            if(history.runs.Count>5)history.runs.RemoveRange(5,history.runs.Count-5);
            PlayerPrefs.SetString(HistoryKey(runWasCoop),JsonUtility.ToJson(history));
        }
        void OnApplicationQuit(){SaveRunRecap();}
        void DrawHomeRecords()
        {
            var small=new GUIStyle(homeSmall){fontSize=17};
            bool previous=GUI.enabled;GUI.enabled=previous&&!audioCreditsVisible;
            try
            {
                if(FeedbackButton(new Rect(103,221,82,28),"SOLO",new GUIStyle(small){normal={textColor=historyCoop?PauseMuted:PauseInk}}))historyCoop=false;
                if(FeedbackButton(new Rect(198,221,90,28),"CO-OP",new GUIStyle(small){normal={textColor=historyCoop?PauseInk:PauseMuted}}))historyCoop=true;
                string key=BestKey(historyCoop,runDifficulty);
                int best=PlayerPrefs.GetInt(key+"Wave",0);
                PauseLabel(new Rect(103,258,510,36),"BEST WAVE  "+(best==0?"—":best.ToString("00")),homeAction,PauseInk);
                PauseLabel(new Rect(103,299,510,25),"BEST KILLS  "+PlayerPrefs.GetInt(key+"Kills",0)+"     LONGEST RUN  "+RunDuration(PlayerPrefs.GetInt(key+"Seconds",0)),small,PauseMuted);
                Fill(new Rect(103,337,490,1),new Color(.6f,.6f,.55f,.25f));
                PauseLabel(new Rect(103,346,500,25),"RECENT RUNS · WAVE REACHED",small,PauseMuted);
                var history=ReadRunHistory(historyCoop);
                if(history.runs.Count==0)PauseLabel(new Rect(103,385,510,45),"Your next run will appear here.",small,PauseMuted);
                for(int i=0;i<history.runs.Count;i++)
                {
                    var run=history.runs[i];float y=380+i*29;
                    PauseLabel(new Rect(103,y,85,25),run.date,small,PauseMuted);
                    PauseLabel(new Rect(195,y,132,25),"WAVE "+run.wave.ToString("00"),small,PauseInk);
                    PauseLabel(new Rect(335,y,145,25),run.kills+" KILLS",small,PauseMuted);
                    PauseLabel(new Rect(495,y,95,25),RunDuration(run.seconds),small,PauseMuted);
                }
            }
            finally{GUI.enabled=previous;}
        }
    }
}
