using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed class DefenseConstruction:MonoBehaviour
    {public int type,hits,paid;public static readonly int[] RequiredHits={3,6,4};public bool Ready=>hits>=RequiredHits[type];}
    public sealed class DefenseAppearanceState:MonoBehaviour
    {
        public float health=float.NaN,progress=-1;
        public MaterialPropertyBlock block;
    }
    public sealed partial class YardSurvival
    {
        public GameObject hammerToolModel;
        AnimatedWeaponRig hammerRig;
        float hammerAge=1;
        int hammerTarget;
        bool hammerContact;
        static bool ConstructionReady(Transform body)=>body.GetComponent<DefenseConstruction>()==null||body.GetComponent<DefenseConstruction>().Ready;
        void InitializeConstruction(Transform body,int type)
        {var c=body.gameObject.AddComponent<DefenseConstruction>();c.type=type;c.hits=DefenseConstruction.RequiredHits[type];c.paid=DefenseCosts[type];supplies-=c.paid;UpdateConstructionAppearance(body,type==0?100:type==1?150:80);}
        void SetupHammer()
        {
            if(hammerToolModel==null)return;
            hammerRig=Instantiate(hammerToolModel,arenaCamera.transform).GetComponent<AnimatedWeaponRig>();
            if(hammerRig==null)return;
            foreach(var renderer in hammerRig.GetComponentsInChildren<Renderer>(true)){renderer.gameObject.layer=30;renderer.renderingLayerMask=2;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
            foreach(var shape in hammerRig.GetComponentsInChildren<Collider>())shape.enabled=false;
            foreach(var animator in hammerRig.GetComponentsInChildren<Animator>())animator.enabled=false;
            hammerAge=1;hammerTarget=0;hammerRig.gameObject.SetActive(false);
        }
        Transform DefenseById(int id,out int type,out float hp)
        {
            foreach(var b in barricades)if(b.netId==id){type=0;hp=b.hp;return b.body;}
            foreach(var b in turrets)if(b.netId==id){type=1;hp=b.hp;return b.body;}
            foreach(var b in traps)if(b.netId==id){type=2;hp=b.hp;return b.body;}
            type=-1;hp=0;return null;
        }
        int HammerRayTarget(Ray ray)
        {
            int best=0;float distance=4;
            foreach(var b in barricades){float d=RayVolume(ray.origin,ray.direction,b.bounds,1.2f,GroundHeight(b.body.position));if(d<distance){best=b.netId;distance=d;}}
            foreach(var b in turrets){float d=RayVolume(ray.origin,ray.direction,b.bounds,1.5f,GroundHeight(b.body.position));if(d<distance){best=b.netId;distance=d;}}
            foreach(var b in traps){float d=RayVolume(ray.origin,ray.direction,DefenseBounds(b.position,2,false),.35f,GroundHeight(b.position));if(d<distance){best=b.netId;distance=d;}}
            return best!=0&&ShotStopDistance(ray.origin,ray.direction)+.06f>=distance?best:0;
        }
        void StartHammer(Ray ray, bool allowPlacement = true)
        {
            if(hammerAge<.55f)return;
            if(selectedDefense==3){if(allowPlacement&&buildRayValid&&TryPlaceDefense(buildPoint,3,false)){hammerAge=0;hammerContact=true;hammerTarget=0;}return;}
            int target=HammerRayTarget(ray);
            if(target==0&&allowPlacement&&buildRayValid&&TryPlaceDefense(buildPoint,selectedDefense,rotated))
            {hammerAge=0;hammerContact=true;hammerTarget=0;return;}
            if(target==0)return;
            var body=DefenseById(target,out int type,out float hp);
            if(body==null)return;
            string unavailable=HammerUnavailable(body,type,hp,player.position);
            if(unavailable!=null){if(allowPlacement)ShowBanner(unavailable);return;}
            hammerTarget=target;hammerAge=0;hammerContact=false;hammerRig?.Shot();
        }
        static float DefenseMaxHealth(int type)=>type==0?100:type==1?150:80;
        static int NextHammerCost(DefenseConstruction state,int type)
        {
            if(state==null||state.Ready)return 1;
            int wanted=Mathf.CeilToInt((state.hits+1)*DefenseCosts[type]/(float)DefenseConstruction.RequiredHits[type]);
            return Mathf.Max(0,wanted-state.paid);
        }
        string HammerUnavailable(Transform body,int type,float hp,Vector3 collector)
        {
            Vector3 target=body.position;target.y=GroundHeight(target)+(type==2?.2f:.8f);
            if(Vector3.Distance(collector+Vector3.up*.7f,target)>3.2f)return "Move closer to build or repair \u00b7 Range 3 m";
            var state=body.GetComponent<DefenseConstruction>();
            if((state==null||state.Ready)&&hp>=DefenseMaxHealth(type))return "Fully repaired";
            if(supplies<NextHammerCost(state,type))return "Not enough scrap \u00b7 Need 1 scrap";
            return null;
        }
        void TickHammer(float dt)
        {
            if(hammerRig!=null)
            {
                hammerRig.gameObject.SetActive(building&&selectedDefense!=3&&!dead&&frontScreen==FrontScreen.Game);
                hammerRig.transform.localPosition=hammerRig.hipPosition;hammerRig.transform.localRotation=Quaternion.identity;
                if(hammerRig.gameObject.activeSelf)hammerRig.Pose(0,false,dt);
            }
            if(gun!=null)gun.gameObject.SetActive(HasEquippedWeapon&&!building&&!dead&&frontScreen==FrontScreen.Game);
            hammerAge+=dt;
            if(!hammerContact&&hammerAge>=.18f)
            {
                hammerContact=true;
                if(!building||dead||showHelp||HologramBlocking)return;
                if(IsCoopGuest)SendCoopCommand("hammer",hammerTarget);
                else ApplyHammerHit(hammerTarget,player.position);
            }
            foreach(var b in barricades)UpdateConstructionAppearance(b.body,b.hp);
            foreach(var b in turrets)UpdateConstructionAppearance(b.body,b.hp);
            foreach(var b in traps)UpdateConstructionAppearance(b.body,b.hp);
        }
        public bool ApplyHammerHit(int id,Vector3 collector)
        {
            Transform body=DefenseById(id,out int type,out float hp);if(body==null||hp<=0)return false;
            Vector3 target=body.position;target.y=GroundHeight(target)+(type==2?.2f:.8f);
            Vector3 origin=collector+Vector3.up*.7f,delta=target-origin;
            if(delta.magnitude>3.2f)return false;
            // Stop at the target's near face; another wall or cover cannot be crossed.
            float near=type==0?RayVolume(origin,delta.normalized,barricades.Find(b=>b.netId==id).bounds,1.2f):type==1?RayVolume(origin,delta.normalized,turrets.Find(b=>b.netId==id).bounds,1.5f):delta.magnitude;
            if(ShotStopDistance(origin,delta.normalized)+.08f<near)return false;
            var state=body.GetComponent<DefenseConstruction>();
            if(state!=null&&!state.Ready)
            {
                int next=state.hits+1;
                int price=NextHammerCost(state,type);if(supplies<price){ShowBanner("Not enough scrap for the next hammer strike.");return false;}
                supplies-=price;state.paid+=price;state.hits=next;
                if(state.Ready)ShowBanner("Construction complete.");
            }
            else
            {
                float max=DefenseMaxHealth(type);if(hp>=max||supplies<=0)return false;
                supplies--;float repaired=Mathf.Min(max,hp+25);
                if(type==0)barricades.Find(b=>b.netId==id).hp=repaired;else if(type==1)turrets.Find(b=>b.netId==id).hp=repaired;else traps.Find(b=>b.netId==id).hp=repaired;
                hp=repaired;
            }
            PlayEffect(scrapSound,.45f);UpdateConstructionAppearance(body,hp);return true;
        }
        void UpdateConstructionAppearance(Transform body,float hp)
        {
            if(body==null)return;var state=body.GetComponent<DefenseConstruction>();int type=state!=null?state.type:body.name.Contains("Turret")?1:0;
            float progress=state==null?1:Mathf.Clamp01(state.hits/(float)DefenseConstruction.RequiredHits[type]);float max=type==0?100:type==1?150:80;
            var cached=body.GetComponent<DefenseAppearanceState>();
            if(cached==null)cached=body.gameObject.AddComponent<DefenseAppearanceState>();
            if(cached.health==hp&&cached.progress==progress)return;
            cached.health=hp;cached.progress=progress;
            body.GetComponent<EntranceBoards>()?.SetHealth(hp);
            var block=cached.block??(cached.block=new MaterialPropertyBlock());
            foreach(var renderer in body.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)if(materials[i]!=null)
                {Color color=materials[i].HasProperty("_BaseColor")?materials[i].GetColor("_BaseColor"):Color.white;color=Color.Lerp(new Color(.24f,.29f,.31f),color,Mathf.Lerp(.3f,1,progress));color=Color.Lerp(new Color(.3f,.13f,.09f),color,Mathf.Clamp01(hp/max));block.Clear();block.SetColor("_BaseColor",color);renderer.SetPropertyBlock(block,i);}
            }
        }
        void DrawConstructionHud(float width,float height)
        {
            if(!building||dead||showHelp)return;
            DrawBuildSelectionBar(width, height);
            if(selectedDefense==3){HudLabel(new Rect(width/2-320,height-108,640,28),ControlHint(Control.Fire) + " Place explosive barrel / 2 scrap",centerStyle,HudWhite);return;}
            int target=HammerRayTarget(new Ray(arenaCamera.transform.position,arenaCamera.transform.forward));
            var body=DefenseById(target,out int type,out float hp);
            if(body!=null)
            {
                var state=body.GetComponent<DefenseConstruction>();
                bool unfinished=state!=null&&!state.Ready;
                float maximum=DefenseMaxHealth(type);
                string name=type==0?"BARRICADE":type==1?"TURRET":"SLOW TRAP";
                string status=unfinished?""+state.hits+" / "+DefenseConstruction.RequiredHits[type]:Mathf.CeilToInt(hp)+" / "+maximum+" HP";
                string action=HammerUnavailable(body,type,hp,player.position)??
                    (unfinished?ControlHint(Control.Fire)+" Hold to build":ControlHint(Control.Fire)+" Hold to repair \u00b7 1 scrap");
                float x=width/2-180,y=height/2+55;
                Fill(new Rect(x-12,y-5,384,83),new Color(.025f,.03f,.035f,.88f));
                HudLabel(new Rect(x,y,360,25),name+" \u00b7 "+status,centerStyle,HudWhite);
                Fill(new Rect(x,y+31,360,4),new Color(.2f,.23f,.24f));
                float progress=unfinished?state.hits/(float)DefenseConstruction.RequiredHits[type]:hp/maximum;
                Fill(new Rect(x,y+31,360*Mathf.Clamp01(progress),4),hp<maximum*.3f?HudRed:HudWhite);
                HudLabel(new Rect(x,y+43,360,25),action,centerStyle,HudWhite);
            }
            else
            {
                string issue=buildRayValid?DefensePlacementIssue(buildPoint,selectedDefense,rotated):"Aim at nearby ground to place a defense";
                string name=selectedDefense==0?"BARRICADE":selectedDefense==1?"TURRET":"SLOW TRAP";
                HudLabel(new Rect(width/2-370,height-108,740,28),issue??
                    ControlHint(Control.Fire) + " Place " + name.ToLowerInvariant() + (selectedDefense == 0 ? " / " + ControlHint(Control.Rotate) + " Rotate" : ""), centerStyle, issue == null ? HudWhite : new Color(.85f,.73f,.43f));
            }
        }
        // Preserve defense IDs for existing saves and co-op messages.
        static readonly int[] AvailableDefenses = { 0, 1, 3 };
        static readonly string[] BuildNames = { "BARRICADE", "TURRET", "SLOW TRAP", "EXPLOSIVE BARREL" };
        static readonly Control[] BuildKeys = { Control.Barricade, Control.Turret, Control.Trap, Control.Barrel };
        static readonly Color BuildAccent = new Color(.35f, .82f, 1f);
        Rect BuildChoiceCard(int index,float width,float height)
        {
            float total=Mathf.Min(540,width-48), cell=total/3;
            return new Rect((width-total)/2+index*cell,height-218,cell-8,108);
        }
        void DrawBuildSelectionBar(float width, float height)
        {
            EnsureDreadHud();
            for(int i=0;i<AvailableDefenses.Length;i++)
            {
                int type=AvailableDefenses[i];bool selected=selectedDefense==type;
                Rect card=BuildChoiceCard(i,width,height);
                Fill(card,new Color(.025f,.03f,.035f,.88f));
                DreadFrame(card,selected?HudRed:DreadRule);
                if(selected)Fill(new Rect(card.x,card.yMax-3,card.width,3),HudRed);
                var badge=new Rect(card.x+8,card.y+8,25,25);
                Fill(badge,selected?HudRed:new Color(.13f,.15f,.16f));
                DreadLabel(badge,BindingName(bindings[(int)BuildKeys[type]].primary),dreadCaption,HudWhite,TextAnchor.MiddleCenter);
                DrawBuildIcon(new Rect(card.x+32,card.y+5,card.width-43,59),type,HudWhite);
                DreadLabel(new Rect(card.x+9,card.y+66,card.width-18,20),BuildNames[type],dreadSmall,HudWhite);
                DreadLabel(new Rect(card.x+9,card.y+85,card.width-18,18),DefenseCosts[type]+" SCRAP",dreadSmall,supplies>=DefenseCosts[type]?(selected?HudRed:DreadMuted):HudRed);
                if(Cursor.lockState!=CursorLockMode.Locked&&GUI.Button(card,GUIContent.none,GUIStyle.none))SelectDefense(type);
            }
            DreadLabel(new Rect(width/2-260,height-29,520,20),ControlHint(Control.EquipHammer)+" HAMMER / WEAPON    "+ControlHint(Control.Hologram)+" MENU",dreadSmall,DreadMuted,TextAnchor.MiddleCenter);
        }

        static readonly string[] BuildImageNames = { "Barricade", "Turret", "Trap", "Barrel" };
        readonly Texture2D[] buildImages = new Texture2D[4];
        Texture2D BuildImage(int type)
        {
            if (type < 0 || type >= buildImages.Length) return null;
            if (buildImages[type] == null) buildImages[type] = Resources.Load<Texture2D>("HudBuild/" + BuildImageNames[type]);
            return buildImages[type];
        }
        void DrawBuildIcon(Rect rect, int type, Color color)
        {
            var image = BuildImage(type);
            if (image == null) return;
            Color previous = GUI.color; GUI.color = Color.white;
            GUI.DrawTexture(rect, image, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }
        void DrawConstructionLabel(Transform body,float hp,float max,float width,float height)
        {
            if(Vector3.Distance(player.position,body.position)>8)return;
            var p=arenaCamera.WorldToViewportPoint(body.position+Vector3.up);if(p.z<=0||p.x<0||p.x>1||p.y<0||p.y>1)return;
            var c=body.GetComponent<DefenseConstruction>();string label=c!=null&&!c.Ready?"BUILD "+c.hits+" / "+DefenseConstruction.RequiredHits[c.type]:Mathf.CeilToInt(hp)+" / "+max+" HP";
            HudLabel(new Rect(p.x*width-80,(1-p.y)*height-25,160,25),label,centerStyle,HudWhite);
        }
    }
}
