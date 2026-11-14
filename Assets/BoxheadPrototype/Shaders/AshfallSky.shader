Shader "Dustline/Ashfall Sky"
{
    Properties {
        _Top ("Storm ceiling", Color) = (0.0003,0.0005,0.0009,1)
        _Horizon ("Dust horizon", Color) = (0.001,0.0015,0.0025,1)
        _WatcherSkyPlate ("Storm clouds", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WatcherAtmosphere.hlsl"
            float4 _Top, _Horizon;
            float _StormLightningFlash;
            struct Varyings { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings vert(float4 position : POSITION)
            {
                Varyings o; o.position = TransformObjectToHClip(position.xyz); o.direction = position.xyz; return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 d=normalize(i.direction);
                float elevation=saturate(d.y);
                float3 color=lerp(_Horizon.rgb,_Top.rgb,pow(elevation,.48));
                float2 uv=d.xz / max(.16,d.y+.2) * 2.2 + _Time.y * float2(.008,.003);
                float cloud=noise(uv)*.55+noise(uv*2.1)*.28+noise(uv*4.3)*.14;
                color=lerp(color,color*.42+float3(.0003,.0005,.0008),smoothstep(.38,.72,cloud)*smoothstep(0,.28,elevation)*.7);
                float sun=dot(d,normalize(float3(-.45,.18,.85)));
                color+=float3(.001,.0015,.002)*pow(saturate(sun),18)*.12;
                color+=float3(.03,.035,.04)*smoothstep(.9991,.9996,sun)*.06;
                color=lerp(color,WatcherSkyPlate(d),smoothstep(-.32,-.12,d.y));
                // Moving backlit cloud breaks keep silhouettes readable between lightning strikes.
                float cloudBreak=smoothstep(.32,.67,cloud);
                float skyBand=smoothstep(-.08,.12,d.y);
                color+=float3(.014,.023,.038)*lerp(.24,1,cloudBreak)*skyBand;
                float horizonHaze=exp(-abs(d.y-.035)*14);
                color+=float3(.008,.012,.017)*horizonHaze;
                // Lower the resting sky exposure while retaining the lightning reveal.
                color*=.42;
                // The cloud deck lights up with the world, not just the bolt itself.
                color += float3(.38,.49,.68) * _StormLightningFlash * lerp(.55,1.0,cloud);
                float4 atmosphere=WatcherClouds(_WorldSpaceCameraPos,d,1000,_StormLightningFlash);
                color=color*(1-atmosphere.a*.2)+atmosphere.rgb*.2;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
