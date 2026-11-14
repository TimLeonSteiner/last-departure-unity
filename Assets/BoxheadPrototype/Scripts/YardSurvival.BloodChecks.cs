#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunNonGraphicCombatChecks()
        {
            var report=new System.Text.StringBuilder();
            void Check(bool value,string message){if(!value)throw new Exception(message);report.AppendLine("PASS "+message);}
            ResetRun();
            try
            {
                SpawnBlood(new Vector3(0,1,4),Vector3.forward,true);
                Check(bloodRecords.Count==0&&bloodVisuals.Count==0,"Hits create no blood stains");
                ApplyBloodSnapshot(new[]{new BloodState{id=1,seed=1,position=Vector3.zero,normal=Vector3.up,radius=.5f,spray=true}});
                Check(bloodVisuals.Count==0,"Replicated blood is not displayed");
                for(int region=0;region<6;region++)
                {
                    SpawnEnemy(new Vector3(0,0,4));var e=enemies[enemies.Count-1];e.hp=10;
                    var z=e.zombie;
                    Check(z!=null,"Zombie model loaded");
                    if(region<5)Check(!z.Sever(region,Vector3.forward,actors),"Direct dismemberment is disabled for region "+region);
                    ApplyRayDamage(e,region,e.position+Vector3.up,Vector3.forward,2);
                    Check(z.parts.All(part=>part.enabled),"Shot leaves body intact for region "+region);
                    Check(z.InjuredLegs == ((region==3||region==4)?1:0),"Only leg hits injure legs for region "+region);
                    Check(region==0?!enemies.Contains(e):e.hp==8,"Headshot or weapon damage applied for region "+region);
                    if(enemies.Contains(e))HurtArsenalEnemy(e,20,Vector3.forward);
                    Check(!enemies.Contains(e),"Lethal damage still kills zombie");
                }
                SpawnEnemy(new Vector3(0,0,4));var crawler=enemies[enemies.Count-1];crawler.hp=1;
                var model=crawler.zombie;int beforeKills=kills;
                ApplyRayDamage(crawler,3,model.RegionCenter(3),Vector3.forward,100);
                Check(enemies.Contains(crawler)&&crawler.hp==1&&model.InjuredLegs==1,"Lethal-strength leg shot causes a limp and preserves life");
                ApplyRayDamage(crawler,3,model.RegionCenter(3),Vector3.forward,100);
                Check(model.InjuredLegs==1,"Repeated shots to one leg do not trigger crawling");
                ApplyRayDamage(crawler,4,model.RegionCenter(4),Vector3.forward,100);
                Check(enemies.Contains(crawler)&&model.InjuredLegs==2&&kills==beforeKills,"Both legs shot starts crawling without awarding a kill");
                for(int i=0;i<600;i++)model.TickMovement(.1f,.025f,false);
                Check(enemies.Contains(crawler)&&crawler.hp==1&&!model.IsCollapsing,"Crawler remains alive after a minute of movement");
                Check(model.parts.All(part=>part.enabled),"Leg injuries preserve intact meshes");
                var replica=JsonUtility.FromJson<CoopEnemyState>(JsonUtility.ToJson(new CoopEnemyState{legInjuries=model.LegInjuryMask}));
                SpawnEnemy(new Vector3(2,0,4));var replicated=enemies[enemies.Count-1].zombie;
                replicated.SyncLegInjuries(replica.legInjuries);replicated.SyncLegInjuries(replica.legInjuries);
                Check(replicated.InjuredLegs==2,"Co-op round trip preserves crawling injuries");
                ApplyRayDamage(crawler,5,model.RegionCenter(5),Vector3.forward,2);
                Check(!enemies.Contains(crawler)&&kills==beforeKills+1,"Body shot can finish a crawling zombie");
                Check(bloodRecords.Count==0&&bloodVisuals.Count==0,"Deaths create no blood");
                return report.ToString();
            }
            finally{ResetRun();}
        }
        public string RunBloodChecks()
        {
            if(!GoreEnabled)return RunNonGraphicCombatChecks();
            var report=new System.Text.StringBuilder();
            void Check(bool value,string message){if(!value)throw new Exception(message);report.AppendLine("PASS "+message);}
            ClearBloodStains();Physics.SyncTransforms();
            SpawnBlood(new Vector3(0,1,4),Vector3.forward,false);
            Check(bloodRecords.Count==1&&bloodVisuals.Count==1,"Hit creates a floor stain without a collider");
            Check(bloodVisuals[bloodRecords[0].state.id].GetComponent<Collider>()==null,"Stains cannot obstruct gameplay");
            SpawnBlood(new Vector3(8,.65f,23),Vector3.forward,true);
            Check(bloodRecords.Count==3&&bloodRecords[2].state.normal.y<.3f,"Nearby wall receives a separate stain");
            var snapshot=BloodSnapshot(900);string json=JsonUtility.ToJson(new CoopWorldState{blood=snapshot});
            var copy=JsonUtility.FromJson<CoopWorldState>(json).blood;
            var originalVertices=bloodVisuals[copy[0].id].GetComponent<MeshFilter>().sharedMesh.vertices;
            Check(copy.Length==3&&copy[2].seed==snapshot[2].seed&&copy[2].position==snapshot[2].position,"Snapshot preserves seed and surface placement");
            sentBloodRevision[900]=bloodRevision;Check(BloodSnapshot(900)==null,"Unchanged stains do not repeat in snapshots");
            ClearBloodStains();ApplyBloodSnapshot(copy);int count=bloodVisuals.Count;ApplyBloodSnapshot(copy);
            Check(count==3&&bloodVisuals.Count==count,"Repeated network snapshots do not duplicate marks");
            Check(originalVertices.SequenceEqual(bloodVisuals[copy[0].id].GetComponent<MeshFilter>().sharedMesh.vertices),"Replicated seeds generate identical splatter geometry");
            var aged=bloodVisuals[copy[0].id];aged.SetAge(82);aged.SendMessage("Update");
            var properties=new MaterialPropertyBlock();aged.GetComponent<MeshRenderer>().GetPropertyBlock(properties);
            var color=properties.GetColor("_BloodColor");Check(color.a<.7f&&color.r<.1f,"Old stains darken and fade before expiry");
            ClearBloodStains();
            for(int i=0;i<60;i++)SpawnBlood(new Vector3(0,1,4),Vector3.forward,false);
            Check(bloodRecords.Count==BloodLimit&&bloodVisuals.Count==BloodLimit,"Stain count is bounded under sustained fire");
            foreach(var record in bloodRecords)record.born=Time.time-BloodLifetime-1;
            PruneBlood();Check(bloodRecords.Count==0,"Expired marks are removed from authoritative state");
            ClearBloodStains();
            SpawnEnemy(new Vector3(0,0,4));var enemy=enemies[enemies.Count-1];enemy.hp=1;
            HurtArsenalEnemy(enemy,2,Vector3.forward);
            Check(bloodRecords.Exists(r=>r.state.large),"A lethal zombie hit creates a larger death stain");
            ClearBloodStains();Check(bloodVisuals.Count==0&&bloodRecords.Count==0,"Run cleanup removes all marks and replication history");
            // Small preview group remains visible in the level for art review.
            SpawnBlood(new Vector3(0,1,4),Vector3.forward,true);
            SpawnBlood(new Vector3(.8f,1,4.5f),new Vector3(.4f,0,1),false);
            SpawnBlood(new Vector3(8,.65f,23),Vector3.forward,true);
            return report.ToString();
        }
    }
}
#endif
