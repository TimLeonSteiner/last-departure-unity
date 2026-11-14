Shader "Yard/HologramOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _SweepPhase ("Scan sweep phase", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ HOLOGRAM_BACKPLATE
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;
            float _SweepPhase;
            Output vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            fixed4 frag(Output i):SV_Target
            {
                #if defined(HOLOGRAM_BACKPLATE)
                    float2 panel = (i.uv - 0.5) * float2(832, 652);
                    float2 bounds = abs(panel) - float2(400, 310);
                    float edge = max(max(bounds.x, bounds.y), (bounds.x + bounds.y + 10) * 0.7071);
                    float antialias = max(fwidth(edge), 0.5);
                    float inside = 1 - smoothstep(-antialias, antialias, edge);
                    float rim = 1 - smoothstep(0.7, 1.8 + antialias, abs(edge));
                    float halo = pow(saturate(1 - max(edge, 0) / 15), 3);

                    float scanlines = 0.5 + 0.5 * cos(panel.y * 1.0472);
                    scanlines *= 1 - smoothstep(1.5, 3.0, fwidth(panel.y));
                    float sweepY = lerp(360, -360, _SweepPhase);
                    float sweep = 1 - smoothstep(1, 29, abs(panel.y - sweepY));
                    float3 cyan = float3(0.10, 0.48, 0.62);
                    float3 background = float3(0.006, 0.019, 0.031);
                    background += cyan * (0.012 * scanlines + 0.025 * sweep);
                    background += cyan * (0.10 * pow(saturate(1 + edge / 22), 2));
                    float3 color = lerp(background, cyan, saturate(rim * 0.86 + (1 - inside)));
                    float alpha = max(inside * i.color.a, max(rim * 0.84, halo * 0.26)) * saturate(i.color.a / 0.86);
                    return fixed4(color, alpha);
                #else
                    return (tex2D(_MainTex,i.uv)+_TextureSampleAdd)*i.color;
                #endif
            }
            ENDCG
        }
    }
}
