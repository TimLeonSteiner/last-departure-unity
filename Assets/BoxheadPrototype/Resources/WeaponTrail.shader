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
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 routeData : TEXCOORD0;
                float3 center : TEXCOORD1;
                float3 side : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 routeData : TEXCOORD0;
                float3 center : TEXCOORD1;
                float3 side : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.routeData = input.routeData;
                output.center = input.center;
                output.side = input.side;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float depth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    clip(depth - .00001);
                #else
                    clip(.99999 - depth);
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                float3 surface = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 normal = normalize(cross(ddy(surface), ddx(surface)));
                float across = dot(surface - input.center, normalize(input.side));
                float edge = clamp(fwidth(across), .006, .035);
                float floorDistance = abs(surface.y - input.center.y);
                half floorMask = (1 - smoothstep(.07, .16, floorDistance)) * smoothstep(.78, .97, abs(normal.y));
                half core = 1 - smoothstep(.028 - edge, .046 + edge, abs(across));
                half halo = (1 - smoothstep(.015, .15, abs(across))) * .42;
                half fade = smoothstep(.75, 2.4, input.routeData.y) * smoothstep(0, .65, -input.routeData.z);
                // Signed route distance grows toward zero at the pickup.
                float phase = frac((input.routeData.z - _Time.y * 2.8) / 7 + .5);
                half pulse = 1 - smoothstep(.025, .22, abs(phase - .5));
                return half4(_BaseColor.rgb, floorMask * fade * saturate((core + halo) * lerp(.44, .80, pulse)));
            }
            ENDHLSL
        }
    }
}
