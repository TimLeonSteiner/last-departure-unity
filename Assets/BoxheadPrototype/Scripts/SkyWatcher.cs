using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed class SkyWatcher : MonoBehaviour
    {
        SkinnedMeshRenderer[] lids;
        Transform eyeball; Quaternion eyeRest;

        public static SkyWatcher Build(Transform world)
        {
            var existing=world.Find("Sky watcher 3D");
            if(existing!=null)return existing.GetComponent<SkyWatcher>();
            var prefab=Resources.Load<GameObject>("SkyWatcher/SkyWatcherRig");
            if(prefab==null)return null;
            var obj=Instantiate(prefab,world,false);obj.name="Sky watcher 3D";
            obj.transform.localPosition=new Vector3(0,200,12);
            // Imported FBX keeps its axis/unit conversion beneath a world-space wrapper.
            obj.transform.localRotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
            obj.transform.localScale=Vector3.one*105;
            return obj.AddComponent<SkyWatcher>();
        }
        void Awake()
        {
            lids=GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach(var child in GetComponentsInChildren<Transform>())if(child.name=="Eyeball"){eyeball=child;eyeRest=child.localRotation;}
            Shader.SetGlobalTexture("_WatcherSkyPlate",Resources.Load<Texture2D>("SkyWatcher/StormSky"));
            Shader.SetGlobalFloat("_Watcher3DActive",1);
        }
        void Update(){Pose(Time.time);}
        public void Pose(float time)
        {
            float cycle=Mathf.Repeat(time+3,13);
            float close=Mathf.SmoothStep(0,1,Mathf.Clamp01(cycle/.55f));
            float open=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((cycle-.65f)/.85f));
            float blink=close*open;
            if(eyeball!=null)eyeball.localRotation=eyeRest*Quaternion.Euler(Mathf.Sin(time*.23f)*2,0,Mathf.Sin(time*.2f)*5);
            foreach(var skin in lids)
                for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)
                {
                    string name=skin.sharedMesh.GetBlendShapeName(i);
                    if(name.Contains("Blink"))skin.SetBlendShapeWeight(i,blink*100);
                    if(name.Contains("LookHorizontal"))skin.SetBlendShapeWeight(i,(Mathf.Sin(time*.31f)*.8f+Mathf.Sin(time*.83f)*.2f)*(1-blink)*100);
                    if(name.Contains("LookVertical"))skin.SetBlendShapeWeight(i,Mathf.Sin(time*.23f)*45*(1-blink));
                }
        }
        void OnDestroy(){Shader.SetGlobalFloat("_Watcher3DActive",0);}
    }
}






