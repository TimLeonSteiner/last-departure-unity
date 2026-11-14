Shader "Hidden/Dustline/TerminalBounce"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
        TEXTURE2D_X(_BounceTexture);
        float _BounceStrength, _BounceRadius;
        bool Surface(float2 uv, out float3 p)
        {
            float depth = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                bool valid = depth > .00001;
            #else
                bool valid = depth < .99999;
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
            #endif
            p = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
            return valid;
        }
        bool InRoom(float3 p) { return max(abs(p.x), abs(p.z)) < 24 && p.y >= 0 && p.y < 8.5; }
        float EyeDepth(float3 p) { return -TransformWorldToView(p).z; }
        half4 Trace(Varyings i) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            float3 p;
            if (!Surface(i.texcoord, p) || !InRoom(p)) return 0;
            float3 n = normalize(SampleSceneNormals(i.texcoord));
            float3 tangent = normalize(cross(n, abs(n.y) < .9 ? float3(0,1,0) : float3(1,0,0)));
            float3 bitangent = cross(n, tangent);
            float3 total = 0;
            // Stable world-space rotation breaks up radial bands without temporal ghosting.
            float rotation = frac(sin(dot(floor(p*32),float3(12.9898,78.233,37.719)))*43758.5453)*6.283185;
            const int rays = 12;
            [loop] for (int r = 0; r < rays; r++)
            {
                float u = (r + .5) / rays;
                float angle = r * 2.39996323 + rotation;
                float3 direction = tangent * (cos(angle) * sqrt(u)) +
                    bitangent * (sin(angle) * sqrt(u)) + n * sqrt(1-u);
                float3 origin = p + n * .08;
                [loop] for (int step = 1; step <= 24; step++)
                {
                    float distance = _BounceRadius * pow(step / 24.0, 1.5);
                    float3 probe = origin + direction * distance;
                    float4 clip = TransformWorldToHClip(probe);
                    if (clip.w <= 0) break;
                    float2 uv = ComputeScreenPos(clip).xy / clip.w;
                    if (any(uv < .002) || any(uv > .998)) break;
                    float3 hit;
                    if (!Surface(uv, hit)) continue;
                    float gap = EyeDepth(probe) - EyeDepth(hit);
                    if (gap > 0)
                    {
                        // Stop at the first visible obstruction; do not march through walls.
                        if (gap < .35 && distance > .18 && InRoom(hit) &&
                            dot(SampleSceneNormals(uv), -direction) > .05)
                        {
                            float edge = saturate(min(min(uv.x,uv.y),min(1-uv.x,1-uv.y)) * 20);
                            float3 radiance = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
                            total += min(radiance, 4) * edge * saturate(1-distance/_BounceRadius);
                        }
                        break;
                    }
                }
            }
            // Neutral receiver reflectance is an approximation; this is not full material-aware RTGI.
            return half4(total * (_BounceStrength / rays), 1);
        }
        half4 Composite(Varyings i) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            half4 direct = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, i.texcoord, 0);
            float3 p;
            if (!Surface(i.texcoord, p) || !InRoom(p)) return direct;
            float3 normal = normalize(SampleSceneNormals(i.texcoord));
            float3 sum = 0; float weightSum = 0;
            [unroll] for (int y=-1; y<=1; y++)
                [unroll] for (int x=-1; x<=1; x++)
                {
                    float2 uv = i.texcoord + float2(x,y) * 2 / _ScaledScreenParams.xy;
                    float3 q;
                    if (!Surface(uv,q) || !InRoom(q)) continue;
                    float weight = exp(-abs(EyeDepth(p)-EyeDepth(q))*12) *
                        pow(saturate(dot(normal,SampleSceneNormals(uv))),16);
                    sum += SAMPLE_TEXTURE2D_X_LOD(_BounceTexture,sampler_LinearClamp,uv,0).rgb * weight;
                    weightSum += weight;
                }
            return half4(direct.rgb + sum / max(weightSum,.0001), direct.a);
        }
        ENDHLSL
        Pass
        {
            Name "Trace"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Trace
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
}
