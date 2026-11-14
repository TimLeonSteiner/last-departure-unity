Shader "Dustline/Sky Watcher Cloud"
{
 Properties { _Color("Cloud",Color)=(.015,.022,.031,.8) }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
 Blend SrcAlpha OneMinusSrcAlpha
 ZWrite Off Cull Back
 Pass {
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 half4 _Color;
 CBUFFER_END
 struct V { float4 pos:SV_POSITION; float3 local:TEXCOORD0; float3 world:TEXCOORD1; };
 V vert(float4 p:POSITION) { V o;o.pos=TransformObjectToHClip(p.xyz);o.local=p.xyz;o.world=TransformObjectToWorld(p.xyz);return o; }
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(c),hash(c+float2(1,0)),f.x),lerp(hash(c+float2(0,1)),hash(c+1),f.x),f.y);}
 half4 frag(V i):SV_Target {
 float2 p=i.world.xy*.035+float2(_Time.y*.025,_Time.y*.008);
 float n=noise(p)*.55+noise(p*2.1)*.3+noise(p*4.3)*.15;
 float edge=1-smoothstep(.08,.49,length(i.local.xy));
 float density=edge*smoothstep(.22,.72,n);
 return half4(_Color.rgb*(.65+n*.7),_Color.a*density);
 }
 ENDHLSL
 }
 }
}
