using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        Vector3 ZombiePursuit(Enemy enemy,Vector3 target,bool targetPeer,bool targetCore=false)
        {
            Vector3 approach=ZombieApproach(enemy,target,targetCore?1.95f:.94f);
            Vector3 waypoint=NextPath(enemy.position,approach,targetPeer,enemy.netId,targetCore);
            for(int i=0;i<5;i++)
            {
                Vector3 next=NextPath(waypoint,approach,targetPeer,enemy.netId,targetCore);
                if((next-waypoint).sqrMagnitude<.001f||!ClearPursuitLine(enemy.position,next))break;
                waypoint=next;
            }
            Vector3 delta=waypoint-enemy.position;delta.y=0;
            float distanceToGoal=(target-enemy.position).magnitude;
            if(delta.sqrMagnitude<.01f)return delta;
            Vector3 forward=delta.normalized,side=Vector3.Cross(Vector3.up,forward);
            // A short, persistent personal lane creates broad curved approaches instead
            // of aiming every visible zombie at the same distant point. Fade lanes near
            // melee range and in tight corners; the collision-safe flow field still owns routing.
            float spread=Mathf.InverseLerp(2.2f,8, distanceToGoal)*Mathf.InverseLerp(1,3,delta.magnitude);
            float lane=((enemy.netId*37%11)-5)*.34f;
            float drift=(Mathf.PerlinNoise(enemy.netId*.73f,Time.time*.18f)-.5f)*.7f;
            Vector3 ahead=enemy.position+forward*Mathf.Min(3.2f,delta.magnitude);
            for(int attempt=0;attempt<3;attempt++)
            {
                float width=attempt==0?1:attempt==1?.5f:0;
                Vector3 candidate=ahead+side*(lane+drift)*spread*width;
                if(Free(candidate,EnemyRadius,true) && ClearPursuitLine(enemy.position,candidate))
                { delta=candidate-enemy.position;break; }
            }
            delta.y=0;return delta;
        }
    }
}
