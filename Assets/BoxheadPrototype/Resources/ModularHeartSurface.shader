Shader "Dustline/Modular Heart"
{
    Properties
    {
        _BaseMap("Color", 2D) = "white" {}
        _BumpMap("Normal", 2D) = "bump" {}
        _MetalMask("Rigid Hardware Mask", 2D) = "white" {}
        _Roughness("Roughness", 2D) = "gray" {}
        _Pulse("Heartbeat", Float) = 0
        _Kind("Module kind", Float) = 0
        _HoseScale("Hose radius", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_Roughness); SAMPLER(sampler_Roughness);
        TEXTURE2D(_MetalMask); SAMPLER(sampler_MetalMask);
        CBUFFER_START(UnityPerMaterial)
            float _Pulse;
            float _Kind;
            float _HoseScale;
            float4 _HoseA, _HoseB, _HoseC, _HoseD, _HoseSide;
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
            return smoothstep(.25, .55, redness) * smoothstep(.035, .085, color.r);
        }

        float OrganRegion(float3 position) { return _Kind > .5 && _Kind < 1.5 ? 1 : 0; }
        void HoseBasis(float t, out float3 center, out float3 tangent, out float3 lateral, out float3 side)
        {
            float u=1-t;
            center=u*u*u*_HoseA.xyz+3*u*u*t*_HoseB.xyz+3*u*t*t*_HoseC.xyz+t*t*t*_HoseD.xyz;
            tangent=normalize(3*u*u*(_HoseB.xyz-_HoseA.xyz)+6*u*t*(_HoseC.xyz-_HoseB.xyz)+3*t*t*(_HoseD.xyz-_HoseC.xyz));
            side=normalize(_HoseSide.xyz);
            lateral=normalize(cross(side,tangent));
            side=normalize(cross(tangent,lateral));
        }
        float3 DeformedDirection(Attributes input,float3 direction)
        {
            if(_Kind<1.5)return TransformObjectToWorldDir(direction);
            float3 center,tangent,lateral,side;
            HoseBasis(_Kind>2.5?input.positionOS.z:saturate(input.positionOS.z),center,tangent,lateral,side);
            return TransformObjectToWorldDir(direction.x*lateral+direction.y*side+direction.z*tangent);
        }
        float3 BeatingPosition(Attributes input, float3 normalWS)
        {
            if(_Kind>1.5)
            {
                float3 center,tangent,lateral,side;
                HoseBasis(_Kind>2.5?input.positionOS.z:saturate(input.positionOS.z),center,tangent,lateral,side);
                float t=saturate(input.positionOS.z);
                float travel=t*2;
                float pulse=lerp(_TubePulse.x,_TubePulse.y,saturate(travel));
                pulse=lerp(pulse,_TubePulse.z,saturate(travel-1));
                // Flexible hose swells gently along its length; sockets and both ends stay fixed.
                float flex=_Kind<2.5 ? .055*sin(t*PI)*pulse : 0;
                return TransformObjectToWorld(center+_HoseScale*(1+flex)*(input.positionOS.x*lateral+input.positionOS.y*side));
            }
            float3 position=TransformObjectToWorld(input.positionOS.xyz);
            if(_Kind<.5)return position;
            half3 color=SAMPLE_TEXTURE2D_LOD(_BaseMap,sampler_BaseMap,input.uv,0).rgb;
            float metal=SAMPLE_TEXTURE2D_LOD(_MetalMask,sampler_MetalMask,input.uv,0).r;
            float tissue=TissueMask(color)*(1-smoothstep(.02,.10,metal));
            return position+normalWS*(.045*_Pulse*tissue);
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
                float3 heart : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.normalWS = DeformedDirection(input,input.normalOS);
                output.positionWS = BeatingPosition(input, output.normalWS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.tangentWS = half4(DeformedDirection(input,input.tangentOS.xyz),
                    input.tangentOS.w * GetOddNegativeScale());
                output.uv = input.uv;
                output.heart = mul(_HeartFromObject, input.positionOS).xyz;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 source = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half metallic = SAMPLE_TEXTURE2D(_MetalMask, sampler_MetalMask, input.uv).r;
                half tissue = TissueMask(source) * OrganRegion(input.heart) * (1 - smoothstep(.02, .10, metallic));
                half3 flesh = source * half3(1.38, .85, .96);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), lerp(.8, 1.45, tissue));
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
                surface.albedo = lerp(source, flesh, tissue);
                surface.alpha = 1;
                surface.occlusion = 1;
                surface.normalTS = normalTS;
                surface.metallic = metallic;
                half smoothness = 1 - SAMPLE_TEXTURE2D(_Roughness, sampler_Roughness, input.uv).r;
                surface.smoothness = lerp(smoothness, clamp(smoothness, .27, .48), tissue);
                // Old pump seepage is confined to the low rigid deck, never the living tissue.
                if(_Kind<.5 && input.positionWS.y<.35 && input.normalWS.y>.65)
                {
                    float2 pump=float2((abs(input.positionWS.x)-2.45)/.72,(input.positionWS.z+.65)/.92);
                    float ragged=sin(input.positionWS.x*17+sin(input.positionWS.z*11))*.075+sin(input.positionWS.z*23)*.04;
                    float oil=1-smoothstep(.46,.95,length(pump)+ragged);
                    surface.albedo=lerp(surface.albedo,half3(.016,.012,.009),oil*.72);
                    surface.smoothness=lerp(surface.smoothness,.77,oil*.8);
                    surface.metallic=lerp(surface.metallic,.12,oil);
                }

                surface.emission = flesh * (.09 * tissue);
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
                float3 normalWS = DeformedDirection(input,input.normalOS);
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
