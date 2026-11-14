#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public void BeginMapInspection()
        {
            StopCoop();
            StopLaunchSounds();
            RestoreFrontEndEffects();
            ResetRun();
            frontScreen = FrontScreen.Game;
            if (gun != null) gun.gameObject.SetActive(false);
            if (viewmodelCamera != null) viewmodelCamera.enabled = false;
            if (ghost != null) ghost.gameObject.SetActive(false);
            arenaCamera.transform.SetParent(null, true);
            arenaCamera.transform.position = new Vector3(0, 3, -12);
            arenaCamera.transform.rotation = Quaternion.Euler(5, 0, 0);
            enabled = false;
            arenaCamera.gameObject.AddComponent<MapInspectionCamera>();
            Debug.Log("MAP_INSPECTION_READY: zombies=" + enemies.Count + "; waves disabled; free camera active");
        }
    }

    public sealed class MapInspectionCamera : MonoBehaviour
    {
        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            bool looking = mouse.rightButton.isPressed && Application.isFocused;
            Cursor.lockState = looking ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !looking;
            if (!Application.isFocused) return;
            if (looking)
            {
                var angles = transform.eulerAngles;
                float pitch = Mathf.DeltaAngle(0, angles.x);
                var delta = mouse.delta.ReadValue() * .12f;
                transform.rotation = Quaternion.Euler(Mathf.Clamp(pitch - delta.y, -89, 89), angles.y + delta.x, 0);
            }
            var move = transform.forward * ((keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0))
                + transform.right * ((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0))
                + Vector3.up * ((keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0));
            transform.position += Vector3.ClampMagnitude(move, 1) * (keyboard.leftShiftKey.isPressed ? 18 : 5) * Time.unscaledDeltaTime;
        }
        void OnGUI()
        {
            GUI.Box(new Rect(12, 12, 565, 52), "MAP INSPECTION — no zombies or waves\nWASD move • Q/E down/up • Shift faster • Hold right mouse to look • Stop Play to exit");
        }
        void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
#endif
