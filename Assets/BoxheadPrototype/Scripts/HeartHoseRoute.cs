using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed class HeartHoseRoute : MonoBehaviour
    {
        public Vector3 a,b,c,d,side;
        public float radiusScale=1;
        Renderer[] surfaces;
        MaterialPropertyBlock properties;
        SuspendedCore pulseSource;
        void OnEnable()
        {
            surfaces=GetComponentsInChildren<Renderer>(true);
            pulseSource=GetComponentInParent<SuspendedCore>();
            var block=properties=new MaterialPropertyBlock();
            block.SetVector("_HoseA",a);block.SetVector("_HoseB",b);
            block.SetVector("_HoseC",c);block.SetVector("_HoseD",d);
            block.SetVector("_HoseSide",side);block.SetFloat("_HoseScale",radiusScale);
            foreach(var renderer in surfaces)renderer.SetPropertyBlock(block);
        }
        void LateUpdate()
        {
            if(pulseSource==null)return;
            float time=pulseSource.PulseSampleTime;
            var pulse=new Vector4(pulseSource.EvaluatePulse(time),pulseSource.EvaluatePulse(time-.12f),pulseSource.EvaluatePulse(time-.24f),0);
            foreach(var renderer in surfaces)
            {
                renderer.GetPropertyBlock(properties);
                properties.SetVector("_TubePulse",pulse);
                renderer.SetPropertyBlock(properties);
            }
        }
    }
}
