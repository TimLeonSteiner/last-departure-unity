#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunZombieInjuryChecks()
        {
            var report=new System.Text.StringBuilder();
            void Check(bool ok,string text){if(!ok)throw new Exception(text);report.AppendLine("PASS "+text);}
            foreach(var prefab in zombieModels)
            {
                var root=new GameObject("Injury verification");
                try
                {
                    var z=Instantiate(prefab,root.transform).GetComponent<ImportedZombie>();z.DressWounds(woundMaterial);
                    z.TickMovement(.2f,0,false);
                    var chest=z.animationRig.GetBoneTransform(HumanBodyBones.Chest)??z.animationRig.GetBoneTransform(HumanBodyBones.Spine);
                    Quaternion rest=chest.rotation;
                    z.ReactToHit(Vector3.forward,5,1);z.TickMovement(.04f,0,false);float lightAngle=Quaternion.Angle(rest,chest.rotation);
                    z.TickMovement(.5f,0,false);Check(Quaternion.Angle(rest,chest.rotation)<.5f,"Hit reaction recovers without leaving a pose offset");
                    z.ReactToHit(Vector3.forward,5,8);z.TickMovement(.04f,0,false);
                    Check(Quaternion.Angle(rest,chest.rotation)>lightAngle*1.3f,"Strong shots produce a larger body reaction");
                    z.TickMovement(.5f,0,false);z.ReactToHit(Vector3.forward,1,3);z.TickMovement(.06f,0,false);Quaternion left=chest.rotation;
                    z.TickMovement(.5f,0,false);z.ReactToHit(Vector3.forward,2,3);z.TickMovement(.06f,0,false);
                    Check(Quaternion.Angle(left,chest.rotation)>15,"Opposite shoulder hits twist in different directions");
                    z.TickMovement(.5f,0,false);
                    z.TickMovement(.2f,.2f,false);
                    Check(z.Sever(3,Vector3.forward,root.transform)&&z.LostLegs==1,prefab.name+": leg detaches");
                    Check(!z.Sever(3,Vector3.forward,root.transform),"Repeated hits cannot detach the same leg twice");
                    z.TickMovement(.2f,.1f,false);
                    var foot=z.animationRig.GetBoneTransform(HumanBodyBones.RightFoot);
                    Check(Mathf.Abs(foot.position.y-.09f)<.025f,"Remaining foot supports the limping pose");
                    var cap=z.GetComponentsInChildren<MeshFilter>().First(m=>m.name=="Detailed cut surface");
                    Check(cap.transform.parent==z.severJoints[3].parent,"Stump is attached to surviving skeleton");
                    Check(cap.GetComponent<Collider>()==null&&cap.sharedMesh.vertexCount>100,"Detailed wound cap adds no blocking collider");
                    var hips=z.animationRig.GetBoneTransform(HumanBodyBones.Hips);
                    float standingHeight=hips.position.y;
                    z.Sever(4,Vector3.forward,root.transform);z.TickMovement(.2f,.1f,false);
                    Check(z.IsCollapsing&&hips.position.y<standingHeight&&hips.position.y>.5f,"Second leg loss begins a visible fall instead of instantly crawling");
                    z.TickMovement(.3f,.2f,true,.4f);Check(z.IsCollapsing,"Collapse takes priority over an attack animation");
                    z.TickMovement(.25f,0,false);Check(!z.IsCollapsing,"Collapse settles before crawling resumes");
                    var head=z.animationRig.GetBoneTransform(HumanBodyBones.Head);
                    Check(z.LostLegs==2&&hips.position.y>.3f&&hips.position.y<.5f,"Double leg loss lowers pelvis into crawl stance");
                    Check(head.position.y<1&&head.position.y>.1f,"Crawling upper body stays above the floor and below standing height");
                    var hand=z.animationRig.GetBoneTransform(HumanBodyBones.LeftHand);Vector3 before=hand.position;
                    z.TickMovement(.2f,.2f,false);Check(Vector3.Distance(before,hand.position)>.02f,"Travel advances alternating crawl arm reach");
                    before=hand.position;z.TickMovement(.2f,0,false);Check(Vector3.Distance(before,hand.position)<.003f,"Stationary crawler does not paddle in place");
                    z.Sever(1,Vector3.right,root.transform);z.TickMovement(.2f,.1f,true,.4f);
                    Check(z.LostArms==1&&float.IsFinite(head.position.y),"Crawl attack remains valid with a missing arm");
                    z.Die(Vector3.forward,hips.position,1);Check(z.RagdollBodyCount>0,"Injured pose transfers into the death ragdoll");
                }
                finally{DestroyImmediate(root);}
            }
            return report.ToString();
        }
        public void ReviewZombieInjuryPoses(Action capture,bool collapseStages=false)
        {
            ResetRun();
            try
            {
                for(int i=0;i<3;i++)
                {
                    SpawnEnemy(new Vector3((i-1)*2,0,4));var e=enemies[enemies.Count-1];e.body.rotation=Quaternion.Euler(0,160,0);
                    if(collapseStages)
                    {
                        e.zombie.Sever(3,Vector3.right,actors);e.zombie.TickMovement(.2f,0,false);
                        e.zombie.Sever(4,Vector3.right,actors);
                        float elapsed=i==0?.14f:i==1?.38f:.72f;
                        for(float t=0;t<elapsed;t+=.02f)e.zombie.TickMovement(.02f,0,false);
                    }
                    else
                    {
                        if(i>0)e.zombie.Sever(3,Vector3.right,actors);
                        if(i>1)e.zombie.Sever(4,Vector3.right,actors);
                        e.zombie.TickMovement(.2f,.15f,false);
                        if(i>1)e.zombie.TickMovement(.6f,0,false);
                    }
                }
                // The comparison is captured before physics advances. Hide the loose
                // limbs so they do not appear to remain attached in this still image.
                foreach(var debris in actors.GetComponentsInChildren<ZombieDebris>())debris.gameObject.SetActive(false);
                capture();
            }
            finally{ResetRun();}
        }
    }
}
#endif
