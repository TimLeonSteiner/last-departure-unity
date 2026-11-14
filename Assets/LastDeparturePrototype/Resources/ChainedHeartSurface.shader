Shader "Dustline/Chained Heart"
{
    Properties
    {
        _BaseMap("Color", 2D) = "white" {}
        _BumpMap("Normal", 2D) = "bump" {}
        _MetalMask("Rigid Hardware Mask", 2D) = "white" {}
        _Pulse("Heartbeat", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetalMask); SAMPLER(sampler_MetalMask);
        CBUFFER_START(UnityPerMaterial)
            float _Pulse;
            float4 _TubePulse;
            float4x4 _HeartFromObject;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
        };

        half TissueMask(half3 color)
        {
            half redness = (color.r - color.g) / max(color.r, .025);
            return smoothstep(.59, .82, redness) * smoothstep(.035, .085, color.r);
        }

        float3 BeatingPosition(Attributes input, float3 normalWS)
        {
            half3 color = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, input.uv, 0).rgb;
            float3 heart = mul(_HeartFromObject, input.positionOS).xyz;
            float tissue = TissueMask(color);
            float tubeHeight = smoothstep(.42, .55, heart.y) * (1 - smoothstep(.76, .87, heart.y));
            float innerHoses = 1 - smoothstep(.16, .22, length(heart.xz));
            float tubeTravel = saturate((heart.y - .44) / .42) * 2;
            float tubePulse = lerp(_TubePulse.x, _TubePulse.y, saturate(tubeTravel));
            tubePulse = lerp(tubePulse, _TubePulse.z, saturate(tubeTravel - 1));
            float tubeFlex = .048 * tubeHeight * innerHoses * tubePulse;
            // The scan shares one mesh and color atlas. Use its metallic atlas to
            // exclude chains, cuffs and brackets from both deformation paths.
            float metal = SAMPLE_TEXTURE2D_LOD(_MetalMask, sampler_MetalMask, input.uv, 0).r;
            float organic = 1 - smoothstep(.02, .10, metal);
            return TransformObjectToWorld(input.positionOS.xyz) +
                normalWS * (organic * lerp(tubeFlex, .085 * _Pulse, tissue));
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = BeatingPosition(input, output.normalWS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz),
                    input.tangentOS.w * GetOddNegativeScale());
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 source = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half tissue = TissueMask(source);
                half gray = dot(source, half3(.2126, .7152, .0722));
                half3 bronze = lerp(source, gray * half3(1.38, .95, .49), .76);
                half3 flesh = source * half3(.64, .81, .9) + gray * half3(.04, .055, .15);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), .8);
                half3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS,
                    half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));

                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.normalWS = normalWS;
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                data.bakedGI = SampleSH(normalWS);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = 1;

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = lerp(bronze, flesh, tissue);
                surface.alpha = 1;
                surface.occlusion = 1;
                surface.normalTS = normalTS;
                surface.metallic = lerp(.48, 0, tissue);
                surface.smoothness = lerp(.32, .56, tissue);
                half4 result = UniversalFragmentPBR(data, surface);
                result.rgb = MixFog(result.rgb, input.fog);
                return result;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            float4 DepthVertex(Attributes input) : SV_POSITION
            {
                return TransformWorldToHClip(BeatingPosition(input, TransformObjectToWorldNormal(input.normalOS)));
            }
            half DepthFragment(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            float4 ShadowVertex(Attributes input) : SV_POSITION
            {
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = BeatingPosition(input, normalWS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection)));
            }
            half4 ShadowFragment() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
