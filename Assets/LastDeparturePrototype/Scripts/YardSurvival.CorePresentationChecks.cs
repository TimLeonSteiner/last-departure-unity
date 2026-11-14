#if UNITY_EDITOR
using System;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunCorePresentationChecks(bool requireAudio=true)
        {
            var report=new System.Text.StringBuilder();
            void Check(bool ok,string text){if(!ok)throw new Exception(text);report.AppendLine("PASS "+text);}
            ResetRun();
            try
            {
                EnsureCoreAnnouncer();
                if(requireAudio)for(int i=0;i<announcerClips.Length;i++)Check(announcerClips[i]!=null&&announcerClips[i].length>.5f,"Announcer clip loaded: "+AnnouncerFiles[i]);
                AnnounceCoreDamage();Check(announcerSubtitle==AnnouncerLines[0],"Core hit triggers the attack warning");
                float deadline=announcerUntil;AnnounceCoreDamage();Check(announcerUntil==deadline&&queuedAnnouncement<0,"Repeated hits do not spam or queue warnings");
                coreHealth=100;AnnounceCoreDamage();Check(announcerPriority==3&&announcerSubtitle==AnnouncerLines[1],"Critical warning interrupts a routine call");
                coreHealth=0;AnnounceCoreDamage();Check(announcerPriority==4&&announcerSubtitle==AnnouncerLines[2],"Core destruction takes highest priority");
                ResetCoreAnnouncer();Check(announcerSubtitle==""&&queuedAnnouncement<0&&!coreAnnouncer.isPlaying,"Restart clears voice playback and subtitles");
                var mount=coreVisual.transform.Find("Chained Heart Machine");
                if(mount!=null)
                {
                    var material=mount.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                    Check(material.shader.name=="Dustline/Chained Heart","Imported heart uses its tissue and steel material");
                    coreVisual.SetCondition(1,false);coreVisual.SamplePulse(0);
                    float rest=material.GetFloat("_Pulse");Vector3 scale=mount.localScale;
                    coreVisual.SamplePulse(.17f/1.05f);
                    Check(material.GetFloat("_Pulse")>rest+.9f&&mount.localScale==scale,"Heartbeat deforms tissue with a fixed machine mount");
                    coreVisual.SetCondition(0,true);coreVisual.SamplePulse(.2f);
                    Check(material.GetFloat("_Pulse")==0,"Destroyed core stops pulsing");
                }
                else
                {
                    var mesh=coreVisual.transform.Find("Living anatomical heart");
                    coreVisual.SetCondition(1,false);coreVisual.SamplePulse(0);Vector3 rest=mesh.localScale;coreVisual.SamplePulse(.17f/1.05f);
                    Check(mesh.localScale.x>rest.x+.06f&&mesh.localScale.y<rest.y,"Heart contracts lengthwise and expands across each heartbeat");
                    coreVisual.SetCondition(0,true);coreVisual.SamplePulse(.2f);Vector3 stopped=mesh.localScale;coreVisual.SamplePulse(.7f);
                    Check(mesh.localScale==stopped,"Destroyed core stops pulsing");
                }
                return report.ToString();
            }
            finally{ResetRun();}
        }
    }
}
#endif
