Shader "Dustline/Weapon Trail"
{
    Properties
    {
        _BaseColor("Line Color", Color) = (.24, .86, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 routeData : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 routeData : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.routeData = input.routeData;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float across = input.routeData.x * .15;
                float edge = clamp(fwidth(across), .006, .035);
                half core = 1 - smoothstep(.028 - edge, .046 + edge, abs(across));
                half halo = (1 - smoothstep(.015, .15, abs(across))) * .42;
                half fade = smoothstep(.75, 2.4, input.routeData.y) * smoothstep(0, .65, -input.routeData.z);
                // Signed route distance grows toward zero at the pickup.
                float phase = frac((input.routeData.z - _Time.y * 2.8) / 7 + .5);
                half pulse = 1 - smoothstep(.025, .22, abs(phase - .5));
                return half4(_BaseColor.rgb, fade * saturate((core + halo) * lerp(.65, .95, pulse)));
            }
            ENDHLSL
        }
    }
}
