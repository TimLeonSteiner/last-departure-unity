Shader "Dustline/Heart Steam"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; half4 color:COLOR; };
            V vert(A v) { V o;o.positionWS=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.uv=v.uv;o.color=v.color;return o; }
            half4 frag(V i):SV_Target
            {
                float2 q=i.uv*2-1;
                float shape=pow(saturate(1-dot(q,q)),2);
                float wisps=.65+.35*sin(i.uv.x*19+sin(i.uv.y*15)*2);
                InputData inputData=(InputData)0;
                inputData.positionWS=i.positionWS;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                half3 illumination=SampleSH(half3(0,1,0));
                Light mainLight=GetMainLight();illumination+=mainLight.color*.15;
                uint count=GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light light=GetAdditionalLight(lightIndex,i.positionWS);
                    illumination+=light.color*light.distanceAttenuation*.7;
                LIGHT_LOOP_END
                return half4(i.color.rgb*min(illumination,half3(2,2,2)),i.color.a*shape*wisps);
            }
            ENDHLSL
        }
    }
}
