using System.Collections.Generic;
using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class SuspendedCore
    {
        Material bayPaint,bayDark,bayDisplay,bayIvory;
        public readonly List<Bounds> BayBarriers=new List<Bounds>();
        void BuildBayCollision()
        {
            void Barrier(Vector3 center,Vector3 size)
            {
                var obj=new GameObject("Maintenance bay solid barrier");obj.transform.SetParent(transform,false);obj.transform.localPosition=center;
                obj.AddComponent<BoxCollider>().size=size;
                BayBarriers.Add(new Bounds(transform.TransformPoint(center),Vector3.Scale(size,transform.lossyScale)));
            }
            foreach(int side in new[]{-1,1})
            {
                foreach(float x in new[]{-1.8f,1.8f})Barrier(new Vector3(x,.65f,side*2.2f),new Vector3(1.97f,1.1f,.09f));
                foreach(float z in new[]{-1.3f,1.3f})Barrier(new Vector3(side*2.7f,.65f,z),new Vector3(.09f,1.1f,1.47f));
                Barrier(new Vector3(side*2.05f,.86f,1.48f),new Vector3(.94f,1.43f,.78f));
            }
            Barrier(new Vector3(0,1.23f,.95f),new Vector3(3.39f,.81f,.09f));
        }
        void BuildMaintenanceBay()
        {
            BuildBayCollision();
            bayPaint=Make("Bay / aged blue steel",new Color(.13f,.19f,.20f),.65f,.34f);
            bayDark=Make("Bay / rubber and recesses",new Color(.025f,.033f,.036f),.15f,.3f);
            bayDisplay=Make("Bay / instrument phosphor",new Color(.13f,.65f,.58f),.1f,.4f,1.2f);
            bayIvory=Make("Bay / gauge faces",new Color(.56f,.53f,.43f),.1f,.4f);
            var bay=new GameObject("Heart maintenance bay").transform;bay.SetParent(transform,false);
            void Box(string name,Vector3 p,Vector3 size,Material m){Shape(name,PrimitiveType.Cube,p,size,m,bay);}
            void Pipe(string name,Vector3[] path,float r,Material m){Tube(name,path,r,m,bay);}
            // Rear corner cabinets stay behind the existing rails; all four axial approaches stay open.
            foreach(int side in new[]{-1,1})
            {
                float x=side*2.05f;
                Box("Cabinet raised plinth",new Vector3(x,.23f,1.48f),new Vector3(.94f,.25f,.78f),metal);
                Box("Pump control cabinet",new Vector3(x,.91f,1.48f),new Vector3(.82f,1.18f,.66f),bayPaint);
                Box("Inset cabinet door",new Vector3(x,.92f,1.135f),new Vector3(.71f,1.04f,.035f),metal);
                Box("Instrument panel",new Vector3(x,1.2f,1.105f),new Vector3(.61f,.37f,.025f),bayDark);
                for(int j=0;j<7;j++)Box("Cabinet cooling louvre",new Vector3(x,.49f+j*.052f,1.103f),new Vector3(.5f,.018f,.025f),bayDark);
                Box("Door pull",new Vector3(x+.27f,.88f,1.07f),new Vector3(.025f,.16f,.035f),bayIvory);
                for(int j=0;j<3;j++)
                {
                    var face=Shape("Pressure dial",PrimitiveType.Cylinder,new Vector3(x-.2f+j*.2f,1.23f,1.072f),new Vector3(.145f,.018f,.145f),bayIvory,bay);
                    face.localRotation=Quaternion.Euler(90,0,0);
                    Pipe("Pressure dial needle",new[]{new Vector3(x-.2f+j*.2f,1.23f,1.05f),new Vector3(x-.22f+j*.2f,1.275f,1.05f)},.005f,bayDark);
                    Box("Instrument status lamp",new Vector3(x-.2f+j*.2f,1.095f,1.078f),new Vector3(.035f,.022f,.018f),j==2?lightMaterial:bayDisplay);
                }
                // A separate return reservoir gives the two control stations a readable industrial silhouette.
                Shape("Coolant accumulator",PrimitiveType.Cylinder,new Vector3(side*2.38f,1.92f,1.68f),new Vector3(.28f,.46f,.28f),metal,bay);
                foreach(float y in new[]{1.58f,2.22f})Ring("Accumulator retaining band",new Vector3(side*2.38f,y,1.68f),.155f,.024f,bayDark,bay);
                Pipe("Rear service riser",new[]{new Vector3(x,1.5f,1.65f),new Vector3(x,2.5f,1.65f),new Vector3(x,6.55f,1.65f)},.065f,metal);
                Pipe("Overhead supply elbow",Curve(new Vector3(x,6.55f,1.65f),new Vector3(x,6.85f,1.65f),new Vector3(side*1.4f,6.85f,.8f),new Vector3(side*1.4f,6.10f,.8f)),.065f,metal);
                for(int j=0;j<5;j++)
                {
                    float y=2.6f+j*.75f;
                    Box("Riser mounting bracket",new Vector3(x,y,1.72f),new Vector3(.26f,.075f,.22f),bayDark);
                    Ring("Pipe compression joint",new Vector3(x,y,1.65f),.09f,.018f,metal,bay);
                }
                for(int j=0;j<2;j++)Pipe("Recessed platform feed",new[]{new Vector3(x+j*.10f,.18f,1.1f),new Vector3(x+j*.10f,.18f,-1.6f),new Vector3(side*.8f,.18f,-1.6f),new Vector3(side*.8f,.23f,-.7f)},.026f,bayDark);
                // Worklights are mounted above the rear corners and aimed at the organ.
                Pipe("Inspection light arm",new[]{new Vector3(x,4.8f,1.65f),new Vector3(x,4.8f,-.85f)},.045f,metal);
                Box("Inspection lamp housing",new Vector3(x,4.73f,-.85f),new Vector3(.48f,.16f,.30f),bayDark);
                Box("Inspection lamp lens",new Vector3(x,4.64f,-.85f),new Vector3(.39f,.025f,.23f),lightMaterial);
                BayLight("Warm maintenance spotlight",new Vector3(x,4.6f,-.85f),new Vector3(0,2.7f,0),new Color(1,.75f,.49f),8,7,65);
            }
            // Batch the repeated cabinet fittings and pipe meshes by material.
            var batches=new Dictionary<Material,List<CombineInstance>>();
            foreach(var filter in bay.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();var material=renderer.sharedMaterial;
                if(!batches.TryGetValue(material,out var list)){list=new List<CombineInstance>();batches.Add(material,list);}
                list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=bay.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                renderer.enabled=false;Destroy(filter.gameObject);
            }
            foreach(var batch in batches)
            {
                var mesh=new Mesh{name="Maintenance bay / "+batch.Key.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(batch.Value.ToArray());meshes.Add(mesh);
                var obj=new GameObject(mesh.name);obj.transform.SetParent(bay,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=batch.Key;
            }
        }
        void BayLight(string name,Vector3 p,Vector3 target,Color color,float intensity,float range,float angle)
        {
            var obj=new GameObject(name);obj.transform.SetParent(transform,false);obj.transform.localPosition=p;
            obj.transform.localRotation=Quaternion.LookRotation(target-p);
            var light=obj.AddComponent<Light>();light.type=LightType.Spot;light.color=color;light.intensity=intensity;light.range=range;light.spotAngle=angle;light.innerSpotAngle=angle*.55f;
            light.shadows=LightShadows.None;light.cullingMask=~(1<<30);
        }
    }
}
