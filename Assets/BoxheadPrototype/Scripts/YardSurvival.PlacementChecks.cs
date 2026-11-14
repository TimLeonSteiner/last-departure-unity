#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public string RunPlacementChecks()
        {
            int oldSupplies=supplies,oldSelected=selectedSkill,oldRank=selectedSkillRank,oldPoints=skillPoints,oldBoost=weaponBoost,oldRank0=skillRanks[0];
            bool oldPause=AudioListener.pause;var oldStep=preparationStep;var oldTarget=preparationTarget;bool oldBlocked=buildFireBlocked;
            string report="";
            void Check(bool ok,string text){if(!ok)throw new Exception(text);report+="PASS "+text+"\n";}
            try
            {
                preparationStep=PreparationStep.None;
                foreach(int type in new[]{0,1,3})
                {
                    supplies=100;Vector3 point=Vector3.zero;bool found=false;
                    for(int x=-7;x<=7&&!found;x++)for(int z=-7;z<=7&&!found;z++)
                    {var candidate=player.position+new Vector3(x,0,z);candidate.y=0;if(DefensePlacementIssue(candidate,type,false)==null){point=candidate;found=true;}}
                    Check(found,"Valid placement fixture for "+BuildNames[type]);
                    int before=supplies;Check(TryPlaceDefense(point,type,false),"Place "+BuildNames[type]);
                    Check(supplies==before-DefenseCosts[type],"Full displayed cost charged once");
                    if(type==3){var b=placedBarrels.OrderBy(k=>k.Key).Last();Check(b.Value!=null&&!b.Value.Detonated,"Barrel ready immediately");placedBarrels.Remove(b.Key);explosiveBarrels.Remove(b.Value);DestroyImmediate(b.Value.gameObject);}
                    else{Transform body=type==0?barricades.Last().body:turrets.Last().body;var state=body.GetComponent<DefenseConstruction>();Check(ConstructionReady(body)&&state.paid==DefenseCosts[type],"Defense ready without hammer strikes");if(type==0)barricades.RemoveAt(barricades.Count-1);else turrets.RemoveAt(turrets.Count-1);DestroyImmediate(body.gameObject);}
                    supplies=DefenseCosts[type]-1;Check(DefensePlacementIssue(point,type,false).StartsWith("Need "),"Insufficient scrap rejected");
                    var clip=placementClips[type];Check(clip!=null&&clip.length>.1f,"Placement sound loaded");
                }
                AudioListener.pause=true;selectedSkill=0;selectedSkillRank=0;nextMenuHover=0;
                SelectSkillNode(3,0);Check(selectedSkill==3&&skillFeedback!=null&&skillFeedback.ignoreListenerPause&&!skillFeedback.mute,"Skill selection audio works while gameplay audio is paused");
                skillPoints=1;skillRanks[0]=0;Check(BuySkill(0)&&skillPoints==0&&weaponBoost==oldBoost+1,"Skill purchase applies upgrade");
                Check(skillInstallClip!=null&&skillSelectClip!=null,"Selection and confirmation clips loaded");
                return report;
            }
            finally{supplies=oldSupplies;selectedSkill=oldSelected;selectedSkillRank=oldRank;skillPoints=oldPoints;weaponBoost=oldBoost;skillRanks[0]=oldRank0;AudioListener.pause=oldPause;preparationStep=oldStep;preparationTarget=oldTarget;buildFireBlocked=oldBlocked;hologramRenderedTab=-1;}
        }
    }
}
#endif
