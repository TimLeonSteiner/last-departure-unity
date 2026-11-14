#if UNITY_EDITOR
using System;
using System.Text;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunUniqueSkillChecks()
        {
            var report=new StringBuilder();bool previous=suppressRunRecords;suppressRunRecords=true;
            void Check(bool condition,string label){if(!condition)throw new Exception(label);report.AppendLine("PASS "+label);}
            try
            {
                report.Append(RunSkillTreeChecks());ResetRun();skillPoints=27;
                for(int id=0;id<9;id++)
                {
                    var names=new System.Collections.Generic.HashSet<string>();
                    var effects=new System.Collections.Generic.HashSet<string>();
                    for(int rank=0;rank<3;rank++)
                    {
                        Check(names.Add(SkillNodeNames[id,rank])&&effects.Add(SkillBenefits[id,rank]),"Distinct node "+id+"/"+rank);
                        Check(BuySkill(id),"Purchase node "+id+"/"+rank);
                    }
                    Check(!BuySkill(id),"Rank cap "+id);
                }
                Check(skillPoints==0,"Exactly 27 points unlock every node");
                aiming=false;health=maxPlayerHealth;Check(SkillWeaponBonus==0,"Conditional combat bonuses inactive at full health from hip");
                aiming=true;Check(SkillWeaponBonus==1&&Mathf.Approximately(SkillRecoilScale,.65f),"Precision and focused fire activate while aiming");
                health=20;Check(SkillWeaponBonus==3,"Last Stand adds low-health damage");
                Check(PlayerSkillDamage(100)>=67&&PlayerSkillDamage(100)<=68,"Armor and Grit combine while injured");
                health=maxPlayerHealth;Check(PlayerSkillDamage(100)==90,"Armor works without low-health Grit");
                Check(CoreSkillDamage(100)==85,"Core shock absorber reduces damage");
                health=50;int scrap=supplies;
                for(int i=0;i<10;i++)ApplySkillKillBonuses();
                Check(health==60&&supplies==scrap+1,"Ten kills yield two healing pulses and one scrap");
                reserveAmmo=0;coreHealth=100;wave=3;int points=skillPoints;ApplySkillWaveBonuses();
                Check(reserveAmmo==MagazineSize&&coreHealth==125&&skillPoints==points+1,"Wave bonuses refill reserve, repair core and recover a node");
                health=0;ApplyPlayerFailsafe();Check(health==25&&playerFailsafeUsed,"Defibrillator rescues first lethal hit");
                health=0;ApplyPlayerFailsafe();Check(health==0,"Defibrillator cannot trigger twice");health=100;
                coreHealth=0;ApplyCoreFailsafe();Check(coreHealth==100&&coreFailsafeUsed,"Core failsafe rescues first lethal hit");
                coreHealth=0;ApplyCoreFailsafe();Check(coreHealth==0,"Core failsafe cannot trigger twice");coreHealth=100;
                ResetRun();Check(!playerFailsafeUsed&&!coreFailsafeUsed&&medicKillProgress==0&&salvageKillProgress==0,"New runs reset perk counters and one-use effects");
                return report.ToString();
            }
            finally{suppressRunRecords=previous;}
        }
    }
}
#endif
