Shader "Dustline/Spaceport Mist"
{
    Properties { _Color("Mist", Color) = (.17,.3,.34,.2) }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
            V vert(float4 p:POSITION,float2 uv:TEXCOORD0) { V o;o.pos=TransformObjectToHClip(p.xyz);o.uv=uv;o.world=TransformObjectToWorld(p.xyz);return o; }
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(c),hash(c+float2(1,0)),f.x),lerp(hash(c+float2(0,1)),hash(c+1),f.x),f.y);}
            half4 frag(V i):SV_Target {
                float edge=saturate(i.uv.x*9)*saturate((1-i.uv.x)*9);
                float height=sin(saturate(i.uv.y)*PI);
                float wisp=noise(i.world.xz*.13+float2(_Time.y*.018,0)+i.uv.y*3);
                float fade=saturate((distance(_WorldSpaceCameraPos,i.world)-5)/12);
                return half4(_Color.rgb,_Color.a*edge*height*height*smoothstep(.2,.8,wisp)*fade);
            }
            ENDHLSL
        }
    }
}
