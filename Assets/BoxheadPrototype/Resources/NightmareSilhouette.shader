Shader "Dustline/Nightmare Silhouette"
{
    Properties
    {
        _BaseMap("Original texture", 2D) = "white" {}
        _BaseColor("Original color", Color) = (1,1,1,1)
        [Normal] _BumpMap("Original normal detail", 2D) = "bump" {}
        _BumpScale("Normal strength", Float) = 1
        _Dream("Drowsiness", Range(0,1)) = 0
        _DreamTime("Perception clock", Float) = 0
        _DreamOrigin("Body origin", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST, _BumpMap_ST, _BaseColor, _DreamOrigin;
        float _Dream, _DreamTime, _BumpScale;
        CBUFFER_END
        struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; };
        struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 world:TEXCOORD1; float2 uv:TEXCOORD2; float fog:TEXCOORD3; float4 tangentWS:TEXCOORD4; float2 normalUV:TEXCOORD5; };
        float DreamNoise(float3 p)
        {
            float3 cell = floor(p), f = frac(p);
            f = f * f * (3 - 2 * f);
            float4 a = frac(sin(float4(dot(cell, float3(127.1,311.7,74.7)),
                dot(cell + float3(1,0,0), float3(127.1,311.7,74.7)),
                dot(cell + float3(0,1,0), float3(127.1,311.7,74.7)),
                dot(cell + float3(1,1,0), float3(127.1,311.7,74.7)))) * 43758.5453);
            float4 b = frac(sin(float4(dot(cell + float3(0,0,1), float3(127.1,311.7,74.7)),
                dot(cell + float3(1,0,1), float3(127.1,311.7,74.7)),
                dot(cell + float3(0,1,1), float3(127.1,311.7,74.7)),
                dot(cell + float3(1,1,1), float3(127.1,311.7,74.7)))) * 43758.5453);
            return lerp(lerp(lerp(a.x,a.y,f.x),lerp(a.z,a.w,f.x),f.y),
                lerp(lerp(b.x,b.y,f.x),lerp(b.z,b.w,f.x),f.y),f.z);
        }
        float Cloud(float3 world)
        {
            float3 p = (world - _DreamOrigin.xyz) * 2.1 + _DreamOrigin.xyz * .31;
            p += float3(_DreamTime * .065, -_DreamTime * .045, _DreamTime * .025);
            float broad = DreamNoise(p);
            return broad * .65 + DreamNoise(p * 2.13 + float3(7,3,11)
                + float3(-_DreamTime * .04, 0, _DreamTime * .035)) * .35;
        }
        float3 Distort(float3 p)
        {
            float height = p.y - _DreamOrigin.y;
            float head = smoothstep(1.05, 1.65, height);
            float phase = _DreamOrigin.x * .73 + _DreamOrigin.z * .41;
            float warp = _Dream * _Dream;
            p.x += warp * (head * .09 * sin(height * 7 + phase + _DreamTime * .55) + .018 * sin(height * 11 + _DreamTime));
            p.z += warp * head * .06 * sin(height * 9 + phase);
            p.y += warp * head * .065 * sin(height * 6 + phase);
            return p;
        }
        V Vertex(A a)
        {
            V o; o.world = Distort(TransformObjectToWorld(a.positionOS.xyz));
            o.positionCS = TransformWorldToHClip(o.world); o.normalWS = TransformObjectToWorldNormal(a.normalOS);
            o.tangentWS = float4(TransformObjectToWorldDir(a.tangentOS.xyz),a.tangentOS.w * GetOddNegativeScale());
            o.normalUV=TRANSFORM_TEX(a.uv,_BumpMap);
            o.uv = TRANSFORM_TEX(a.uv, _BaseMap); o.fog = ComputeFogFactor(o.positionCS.z); return o;
        }
        ENDHLSL
        Pass
        {
            Name "DreamBody"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            half4 Fragment(V i):SV_Target
            {
                float3 n = normalize(i.normalWS), view = normalize(_WorldSpaceCameraPos - i.world);
                float3 tangent = i.tangentWS.xyz;
                if(dot(tangent,tangent)>.01)
                {
                    tangent=normalize(tangent);
                    float3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.normalUV),_BumpScale);
                    n=normalize(tangent*normalTS.x+cross(n,tangent)*i.tangentWS.w*normalTS.y+n*normalTS.z);
                }
                Light light = GetMainLight(TransformWorldToShadowCoord(i.world));
                float3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                float3 lit = base * (SampleSH(n) + light.color * saturate(dot(n, light.direction)) * light.shadowAttenuation);
                // Drifting cloud patches obscure portions while retaining recognizable detail.
                float grain = Cloud(i.world);
                float facing = smoothstep(.05, .65, saturate(dot(n, view)));
                float3 local = i.world - _DreamOrigin.xyz;
                float patches = DreamNoise(local * 4.7 + _DreamOrigin.xyz * .31 + float3(-_DreamTime * .06, _DreamTime * .035, 0));
                float fineFade = 1 - smoothstep(.015, .055, length(fwidth(local)));
                float flecks = lerp(.5, DreamNoise(local * 36), fineFade);
                float marbling = smoothstep(.25, .78, patches * .7 + grain * .3);
                float3 silhouette = lerp(float3(.014,.016,.019),float3(.072,.077,.082),marbling);
                silhouette *= lerp(.65,1.15,facing) * lerp(.72,1.15,flecks);
                float cloudCover = smoothstep(.28, .58, grain);
                float luminance = dot(lit, float3(.2126,.7152,.0722));
                float3 readable = lerp(lit, luminance.xxx, .65) * .30;
                float3 dream = lerp(readable, silhouette * .40, cloudCover * .96);
                return half4(MixFog(lerp(lit, dream, saturate(_Dream)), i.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
