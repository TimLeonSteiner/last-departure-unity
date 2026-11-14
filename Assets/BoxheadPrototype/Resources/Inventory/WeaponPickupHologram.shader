Shader "Dustline/Weapon Pickup Hologram"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float height = input.positionWS.y;
                float scan = .5 + .5 * cos(height * 175);
                scan *= 1 - smoothstep(.014, .034, fwidth(height));
                float sweep = abs(frac(height * .85 - _Time.y * .65) - .5);
                sweep = 1 - smoothstep(.016, .09, sweep);
                float3 normal = normalize(input.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float rim = pow(1 - saturate(dot(normal, view)), 2);
                float facing = saturate(dot(normal, normalize(float3(.3, .8, -.5))));
                half3 cyan = half3(.08, .77, 1);
                return half4(cyan * (.17 + .29 * facing + .58 * rim + .15 * scan + .32 * sweep), 1);
            }
            ENDHLSL
        }
    }
}
