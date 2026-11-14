Shader "Dustline/Facility Vessel"
{
 Properties { _BaseColor("Tissue",Color)=(.12,.018,.025,1) _Pulse("Heartbeat",Range(0,1))=0 _Smoothness("Moisture",Range(0,1))=.55 _Cull("Cull",Float)=2 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 Pass {
 Cull [_Cull]
 Tags {"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
 #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
 #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 float _D27CheapTissue;
 CBUFFER_START(UnityPerMaterial)
 half4 _BaseColor;half _Pulse;half _Smoothness;
 CBUFFER_END
 struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
 struct V {float4 positionCS:SV_POSITION;float3 p:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;float fog:TEXCOORD3;};
 V vert(A a){V o;float3 p=a.positionOS.xyz+a.normalOS*.009*_Pulse*sin(a.uv.y*.8);o.positionCS=TransformObjectToHClip(p);o.p=TransformObjectToWorld(p);o.n=TransformObjectToWorldNormal(a.normalOS);o.uv=a.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
 float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
 float noise(float3 p){float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);}
 half4 frag(V i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC):SV_Target{
 if(_D27CheapTissue>.5){
 InputData d=(InputData)0;d.positionWS=i.p;d.normalWS=normalize(i.n)*IS_FRONT_VFACE(facing,1,-1);d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.p);d.shadowCoord=TransformWorldToShadowCoord(i.p);d.bakedGI=SampleSH(d.normalWS)+half3(.07,.055,.05);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);d.shadowMask=1;
 SurfaceData s=(SurfaceData)0;s.albedo=_BaseColor.rgb;s.alpha=1;s.normalTS=half3(0,0,1);s.occlusion=1;s.smoothness=_Smoothness;
 half4 result=UniversalFragmentPBR(d,s);result.rgb=MixFog(result.rgb,i.fog);return result;
 }

 float m=noise(i.p*7)*.65+noise(i.p*19)*.35,fine=noise(i.p*125);
 float mottling=noise(i.p*1.8+float3(8,2,5));
 float warp=noise(i.p*13);
 float fold=pow(.5+.5*sin(i.uv.y*31+warp*6),8);
 float vessel=1-smoothstep(.025,.13,abs(sin((i.uv.x*6+sin(i.uv.y*1.9)*.2)*6.283185)));
 float scar=pow(1-abs(noise(i.p*11+float3(1,6,2))*2-1),18)*smoothstep(.5,.75,m);
 float3 n=normalize(i.n)*IS_FRONT_VFACE(facing,1,-1);
 float fibre=pow(.5+.5*sin(i.uv.x*6.283185*19+noise(i.p*22)*5+i.uv.y*2),6);
 float h=m*.007+fold*.001+fine*.00065+scar*.001+fibre*.0008;
 float3 dx=ddx(i.p),dy=ddy(i.p),rx=cross(dy,n),ry=cross(n,dx);float det=dot(dx,rx);
 n=normalize(abs(det)*n-sign(det)*(ddx(h)*rx+ddy(h)*ry)+n*1e-10);
 InputData d=(InputData)0;d.positionWS=i.p;d.normalWS=n;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.p);d.shadowCoord=TransformWorldToShadowCoord(i.p);d.bakedGI=SampleSH(n)+half3(.07,.055,.05);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);d.shadowMask=1;
 SurfaceData s=(SurfaceData)0;
 half3 tissue=lerp(_BaseColor.rgb*half3(.56,.55,.85),_BaseColor.rgb*half3(1.22,.94,.82),mottling);
 tissue=lerp(tissue,half3(.27,.15,.12),scar*.23);
 s.albedo=tissue*lerp(.75,1.18,m)*(1-vessel*.24)*(1-fold*.08);
 s.alpha=1;s.normalTS=half3(0,0,1);s.occlusion=lerp(.82,1,m);
 s.smoothness=saturate(_Smoothness+(m-.5)*.4-scar*.22);
 s.emission=_BaseColor.rgb*_Pulse*.012;
 half4 result=UniversalFragmentPBR(d,s);result.rgb=MixFog(result.rgb,i.fog);return result;
 }
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
