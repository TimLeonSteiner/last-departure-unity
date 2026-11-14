#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunSkillTreePresentationChecks()
        {
            hologramTab = 2; selectedSkill = selectedSkillRank = 0; RebuildWristDisplay();
            bool ClickAt(Vector2 point)
            {
                foreach (var hit in hologramHits)
                    if (hit.rect.Contains(point)) { hit.action(); return true; }
                return false;
            }
            int points = skillPoints;
            if (!ClickAt(CircuitNodePosition(1,0)) || selectedSkill != 1 || skillPoints != points)
                throw new Exception("Selecting a tree node must inspect it without spending points");
            RebuildWristDisplay();
            if (ClickAt(new Vector2(650,534)) || skillPoints != points)
                throw new Exception("Locked node must not expose an active purchase button");
            ClickAt(CircuitNodePosition(0,0)); RebuildWristDisplay();
            int rank = skillRanks[0];
            if (!ClickAt(new Vector2(650,534)) || skillPoints != points - 1 || skillRanks[0] != rank + 1 || !SkillUnlocked(1))
                throw new Exception("Details upgrade button must purchase the selected node and unlock its child");
            RebuildWristDisplay();
            if (ClickAt(new Vector2(650,534))) throw new Exception("An installed circuit node cannot be purchased again");
            ClickAt(CircuitNodePosition(0,2)); RebuildWristDisplay();
            if (ClickAt(new Vector2(650,534)) || skillPoints != points - 1) throw new Exception("Circuit ranks cannot be skipped");
            ClickAt(CircuitNodePosition(0,0)); RebuildWristDisplay();
            return "PASS: Circuit node selection is separate from spending; locked node cannot be purchased; install buys selected skill and unlocks connected branch; installed nodes cannot be repurchased and ranks cannot be skipped.\n";
        }
        public string RunSkillTreeChecks()
        {
            var report = new StringBuilder();
            void Check(bool result, string message) { if (!result) throw new Exception(message); report.AppendLine("PASS " + message); }
            ResetRun(); frontScreen = FrontScreen.Game;
            try
            {
                Check(!BuySkill(0) && weaponBoost == 0, "No free purchases");
                wave = 1; fighting = true; CompleteWave();
                Check(skillPoints == 1 && !upgradePending && nextWaveCountdown == 15, "Wave grants one point without the forced upgrade modal");
                Check(!AwardWaveSkillPoint() && skillPoints == 1, "Same wave cannot grant a second point");
                Check(!BuySkill(1) && skillPoints == 1, "Prerequisite blocks locked skill without charging points");
                Check(BuySkill(0) && weaponBoost == 1 && skillPoints == 0, "Combat rank changes real weapon damage");
                Check(!BuySkill(1), "Cannot overspend");
                skillPoints = 30;
                Check(!BuySkill(-1) && !BuySkill(9), "Invalid skill ids are rejected");
                Check(BuySkill(1) && Mathf.Approximately(reloadBoost, .85f), "Reload skill affects reload duration");
                ResetRecoil(); ApplyWeaponRecoil(); float baseKick=cameraKick, baseWeaponKick=weaponKick;
                Check(BuySkill(2) && weaponBoost == 1 && Mathf.Approximately(SkillRecoilScale,.85f), "Steady Aim unlocks recoil control without adding damage");
                ResetRecoil(); ApplyWeaponRecoil();
                Check(Mathf.Approximately(cameraKick,baseKick*.85f) && Mathf.Approximately(weaponKick,baseWeaponKick*.85f), "Steady Aim reduces actual camera and weapon recoil");
                Check(BuySkill(2) && BuySkill(2) && !BuySkill(2) && Mathf.Approximately(SkillRecoilScale,.85f), "Later nodes add lateral control and aimed control without stacking hip recoil bonuses");
                Check(BuySkill(0) && BuySkill(0) && !BuySkill(0) && skillRanks[0] == 3, "Maximum rank cannot be exceeded");
                health = 40;
                Check(BuySkill(3) && maxPlayerHealth == 125 && health == 65, "Vitality increases health and cap");
                Check(BuySkill(4) && waveHealing == 10, "Medic adds wave healing");
                Check(BuySkill(5) && maxPlayerHealth == 175, "Survivor increases health cap");
                coreHealth = 200;
                Check(BuySkill(6) && maxCoreHealth == 600 && coreHealth == 300, "Engineering reinforces and repairs core");
                Check(BuySkill(7) && waveSalvage == 3 && BuySkill(8) && maxCoreHealth == 750, "Engineering unlocks salvage and bastion");
                int hp = health, scrap = supplies, points = skillPoints;
                wave = 2; AwardWaveSkillPoint();
                Check(health == hp + 10 && supplies == scrap + 3 && skillPoints == points + 1, "Earned wave bonuses apply once at the next wave completion");
                for (int branch = 0; branch < 3; branch++)
                {
                    Check(!HasBranchMastery(branch), "Partial branch does not grant mastery");
                    for (int id = branch * 3; id < branch * 3 + 3; id++)
                        while (skillRanks[id] < SkillMaxRank)
                            Check(BuySkill(id), "Install remaining specialist rank");
                    Check(HasBranchMastery(branch), "All nine ranks grant branch mastery");
                }
                Check(!WeaponCatalog.IsAvailable(13), "Heavy Pistol is excluded from progression");
                ResetRun();
                Check(!HasBranchMastery(0) && !HasBranchMastery(1) && !HasBranchMastery(2), "Restart clears all specialist perks");
                Check(skillPoints == 0 && skillRanks.All(r => r == 0) && weaponBoost == 0 && maxPlayerHealth == 100 && waveHealing == 0 && waveSalvage == 0 && SkillRecoilScale == 1, "Restart clears run skills and effects");
                return report.ToString();
            }
            finally { ResetRun(); }
        }
    }
}
#endif
