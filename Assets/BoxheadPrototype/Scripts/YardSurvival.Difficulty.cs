using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        // Normal is zero so older snapshots and saved runs retain their balance.
        enum RunDifficulty { Normal, Easy, Hard }
        RunDifficulty selectedDifficulty, runDifficulty;
        const string DifficultyPreference = "LastDeparture.Difficulty";
        static readonly RunDifficulty[] DifficultyOrder = { RunDifficulty.Easy, RunDifficulty.Normal, RunDifficulty.Hard };
        static string DifficultyName(RunDifficulty mode) => mode.ToString().ToUpperInvariant();
        static float HealthScale(RunDifficulty mode) => mode == RunDifficulty.Easy ? .75f : mode == RunDifficulty.Hard ? 1.35f : 1f;
        static float DamageScale(RunDifficulty mode) => mode == RunDifficulty.Easy ? .7f : mode == RunDifficulty.Hard ? 1.3f : 1f;
        static int DifficultyDamage(int amount, RunDifficulty mode) => amount <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(amount * DamageScale(mode)));
        int IncomingDamage(int amount) => DifficultyDamage(amount, runDifficulty);
        int StartingScrap => runDifficulty == RunDifficulty.Easy ? 18 : runDifficulty == RunDifficulty.Hard ? 8 : 12;
        int WaveScrap => runDifficulty == RunDifficulty.Easy ? 8 : runDifficulty == RunDifficulty.Hard ? 4 : 6;
        static string ModeSuffix(RunDifficulty mode) => mode == RunDifficulty.Normal ? "" : "." + mode;
        static string BestKey(bool coop, RunDifficulty mode) => "LastDeparture.Best.v1." + (coop ? "Coop" : "Solo") + ModeSuffix(mode) + ".";
        void SelectDifficulty(RunDifficulty mode)
        {
            selectedDifficulty = mode;
            PlayerPrefs.SetInt(DifficultyPreference, (int)mode);
            PlayerPrefs.Save();
        }
    }
}
