using UnityEngine;
using UnityEngine.Rendering;

namespace YardSurvivalPrototype
{
    // Small six-tablet foil pack, built from opaque meshes to stay readable at low quality.
    public sealed class CaffeinePickupVisual : MonoBehaviour
    {
        Material foil, paper, tablet, ink;
        static GameObject originalModel;
        public static GameObject Create(Transform parent)
        {
            if(originalModel==null)originalModel=Resources.Load<GameObject>("OriginalPickups/Caffeine");
            if(originalModel!=null)return Instantiate(originalModel,parent,false);
            var root=new GameObject("Caffeine tablets");root.transform.SetParent(parent,false);
            root.AddComponent<CaffeinePickupVisual>().Build();return root;
        }
        Material Surface(string name,Color color,float metal,float smooth)
        {
            var material=new Material(RuntimeShaders.Find("Universal Render Pipeline/Lit"));
            material.name=name;material.SetColor("_BaseColor",color);
            material.SetFloat("_Metallic",metal);material.SetFloat("_Smoothness",smooth);return material;
        }
        void Part(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var part=GameObject.CreatePrimitive(type);part.name=name;part.transform.SetParent(transform,false);
            part.transform.localPosition=position;part.transform.localScale=scale;
            var collider=part.GetComponent<Collider>();collider.enabled=false;Release(collider);
            var renderer=part.GetComponent<Renderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        void Build()
        {
            foil=Surface("Brushed silver foil",new Color(.63f,.68f,.71f),.65f,.3f);
            paper=Surface("Caffeine teal label",new Color(.025f,.20f,.19f),0,.25f);
            tablet=Surface("Chalk white tablet",new Color(.88f,.88f,.79f),0,.16f);
            ink=Surface("Tablet score",new Color(.35f,.39f,.36f),0,.1f);
            Part("Foil backing",PrimitiveType.Cube,Vector3.zero,new Vector3(.24f,.008f,.34f),foil);
            Part("Label strip",PrimitiveType.Cube,new Vector3(0,.005f,.125f),new Vector3(.222f,.003f,.068f),paper);
            for(int row=0;row<3;row++)for(int column=0;column<2;column++)
            {
                float x=column==0?-.057f:.057f,z=.049f-row*.075f;
                Part("Pressed foil pocket",PrimitiveType.Sphere,new Vector3(x,.006f,z),new Vector3(.09f,.014f,.066f),foil);
                Part("Caffeine tablet",PrimitiveType.Sphere,new Vector3(x,.017f,z),new Vector3(.067f,.021f,.048f),tablet);
                Part("Tablet score line",PrimitiveType.Cube,new Vector3(x,.027f,z),new Vector3(.002f,.001f,.032f),ink);
            }
            var label=new GameObject("Caffeine label");label.transform.SetParent(transform,false);
            label.transform.localPosition=new Vector3(0,.008f,.126f);label.transform.localRotation=Quaternion.Euler(90,0,0);
            var text=label.AddComponent<TextMesh>();text.text="CAFFEINE";text.fontSize=48;text.characterSize=.0085f;
            text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color(.93f,.94f,.87f);
        }
        static void Release(Object value) { if(Application.isPlaying)Destroy(value);else DestroyImmediate(value); }
        void OnDestroy()
        {
            foreach(var material in new[]{foil,paper,tablet,ink})if(material!=null)Release(material);
        }
    }
}
