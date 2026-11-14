using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        int SkillWeaponBonus => (skillRanks[0]>=2&&aiming?1:0)+(skillRanks[0]>=3&&health<maxPlayerHealth*.35f?2:0);
        int PlayerSkillDamage(int amount)
        {
            float scale=(skillRanks[3]>=2?.9f:1)*(skillRanks[5]>=2&&health<maxPlayerHealth*.35f?.75f:1);
            if (HasBranchMastery(1)) scale *= .85f;
            return amount<=0?0:Mathf.Max(1,Mathf.RoundToInt(amount*scale));
        }
        void ApplyPlayerFailsafe()
        {
            if(health!=0||skillRanks[5]<3||playerFailsafeUsed)return;
            playerFailsafeUsed=true;health=Mathf.Min(25,maxPlayerHealth);damageTimer=1.5f;
            ShowBanner("DEFIBRILLATOR - last chance used");
        }
        int CoreSkillDamage(int amount) => amount<=0?0:Mathf.Max(1,Mathf.RoundToInt(amount*(skillRanks[6]>=2?.85f:1)));
        void ApplyCoreFailsafe()
        {
            if(coreHealth!=0||skillRanks[8]<3||coreFailsafeUsed)return;
            coreFailsafeUsed=true;coreHealth=Mathf.Min(100,maxCoreHealth);
            ShowBanner("CORE FAILSAFE - emergency reserve used");
        }
        void ApplySkillKillBonuses()
        {
            if(!CoreMode)return;
            if(skillRanks[4]>=3&&++medicKillProgress>=5){medicKillProgress=0;if(!dead)health=Mathf.Min(maxPlayerHealth,health+5);}
            if(skillRanks[7]>=2&&++salvageKillProgress>=10){salvageKillProgress=0;supplies++;}
        }
        void ApplySkillWaveBonuses()
        {
            if(skillRanks[1]>=3&&!WeaponCatalog.IsMelee(weaponId))reserveAmmo=Mathf.Min(MagazineSize*5,reserveAmmo+MagazineSize);
            if(skillRanks[6]>=3&&!coreLost)coreHealth=Mathf.Min(maxCoreHealth,coreHealth+25);
            if(skillRanks[7]>=3&&wave%3==0)skillPoints++;
        }
    }
}
