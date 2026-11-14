Shader "Dustline/Nightmare Cloud"
{
    Properties { _DreamTime("Perception clock", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "CloudVeil"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _DreamTime;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float fog:TEXCOORD1; };
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),
                    lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            float Cloud(float2 p)
            {
                return Noise(p)*.57 + Noise(p*2.07+7)*.28 + Noise(p*4.13+19)*.15;
            }
            V Vert(A a)
            {
                V o;
                o.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                o.uv = a.uv; o.color = a.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                float2 q = i.uv * 2 - 1;
                float2 drift = float2(_DreamTime*.027, -_DreamTime*.019);
                float density = Cloud(i.uv*3.8 + drift);
                float edge = 1 - smoothstep(.18, 1, length(q) + (density-.5)*.24);
                float wisps = smoothstep(.19,.60,density);
                float alpha = saturate(edge * wisps * i.color.a);
                // Broad feathered opacity, never a hard cloud-shaped mesh silhouette.
                return half4(MixFog(i.color.rgb * lerp(.65,1.2,density), i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
