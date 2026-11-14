using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        [Header("Additional library weapons: AK-47, shotgun, Suomi, Grease Gun")]
        public GameObject[] additionalWeaponModels = new GameObject[4];

        void DrawSniperScope(float width, float height)
        {
            if (!WeaponCatalog.IsScoped(weaponId) || aimBlend < .9f || reloading || showHelp || dead || building) return;
            float radius = height * .44f, cx = width * .5f, cy = height * .5f;
            // Mask the opaque library lens with a clear magnified sight picture.
            for (int strip = 0; strip < 160; strip++)
            {
                float y = height * strip / 160f;
                float dy = y + height / 320f - cy;
                float half = Mathf.Abs(dy) >= radius ? 0 : Mathf.Sqrt(radius * radius - dy * dy);
                Fill(new Rect(0, y, cx - half, height / 160f + 1), Color.black);
                Fill(new Rect(cx + half, y, width - cx - half, height / 160f + 1), Color.black);
            }
            Fill(new Rect(cx - radius, cy - .6f, radius * 2, 1.2f), Color.black);
            Fill(new Rect(cx - .6f, cy - radius, 1.2f, radius * 2), Color.black);
            for (int tick = -3; tick <= 3; tick++)
            {
                if (tick == 0) continue;
                Fill(new Rect(cx + tick * radius / 6 - 1, cy - 4, 2, 8), Color.black);
                Fill(new Rect(cx - 4, cy + tick * radius / 6 - 1, 8, 2), Color.black);
            }
            Fill(new Rect(cx - 1, cy - 1, 2, 2), new Color(.6f, .08f, .05f));
        }

        GameObject AdditionalModel(int id)
        {
            int index = id - 4;
            if (additionalWeaponModels == null || index < 0 || index >= additionalWeaponModels.Length || additionalWeaponModels[index] == null)
                throw new System.InvalidOperationException("Missing weapon model: " + id);
            return additionalWeaponModels[index];
        }

        // A trigger pull consumes one shell and produces a pattern of distinct pellet hits.
        void FireWeaponProjectiles(Vector3 direction)
        {
            if (WeaponCatalog.IsMelee(weaponId)) { MeleeAttack(direction); return; }
            if (weaponId == 4 || weaponId == 7) { LaunchArsenalProjectile(direction, weaponId == 4); return; }
            if (!WeaponCatalog.IsShotgun(weaponId)) { TraceShot(arenaCamera.transform.position, direction.normalized, null); return; }
            Vector3 forward = direction.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < .01f) right = arenaCamera.transform.right;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            float spread = aiming ? .033f : .055f;
            TraceShot(arenaCamera.transform.position, forward, null);
            float phase = Random.value * Mathf.PI * 2;
            for (int pellet = 0; pellet < 7; pellet++)
            {
                float angle = phase + pellet * Mathf.PI * 2 / 7;
                float radius = spread * Random.Range(.55f, 1f);
                TraceShot(arenaCamera.transform.position, (forward + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius).normalized, null);
            }
        }

#if UNITY_EDITOR
        public string RunAdditionalWeaponChecks()
        {
            if (WeaponCatalog.All.Length != 17) throw new System.Exception("Expected 17 distinct weapon definitions");
            var seen = new System.Collections.Generic.HashSet<GameObject>();
            for (int id = 0; id < WeaponCatalog.All.Length; id++)
            {
                if (ModelFor(id) == null || !seen.Add(ModelFor(id))) throw new System.Exception("Missing or reused base model " + id);
                ResetRun(); EquipWeapon(id, WeaponCatalog.All[id].capacity);
                if (!viewWeapon.HasModeledHands || !viewWeapon.HasReloadParts) throw new System.Exception("Missing hands/reload parts " + id);
                int full = ammunition; Fire(Vector3.up);
                if (ammunition != full - (WeaponCatalog.IsMelee(id) ? 0 : 1)) throw new System.Exception("Trigger must consume one round/shell " + id);
                if (WeaponCatalog.IsMelee(id)) continue;
                BeginReload(); Fire(Vector3.up);
                if (!reloading || ammunition != full - (WeaponCatalog.IsMelee(id) ? 0 : 1)) throw new System.Exception("Reload must block firing " + id);
                for (int phase = 0; phase <= 20; phase++)
                {
                    viewWeapon.Tick(phase / 20f, true, 0, 0);
                    if (!viewWeapon.HasConnectedArms) throw new System.Exception("Disconnected arm during reload " + id + "/" + phase);
                }
                UpdateReload(WeaponCatalog.All[id].reload + .01f);
                if (reloading || ammunition != full) throw new System.Exception("Reload completion " + id);
            }
            for (int wave = 1; wave <= 9; wave++)
            {
                ResetRun(); this.wave = wave; fighting = true; SpawnWaveWeapons(wave);
                if (pickups.Count != 1) throw new System.Exception("Expected one ground firearm at wave " + wave);
            }
            ResetRun();
            return "17 WEAPONS PASS: unique imported models, one ground firearm per wave across nine waves, one round/shell per trigger, reload lockout/completion, and continuous arm chains at 21 reload phases per weapon.";
        }
#endif
    }
}
