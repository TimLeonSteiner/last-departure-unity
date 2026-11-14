Shader "Dustline/Server Floor Grime"
{
    Properties { _BaseColor("Dirt tint",Color)=(.13,.095,.055,.7) _Tide("Dried water ring",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1,-1
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Tide;
            CBUFFER_END
            struct A {float4 p:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;half fog:TEXCOORD2;};
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);}
            V Vert(A a){V o;o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);o.uv=a.uv;o.fog=ComputeFogFactor(o.p.z);return o;}
            half4 Frag(V i):SV_Target
            {
                float2 q=i.uv*2-1;
                float grain=Noise(i.world.xz*13),coarse=Noise(i.world.xz*2.1);
                float radius=length(q)+.18*(coarse-.5)+.07*(grain-.5);
                float patch=(1-smoothstep(.5,.95,radius))*smoothstep(.17,.62,coarse*.65+grain*.35);
                float ring=(1-smoothstep(.015,.075,abs(radius-.68)))*lerp(.15,1,grain);
                float alpha=lerp(patch,ring,_Tide)*_BaseColor.a;
                clip(alpha-.015);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=_BaseColor.rgb*lerp(.62,1.4,grain);surface.alpha=alpha;surface.smoothness=.08;surface.occlusion=1;
                InputData data=(InputData)0;data.positionWS=i.world;data.normalWS=half3(0,1,0);data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);
                data.bakedGI=SampleSH(data.normalWS);data.shadowCoord=TransformWorldToShadowCoord(i.world);data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);data.shadowMask=half4(1,1,1,1);
                half4 color=UniversalFragmentPBR(data,surface);color.rgb=MixFog(color.rgb,i.fog);color.a=alpha;return color;
            }
            ENDHLSL
        }
    }
}
