Shader "Dustline/Blood Stain"
{
    Properties { _BloodColor("Blood",Color)=(.3,.012,.018,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off Offset -1,-1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BloodColor;
            CBUFFER_END
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;};
            V vert(float4 p:POSITION){V o;o.positionCS=TransformObjectToHClip(p.xyz);o.world=TransformObjectToWorld(p.xyz);return o;}
            half4 frag(V i):SV_Target
            {
                float grain=frac(sin(dot(floor(i.world*150),float3(12.9898,78.233,37.719)))*43758.5453);
                float detail=1-smoothstep(.015,.05,length(fwidth(i.world)));
                return half4(_BloodColor.rgb*lerp(1,.88+grain*.2,detail),_BloodColor.a);
            }
            ENDHLSL
        }
    }
}
