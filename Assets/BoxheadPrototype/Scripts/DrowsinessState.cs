using UnityEngine;

namespace YardSurvivalPrototype
{
    // Run-local clock: only advance while the player can act. No wall-clock expiry in menus.
    public sealed class DrowsinessState
    {
        public const int StackLimit = 64;
        public float Amount { get; private set; }
        public float CaffeineSeconds { get; private set; }
        public float WakeSeconds { get; private set; }
        public int Caffeine { get; private set; }
        public const float RecoverySeconds = 3;
        float recoveryRemaining, recoveryStart, recoveryDuration;
        public void Reset(float initial)
        { Amount = Mathf.Clamp01(initial); CaffeineSeconds = WakeSeconds = 0; Caffeine = 0; recoveryRemaining = 0; }
        public int Collect(int count)
        {
            int accepted = Mathf.Clamp(count, 0, StackLimit - Caffeine);
            Caffeine += accepted; return accepted;
        }
        public bool Drink(float duration)
        {
            if (Caffeine == 0 || CaffeineSeconds > 0 || duration <= 0) return false;
            Caffeine--; CaffeineSeconds = duration; recoveryStart = Amount;
            recoveryDuration = recoveryRemaining = Mathf.Min(RecoverySeconds, duration); return true;
        }
        public void Wake(float duration)
        { WakeSeconds = Mathf.Max(WakeSeconds, duration); Amount = 0; recoveryRemaining = 0; }
        public void Tick(float dt, float buildupSeconds)
        {
            dt = Mathf.Max(0, dt);
            if (recoveryRemaining > 0)
            {
                recoveryRemaining = Mathf.Max(0, recoveryRemaining - dt);
                Amount = recoveryStart * Mathf.SmoothStep(0, 1, recoveryRemaining / recoveryDuration);
            }
            // Account for only the part of a long tick after both protections have expired.
            float unprotected = Mathf.Max(0, dt - Mathf.Max(CaffeineSeconds, WakeSeconds));
            CaffeineSeconds = Mathf.Max(0, CaffeineSeconds - dt);
            WakeSeconds = Mathf.Max(0, WakeSeconds - dt);
            Amount = Mathf.Clamp01(Amount + unprotected / Mathf.Max(1, buildupSeconds));
        }
    }
}
