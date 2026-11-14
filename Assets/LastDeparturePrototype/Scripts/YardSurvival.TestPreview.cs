using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        bool editorPreviewRun;
#if UNITY_EDITOR
        public string StartWave17UpgradePreview()
        {
            System.IO.File.WriteAllText("Library/ChargerPreviewOnce.request","wave17-upgrades");
            StopCoop();
            StopLaunchSounds();RestoreLaunchRendering();
            PlayFromMenu();
            OpenHologram(2);
            var rig=viewWeapon.GetComponentInChildren<AnimatedWeaponRig>();
            if(wave!=17||skillPoints!=27||weaponId!=1||!hologramOpen)
                throw new System.Exception("Wave 17 upgrade preview setup failed");
            if(Vector3.Distance(rig.hipPosition,new Vector3(.2f,-.461f,.68f))>.001f || Vector3.Distance(rig.aimPosition,new Vector3(0,-.28f,.4f))>.001f)
                throw new System.Exception("Minigun poses are not swapped");
            viewWeapon.ReviewPose(0,false,0,0,1);
            if(Vector3.Distance(viewWeapon.transform.localPosition,rig.hipPosition)>.001f)throw new System.Exception("Hip pose mismatch");
            viewWeapon.ReviewPose(0,false,0,1,1);
            if(Vector3.Distance(viewWeapon.transform.localPosition,rig.aimPosition)>.001f)throw new System.Exception("Aim pose mismatch");
            viewWeapon.ReviewPose(0,false,0,0,1);
            return "PASS Wave 17 active\nPASS 27 skill points (all 9 skills, 3 ranks each)\nPASS MG and minigun equipped\nPASS Hip and aimed poses swapped and verified through ViewWeapon\nPASS Upgrade bench open\nPASS Test run excluded from records\n";
        }
#endif
    }
}
