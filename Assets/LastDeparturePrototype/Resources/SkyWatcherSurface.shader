Shader "Dustline/Sky Watcher Surface"
{
    Properties
    {
        _BaseMap("Surface",2D)="white"{}
        [Normal] _BumpMap("Sculpt detail",2D)="bump"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _Brightness("Night visibility",Range(0,2))=.65
        _Eye("Separate globe",Float)=0
        _BoundsCenter("Mesh centre",Vector)=(0,0,0,0)
        _BoundsExtent("Mesh half-size",Vector)=(1,1,1,0)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Cull Back ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Shaders/WatcherAtmosphere.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST,_BaseColor,_BoundsCenter,_BoundsExtent;float _Brightness,_Eye;
            CBUFFER_END
            float _StormLightningFlash;
            struct Attributes{float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float4 tangent:TANGENT;};
            struct Varyings{float4 position:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;float2 uv:TEXCOORD2;float3 local:TEXCOORD3;float4 tangent:TEXCOORD4;};
            Varyings vert(Attributes v)
            {
                Varyings o;o.world=TransformObjectToWorld(v.position.xyz);o.position=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normal);o.uv=TRANSFORM_TEX(v.uv,_BaseMap);
                o.local=(v.position.xyz-_BoundsCenter.xyz)/max(_BoundsExtent.xyz,float3(.00001,.00001,.00001));
                o.tangent=float4(TransformObjectToWorldDir(v.tangent.xyz),v.tangent.w*GetOddNegativeScale());return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float3 n=normalize(i.normal),view=normalize(_WorldSpaceCameraPos-i.world);
                bool globe=_Eye>.5;
                float2 face=i.local.xz;
                float3 coolLight=normalize(float3(-.32,-.55,-.77));
                float3 towardHall=float3(0,-1,0);
                float3 surface;
                if(globe)
                {
                    // Keep the iris on the rotating globe and keep the lid texture off the sclera.
                    float radial=length(face);
                    float2 irisUV=float2(.5,.519)+face*float2(.35,.43);
                    float3 iris=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,irisUV).rgb;
                    float irisMask=1-smoothstep(.49,.56,radial);
                    float corner=smoothstep(.52,.99,radial);
                    float grain=CloudNoise(float3(face*32,8));
                    float3 sclera=lerp(float3(.20,.125,.105),float3(.10,.023,.016),corner);
                    sclera*=.82+.28*grain;
                    float diffuse=.31+.58*saturate(dot(n,coolLight));
                    surface=lerp(sclera*diffuse,iris*float3(1.24,.66,.52),irisMask);
                    float glint=pow(saturate(dot(n,normalize(view+coolLight))),105);
                    surface+=glint*float3(.085,.095,.105);
                }
                else
                {
                    // Mottled skin follows the deforming surface without stretching an image strip.
                    float3 p=float3(face*float2(1.1,.72),i.local.y*.22);
                    float mottling=CloudNoise(p*13)*.62+CloudNoise(p*33+8)*.38;
                    float pores=CloudNoise(p*122);
                    float2 skinUV=float2(.5+face.x*.46,.79+abs(face.y)*.20);
                    float3 folds=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,skinUV).rgb;
                    float3 skin=lerp(float3(.011,.007,.007),float3(.047,.031,.025),mottling);
                    skin=lerp(skin,folds*float3(.56,.44,.40),.56);
                    float aperture=length(face*float2(1,1.25));
                    float bounce=exp(-aperture*4.5)*(.5+.5*mottling);
                    float3 softNormal=normalize(lerp(n,towardHall,.36));
                    float diffuse=.28+.9*saturate(dot(softNormal,coolLight));
                    float sheen=pow(saturate(dot(softNormal,normalize(view+coolLight))),12);
                    surface=skin*diffuse*(.65+.55*pores);
                    surface+=sheen*float3(.052,.059,.065)*pow(pores,3);
                    surface+=bounce*float3(.19,.018,.009);
                    surface*=lerp(1,.32,smoothstep(.28,.75,aperture));
                }
                surface*=_BaseColor.rgb*(_Brightness+_StormLightningFlash*.45);
                float raggedEdge=CloudNoise(float3(face*6,_Time.y*.023))-.5;
                float border=length(face*float2(1,1.05))+raggedEdge*.17;
                float conceal=globe?1:1-smoothstep(.36,.74,border);
                float4 cloud=WatcherClouds(_WorldSpaceCameraPos,-view,distance(_WorldSpaceCameraPos,i.world),_StormLightningFlash);
                surface=surface*(1-cloud.a*.52)+cloud.rgb*.52;
                float3 skyPlate=WatcherSkyPlate(-view);
                float cloudCover=smoothstep(.011,.07,dot(skyPlate,float3(.2126,.7152,.0722)));
                surface=lerp(surface,skyPlate,cloudCover);
                return half4(surface,conceal);
            }
            ENDHLSL
        }
    }
}

