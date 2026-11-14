using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed class ExteriorFixture : MonoBehaviour
    {
        public Light Lamp { get; private set; }
        public float Strength { get; private set; }
        Renderer lens;
        float peak, phase;
        bool failing;
        Color tint;
        MaterialPropertyBlock properties;
        public void Configure(Light lamp,bool intermittent,float offset)
        {
            properties=new MaterialPropertyBlock();
            Lamp=lamp;peak=lamp.intensity;failing=intermittent;phase=offset;
            lens=GetComponent<Renderer>();tint=lamp.color;
            var collider=GetComponent<Collider>();if(collider!=null){collider.enabled=false;Destroy(collider);}
            ApplyStrength(1);
        }
        public void ApplyStrength(float strength)
        {
            if(Lamp==null)return;
            if(lens==null)lens=GetComponent<Renderer>();
            if(properties==null)properties=new MaterialPropertyBlock();
            Strength=strength;Lamp.intensity=peak*strength;
            if(lens==null)return;
            lens.GetPropertyBlock(properties);properties.SetColor("_BaseColor",tint*Mathf.Lerp(.08f,1,strength));
            properties.SetFloat("_Glow",1.8f*strength);lens.SetPropertyBlock(properties);
        }
        void Update()
        {
            if(!failing||Lamp==null)return;
            float t=Mathf.Repeat(Time.time+phase,17.3f);
            ApplyStrength(t<.09f||t>.23f&&t<.35f?.08f:t<.55f?.55f:1);
        }
    }
}
