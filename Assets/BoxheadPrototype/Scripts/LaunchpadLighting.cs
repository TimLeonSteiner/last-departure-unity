using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed class LaunchpadLighting : MonoBehaviour
    {
        void Spot(string name,Vector3 position,Vector3 target,Color color,float intensity,float range,float angle,bool shadows=true)
        {
            var obj=new GameObject(name);obj.transform.SetParent(transform,false);obj.transform.localPosition=position;
            obj.transform.localRotation=Quaternion.LookRotation(target-position);
            var light=obj.AddComponent<Light>();light.type=LightType.Spot;light.color=color;light.intensity=intensity;
            light.range=range;light.spotAngle=angle;light.innerSpotAngle=angle*.55f;
            light.shadows=shadows?LightShadows.Soft:LightShadows.None;light.shadowBias=.02f;light.shadowNormalBias=.12f;light.shadowCustomResolution=256;
            light.cullingMask=~(1<<30);
        }
        void Awake()
        {
            Spot("Launch white flood",new Vector3(3,11,86),new Vector3(13,15,102),new Color(.64f,.76f,1),1800,56,90,true);
            Spot("Launch amber rim",new Vector3(26,17,92),new Vector3(13,17,102),new Color(1,.55f,.22f),1600,48,100);
            Spot("Launch rear separation",new Vector3(9,5,115),new Vector3(13,13,102),new Color(.52f,.68f,1),1200,45,100);
            for(int side=-1;side<=1;side+=2)
            {
                // Match the authored foreground fixtures, not approximate fill-light positions.
                foreach(int z in new[]{34,54,74,94})
                    Spot("Apron work light",new Vector3(side*7.6f,6.65f,z),new Vector3(side*7.6f,0,z+1),new Color(1,.65f,.32f),100,14,95);
                for(int row=0;row<4;row++)
                {
                    float x=side*(39+row%2*13),z=78+row*39;
                    foreach(int dz in new[]{-5,0,5})
                        Spot("Hangar door practical",new Vector3(x-side*9.42f,6.73f,z+dz),new Vector3(x-side*10.5f,0,z+dz),new Color(1,.65f,.32f),95,11,80);
                    Spot("Hangar apron mast",new Vector3(side*17,11.72f,z-10),new Vector3(side*17,0,z-10),new Color(1,.65f,.32f),210,22,100);
                }
            }
            gameObject.AddComponent<ExteriorLightBudget>();
        }
    }
}
