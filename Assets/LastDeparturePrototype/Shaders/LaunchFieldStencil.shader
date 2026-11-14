Shader "Dustline/Launch Field Stencil"
{
    Properties { _BaseMap("Glyph atlas",2D)="white"{} _BaseColor("Paint",Color)=(.72,.72,.60,1) }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout"}
        Cull Off ZWrite On
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);float4 _BaseColor;
            struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
            V vert(float4 p:POSITION,float2 uv:TEXCOORD0){V o;o.p=TransformObjectToHClip(p.xyz);o.uv=uv;return o;}
            half4 frag(V i):SV_Target{clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a-.4);return _BaseColor;}
            ENDHLSL
        }
    }
}
