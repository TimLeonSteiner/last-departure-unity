using System.Collections.Generic;
using UnityEngine;
namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        readonly Dictionary<Enemy,float> screamerPings = new Dictionary<Enemy,float>();
        readonly List<Enemy> expiredScreamerPings = new List<Enemy>();
        AudioSource screamerCall;
        Enemy audibleScreamer;
        AudioClip screamerCallClip;
        Texture2D screamerRing, screamerGlow;
        GUIStyle screamerPingStyle, screamerAlertStyle;
        void PlayScreamerCall(Enemy enemy)
        {
            screamerPings[enemy]=Time.time;
            if(screamerCallClip==null)screamerCallClip=Resources.Load<AudioClip>("EnemyFeedback/ScreamerCall");
            if (arenaCamera != null && (arenaCamera.transform.position - enemy.position).sqrMagnitude <= MonsterVoiceRange * MonsterVoiceRange)
                enemy.screamReactionAt = Time.time + (screamerCallClip != null ? screamerCallClip.length : 3f) + .25f;
            if(!soundEnabled || !audioUnlocked)return;
            if(screamerCall==null)
            {
                var obj=new GameObject("Screamer warning audio");obj.transform.SetParent(transform,false);
                screamerCall=obj.AddComponent<AudioSource>();screamerCall.playOnAwake=false;
                screamerCall.spatialBlend=.55f;screamerCall.minDistance=16;screamerCall.maxDistance=45;
                screamerCall.rolloffMode=AudioRolloffMode.Linear;screamerCall.dopplerLevel=0;screamerCall.priority=20;
            }
            if(screamerCallClip==null)screamerCallClip=Resources.Load<AudioClip>("EnemyFeedback/ScreamerCall");
            screamerCall.transform.position=enemy.position+Vector3.up*1.5f;
            audibleScreamer = enemy;
            screamerCall.clip=screamerCallClip;screamerCall.volume=soundVolume;screamerCall.Play();
        }
        void DrawScreamerPings(float width,float height)
        {
            if (screamerCall != null)
            {
                screamerCall.volume = soundEnabled ? soundVolume : 0;
                if (audibleScreamer != null && !IsScreaming(audibleScreamer))
                    screamerCall.Stop();
            }
            expiredScreamerPings.Clear();
            foreach(var entry in screamerPings)
            {
                float age=Time.time-entry.Value;
                if(age>4 || entry.Key.body==null || entry.Key.hp<=0){expiredScreamerPings.Add(entry.Key);continue;}
                bool screaming = IsScreaming(entry.Key);
                bool windingUp = entry.Key.specialAttack == EnemySpecial.Scream &&
                    entry.Key.attackTime >= 0 && entry.Key.attackTime < ScreamRelease;
                Vector3 screen=arenaCamera.WorldToViewportPoint(entry.Key.body.position+Vector3.up*2.6f);
                if(screen.z<=0){screen.x=screen.x<.5f?1:0;screen.y=.5f;}
                float x=Mathf.Clamp(screen.x*width,90,width-90),y=Mathf.Clamp((1-screen.y)*height,110,height-100);
                float pulse=Mathf.Repeat(age*1.8f,1);
                Color color=new Color(1,.35f,.16f,screaming ? 1 : Mathf.Clamp01(4-age));
                Color old=GUI.color;GUI.color=color;
                float radius=18+24*pulse;
                if(screamerRing==null)
                {
                    screamerRing=new Texture2D(64,64,TextureFormat.RGBA32,false){name="Screamer ping ring"};
                    screamerGlow = new Texture2D(64, 64, TextureFormat.RGBA32, false)
                        { name = "Screamer alert glow", wrapMode = TextureWrapMode.Clamp };
                    var pixels=new Color[64*64];
                    var glowPixels = new Color[64 * 64];
                    for(int py=0;py<64;py++)for(int px=0;px<64;px++)
                    {
                        float distance=Vector2.Distance(new Vector2(px,py),new Vector2(31.5f,31.5f));
                        pixels[py*64+px]=new Color(1,1,1,Mathf.Clamp01(2-Mathf.Abs(distance-29)));
                        float falloff = Mathf.Clamp01(1 - distance / 31f);
                        glowPixels[py * 64 + px] = new Color(1, 1, 1, falloff * falloff);
                    }
                    screamerRing.SetPixels(pixels);screamerRing.Apply();
                    screamerGlow.SetPixels(glowPixels);
                    screamerGlow.Apply();
                }
                GUI.DrawTexture(new Rect(x-radius,y-radius,radius*2,radius*2),screamerRing);
                if (screaming || windingUp)
                {
                    GUI.color = new Color(1, .04f, .02f, .7f + .3f * pulse);
                    GUI.DrawTexture(new Rect(x - 52, y - 52, 104, 104), screamerGlow);
                    if (screamerAlertStyle == null)
                    {
                        screamerAlertStyle = new GUIStyle(GUI.skin.label)
                            { alignment = TextAnchor.MiddleCenter, fontSize = 46, fontStyle = FontStyle.Bold };
                        screamerAlertStyle.normal.textColor = Color.white;
                    }
                    GUI.color = new Color(.17f, .01f, .01f, 1);
                    GUI.Label(new Rect(x - 24, y - 26, 50, 56), "!", screamerAlertStyle);
                    GUI.color = new Color(1, .13f, .09f, 1);
                    GUI.Label(new Rect(x - 25, y - 28, 50, 56), "!", screamerAlertStyle);
                    GUI.color = color;
                }
                if(screamerPingStyle==null)screamerPingStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=16,fontStyle=FontStyle.Bold};
                GUI.Label(new Rect(x-85,y+radius+3,170,24),"SCREAMER",screamerPingStyle);
                GUI.color=old;
            }
            foreach(var enemy in expiredScreamerPings)screamerPings.Remove(enemy);
        }
        void ClearScreamerFeedback()
        {
            screamerPings.Clear();
            audibleScreamer = null;
            if(screamerCall!=null)screamerCall.Stop();
        }
    }
}
