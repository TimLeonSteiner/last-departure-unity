using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        Texture2D deathBloodTexture, deathVeilTexture;
        Texture2D[] deathDripTextures;
        GUIStyle deathHeadingStyle;
        bool deathBloodPrepared;
        // Position, start height and full length, in viewport coordinates. Keep the result column clear.
        static readonly Vector3[] DeathDrips = {
            new Vector3(.055f,.08f,.57f), new Vector3(.14f,0,.38f),
            new Vector3(.89f,.05f,.53f), new Vector3(.966f,.23f,.61f)
        };
        void PrepareDeathBlood()
        {
            if(deathBloodPrepared)return;
            deathBloodPrepared=true;
            deathBloodTexture=Resources.Load<Texture2D>("DeathScreen/EdgeBlood");
            deathVeilTexture=Resources.Load<Texture2D>("DeathScreen/DeathVeil");
            deathDripTextures=new Texture2D[DeathDrips.Length];
            for(int i=0;i<deathDripTextures.Length;i++)
                deathDripTextures[i]=Resources.Load<Texture2D>("DeathScreen/Drip"+i);
        }
        void DrawDeathBlood(float width,float height)
        {
            PrepareDeathBlood();
            Color previous=GUI.color;
            float settle=Mathf.SmoothStep(0,1,Mathf.Clamp01(deathTime/1.1f));
            float impact=Mathf.SmoothStep(0,1,Mathf.Clamp01((deathTime-.08f)/.42f));
            // Being downed is distinct from a finished run: the world remains more visible.
            bool downed=PartnerCanContinue;
            float strength=downed?.48f:1f;
            if(!downed)impact*=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((deathTime-.65f)/.55f));
            try
            {
                GUI.color=new Color(previous.r,previous.g,previous.b,previous.a*settle*strength);
                if(deathVeilTexture!=null)GUI.DrawTexture(new Rect(0,0,width,height),deathVeilTexture,ScaleMode.StretchToFill,true);
                GUI.color=new Color(previous.r,previous.g,previous.b,previous.a*impact*strength);
                if(deathBloodTexture!=null)GUI.DrawTexture(new Rect(0,0,width,height),deathBloodTexture,ScaleMode.StretchToFill,true);
                for(int i=0;i<DeathDrips.Length;i++)
                {
                    if(deathDripTextures[i]==null)continue;
                    float elapsed=Mathf.Max(0,deathTime-.35f-i*.31f);
                    float t=Mathf.Clamp01(elapsed/(3.8f+i*.7f));
                    float reveal=1-Mathf.Pow(1-t,2);
                    if(reveal<=0)continue;
                    Vector3 drip=DeathDrips[i];
                    float fullHeight=height*drip.z,dripWidth=fullHeight*.25f;
                    // Crop from the top at a fixed scale. Never stretch the entire drip as it grows.
                    GUI.DrawTextureWithTexCoords(new Rect(width*drip.x-dripWidth*.5f,height*drip.y,dripWidth,fullHeight*reveal),
                        deathDripTextures[i],new Rect(0,1-reveal,1,reveal),true);
                }
            }
            finally{GUI.color=previous;}
        }
        void DrawDeathHeading(float width,float height)
        {
            EnsureFrontTypography();
            if(deathHeadingStyle==null)deathHeadingStyle=new GUIStyle(pauseHeading){alignment=TextAnchor.MiddleCenter};
            deathHeadingStyle.fontSize=PartnerCanContinue?32:64;
            float fade=Mathf.SmoothStep(0,1,Mathf.Clamp01((deathTime-.8f)/.65f));
            string title=PartnerCanContinue?"YOUR PARTNER CAN REVIVE YOU":coreLost?"CORE LOST":"YOU DIED";
            HudLabel(new Rect(20,height*.34f+(1-fade)*8,width-40,100),title,deathHeadingStyle,
                new Color(.88f,.84f,.81f,fade));
        }
    }
}
