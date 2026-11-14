Shader "Dustline/Puddle Water"
{
    Properties
    {
        _BaseColor("Water tint and opacity", Color) = (.025,.032,.035,.22)
        _Smoothness("Water smoothness", Range(0,1)) = .86
        _RainStrength("Rain impacts", Range(0,2)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-5" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define _ALPHAPREMULTIPLY_ON 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Smoothness;
            half _RainStrength;
            CBUFFER_END
            TEXTURE2D(_PlanarReflection); SAMPLER(sampler_PlanarReflection);
            float4x4 _ReflectionVP;
            float _ReflectionReady;
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float2 RainRipples(float2 p)
            {
                float2 cell=floor(p*1.7),slope=0;
                for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                {
                    float2 id=cell+float2(x,y);
                    float seed=hash(id);
                    float age=frac(_Time.y*.72+seed);
                    float2 center=(id+float2(hash(id+17),hash(id+39)))/1.7;
                    float2 delta=p-center;
                    float d=length(delta);
                    float ring=d-age*.52;
                    float envelope=exp(-ring*ring*900)*sin(age*3.14159)*(1-age);
                    slope+=delta/max(d,.001)*cos(ring*95)*envelope*.085;
                }
                return slope*saturate(_RainStrength);
            }
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.uv=v.uv;return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.positionWS.xz;
                float radius=length(i.uv*2-1);
                // A feathered waterline reveals the original concrete and its seams.
                float edge=1-smoothstep(.72,1,radius);
                float rippleFade=1-smoothstep(.02,.12,length(fwidth(p)));
                float2 slope=float2(cos(dot(p,float2(7,3))+_Time.y*.8),
                                   cos(dot(p,float2(-4,9))-_Time.y*.65))*.012*rippleFade;
                slope+=RainRipples(p)*rippleFade*edge;
                half3 normal=normalize(half3(slope.x,1,slope.y));
                InputData inputData=(InputData)0;
                inputData.positionWS=i.positionWS;
                inputData.normalWS=normal;
                inputData.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                inputData.bakedGI=SampleSH(normal);
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=_BaseColor.rgb;
                float depth=(1-smoothstep(.2,.9,radius))*(.7+.3*sin(p.x*1.4)*sin(p.y*1.1));
                surface.alpha=lerp(.07,_BaseColor.a,depth);
                surface.metallic=0;
                surface.smoothness=_Smoothness;
                surface.normalTS=half3(0,0,1);
                surface.occlusion=1;
                half4 water=UniversalFragmentPBR(inputData,surface);
                float4 reflectionPosition=mul(_ReflectionVP,float4(i.positionWS,1));
                float2 reflectionUV=reflectionPosition.xy/max(reflectionPosition.w,.001)*.5+.5;
                reflectionUV+=slope*.018;
                float valid=_ReflectionReady*step(0,reflectionPosition.w)
                    *step(0,reflectionUV.x)*step(reflectionUV.x,1)*step(0,reflectionUV.y)*step(reflectionUV.y,1);
                // Water's Fresnel reflection strengthens naturally at a grazing angle.
                float fresnel=(.025+.975*pow(1-saturate(dot(normal,inputData.viewDirectionWS)),5))*valid;
                half3 room=SAMPLE_TEXTURE2D_LOD(_PlanarReflection,sampler_PlanarReflection,saturate(reflectionUV),.6).rgb;
                water.rgb+=room*fresnel;
                water.a=saturate(water.a+fresnel*(1-water.a));
                // Premultiplied shading preserves light glints through clear water;
                // fade BOTH reflection and opacity to zero at the shoreline.
                return half4(water.rgb*edge,water.a*edge);
            }
            ENDHLSL
        }
    }
}
