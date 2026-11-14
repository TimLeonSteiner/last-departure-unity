#if UNITY_EDITOR
using System;
using System.Text;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunDifficultyScreenChecks()
        {
            var report=new StringBuilder();bool suppression=suppressRunRecords;
            var saved=selectedDifficulty;bool hadPreference=PlayerPrefs.HasKey(DifficultyPreference);int preference=PlayerPrefs.GetInt(DifficultyPreference);
            void Check(bool ok,string label){if(!ok)throw new Exception(label);report.AppendLine("PASS "+label);}
            suppressRunRecords=true;
            try
            {
                StopLaunchSounds();RestoreLaunchRendering();ShowHome();
                ActivateHomeAction(0);
                Check(frontScreen==FrontScreen.Difficulty&&wave==0&&!fighting,"Play opens setup without starting a wave");
                CloseDifficultyScreen();Check(frontScreen==FrontScreen.Home&&wave==0,"Back returns home without starting a run");
                foreach(var mode in DifficultyOrder)
                {
                    ActivateHomeAction(0);SelectDifficulty(mode);
                    Check(PlayerPrefs.GetInt(DifficultyPreference)==(int)mode,"Remember "+mode);
                    CloseDifficultyScreen();ActivateHomeAction(0);
                    Check(selectedDifficulty==mode,"Reopening retains "+mode);
                    StartDifficultyRun();
                    Check(frontScreen==FrontScreen.Game&&fighting&&wave==1&&runDifficulty==mode,"Start Run launches "+mode+" screen="+frontScreen+" wave="+wave+" fighting="+fighting+" mode="+runDifficulty);
                    Check(supplies==(mode==RunDifficulty.Easy?18:mode==RunDifficulty.Hard?8:12),"Starting resources match "+mode);
                    int currentWave=wave;StartDifficultyRun();Check(wave==currentWave,"Start Run cannot retrigger from gameplay");
                    ShowHome();
                }
                return report.ToString();
            }
            finally
            {
                selectedDifficulty=saved;
                if(hadPreference)PlayerPrefs.SetInt(DifficultyPreference,preference);else PlayerPrefs.DeleteKey(DifficultyPreference);
                PlayerPrefs.Save();ShowHome();ShowDifficultyScreen();suppressRunRecords=suppression;
            }
        }
    }
}
#endif
