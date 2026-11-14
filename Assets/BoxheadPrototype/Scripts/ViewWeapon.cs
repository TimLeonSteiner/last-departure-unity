using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace YardSurvivalPrototype
{
    public sealed class ViewWeapon : MonoBehaviour
    {
        AnimatedWeaponRig rig;
        WeaponMuzzleFlash flash;
        Renderer[] renderers;
        float alignment;
        int id;
        public void Initialize(GameObject prefab,YardSurvival game,int weaponId)
        {
            id=weaponId;rig=Instantiate(prefab,transform).GetComponent<AnimatedWeaponRig>();
            if(rig==null)throw new System.InvalidOperationException("Animated weapon rig missing");
            if (id == 9)
            {
                // This rifle's stock occupies the eye line in an iron-sight pose.
                // Use a supported shoulder aim, retaining zoom and a centre reticle.
                rig.aimPosition = new Vector3(.12f, -.215f, .30f);
                rig.aimEuler = Vector3.zero;
                rig.calibratedSights = false;
            }
            foreach(var animator in rig.GetComponentsInChildren<Animator>())animator.enabled=false;
            foreach(var collider in rig.GetComponentsInChildren<Collider>())collider.enabled=false;
            flash=new GameObject("Muzzle flash").AddComponent<WeaponMuzzleFlash>();flash.transform.SetParent(rig.transform,false);flash.transform.localPosition=rig.muzzle;
            flash.Initialize();
            foreach(var renderer in GetComponentsInChildren<Renderer>(true)){renderer.gameObject.layer=30;renderer.renderingLayerMask=2;renderer.shadowCastingMode=ShadowCastingMode.Off;}
            var light=new GameObject("Viewmodel fill").AddComponent<Light>();light.transform.SetParent(transform,false);light.transform.localRotation=Quaternion.Euler(30,-25,0);light.type=LightType.Directional;light.intensity=1.2f;light.color=new Color(.88f,.91f,1);light.cullingMask=1<<30;light.shadows=LightShadows.None;light.GetUniversalAdditionalLightData().renderingLayers=2;
            renderers=GetComponentsInChildren<Renderer>(true);Tick(0,false,0,0);
        }
        public void SetReloadShellCount(int count){if(rig!=null)rig.reloadShellCount=Mathf.Max(1,count);}
        public void OnShot(){rig.Shot();if(!WeaponCatalog.IsMelee(id)&&id!=7)flash.Fire();}
        public bool HasReloadParts=>rig!=null&&(rig.reload!=null||WeaponCatalog.IsMelee(id));
        public bool HasModeledHands=>rig!=null&&rig.HasArms;
        public bool HasConnectedArms=>HasModeledHands;
        public void Tick(float progress,bool reloading,float recoil,float movement,float aim=0,float horizontal=0)
            => TickPose(progress,reloading,recoil,movement,aim,horizontal,Time.deltaTime);
#if UNITY_EDITOR
        public void ReviewPose(float progress,bool reloading,float recoil,float aim,float dt,float horizontal=0)
            => TickPose(progress,reloading,recoil,0,aim,horizontal,dt);
#endif
        void TickPose(float progress,bool reloading,float recoil,float movement,float aim,float horizontal,float dt)
        {
            if (!WeaponCatalog.CanAim(id)) aim = 0;
            alignment=Mathf.MoveTowards(alignment,reloading?0:aim,dt*8);
            rig.Pose(progress,reloading,dt,alignment);
            Vector3 aimPosition = rig.aimPosition;
            // Keep the Persuader receiver below the center of the view at full zoom.
            if (id == 14) aimPosition.y -= .035f;
            transform.localPosition=Vector3.Lerp(rig.hipPosition,aimPosition,alignment)+new Vector3(0,Mathf.Sin(Time.time*10)*movement*.005f*(1-alignment),-recoil*.02f);
            transform.localRotation=Quaternion.Slerp(Quaternion.identity,Quaternion.Euler(rig.aimEuler),alignment)*Quaternion.Euler(-recoil*Mathf.Lerp(3,1,alignment),horizontal*.7f,-horizontal*.6f);
            rig.FitAimShoulders(transform.parent,alignment,id);
            if(rig.muzzleAnchor!=null)flash.transform.position=rig.muzzleAnchor.TransformPoint(rig.muzzleAnchorOffset);
            bool scoped=WeaponCatalog.IsScoped(id)&&aim>=.9f&&!reloading;
            foreach(var renderer in renderers)renderer.enabled=!scoped;
            flash.Tick(dt,reloading||scoped||WeaponCatalog.IsMelee(id)||id==7);
        }
    }
}
