Shader "Dustline/Spaceport Outline"
{
    Properties { _Width("Pixel width", Range(0,3)) = 1.15 }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry-1" }
        Pass {
            Cull Front ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half _Width;
            CBUFFER_END
            struct V { float4 pos:SV_POSITION; float distance:TEXCOORD0; };
            V vert(float4 p:POSITION,float3 n:NORMAL) {
                V o;float3 world=TransformObjectToWorld(p.xyz);o.pos=TransformWorldToHClip(world);
                float2 direction=TransformWorldToViewDir(TransformObjectToWorldNormal(n),true).xy;
                direction/=max(length(direction),.001);
                o.pos.xy+=direction*_Width*2/_ScreenParams.xy*o.pos.w;
                o.distance=distance(world,_WorldSpaceCameraPos);return o;
            }
            half4 frag(V i):SV_Target { return half4(lerp(half3(.013,.024,.03),half3(.075,.13,.16),saturate(i.distance/220)),1); }
            ENDHLSL
        }
    }
}
