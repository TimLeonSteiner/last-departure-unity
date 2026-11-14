Shader "Dustline/SpitterBile"
{
    Properties { _Pool("Pool", Float)=0 _Fade("Fade", Range(0,1))=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Pool, _Fade;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; float4 color:COLOR; };
            V vert(A i) { V o; o.world=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world); o.normal=TransformObjectToWorldNormal(i.normalOS); o.uv=i.uv; o.color=i.color; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) { float2 q=floor(p),f=frac(p); f=f*f*(3-2*f); return lerp(lerp(hash(q),hash(q+float2(1,0)),f.x),lerp(hash(q+float2(0,1)),hash(q+1),f.x),f.y); }
            half4 frag(V i, bool front:SV_IsFrontFace):SV_Target
            {
                float t=_Time.y;
                float2 p=lerp(i.uv*float2(7,3),i.world.xz*2.6,_Pool);
                float n=noise(p*2+float2(t*.12,-t*.18));
                float fine=noise(p*27);
                float ripple=sin(p.x*17+t*2.4)*cos(p.y*19-t*1.6);
                float3 normal=normalize(i.normal*(front?1:-1)+float3(ripple*.12,0,cos(p.x*21-t)*.12));
                float3 view=GetWorldSpaceNormalizeViewDir(i.world);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                float fresnel=pow(1-saturate(dot(normal,view)),4);
                float spec=pow(saturate(dot(normal,normalize(light.direction+view))),95);
                float3 base=lerp(float3(.055,.069,.008),float3(.19,.23,.025),n);
                base*=lerp(.55,1,smoothstep(.12,.32,fine));
                // Cellular foam rings gather in patches rather than covering the liquid uniformly.
                float2 cell=p*15; float2 id=floor(cell); float2 f=frac(cell)-.5;
                float radius=.18+hash(id)*.18;
                float ring=(1-smoothstep(.024,.065,abs(length(f)-radius)))*step(.69,noise(p*3));
                base=lerp(base,float3(.58,.60,.29),ring*.7);
                float3 ambient=max(SampleSH(normal),float3(.17,.19,.16));
                float3 rgb=base*(ambient+light.color*(.25+.75*saturate(dot(normal,light.direction))))+
                    light.color*spec*.8+float3(.28,.31,.13)*fresnel*.35;
                // A restrained acid rim remains legible in unlit corners.
                #ifdef _ADDITIONAL_LIGHTS
                for(uint li=0;li<GetAdditionalLightsCount();li++)
                {
                    Light localLight=GetAdditionalLight(li,i.world);
                    float highlight=pow(saturate(dot(normal,normalize(localLight.direction+view))),95);
                    rgb+=localLight.color*localLight.distanceAttenuation*(base*saturate(dot(normal,localLight.direction))+highlight*.8);
                }
                #endif
                float rim=_Pool*smoothstep(.77,1,i.uv.y);
                rgb+=float3(.12,.17,.012)*(rim*.7+.16);
                float alpha=i.color.a*_Fade*lerp(.94,.78,rim);
                return half4(rgb*i.color.rgb,alpha);
            }
            ENDHLSL
        }
    }
}

