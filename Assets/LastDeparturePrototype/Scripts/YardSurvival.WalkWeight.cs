using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        float walkPhase,walkBlend;
        Vector3 walkCameraOffset;
        float walkPitch,walkRoll;
        void ResetWalkWeight(){walkPhase=walkBlend=walkPitch=walkRoll=0;walkCameraOffset=Vector3.zero;}
        void UpdateWalkWeight(float distance,bool grounded,bool sliding,float dt)
        {
            bool walking=grounded&&!sliding&&distance>.0001f;
            walkBlend=Mathf.MoveTowards(walkBlend,walking?1:0,dt*7);
            if(walking)walkPhase+=distance/FootstepTravel*Mathf.PI;
            float strength=walkBlend*Mathf.Lerp(1,.2f,aimBlend);
            // Two heel strikes per stride; horizontal weight transfer alternates feet.
            walkCameraOffset=new Vector3(Mathf.Sin(walkPhase)*.016f,-Mathf.Abs(Mathf.Sin(walkPhase))*.032f,0)*strength;
            walkPitch=Mathf.Sin(walkPhase*2)*.48f*strength;walkRoll=Mathf.Sin(walkPhase)*.42f*strength;
        }
    }
}
