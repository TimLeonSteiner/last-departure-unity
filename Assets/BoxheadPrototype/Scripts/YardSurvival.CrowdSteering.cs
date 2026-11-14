using UnityEngine;
using System.Collections.Generic;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        const float CrowdCellSize = 2f;
        readonly Dictionary<Vector2Int, List<Enemy>> crowdGrid = new Dictionary<Vector2Int, List<Enemy>>();
        readonly List<Enemy> crowdNeighbours = new List<Enemy>(64);
        bool crowdGridActive;
        static Vector2Int CrowdCell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / CrowdCellSize), Mathf.FloorToInt(p.z / CrowdCellSize));
        void AddCrowdCell(Enemy enemy, Vector2Int cell)
        {
            if (!crowdGrid.TryGetValue(cell, out var bucket)) crowdGrid[cell] = bucket = new List<Enemy>(8);
            bucket.Add(enemy);
        }
        void RebuildCrowdGrid()
        {
            foreach (var bucket in crowdGrid.Values) bucket.Clear();
            foreach (var enemy in enemies) AddCrowdCell(enemy, CrowdCell(enemy.position));
            crowdGridActive = true;
        }
        void UpdateCrowdCell(Enemy enemy, Vector2Int previous)
        {
            var current = CrowdCell(enemy.position);
            if (current == previous) return;
            crowdGrid[previous].Remove(enemy);
            AddCrowdCell(enemy, current);
        }
        List<Enemy> NearbyCrowd(Vector3 position, float radius)
        {
            // Standalone movement checks and callers outside the update retain exact behaviour.
            if (!crowdGridActive) return enemies;
            crowdNeighbours.Clear();
            var min = CrowdCell(position - new Vector3(radius, 0, radius));
            var max = CrowdCell(position + new Vector3(radius, 0, radius));
            for (int z = min.y; z <= max.y; z++)
                for (int x = min.x; x <= max.x; x++)
                    if (crowdGrid.TryGetValue(new Vector2Int(x, z), out var bucket)) crowdNeighbours.AddRange(bucket);
            return crowdNeighbours;
        }
        Vector3 ZombieApproach(Enemy e,Vector3 target,float approachRadius=.94f)
        {
            Vector3 outward=e.position-target;outward.y=0;
            if(outward.sqrMagnitude>100||!ClearPursuitLine(e.position,target))return target;
            if(e.approachTimer<=0)
            {
                float baseAngle=Mathf.Atan2(outward.x,outward.z)*Mathf.Rad2Deg;
                float best=float.PositiveInfinity,chosen=baseAngle;
                // Prefer a nearby free attack position, keeping the choice briefly
                // so moving neighbours do not make the whole crowd change sides.
                for(int i=0;i<7;i++)
                {
                    float offset=i==0?0:((i+1)/2)*30*(i%2==0?-1:1);
                    float angle=baseAngle+offset;
                    Vector3 spot=target+Quaternion.Euler(0,angle,0)*Vector3.forward*approachRadius;
                    if(!Free(spot,EnemyRadius,true)||!ClearPursuitLine(e.position,spot))continue;
                    float score=Mathf.Abs(offset)*.012f;
                    foreach(var other in NearbyCrowd(spot, 1.3f))if(other!=e)
                    {
                        float d=Vector3.Distance(other.position,spot);
                        score+=Mathf.Max(0,1.3f-d)*4;
                    }
                    if(score<best){best=score;chosen=angle;}
                }
                e.approachAngle=chosen;e.approachTimer=.65f+(e.netId%5)*.07f;
            }
            Vector3 result=target+Quaternion.Euler(0,e.approachAngle,0)*Vector3.forward*approachRadius;
            return Free(result,EnemyRadius,true)&&ClearPursuitLine(e.position,result)?result:target;
        }
        Vector3 SteerZombieCrowd(Enemy e,Vector3 desired,float speed)
        {
            if(desired.sqrMagnitude<.0001f)return desired;
            Vector3 forward=desired.normalized,side=Vector3.Cross(Vector3.up,forward),separation=Vector3.zero;
            float sidestep=0;
            foreach(var other in NearbyCrowd(e.position, 1.65f))
            {
                if(other==e)continue;
                Vector3 away=e.position-other.position;away.y=0;
                if(away.sqrMagnitude>1.65f*1.65f)continue;
                float d=away.magnitude;
                if(d<.001f)away=(e.netId<other.netId?-side:side)*.01f;
                if(d<1.05f)separation+=away.normalized*(1.05f-d);
                float ahead=Vector3.Dot(-away,forward),across=Vector3.Dot(-away,side);
                if(ahead>0&&ahead<1.55f&&Mathf.Abs(across)<.75f)
                    sidestep+=(Mathf.Abs(across)>.08f?-Mathf.Sign(across):(e.netId%2==0?1:-1))*(1-ahead/1.55f);
            }
            Vector3 velocity=forward*speed+Vector3.ClampMagnitude(separation*2.6f,speed)+side*Mathf.Clamp(sidestep,-1,1)*speed*.8f;
            return velocity.sqrMagnitude>.001f?velocity.normalized*desired.magnitude:Vector3.zero;
        }
        Vector3 MoveZombieInCrowd(Enemy e,Vector3 step)
        {
            Vector3 position=e.position;
            int count=Mathf.Max(1,Mathf.CeilToInt(step.magnitude/.1f));
            for(int i=0;i<count;i++)
            {
                Vector3 next=Move(position,step/count,EnemyRadius,true);
                bool blocked=false;
                foreach(var other in NearbyCrowd(next, .76f))
                {
                    if(other==e)continue;
                    float before=(position-other.position).sqrMagnitude,after=(next-other.position).sqrMagnitude;
                    // Existing spawn overlaps may separate, but never deepen. Attacking,
                    // staggered and crawling bodies remain obstacles to the followers.
                    if(after<.76f*.76f&&after<before-.000001f){blocked=true;break;}
                }
                if(blocked)break;
                position=next;
            }
            return position;
        }
    }
}
