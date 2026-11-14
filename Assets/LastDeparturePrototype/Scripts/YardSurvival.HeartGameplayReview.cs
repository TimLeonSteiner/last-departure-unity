#if UNITY_EDITOR
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        public void PrepareHeartGameplayReview(int view)
        {
            enabled=true;
            if(view==0)PlayFromMenu();
            player.position=(view==0?new Vector3(-2.5f,.65f,-7):new Vector3(5,.65f,-5));
            Vector3 direction=new Vector3(0,3.35f,0)-(player.position+Vector3.up);
            yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
            pitch=-Mathf.Asin(direction.normalized.y)*Mathf.Rad2Deg;
            player.rotation=Quaternion.Euler(0,yaw,0);
            arenaCamera.transform.localPosition=Vector3.up;
            arenaCamera.transform.localRotation=Quaternion.Euler(pitch,0,0);
            Time.timeScale=0;
            ReleaseMouse();
        }
        public void EndHeartGameplayReview(){Time.timeScale=1;PauseFromGame();}
    }
}
#endif
