using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        Vector3 flashlightSwayPosition, flashlightSwayAngles;

        void LateUpdate()
        {
            if (HologramBlocking) return;
            RefreshDreamEnemies();
            if (flashlight == null) return;
            // The camera already carries the body bob. Add a small, softer hand motion
            // after UpdateFlashlight positions the beam, using the same footstep phase.
            float strength = !dead && !showHelp && !building
                ? walkBlend * Mathf.Lerp(1, .2f, aimBlend) : 0;
            float side = Mathf.Sin(walkPhase);
            float step = Mathf.Sin(walkPhase * 2);
            Vector3 targetPosition = new Vector3(side * .012f, step * .008f, 0) * strength;
            Vector3 targetAngles = new Vector3(step * .45f, side * .7f, -side * .35f) * strength;
            float smoothing = 1 - Mathf.Exp(-10 * Mathf.Min(Time.deltaTime, .05f));
            flashlightSwayPosition = Vector3.Lerp(flashlightSwayPosition, targetPosition, smoothing);
            flashlightSwayAngles = Vector3.Lerp(flashlightSwayAngles, targetAngles, smoothing);
            flashlight.transform.localPosition = new Vector3(.12f, -.08f, .04f) + flashlightSwayPosition;
            // Placement aiming keeps its existing world-space direction.
            if (!building) flashlight.transform.localRotation = Quaternion.Euler(flashlightSwayAngles);
        }
    }
}
