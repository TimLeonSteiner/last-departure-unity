using System.Collections.Generic;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public GameObject ammoBoxModel,healthBoxModel;
        readonly List<SupplyBox> supplyBoxes=new List<SupplyBox>();
        readonly HashSet<int> suppliedWeapons=new HashSet<int>();
        sealed class SupplyBox {public int id;public bool health;public Vector3 position;public Transform body;}
        float supplyRequestAt;
        GameObject originalAmmoModel;
        bool PickupLineClear(Vector3 from,Vector3 to)
        {
            Vector3 origin=new Vector3(from.x,.65f,from.z);Vector3 delta=to+Vector3.up*.35f-origin;
            return ShotStopDistance(origin,delta.normalized)+.04f>=delta.magnitude;
        }
        void CreateSupplyBox(Vector3 position,bool medical,int id=0)
        {
            if(!Free(position,.35f,true))return;
            var root=new GameObject(medical?"Health supply":"Ammo supply").transform;root.SetParent(actors,false);root.position=position;
            if(!medical&&originalAmmoModel==null)originalAmmoModel=Resources.Load<GameObject>("OriginalPickups/AmmoBox");
            var model=medical?healthBoxModel:(originalAmmoModel!=null?originalAmmoModel:ammoBoxModel);
            if(model!=null)Instantiate(model,root);
            else Box(medical?"Medical crate":"Ammunition crate",position+Vector3.up*.23f,new Vector3(.55f,.46f,.4f),medical?redMaterial:yellowMaterial,root);
            supplyBoxes.Add(new SupplyBox{id=id==0?nextCoopEntity++:id,health=medical,position=position,body=root});
        }
        void SpawnWaveSupplies(int upcoming)
        {
            int count=0;foreach(var box in supplyBoxes)if(!box.health)count++;
            if(count<6){CreateSupplyBox(new Vector3(-4,0,1+(upcoming%3)*1.5f),false);CreateSupplyBox(new Vector3(4,0,-1-(upcoming%3)*1.5f),false);}
            count=0;foreach(var box in supplyBoxes)if(box.health)count++;
            if(count<3)CreateSupplyBox(new Vector3((upcoming%3-1)*2,0,6),true);
        }
        bool ConsumeSupply(SupplyBox box,Vector3 position,ref int hp,int weapon,ref int reserve)
        {
            if(hp<=0||(box.position-new Vector3(position.x,0,position.z)).sqrMagnitude>1.0f||!PickupLineClear(position,box.position))return false;
            if(box.health){if(hp>=maxPlayerHealth)return false;hp=Mathf.Min(maxPlayerHealth,hp+35);}
            else
            {
                if(WeaponCatalog.IsMelee(weapon))return false;
                int capacity=WeaponCatalog.All[weapon].capacity,cap=capacity*5;
                if(reserve>=cap)return false;
                reserve=Mathf.Min(cap,reserve+capacity*2);
            }
            supplyBoxes.Remove(box);box.body.gameObject.SetActive(false);Destroy(box.body.gameObject);PlayEffect(hitSound,.35f);return true;
        }
        void UpdateSupplyBoxes()
        {
            if(dead)return;
            foreach(var box in supplyBoxes.ToArray())
            {
                if((box.position-new Vector3(player.position.x,0,player.position.z)).sqrMagnitude>1.0f)continue;
                if(IsCoopGuest){if(Time.unscaledTime>=supplyRequestAt&&PickupLineClear(player.position,box.position)){supplyRequestAt=Time.unscaledTime+.25f;SendCoopCommand("supply",box.id);}break;}
                ConsumeSupply(box,player.position,ref health,weaponId,ref reserveAmmo);
            }
        }
    }
}
