using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        readonly Vector3 slotPosition=new Vector3(5,0,3);
        Vector3 SlotApproach=>slotPosition+Vector3.forward*1.3f;
        Rect SlotBounds=>new Rect(slotPosition.x-.6f,slotPosition.z-.5f,1.2f,1);
        Transform slotMachine;
        int slotBet=1,slotStake,slotPayout,slotSerial,slotMultiplier;
        float slotTimer;
        bool slotBusy;
        Material slotVisualMaterial;
        static int SlotMultiplier(int roll)=>roll<55?2:roll<65?5:0;
        void SetupSlots()
        {
            slotBet=1;slotStake=slotPayout=slotSerial=slotMultiplier=0;slotTimer=0;slotBusy=false;
            if(slotVisualMaterial!=null)Destroy(slotVisualMaterial);
            slotMachine=new GameObject("Scrap slot machine").transform;slotMachine.SetParent(actors,false);slotMachine.position=slotPosition;
            var model=Instantiate(Resources.Load<GameObject>("DustlineSlotMachine"),slotMachine,false);
            slotVisualMaterial=model.GetComponentInChildren<Renderer>().material;
            var light=new GameObject("Slot machine light").AddComponent<Light>();light.transform.SetParent(slotMachine,false);
            light.transform.localPosition=new Vector3(0,1.8f,1.2f);light.type=LightType.Point;light.color=new Color(1,.8f,.4f);light.range=4;light.intensity=3;
        }
        // Targets follow the existing model's console; no extra buttons or screen cover.
        int SlotTarget(Ray ray)
        {
            if(!SlotControlsActive())return -1;
            var bounds=new Bounds(slotPosition+new Vector3(0,1.05f,0),new Vector3(1.05f,2.1f,1));
            if(!bounds.IntersectRay(ray,out float distance)||distance>4)return -1;
            Vector3 hit=ray.GetPoint(distance)-slotPosition;
            if(hit.z<.45f)return -1;
            if(hit.y>.85f&&hit.y<1.22f){if(hit.x>.12f)return 0;if(hit.x>-.12f)return 1;}
            return 2;
        }
        Ray SlotPointerRay()
        {
            var mouse=UnityEngine.InputSystem.Mouse.current;
            return Cursor.lockState==CursorLockMode.Locked||mouse==null?new Ray(arenaCamera.transform.position,arenaCamera.transform.forward):arenaCamera.ScreenPointToRay(mouse.position.ReadValue());
        }
        bool HandleSlotClick(Ray ray)
        {
            int target=SlotTarget(ray);if(target<0)return false;
            if(target==0)ChangeSlotBet(-1);else if(target==1)ChangeSlotBet(1);else SpinSlots();
            return true;
        }
        bool NearSlots(Vector3 position)=>slotMachine!=null&&(new Vector3(position.x,0,position.z)-SlotApproach).sqrMagnitude<=2.3f*2.3f&&PickupLineClear(position,SlotApproach);
        bool SlotControlsActive()=>frontScreen==FrontScreen.Game&&!dead&&!building&&!showHelp&&!CanRevivePartner()&&NearSlots(player.position);
        void ChangeSlotBet(int delta){if(!slotBusy)slotBet=Mathf.Clamp(slotBet+delta,1,Mathf.Max(1,Mathf.Min(100,supplies)));}
        bool TrySlotSpin(Vector3 position,int bet,bool alive)
        {
            if(!alive||coopLobby||slotBusy||!NearSlots(position)||bet<1||bet>100||supplies<bet)return false;
            supplies-=bet;slotStake=bet;slotMultiplier=SlotMultiplier(Random.Range(0,100));slotPayout=bet*slotMultiplier;
            slotTimer=2.4f;slotBusy=true;slotSerial++;coopSnapshotAt=0;PlayEffect(scrapSound,.45f);return true;
        }
        void SpinSlots()
        {
            if(!SlotControlsActive()||slotBusy)return;
            if(IsCoopGuest)SendCoopCommand("slots",type:slotBet);
            else if(!TrySlotSpin(player.position,slotBet,!dead))ShowBanner(slotBusy?"SPINNING …":"NOT ENOUGH SCRAP");
        }
        void UpdateSlots(float dt)
        {
            if(slotMachine==null)return;
            if(slotBusy){slotTimer=Mathf.Max(0,slotTimer-dt);if(!IsCoopGuest&&slotTimer<=0){supplies=Mathf.Min(1000000,supplies+slotPayout);slotBusy=false;coopSnapshotAt=0;ShowBanner(slotPayout>0?"SLOTS: "+slotPayout+" SCRAP WON":"SLOTS: NO WIN");PlayEffect(slotPayout>0?waveSound:scrapSound,.5f);}}
            if(slotVisualMaterial!=null)slotVisualMaterial.SetColor("_EmissionColor",Color.white*(slotBusy?1.4f+Mathf.Sin(slotTimer*22)*.8f:1.4f));
        }
        void DrawSlotsHud(float width,float height)
        {
            if(!SlotControlsActive())return;
            int target=SlotTarget(SlotPointerRay());if(target<0)return;
            string action=target==0?"Bet -1":target==1?"Bet +1":"Rotate";
            string message=slotBusy?"Spinning …":"Left click: "+action+" · Bet "+slotBet+" scrap · Q / E: - / +";
            HudLabel(new Rect(width/2-350,height*.65f,700,28),message,centerStyle,HudWhite);
        }
    }
}
