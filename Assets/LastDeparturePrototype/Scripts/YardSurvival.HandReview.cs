#if UNITY_EDITOR
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public void PrepareHandReview(int id, float progress, bool reload, bool firing = false, bool ads = false)
        {
            // A fresh skin forces Unity to upload the reviewed bone pose even when
            // several cameras are rendered synchronously within one editor frame.
            if (gun != null) { Object.DestroyImmediate(gun.gameObject); gun = null; }
            EquipWeapon(id, WeaponCatalog.All[id].capacity);
            if (!viewWeapon.HasModeledHands || !viewWeapon.HasReloadParts) throw new System.Exception("Missing modeled hands or reload parts");
            arenaCamera.fieldOfView = ads ? 58 : 75;
            if (firing) viewWeapon.OnShot();
            viewWeapon.Tick(progress, reload, firing ? .16f : 0, 0, ads ? 1 : 0);
            if (!viewWeapon.HasConnectedArms) throw new System.Exception("Arm chain separated during hand review: weapon=" + id + " progress=" + progress);
        }
    }
}
#endif
