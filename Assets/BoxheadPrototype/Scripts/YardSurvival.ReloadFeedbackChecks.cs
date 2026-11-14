#if UNITY_EDITOR
using System;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunReloadFeedbackChecks()
        {
            string report="";void Check(bool ok,string text){if(!ok)throw new Exception(text);report+="PASS "+text+"\n";}
            foreach(int count in new[]{1,3,6})
            {
                int initial=6-count,ammo=initial,reserve=count+4;
                for(int i=0;i<count;i++)
                {
                    float insert=.12f+.73f*(i+.72f)/count;
                    TransferReloadShells(insert-.0001f,initial,count,ref ammo,ref reserve);Check(ammo==initial+i,"No early shell credit ("+count+" shells, insertion "+(i+1)+")");
                    TransferReloadShells(insert+.0001f,initial,count,ref ammo,ref reserve);Check(ammo==initial+i+1&&reserve==count+4-i-1,"One shell transferred per visible insertion");
                }
                TransferReloadShells(1,initial,count,ref ammo,ref reserve);Check(ammo==6&&reserve==4,"Closing action adds no duplicate ammunition");
            }
            int a=1,r=2;TransferReloadShells(1.2f,1,2,ref a,ref r);Check(a==3&&r==0,"Low reserve and large frame step preserve ammo");
            TransferReloadShells(1.2f,1,2,ref a,ref r);Check(a==3&&r==0,"Repeated authoritative update does not double transfer");
            float old=displayedDrowsiness;bool oldSound=soundEnabled,oldUnlock=audioUnlocked;
            try{
                displayedDrowsiness=.8f;soundEnabled=audioUnlocked=true;
                ShowTurretShot(player.position+Vector3.up*2,Vector3.forward,player.position+Vector3.forward*3);
                AudioSource shot=null;foreach(var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))if(source.name=="Sentry discharge audio")shot=source;
                Check(shot!=null,"Turret shot audio created");var filter=shot.GetComponent<AudioLowPassFilter>();
                Check(filter!=null&&filter.enabled&&Mathf.Abs(filter.cutoffFrequency-7600)<1,"Drowsy turret filtering configured before playback");
                Check(shot.pitch==1&&shot.volume<=effects.volume*.46f,"Stable pitch and reduced turret gain");
            }finally{displayedDrowsiness=old;soundEnabled=oldSound;audioUnlocked=oldUnlock;}
            return report;
        }
    }
}
#endif
