using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        int coreHealth=500,maxCoreHealth=500,maxPlayerHealth=100,weaponBoost;
        float reloadBoost=1;
        bool upgradePending,coreLost;
        readonly int[] coreOffers=new int[3];
        readonly int[,] coreDistance=new int[GridSize,GridSize];
        SuspendedCore coreVisual;

        bool CoreMode => !IsCoopHost&&!IsCoopGuest&&!dedicatedServer;
        bool HasCoreObjective => CoreMode&&coreVisual!=null;
        static readonly string[] UpgradeNames={"STOPPING POWER","VITALITY","FAST RELOAD","CORE REINFORCEMENT","BUILDING SUPPLIES"};
        static readonly string[] UpgradeDescriptions={"+1 damage for all weapons, including future unlocks.","+25 maximum health and restores 25 health.","15% faster reloads for this run.","+100 maximum core strength and repairs 100 core strength.","+12 scrap for barricades, traps, and turrets."};
        void ResetCoreProgression()
        {
            ResetCoreAnnouncer(); ResetSkillTree();
            coreHealth=maxCoreHealth=500;maxPlayerHealth=100;weaponBoost=0;reloadBoost=1;
            upgradePending=coreLost=false;coreVisual=null;
            if(!CoreMode)return;
            var root=new GameObject("Suspended nexus heart");root.transform.SetParent(actors,false);
            coreVisual=root.AddComponent<SuspendedCore>();coreVisual.Build();
        }
        void TickCoreVisual()
        {
            if(coreVisual!=null)coreVisual.SetCondition(coreHealth/(float)maxCoreHealth,coreLost);
        }
        void DamageCore(int amount)
        {
            if(!HasCoreObjective||coreLost||dead)return;
            coreHealth=Mathf.Max(0,coreHealth-CoreSkillDamage(IncomingDamage(amount)));ApplyCoreFailsafe();AnnounceCoreDamage();
            if(coreHealth==0)
            {
                coreLost=true;upgradePending=false;nextWaveCountdown=-1;fighting=false;
                BeginDeathSequence();
            }
        }
        void OfferCoreUpgrades()
        {
            ResetUpgradeHold();
            nextWaveCountdown=-1;upgradePending=true;building=false;aiming=false;
            int[] pool={0,1,2,3,4};
            for(int i=0;i<3;i++){int pick=Random.Range(i,pool.Length);int tmp=pool[i];pool[i]=pool[pick];pool[pick]=tmp;coreOffers[i]=pool[i];}
            RequestAnnouncement(4);
            ReleaseMouse();ghost.gameObject.SetActive(false);PauseMovementAudio(true);
        }
        void ChooseCoreUpgrade(int choice)
        {
            if(!upgradePending||choice<0||choice>=3)return;
            int upgrade=coreOffers[choice];
            switch(upgrade)
            {
                case 0:weaponBoost++;break;
                case 1:maxPlayerHealth+=25;health=Mathf.Min(maxPlayerHealth,health+25);break;
                case 2:reloadBoost*=.85f;break;
                case 3:maxCoreHealth+=100;coreHealth=Mathf.Min(maxCoreHealth,coreHealth+100);break;
                case 4:supplies+=12;break;
            }
            PlayMenuFeedback(true, true);
            upgradePending=false;nextWaveCountdown=15;CaptureMouse();RequestAnnouncement(5);
            ShowBanner(UpgradeNames[upgrade]+" · 15 seconds to prepare");
        }
        void DrawCoreHud(float width,float height)
        {
            if(!HasCoreObjective)return;
            EnsureDreadHud();
            float w=Mathf.Min(370,width*.32f),x=(width-w)/2+14,y=30;
            if(dreadHeartImage==null)dreadHeartImage=Resources.Load<Texture2D>("HudWeapons/Heart");
            if(dreadHeartImage!=null)
            {
                // Follow the living core's double beat, including its faster critical rhythm.
                float beat=coreLost?0:coreVisual.EvaluatePulse(coreVisual.PulseSampleTime);
                float scale=1+.18f*beat;
                var heartRect=new Rect(x-43-16*(scale-1),y-8-22.5f*(scale-1),32*scale,45*scale);
                GUI.DrawTexture(heartRect,dreadHeartImage,ScaleMode.ScaleToFit,true);
            }
            DreadLabel(new Rect(x,y,w*.5f,22),"H E A R T   C O R E",dreadCaption,HudWhite);
            DreadLabel(new Rect(x+w*.5f,y,w*.5f,22),coreHealth+" / "+maxCoreHealth,dreadValue,coreHealth<maxCoreHealth*.3f?HudRed:HudWhite,TextAnchor.MiddleRight);
            DrawDreadMeter(new Rect(x, y + 28, w, 5), coreHealth / (float)maxCoreHealth, framed: false);
        }
        Texture2D dreadHeartImage;
        void DrawCoreUpgrades(float width,float height)
        {
            DrawDossierUpgrades(width,height);
        }
    }
}

