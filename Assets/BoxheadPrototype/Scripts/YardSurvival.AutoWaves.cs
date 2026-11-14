using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        float nextWaveCountdown=-1;
        void TickNextWave(float dt)
        {
            if(PreparationActive||IsCoopGuest||nextWaveCountdown<0||frontScreen!=FrontScreen.Game||(dead&&(!HasCoopPartner||peer.health<=0)))return;
            nextWaveCountdown=Mathf.Max(0,nextWaveCountdown-dt);
            if(nextWaveCountdown<=0){if(dead&&HasCoopPartner){bool saved=dead;dead=false;try{StartWave();}finally{dead=saved;ReleaseMouse();}}else StartWave();}
        }
    }
}
