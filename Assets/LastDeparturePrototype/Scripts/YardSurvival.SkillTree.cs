using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        const int SkillMaxRank = 3;
        int skillPoints, skillRewardWave, waveHealing, waveSalvage, selectedSkill, selectedSkillRank;
        readonly int[] skillRanks = new int[9];
        static readonly string[] SkillArtNames = { "Ballistics", "QuickHands", "Overpressure", "Vitality", "FieldMedic", "Survivor", "Reinforcement", "Salvager", "Bastion" };
        readonly Texture2D[] skillArt = new Texture2D[9];
        static readonly string[] SkillNames = { "BALLISTICS", "QUICK HANDS", "STEADY AIM", "VITALITY", "FIELD MEDIC", "SURVIVOR", "REINFORCEMENT", "SALVAGER", "BASTION" };
        static readonly string[,] SkillNodeNames = {
            { "STOPPING POWER", "PRECISION", "LAST STAND" },
            { "FAST RELOAD", "FOLLOW THROUGH", "RESUPPLY" },
            { "RECOIL DAMPER", "LATERAL BRACE", "FOCUSED FIRE" },
            { "VITALITY", "BODY ARMOR", "SECOND WIND" },
            { "TRIAGE", "COMBAT STIM", "BLOOD TRANSFUSION" },
            { "ENDURANCE", "GRIT", "DEFIBRILLATOR" },
            { "CORE PLATING", "SHOCK ABSORBER", "AUTO REPAIR" },
            { "SALVAGER", "SCRAP HUNTER", "RESEARCH RECOVERY" },
            { "BULWARK", "SENTRY OVERCLOCK", "FAILSAFE" }
        };
        static readonly string[,] SkillBenefits = {
            { "+1 weapon damage", "+1 extra weapon damage while aiming", "+2 weapon damage below 35% health" },
            { "15% shorter reloads", "25% faster recoil recovery", "+1 magazine of reserve ammo after each wave" },
            { "15% less recoil", "35% less sideways recoil", "35% less recoil while aiming (total)" },
            { "+25 max HP and heal 25", "10% less incoming player damage", "+50 max HP and restore full health" },
            { "Heal 10 HP after each wave", "Caffeine also heals 15 HP", "Heal 5 HP for every 5 kills" },
            { "+50 max HP and heal 50", "25% less damage while below 35% HP", "Survive one lethal hit with 25 HP per run" },
            { "+100 core HP and repair 100", "Core takes 15% less damage", "Repair 25 core HP after each wave" },
            { "+3 scrap after each wave", "+1 scrap for every 10 kills", "+1 bonus power node every third wave" },
            { "+150 core HP and repair 150", "Turrets fire 20% faster", "Core survives one lethal hit with 100 HP" }
        };
        bool playerFailsafeUsed, coreFailsafeUsed;
        int medicKillProgress, salvageKillProgress;
        float SkillRecoilScale => skillRanks[2]>=3&&aiming ? .65f : skillRanks[2]>0 ? .85f : 1;
        void ResetSkillTree()
        {
            skillPoints = skillRewardWave = waveHealing = waveSalvage = selectedSkill = selectedSkillRank = 0;
            System.Array.Clear(skillRanks, 0, skillRanks.Length);
            playerFailsafeUsed=coreFailsafeUsed=false;medicKillProgress=salvageKillProgress=0;
        }
        bool AwardWaveSkillPoint()
        {
            if (wave <= skillRewardWave || wave <= 0) return false;
            skillRewardWave = wave; skillPoints++;
            health = Mathf.Min(maxPlayerHealth, health + waveHealing);
            supplies += waveSalvage;
            ApplySkillWaveBonuses();
            hologramRenderedTab = -1;
            return true;
        }
        static readonly Vector2 MasteryPosition = new Vector2(474, 230);
        static readonly string[] MasteryNames = { "COMBAT SPECIALIST", "SURVIVAL SPECIALIST", "ENGINEERING SPECIALIST" };
        static readonly string[] MasteryBenefits = { "Firearms fire 20% faster.", "Take 15% less incoming damage.", "Turrets fire 25% faster." };

        bool HasBranchMastery(int branch)
        {
            int first = branch * 3;
            return skillRanks[first] == SkillMaxRank && skillRanks[first + 1] == SkillMaxRank && skillRanks[first + 2] == SkillMaxRank;
        }

        bool SkillUnlocked(int id) => id >= 0 && id < skillRanks.Length && (id % 3 == 0 || skillRanks[id - 1] > 0);
        void SelectSkillNode(int id,int rank)
        {
            if(selectedSkill==id && selectedSkillRank==rank)return;
            selectedSkill=id;selectedSkillRank=rank;hologramRenderedTab=-1;
            PlayMenuFeedback(false, true);
        }
        bool BuySkill(int id)
        {
            if (dead || coreLost || !CoreMode || skillPoints <= 0 || !SkillUnlocked(id) || skillRanks[id] >= SkillMaxRank) return false;
            skillPoints--; skillRanks[id]++;
            int rank=skillRanks[id];
            // Each installed node adds its own effect; previous nodes stay active.
            if(rank==1)
            {
                switch(id)
                {
                    case 0: weaponBoost++; break;
                    case 1: reloadBoost *= .85f; break;
                    case 3: maxPlayerHealth+=25;health=Mathf.Min(maxPlayerHealth,health+25);break;
                    case 4: waveHealing+=10;break;
                    case 5: maxPlayerHealth+=50;health=Mathf.Min(maxPlayerHealth,health+50);break;
                    case 6: maxCoreHealth+=100;coreHealth=Mathf.Min(maxCoreHealth,coreHealth+100);break;
                    case 7: waveSalvage+=3;break;
                    case 8: maxCoreHealth+=150;coreHealth=Mathf.Min(maxCoreHealth,coreHealth+150);break;
                }
            }
            if(id==3&&rank==3){maxPlayerHealth+=50;health=maxPlayerHealth;}
            hologramRenderedTab = -1;
            PlayMenuFeedback(true, true);
            return true;
        }
        void SkillTreeLink(Vector2 from, Vector2 to, Color color)
        {
            // Orthogonal wires overlap at elbows, without gaps from rotated pivots.
            HoloBox(new Rect(Mathf.Min(from.x,to.x) - 1.5f, Mathf.Min(from.y,to.y) - 1.5f,
                Mathf.Abs(to.x-from.x) + 3, Mathf.Abs(to.y-from.y) + 3), color);
        }
        void SkillIllustration(int id, Rect rect, float opacity)
        {
            if (skillArt[id] == null) skillArt[id] = Resources.Load<Texture2D>("SkillArt/" + SkillArtNames[id]);
            var art = HoloRect("Skill illustration / " + SkillArtNames[id], rect).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            art.texture = skillArt[id]; art.raycastTarget = false; art.color = new Color(1, 1, 1, opacity);
        }
        static Vector2 CircuitNodePosition(int skill, int rank)
        {
            if (rank == SkillMaxRank) return MasteryPosition;
            // Three branching paths, with empty lanes reserved for the wires.
            Vector2[] sockets = {
                new Vector2(280,310), new Vector2(280,230), new Vector2(390,230),
                new Vector2(390,310), new Vector2(390,390), new Vector2(MasteryPosition.x,390),
                new Vector2(670,310), new Vector2(670,230), new Vector2(560,230)
            };
            return sockets[skill % 3 * SkillMaxRank + rank];
        }
        void ConnectCircuitNodes(Vector2 from, Vector2 to, Color color)
        {
            Vector2 direction = (to-from).normalized;
            SkillTreeLink(from + direction*24, to - direction*24, color);
        }
        void BuildWristSkills()
        {
            Color wire = new Color(.36f,.58f,.64f,1), bright = new Color(.76f,.94f,1,1);
            Color[] tones = { new Color(.82f,.56f,.54f), new Color(.5f,.76f,.9f), new Color(.65f,.78f,.54f) };
            string[] branches = { "COMBAT", "SURVIVAL", "ENGINEERING" };
            string[] labels = { "DMG", "REL", "AIM", "HP", "MED", "VIT", "CORE", "SCR", "ARM" };
            int branch = selectedSkill / 3;
            HoloText(new Rect(24,164,510,28), branches[branch] + " / NANO-CIRCUIT", 20, bright);
            HoloText(new Rect(610,164,170,28), "NODES   " + skillPoints.ToString("00"), 20, bright);
            for (int b = 0; b < 3; b++)
            {
                int choice = b;
                var row = new Rect(24,224+b*60,160,42);
                if (b == branch) { HoloBox(row,new Color(.3f,.55f,.63f,.42f)); HoloFrame(row,bright); }
                HoloText(new Rect(row.x+10,row.y+12,148,24), branches[b], 16,b == branch ? bright : wire);
                hologramHits.Add(new HologramHit { rect=row, action=()=> { SelectSkillNode(choice*3,0); } });
            }
            // Draw the circuit first, then the sockets, so wires terminate at their rims.
            for (int tier=0;tier<3;tier++)
            {
                int id=branch*3+tier;
                for (int rank=0;rank<2;rank++)
                    ConnectCircuitNodes(CircuitNodePosition(id,rank),CircuitNodePosition(id,rank+1),skillRanks[id]>rank?bright:wire);
                if(tier<2)
                {
                    Color c=SkillUnlocked(id+1)?bright:wire;
                    ConnectCircuitNodes(CircuitNodePosition(id,0),CircuitNodePosition(id+1,0),c);
                }
            }
            bool mastered = HasBranchMastery(branch);
            {
                var mastery = new Rect(MasteryPosition.x - 29, MasteryPosition.y - 22, 58, 44);
                ConnectCircuitNodes(CircuitNodePosition(branch * 3, 2), MasteryPosition, mastered ? tones[0] : wire);
                Vector2 reloadEnd = CircuitNodePosition(branch * 3 + 1, 2);
                ConnectCircuitNodes(reloadEnd, MasteryPosition, mastered ? tones[1] : wire);
                ConnectCircuitNodes(CircuitNodePosition(branch * 3 + 2, 2), MasteryPosition, mastered ? tones[2] : wire);
                HoloBox(mastery, mastered ? new Color(.11f,.22f,.25f,1) : new Color(.025f,.065f,.08f,1));
                HoloFrame(mastery, mastered ? bright : wire);
                if (selectedSkillRank == SkillMaxRank) HoloSelection(mastery, tintInterior: false);
                HoloText(mastery, "MASTER", 12, mastered ? bright : wire).alignment = TextAnchor.MiddleCenter;
                hologramHits.Add(new HologramHit { rect = mastery, action = () => SelectSkillNode(branch * 3, SkillMaxRank) });
            }
            for(int tier=0;tier<3;tier++)
            for(int rank=0;rank<3;rank++)
            {
                int id=branch*3+tier;
                Vector2 point=CircuitNodePosition(id,rank);
                bool owned=rank<skillRanks[id], ready=rank==skillRanks[id]&&SkillUnlocked(id), focused=id==selectedSkill&&rank==selectedSkillRank;
                Rect box=new Rect(point.x-22,point.y-22,44,44);
                HoloBox(new Rect(box.x-2,box.y-2,48,48),owned?tones[tier]:ready?bright:wire);
                Color fill = owned ? new Color(.065f,.14f,.17f,1) : new Color(.025f,.065f,.08f,1);
                Color textColor = owned || ready || focused ? Color.white : new Color(.68f,.82f,.86f,1);
                HoloBox(box,fill);
                if (focused) HoloSelection(box, tintInterior: false);
                HoloText(new Rect(box.x,box.y+3,44,21),labels[id],15,textColor).alignment=TextAnchor.MiddleCenter;
                HoloText(new Rect(box.x,box.y+23,44,18),(rank+1).ToString(),14,textColor).alignment=TextAnchor.MiddleCenter;
                int chosen=id,level=rank;
                hologramHits.Add(new HologramHit {rect=new Rect(point.x-26,point.y-26,52,52),action=()=>{SelectSkillNode(chosen,level);}});
            }
            // The bench summary has rank sockets, rather than a second row of cards.
            for(int tier=0;tier<3;tier++)
            {
                int id=branch*3+tier; float x=24+tier*254;
                HoloText(new Rect(x,436,160,22),SkillNames[id],14,tones[tier]);
                for(int rank=0;rank<3;rank++)
                {
                    Rect socket=new Rect(x+rank*32,464,24,10);
                    HoloBox(socket,rank<skillRanks[id]?tones[tier]:new Color(.05f,.12f,.15f,1)); HoloFrame(socket,wire);
                }
                HoloText(new Rect(x+110,456,90,24),skillRanks[id]+" / 3",16,bright);
            }
            if (selectedSkillRank == SkillMaxRank)
            {
                HoloBox(new Rect(22,490,756,2),wire);
                HoloText(new Rect(22,498,740,24), MasteryNames[branch], 18, bright);
                HoloText(new Rect(22,524,740,22), MasteryBenefits[branch], 16, bright);
                HoloText(new Rect(22,548,740,19), mastered ? "MASTERY ACTIVE" : "LOCKED / INSTALL ALL 9 BRANCH RANKS", 12, mastered ? bright : wire);
                return;
            }
            int skill=selectedSkill;
            bool purchased=selectedSkillRank<skillRanks[skill], available=SkillUnlocked(skill)&&selectedSkillRank==skillRanks[skill];
            HoloBox(new Rect(22,490,756,2),wire);
            HoloText(new Rect(22,498,550,24),SkillNodeNames[skill,selectedSkillRank]+" / "+(selectedSkillRank+1),18,bright);
            HoloText(new Rect(22,524,550,22),SkillBenefits[skill,selectedSkillRank],16,bright);
            string requirement=purchased?"NODE INSTALLED":!SkillUnlocked(skill)?"REQUIRES "+SkillNames[skill-1]+" / 1":!available?"INSTALL PREVIOUS RANK":"COST: 1 POWER NODE";
            HoloText(new Rect(22,548,550,19),requirement,12,wire);
            HoloButton(new Rect(590,510,188,42),purchased?"INSTALLED":"INSTALL NODE",()=>BuySkill(skill),CoreMode&&available&&!purchased&&skillPoints>0);
        }
    }
}
