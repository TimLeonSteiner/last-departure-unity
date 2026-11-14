using System.Linq;
using UnityEngine;
using YardSurvivalPrototype;
public static partial class AnimatedArsenalImport
{
    public static void CalibrateMuzzle(AnimatedWeaponRig rig,int id)
    {
        if(WeaponCatalog.IsMelee(id)||id==7)return;
        var weapon=rig.model.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>id==6?r.name=="front":r.name=="base"||r.name=="pistol"||r.name=="boomstick"||r.name=="barrels");
        if(weapon==null)return;
        var points=SightVertices(weapon,id,rig);
        if(points.Length==0)return;
        // The whole gun's bounds centre includes the grip/magazine and can put
        // the flash below the barrel. Locate the centre of its frontmost ring.
        float end=points.Max(p=>p.z);
        var tip=points.Where(p=>p.z>=end-.006f).ToArray();
        Vector3 world=rig.transform.position+new Vector3((tip.Min(p=>p.x)+tip.Max(p=>p.x))*.5f,(tip.Min(p=>p.y)+tip.Max(p=>p.y))*.5f,end+.008f);
        rig.muzzle=rig.transform.InverseTransformPoint(world);
        rig.muzzleAnchor=weapon.transform;
        if(weapon is SkinnedMeshRenderer skinned)
        {
            var weights=skinned.sharedMesh.boneWeights;
            var bones=skinned.bones;
            var totals=new float[bones.Length];
            void Add(int bone,float weight){if(bone>=0&&bone<totals.Length)totals[bone]+=weight;}
            for(int i=0;i<points.Length&&i<weights.Length;i++)if(points[i].z>=end-.006f)
            {
                var w=weights[i];Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);
            }
            if(totals.Length>0){int best=0;for(int i=1;i<totals.Length;i++)if(totals[i]>totals[best])best=i;if(totals[best]>0&&bones[best]!=null)rig.muzzleAnchor=bones[best];}
        }
        rig.muzzleAnchorOffset=rig.muzzleAnchor.InverseTransformPoint(world);
    }
    public static void RepairLongGunSights(AnimatedWeaponRig rig,int id)
    {
        var clip=rig.idle!=null?rig.idle:rig.fire;var frames=rig.idle!=null?rig.idleFrames:rig.fireFrames;
        if(clip!=null)clip.SampleAnimation(rig.model,Mathf.Clamp(frames.x/clip.frameRate,0,clip.length));
        CalibrateSights(rig,id);
    }
    static void CalibrateSights(AnimatedWeaponRig rig,int id)
    {
        if(id==1){rig.hipPosition=new Vector3(.2f,-.461f,.68f);rig.aimPosition=new Vector3(0,-.28f,.4f);return;}
        if(WeaponCatalog.IsMelee(id)||id==6||id==8)
        {rig.aimPosition=new Vector3(0,rig.hipPosition.y*.7f,rig.hipPosition.z);return;}
        var renderers=rig.model.GetComponentsInChildren<Renderer>();
        string[] names=id==16?new[]{"scoperender"}:new[]{"base","pistol","boomstick","slide",id==0?"lid":"__none"};
        var points=renderers.Where(r=>names.Contains(r.name)).SelectMany(r=>SightVertices(r,id,rig)).ToArray();
        if(points.Length<3)return;
        float min=points.Min(v=>v.z),max=points.Max(v=>v.z),span=max-min;
        if(id==0||id==2||id==12||id==15)System.IO.File.WriteAllLines("Assets/BoxheadPrototype/SightPoints-"+id+".csv",points.Select(v=>v.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+v.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+v.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
        Vector3 rear,front;
        if(id==2)
        {
            // Authored P9 aperture: the rear notch is at z=.176, not the
            // arbitrary 30% barrel slice. Aim through its opening beneath the wings.
            rear=new Vector3(0,.1706f,.176f);front=new Vector3(0,.17163438f,.39145f);
        }
        else if(id==3)
        {
            // Sight over the stock toward the front bead, keeping the support hand below the eye.
            rear=new Vector3(0,.17f,.111207195f);front=new Vector3(0,.0677865f,.61539733f);
        }
        else if(id==0)
        {rear=new Vector3(0,.151981f,-.021082f);front=new Vector3(0,.16001f,.50535f);}
        else if(id==15)
        {rear=new Vector3(0,.138376f,-.179996f);front=new Vector3(0,.137552f,.450723f);}
        else if(id==16)
        {rear=new Vector3(points.Average(v=>v.x),points.Average(v=>v.y),min);front=rear+Vector3.forward*.2f;}
        else
        {
            var back=points.Where(v=>v.z>=min+span*.18f&&v.z<=min+span*.42f).ToArray();
            var forward=points.Where(v=>v.z>=min+span*.72f&&v.z<=min+span*.96f).ToArray();
            if(back.Length==0||forward.Length==0)return;
            // Rear/front sight crown in the authored idle pose; arm/grip vertices are excluded.
            float rearY=back.Max(v=>v.y),frontY=forward.Max(v=>v.y);
            rear=new Vector3(back.Where(v=>v.y>=rearY-.003f).Average(v=>v.x),rearY,min+span*.3f);
            front=new Vector3(forward.Where(v=>v.y>=frontY-.003f).Average(v=>v.x),frontY,min+span*.84f);
        }
        Quaternion rotation=Quaternion.FromToRotation((front-rear).normalized,Vector3.forward);
        rig.rearSight=rear;rig.frontSight=front;rig.aimEuler=rotation.eulerAngles;rig.calibratedSights=true;
        float relief=id==16?.12f:id==9?.22f:id==15?.24f:(id==4||id==7||id==14)?.30f:id==3?.34f:.44f;
        rig.aimPosition=Vector3.forward*relief-rotation*rear;
    }
    static Vector3[] SightVertices(Renderer renderer,int id,AnimatedWeaponRig rig)
    {
        Vector3[] vertices;
        if(renderer is SkinnedMeshRenderer skin)
        {
            var baked=new Mesh();skin.BakeMesh(baked,false);vertices=baked.vertices;
            // The P9 bone deformation includes the uniform 88x model scale.
            // Normalize LOCAL baked vertices before applying the renderer transform:
            // its translation is already in meters and must never be divided.
            if(id==2)for(int i=0;i<vertices.Length;i++)vertices[i]/=Mathf.Abs(skin.transform.lossyScale.x);
            UnityEngine.Object.DestroyImmediate(baked);
        }
        else {var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)return new Vector3[0];vertices=filter.sharedMesh.vertices;}
        return vertices.Select(v=>renderer.transform.TransformPoint(v)-rig.transform.position).ToArray();
    }

}
