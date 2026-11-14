using System.Collections.Generic;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public GameObject luckyChestModel;
        public int luckyChestCost=6;
        LuckyChestVisual luckyChest;
        Transform chestReward;
        Material chestGlowMaterial;
        readonly Dictionary<Material,Material> chestPrizeMaterials=new Dictionary<Material,Material>();
        Light chestGlow;
        int chestStage,chestWeapon=-1,chestVisualWeapon=-1;
        float chestTimer,chestRollTick;
        Vector3 chestPosition=new Vector3(-5,0,3);
        Rect ChestBounds=>new Rect(chestPosition.x-.85f,chestPosition.z-.55f,1.7f,1.1f);
        Vector3 ChestApproach=>chestPosition+Vector3.forward*1.2f;
        void SetupLuckyChest()
        {
            luckyChest=null;chestReward=null;chestGlow=null;chestStage=0;chestWeapon=chestVisualWeapon=-1;chestTimer=0;
            if(luckyChestModel==null)return;
            luckyChest=Instantiate(luckyChestModel,actors).GetComponent<LuckyChestVisual>();
            luckyChest.transform.position=chestPosition;luckyChest.Pose(0);
            if(chestGlowMaterial==null)
            {
                chestGlowMaterial=new Material(muzzleMaterial);chestGlowMaterial.name="Lucky weapon cyan glow";
                chestGlowMaterial.color=new Color(.12f,.75f,1);
                chestGlowMaterial.EnableKeyword("_EMISSION");chestGlowMaterial.SetColor("_EmissionColor",new Color(.08f,.8f,1)*3);
            }
            chestGlow=new GameObject("Lucky chest reward light").AddComponent<Light>();chestGlow.transform.SetParent(luckyChest.transform,false);
            chestGlow.transform.localPosition=Vector3.up*1.6f;chestGlow.type=LightType.Point;chestGlow.range=4;chestGlow.color=new Color(.15f,.7f,1);chestGlow.intensity=0;
        }
        bool NearLuckyChest(Vector3 position)=>luckyChest!=null&&(new Vector3(position.x,0,position.z)-ChestApproach).sqrMagnitude<2.3f*2.3f&&PickupLineClear(position,ChestApproach);
        int RollChestWeapon(int current)
        {
            var choices=new List<int>();for(int i=0;i<WeaponCatalog.All.Length;i++)if(WeaponCatalog.IsAvailable(i)&&i!=current&&ModelFor(i)!=null)choices.Add(i);
            return choices.Count==0?-1:choices[Random.Range(0,choices.Count)];
        }
        bool BuyLuckyChest(Vector3 position,int current)
        {
            if(!NearLuckyChest(position)||chestStage!=0||supplies<luckyChestCost)return false;
            int result=RollChestWeapon(current);if(result<0)return false;
            supplies-=luckyChestCost;chestWeapon=result;chestStage=1;chestTimer=.65f;chestRollTick=0;PlayEffect(WeaponClip(2),.3f);return true;
        }
        bool ClaimLuckyChest(Vector3 position,bool guest)
        {
            if(chestStage!=3||chestTimer<=0||!NearLuckyChest(position))return false;
            if(guest){GrantPeerChestWeapon(chestWeapon);}
            else {EquipWeapon(chestWeapon,WeaponCatalog.All[chestWeapon].capacity);ShowBanner("LUCKY CHEST: "+ActiveWeapon.name);PlayEffect(WeaponClip(2),.4f);}
            chestStage=4;chestTimer=.45f;return true;
        }
        bool InteractLuckyChest()
        {
            if(!NearLuckyChest(player.position))return false;
            if(IsCoopGuest){SendCoopCommand("chest");return true;}
            if(chestStage==0){if(!BuyLuckyChest(player.position,weaponId))ShowBanner("LUCKY CHEST: "+luckyChestCost+" SCRAP REQUIRED");}
            else if(chestStage==3)ClaimLuckyChest(player.position,false);
            return true;
        }
        void ShowChestWeapon(int id)
        {
            if(chestVisualWeapon==id&&chestReward!=null)return;
            if(chestReward!=null){chestReward.gameObject.SetActive(false);Destroy(chestReward.gameObject);}
            chestVisualWeapon=id;
            chestReward=Instantiate(ModelFor(id),luckyChest.transform).transform;
            var rig=chestReward.GetComponent<AnimatedWeaponRig>();rig?.GroundPose();
            foreach(var a in chestReward.GetComponentsInChildren<Animator>())a.enabled=false;
            foreach(var c in chestReward.GetComponentsInChildren<Collider>())c.enabled=false;
            foreach(var r in chestReward.GetComponentsInChildren<Renderer>())
            {
                r.gameObject.layer=0;
                var materials=r.sharedMaterials;
                for(int i=0;i<materials.Length;i++)if(materials[i]!=null)
                {
                    if(!chestPrizeMaterials.TryGetValue(materials[i],out var glowMat))
                    {glowMat=new Material(materials[i]);glowMat.EnableKeyword("_EMISSION");glowMat.SetColor("_EmissionColor",new Color(.08f,.45f,.65f));chestPrizeMaterials[materials[i]]=glowMat;}
                    materials[i]=glowMat;
                }
                r.sharedMaterials=materials;
            }
            // Bake the static prize mesh into ordinary geometry so no FPS arms or recoil survive.
            var bounds=new Bounds();bool first=true;
            foreach(var r in chestReward.GetComponentsInChildren<Renderer>())if(r.enabled){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            float scale=.95f/Mathf.Max(.1f,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z)));
            chestReward.localScale*=scale;
            var pivot=new GameObject("Floating prize pivot").transform;pivot.SetParent(luckyChest.transform,false);
            Vector3 centerOffset=(bounds.center-chestReward.position)*scale;
            chestReward.SetParent(pivot,false);chestReward.localPosition=-luckyChest.transform.InverseTransformVector(centerOffset);
            var glow=GameObject.CreatePrimitive(PrimitiveType.Sphere);glow.name="Reward halo";glow.transform.SetParent(pivot,false);glow.transform.localPosition=Vector3.down*.3f;glow.transform.localScale=new Vector3(.8f,.025f,.8f);Destroy(glow.GetComponent<Collider>());glow.GetComponent<Renderer>().sharedMaterial=chestGlowMaterial;
            chestReward=pivot;
        }
        void UpdateLuckyChest(float dt)
        {
            if(luckyChest==null)return;
            chestTimer=Mathf.Max(0,chestTimer-dt);
            if(!IsCoopGuest&&chestStage!=0&&chestTimer<=0)
            {
                if(chestStage==1){chestStage=2;chestTimer=1.8f;}
                else if(chestStage==2){chestStage=3;chestTimer=15;ShowChestWeapon(chestWeapon);}
                else if(chestStage==3){chestStage=4;chestTimer=.45f;}
                else {chestStage=0;chestWeapon=-1;}
            }
            float open=chestStage==0?0:chestStage==1?1-chestTimer/.65f:chestStage==4?chestTimer/.45f:1;luckyChest.Pose(open);
            if(chestStage==2){chestRollTick-=dt;if(chestRollTick<=0){chestRollTick=.14f;ShowChestWeapon(RollChestWeapon(-1));}}
            if(chestStage==3&&chestVisualWeapon!=chestWeapon)ShowChestWeapon(chestWeapon);
            bool visible=chestStage==2||chestStage==3;
            if(chestReward!=null){chestReward.gameObject.SetActive(visible);chestReward.localPosition=new Vector3(0,1.65f+Mathf.Sin(Time.time*2)*.045f,0);chestReward.Rotate(Vector3.up,dt*55,Space.Self);}
            chestGlow.intensity=visible?2.5f+Mathf.Sin(Time.time*3)*.3f:0;
        }
        string LuckyChestHint()=>chestStage==0?ControlHint(Control.Pickup)+" LUCKY CHEST — "+luckyChestCost+" SCRAP":chestStage==3?ControlHint(Control.Pickup)+" "+WeaponCatalog.All[chestWeapon].name+" pick up":chestStage==4?"LUCKY CHEST CLOSING …":"ROLLING RANDOM WEAPON …";
    }
}
