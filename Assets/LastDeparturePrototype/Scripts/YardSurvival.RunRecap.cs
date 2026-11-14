using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        float runSeconds;
        int runWavesCleared;
        bool runRecapSaved, runWasCoop;
        int bestRunWave, bestRunKills, bestRunSeconds;
        bool recordWave, recordKills, recordTime;

        bool PartnerCanContinue => (HasCoopPartner && peer.health > 0) ||
            (IsCoopGuest && replicaPeerHealth > 0);

        void ResetRunRecap()
        {
            editorPreviewRun = false;
            runDifficulty = selectedDifficulty;
            runSeconds = 0; runWavesCleared = 0; runRecapSaved = false;
            runWasCoop = IsCoopHost || IsCoopGuest || dedicatedServer;
            recordWave = recordKills = recordTime = false;
        }

        void TickRunRecap()
        {
            if (wave <= 0 || runRecapSaved) return;
            runWasCoop |= IsCoopHost || IsCoopGuest;
            if (dead && !PartnerCanContinue)
            {
                SaveRunRecap();
                return;
            }
            // The authoritative simulation advances the clock; guests receive it in snapshots.
        }

        void SaveRunRecap()
        {
            if (editorPreviewRun || suppressRunRecords || dedicatedServer || runRecapSaved || wave <= 0) return;
            runRecapSaved = true;
            string key = BestKey(runWasCoop,runDifficulty);
            bestRunWave = PlayerPrefs.GetInt(key + "Wave", 0);
            bestRunKills = PlayerPrefs.GetInt(key + "Kills", 0);
            bestRunSeconds = PlayerPrefs.GetInt(key + "Seconds", 0);
            int seconds = Mathf.FloorToInt(runSeconds);
            recordWave = wave > bestRunWave; recordKills = kills > bestRunKills;
            recordTime = seconds > bestRunSeconds;
            bestRunWave = Mathf.Max(wave, bestRunWave);
            bestRunKills = Mathf.Max(kills, bestRunKills);
            bestRunSeconds = Mathf.Max(seconds, bestRunSeconds);
            PlayerPrefs.SetInt(key + "Wave", bestRunWave);
            PlayerPrefs.SetInt(key + "Kills", bestRunKills);
            PlayerPrefs.SetInt(key + "Seconds", bestRunSeconds);
            AppendRunHistory(seconds);
            PlayerPrefs.Save();
        }

        static string RunDuration(int seconds) => (seconds / 60) + ":" + (seconds % 60).ToString("00");

        [System.NonSerialized] GUIStyle recapTitle, recapValue, recapLabel, recapAction, recapSecondary;

        void DrawRunRecap(float width, float height)
        {
            if (recapTitle == null)
            {
                var body = Resources.Load<Font>("PauseMenu/InterQuiet");
                var display = Resources.Load<Font>("PauseMenu/InterDisplayBlack");
                recapTitle = new GUIStyle { font = display, fontSize = 130, alignment = TextAnchor.MiddleCenter };
                recapValue = new GUIStyle(recapTitle) { fontSize = 65 };
                recapLabel = new GUIStyle { font = body, fontSize = 29, alignment = TextAnchor.MiddleCenter };
                recapAction = new GUIStyle(recapTitle) { fontSize = 49 };
                recapSecondary = new GUIStyle(recapLabel) { fontSize = 36 };
            }

            const float panelWidth = 1600, panelHeight = 900;
            float scale = Mathf.Min(width / panelWidth, height / panelHeight);
            if (scale <= 0) return;
            Fill(new Rect(0, 0, width, height), new Color(0, 0, 0, .32f));
            var previous = GUI.matrix;
            GUI.matrix = previous * Matrix4x4.TRS(
                new Vector3((width - panelWidth * scale) / 2, (height - panelHeight * scale) / 2, 0),
                Quaternion.identity, Vector3.one * scale);
            try
            {
                PauseLabel(new Rect(120, 94, 1360, 150), coreLost ? "CORE LOST" : "YOU DIED", recapTitle, PauseInk);
                string cleared = runWavesCleared == 1 ? "1 WAVE CLEARED" : runWavesCleared + " WAVES CLEARED";
                string summary = (runWasCoop ? "CO-OP / " : "") + DifficultyName(runDifficulty) + "   ·   " + cleared;
                float summaryHalfWidth = recapLabel.CalcSize(new GUIContent(summary)).x / 2;
                Fill(new Rect(800 - summaryHalfWidth - 132, 269, 110, 2), DifficultyRed);
                Fill(new Rect(800 + summaryHalfWidth + 22, 269, 110, 2), DifficultyRed);
                PauseLabel(new Rect(180, 245, 1240, 48), summary, recapLabel, PauseInk);

                string[] labels = { "WAVE REACHED", runWasCoop ? "TEAM KILLS" : "KILLS", "SURVIVAL TIME" };
                string[] values = { wave.ToString(), kills.ToString(), RunDuration(Mathf.FloorToInt(runSeconds)) };
                string[] bests = { bestRunWave.ToString(), bestRunKills.ToString(), RunDuration(bestRunSeconds) };
                bool[] records = { recordWave, recordKills, recordTime };
                Fill(new Rect(200, 328, 1200, 176), new Color(.037f, .036f, .036f, .79f));
                Fill(new Rect(200, 503, 1200, 1), DifficultyRule);
                for (int i = 0; i < 3; i++)
                {
                    float x = 200 + i * 400;
                    if (i > 0) Fill(new Rect(x, 360, 2, 112), DifficultyRed);
                    PauseLabel(new Rect(x, 341, 400, 42), labels[i], recapLabel, PauseInk);
                    PauseLabel(new Rect(x, 380, 400, 75), values[i], recapValue, PauseInk);
                    PauseLabel(new Rect(x, 450, 400, 42), (records[i] ? "NEW BEST  " : "BEST  ") + bests[i],
                        recapLabel, records[i] ? new Color(.85f, .73f, .43f) : PauseMuted);
                }

                if (deathTime <= 2.3f) return;
                EnsureDifficultyStyles();
                var retry = new Rect(488, 560, 624, 102);
                var home = new Rect(650, 687, 300, 70);
                if (difficultyRedMetal != null)
                    GUI.DrawTexture(retry, difficultyRedMetal, ScaleMode.StretchToFill);
                else Fill(retry, new Color(.38f, .068f, .045f));
                DifficultyFrame(retry, new Color(.76f, .23f, .15f));
                if (retry.Contains(Event.current.mousePosition))
                    Fill(retry, new Color(1, 1, 1, .08f));
                PauseLabel(retry, "PLAY AGAIN", recapAction, PauseInk);
                bool homeHovered = home.Contains(Event.current.mousePosition);
                PauseLabel(home, "MAIN MENU", recapSecondary, homeHovered ? Color.white : PauseInk);
                Fill(new Rect(685, 751, 230, 2), homeHovered ? new Color(.76f, .23f, .15f) : DifficultyRed);
                if (FeedbackButton(retry, "", GUIStyle.none)) { PlayFromMenu(); return; }
                if (FeedbackButton(home, "", GUIStyle.none)) ShowHome();
            }
            finally { GUI.matrix = previous; }
        }
    }
}
