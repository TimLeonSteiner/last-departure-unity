using UnityEngine;

namespace YardSurvivalPrototype
{
    // Contact points are authored in the normalized model's space (+Z is the muzzle).
    public sealed class WeaponModelRig : MonoBehaviour
    {
        public Transform magazine, bolt, feedCover;
        public Vector3 rightWrist, supportWrist, muzzle, magazineWrist, boltWrist;
        public Vector3 sightPosition;
        public Vector3 magazineTravel = new Vector3(-.065f, -.24f, .06f);
        public Vector3 boltTravel = new Vector3(0, 0, -.045f);
        public Vector3 supportRotation = new Vector3(0, -35, 0);
        public bool shellLoading;
        public Vector3 loadingPort;
    }
}
