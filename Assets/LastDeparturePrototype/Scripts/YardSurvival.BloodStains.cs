using System;
using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        const int BloodLimit=48;
        const float BloodLifetime=90;
        [Serializable] sealed class BloodState
        {
            public int id,seed;
            public Vector3 position,normal,impact,direction;
            public float radius,age;
            public bool large,spray;
        }
        sealed class BloodRecord { public BloodState state; public float born; }
        readonly List<BloodRecord> bloodRecords=new List<BloodRecord>();
        readonly Dictionary<int,BloodStainVisual> bloodVisuals=new Dictionary<int,BloodStainVisual>();
        readonly Dictionary<ulong,int> sentBloodRevision=new Dictionary<ulong,int>();
        readonly RaycastHit[] bloodHits=new RaycastHit[64];
        int nextBloodId,bloodRevision;
        Material stainMaterial;

        public void SpawnBlood(Vector3 position,Vector3 direction,bool severed)
        {
            // Replicas consume the authoritative marks; local limb visuals cannot duplicate them.
            if(!GoreEnabled || IsCoopGuest || actors==null)return;
            if(direction.sqrMagnitude<.001f)direction=Vector3.forward;
            direction.Normalize();
            int seed=unchecked(++nextBloodId*7919+coopEpoch*104729)&int.MaxValue;
            PlayBloodSpray(position,direction,severed,seed);
            if(BloodSurface(position+Vector3.up*.08f,Vector3.down,5,out var floor))
                AddBloodMark(floor,position,direction,severed,seed,true);
            if(BloodSurface(position,direction,2.8f,out var wall)&&Mathf.Abs(wall.normal.y)<.3f)
                AddBloodMark(wall,position,direction,severed,seed+13,false);
        }
        bool BloodSurface(Vector3 origin,Vector3 direction,float distance,out RaycastHit result)
        {
            result=default;float nearest=distance+1;
            var surfaces=transform.Find("Death physics environment");
            int count=Physics.RaycastNonAlloc(origin,direction,bloodHits,distance,~(1<<28),QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var hit=bloodHits[i];
                if(surfaces==null||!hit.transform.IsChildOf(surfaces)||hit.distance>=nearest)continue;
                nearest=hit.distance;result=hit;
            }
            return nearest<=distance;
        }
        void AddBloodMark(RaycastHit hit,Vector3 impact,Vector3 direction,bool large,int seed,bool spray)
        {
            float radius=large?.62f:.19f;
            if(Mathf.Abs(hit.normal.y)<.3f)
            {
                // Keep the whole stain on this panel rather than painting across door/window gaps.
                var b=hit.collider.bounds;var p=hit.point;
                float horizontal=Mathf.Abs(hit.normal.x)>.5f?Mathf.Min(p.z-b.min.z,b.max.z-p.z):Mathf.Min(p.x-b.min.x,b.max.x-p.x);
                radius=Mathf.Min(radius,Mathf.Min(horizontal,Mathf.Min(p.y-b.min.y,b.max.y-p.y))*.7f);
                if(radius<.065f)return;
            }
            var state=new BloodState{id=++nextBloodId,seed=seed,position=hit.point+hit.normal*.032f,normal=hit.normal,
                impact=impact,direction=direction,radius=radius,large=large,spray=spray};
            PruneBlood();
            while(bloodRecords.Count>=BloodLimit)RemoveBloodAt(0);
            bloodRecords.Add(new BloodRecord{state=state,born=Time.time});bloodRevision++;
            ShowBloodMark(state,false);
        }
        void ShowBloodMark(BloodState state,bool replicated)
        {
            if(!GoreEnabled||dedicatedServer||actors==null)return;
            if(stainMaterial==null)
            {
                stainMaterial=Resources.Load<Material>("BloodStain");
                if(stainMaterial==null){Debug.LogWarning("BloodStain material missing");return;}
            }
            var obj=new GameObject("Blood stain "+state.id);obj.transform.SetParent(actors,false);
            obj.transform.position=state.position;
            Vector3 tangent=Vector3.ProjectOnPlane(state.direction,state.normal);
            if(tangent.sqrMagnitude<.001f)tangent=Vector3.Cross(state.normal,Vector3.right);
            if(tangent.sqrMagnitude<.001f)tangent=Vector3.forward;
            obj.transform.rotation=Quaternion.LookRotation(state.normal,tangent);
            var visual=obj.AddComponent<BloodStainVisual>();
            visual.Initialize(stainMaterial,state.seed,state.radius,state.age,BloodLifetime);
            bloodVisuals[state.id]=visual;
            if(replicated&&state.spray&&state.age<.6f)PlayBloodSpray(state.impact,state.direction,state.large,state.seed);
        }
        void RemoveBloodAt(int index)
        {
            int id=bloodRecords[index].state.id;bloodRecords.RemoveAt(index);bloodRevision++;
            if(bloodVisuals.TryGetValue(id,out var visual)&&visual!=null){visual.gameObject.SetActive(false);Destroy(visual.gameObject);}
            bloodVisuals.Remove(id);
        }
        void PruneBlood()
        {for(int i=bloodRecords.Count-1;i>=0;i--)if(Time.time-bloodRecords[i].born>=BloodLifetime)RemoveBloodAt(i);}
        BloodState[] BloodSnapshot(ulong client)
        {
            PruneBlood();
            if(sentBloodRevision.TryGetValue(client,out int revision)&&revision==bloodRevision)return null;
            var result=new BloodState[bloodRecords.Count];
            for(int i=0;i<result.Length;i++){result[i]=bloodRecords[i].state;result[i].age=Time.time-bloodRecords[i].born;}
            return result;
        }
        void ApplyBloodSnapshot(BloodState[] states)
        {
            if(states==null)return;
            var keep=new HashSet<int>();
            foreach(var state in states)
            {
                if(keep.Count>=BloodLimit)break;
                if(state==null||state.age>=BloodLifetime||state.radius<=0||state.radius>1)continue;
                if(!keep.Add(state.id))continue;
                if(!bloodVisuals.TryGetValue(state.id,out var visual)||visual==null)ShowBloodMark(state,true);
                else visual.SetAge(state.age);
            }
            foreach(int id in new List<int>(bloodVisuals.Keys))
                if(!keep.Contains(id)){if(bloodVisuals[id]!=null)Destroy(bloodVisuals[id].gameObject);bloodVisuals.Remove(id);}
        }
        void ClearBloodStains()
        {
            foreach(var visual in bloodVisuals.Values)if(visual!=null){visual.gameObject.SetActive(false);Destroy(visual.gameObject);}
            bloodVisuals.Clear();bloodRecords.Clear();sentBloodRevision.Clear();bloodRevision++;nextBloodId=0;
        }
    }

    public sealed class BloodStainVisual:MonoBehaviour
    {
        Mesh mesh;MeshRenderer surface;MaterialPropertyBlock properties;
        float born,lifetime;
        public void Initialize(Material material,int seed,float radius,float age,float duration)
        {
            lifetime=duration;born=Time.time-age;
            var random=new System.Random(seed);var vertices=new List<Vector3>();var triangles=new List<int>();
            void Spot(Vector2 center,float size,int segments)
            {
                int start=vertices.Count;vertices.Add(center);
                for(int i=0;i<segments;i++)
                {
                    float angle=i*Mathf.PI*2/segments;float reach=size*(.65f+(float)random.NextDouble()*.35f);
                    vertices.Add(center+new Vector2(Mathf.Cos(angle)*reach,Mathf.Sin(angle)*reach));
                }
                for(int i=0;i<segments;i++){triangles.Add(start);triangles.Add(start+1+i);triangles.Add(start+1+(i+1)%segments);}
            }
            Spot(Vector2.zero,radius*.73f,18);
            for(int i=0;i<9;i++)
            {float angle=(float)random.NextDouble()*Mathf.PI*2;float distance=radius*(.45f+(float)random.NextDouble()*.5f);Spot(new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*distance,radius*(.025f+(float)random.NextDouble()*.10f),6);}
            mesh=new Mesh{name="Seeded illustrated blood splatter"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            surface=gameObject.AddComponent<MeshRenderer>();surface.sharedMaterial=material;
            surface.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;surface.receiveShadows=false;
            properties=new MaterialPropertyBlock();Update();
        }
        public void SetAge(float age){born=Time.time-Mathf.Max(age,Time.time-born);}
        void Update()
        {
            float age=Time.time-born;if(age>=lifetime){Destroy(gameObject);return;}
            Color color=Color.Lerp(new Color(.30f,.012f,.018f),new Color(.055f,.006f,.009f),Mathf.Clamp01(age/35));
            color.a=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(lifetime-18,lifetime,age));
            properties.SetColor("_BloodColor",color);surface.SetPropertyBlock(properties);
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);}
    }
}
