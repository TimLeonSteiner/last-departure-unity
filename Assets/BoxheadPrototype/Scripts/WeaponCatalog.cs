namespace YardSurvivalPrototype
{
    public static class WeaponCatalog
    {
        public sealed class Definition
        {
            public readonly string name;
            public readonly int capacity, damage, unlockWave;
            public readonly float interval, reload, kick;
            public Definition(string name, int capacity, int damage, int unlockWave, float interval, float reload, float kick)
            { this.name = name; this.capacity = capacity; this.damage = damage; this.unlockWave = unlockWave; this.interval = interval; this.reload = reload; this.kick = kick; }
        }
        public static readonly Definition[] All = {
            new Definition("LMG / BELT FED", 80, 2, 7, 0.105f, 5.1f, 1.6f),
            new Definition("MINIGUN", 150, 1, 9, 0.06f, 2.3f, 0.8f),
            new Definition("P9 / PISTOL", 15, 1, 1, 0.29f, 1.65f, 1.3f),
            new Definition("BOOMSTICK", 6, 1, 3, 1.05f, 4.2f, 3.2f),
            new Definition("GRENADE LAUNCHER", 1, 7, 6, 1.0f, 2.8f, 3.2f),
            new Definition("PITCHFORK", 1, 4, 1, 0.75f, 0.0f, 1.1f),
            new Definition("DUAL SAWNOFFS", 4, 1, 5, 0.55f, 2.8f, 3.8f),
            new Definition("CROSSBOW", 1, 5, 2, 0.8f, 2.6f, 1.4f),
            new Definition("DUAL SMGS", 60, 1, 4, 0.075f, 2.1f, 0.95f),
            new Definition("HEAVY RIFLE", 20, 3, 6, 0.35f, 3.4f, 2.2f),
            new Definition("KNIFE", 1, 3, 1, 0.5f, 0.0f, 0.6f),
            new Definition("TALON", 8, 3, 3, 0.4f, 2.7f, 2.4f),
            new Definition("BERETTA", 15, 1, 1, 0.25f, 2.7f, 1.3f),
            new Definition("HEAVY PISTOL", 7, 3, 4, 0.5f, 2.7f, 2.7f),
            new Definition("PERSUADER", 40, 1, 3, 0.085f, 2.8f, 1.0f),
            new Definition("FAL / BATTLE RIFLE", 20, 3, 5, 0.23f, 2.7f, 2.1f),
            new Definition("BOLT / SNIPER", 5, 6, 5, 1.1f, 1.85f, 3.0f)
        };
        public const int Starter = 12;
        // Preserve serialized model/audio IDs while excluding retired weapons from rewards.
        public static bool IsAvailable(int id) => id >= 0 && id < All.Length &&
            id != 2 && id != 4 && id != 7 && id != 11 && id != 16 && !IsMelee(id);
        public static bool IsMelee(int id) => id == 5 || id == 10;
        public static bool IsShotgun(int id) => id == 3 || id == 6;
        public static bool CanAim(int id) => id != 9 && !IsShotgun(id) && !IsMelee(id);
        public static bool IsScoped(int id) => id == 16;
        public static bool IsAutomatic(int id) => id == 0 || id == 1 || id == 8 || id == 14 || id == 15;
        public static int TierForWave(int wave)
        { int tier = Starter; for (int i = 0; i < All.Length; i++) if (IsAvailable(i) && wave >= All[i].unlockWave && All[i].unlockWave > All[tier].unlockWave) tier = i; return tier; }
    }
}
