using TMPro;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public static partial class DepotModels
    {
        static TMP_FontAsset industrialFont;
        static TextMeshPro SharpText(Transform parent,string words,Vector3 position,Vector2 size,Color color,float fontSize,bool bold=true)
        {
            if(industrialFont==null)industrialFont=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            var obj=new GameObject(words);obj.transform.SetParent(parent,false);obj.transform.localPosition=position;
            var text=obj.AddComponent<TextMeshPro>();text.font=industrialFont;text.text=words;text.fontSize=fontSize;text.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;text.color=color;
            text.alignment=TextAlignmentOptions.Center;text.rectTransform.sizeDelta=size;text.enableAutoSizing=true;text.fontSizeMin=fontSize*.6f;text.fontSizeMax=fontSize;text.overflowMode=TextOverflowModes.Truncate;
            text.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return text;
        }
        static void LoadingSign(YardSurvival g,Transform parent,int bay)
        {
            var sign=Group("Framed loading bay sign "+bay,parent,new Vector3(0,4.9f,17.18f));
            Box("Enamel backing",sign,Vector3.zero,new Vector3(4.45f,1.15f,.13f),g.blueMaterial);
            foreach(float y in new[]{-.59f,.59f})Box("Rolled frame",sign,new Vector3(0,y,-.08f),new Vector3(4.6f,.07f,.12f),g.wallMaterial);
            foreach(float x in new[]{-2.26f,2.26f})Box("Rolled frame",sign,new Vector3(x,0,-.08f),new Vector3(.07f,1.2f,.12f),g.wallMaterial);
            Box("Bay number field",sign,new Vector3(-1.58f,0,-.08f),new Vector3(1.18f,.96f,.035f),g.yellowMaterial);
            SharpText(sign,bay.ToString("00"),new Vector3(-1.58f,-.04f,-.112f),new Vector2(1.03f,.9f),new Color(.10f,.15f,.14f),8);
            SharpText(sign,"LOADING BAY",new Vector3(.6f,.16f,-.105f),new Vector2(2.85f,.42f),new Color(.94f,.9f,.73f),3.7f);
            SharpText(sign,"D27  /  AUTHORIZED PERSONNEL",new Vector3(.6f,-.29f,-.106f),new Vector2(2.85f,.24f),new Color(.78f,.80f,.71f),1.8f,false);
            foreach(float x in new[]{-2.1f,2.1f})foreach(float y in new[]{-.46f,.46f})Box("Recessed sign fixing",sign,new Vector3(x,y,-.09f),Vector3.one*.045f,g.wallMaterial);
            Box("Sign light hood",sign,new Vector3(0,.78f,-.22f),new Vector3(2.3f,.13f,.35f),g.wallMaterial);
            var lamp=new GameObject("Bay sign wash").AddComponent<Light>();lamp.transform.SetParent(sign,false);lamp.transform.localPosition=new Vector3(0,.63f,-.6f);lamp.transform.localRotation=Quaternion.Euler(50,0,0);lamp.type=LightType.Point;lamp.range=3;lamp.intensity=.65f;lamp.color=new Color(.85f,.83f,.66f);lamp.shadows=LightShadows.None;
        }
        static void HighwaySign(YardSurvival g,Transform billboard)
        {
            foreach(float x in new[]{-4.1f,4.1f})Box("Billboard metal border",billboard,new Vector3(x,6,-.17f),new Vector3(.14f,3.5f,.22f),g.wallMaterial);
            foreach(float y in new[]{4.31f,7.69f})Box("Billboard metal border",billboard,new Vector3(0,y,-.17f),new Vector3(8.3f,.14f,.22f),g.wallMaterial);
            Box("Advert brand strip",billboard,new Vector3(0,4.7f,-.16f),new Vector3(7.85f,.65f,.03f),g.blueMaterial);
            SharpText(billboard,"A BETTER\nTOMORROW.",new Vector3(.4f,6.35f,-.19f),new Vector2(6.9f,2.0f),new Color(.17f,.23f,.21f),8);
            SharpText(billboard,"DUSTLINE INDUSTRIES   /   BUILT TO ENDURE",new Vector3(0,4.7f,-.19f),new Vector2(7.5f,.4f),new Color(.88f,.85f,.7f),2.5f);
            for(int i=0;i<18;i++){float x=-3.7f+i*.43f;Box("Peeling lower edge",billboard,new Vector3(x,4.43f,-.20f),new Vector3(.015f,.10f+(i%4)*.08f,.008f),g.woodMaterial);}
            Box("Maintenance catwalk",billboard,new Vector3(0,4.1f,-.5f),new Vector3(8.3f,.12f,.6f),g.wallMaterial);
        }
    }
}
