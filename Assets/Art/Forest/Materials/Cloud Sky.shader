Shader "TGDC/Cloud Sky"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Cloud painting", 2D) = "white" {}
        _Drift ("Drift speed", Range(0, 2)) = 0.65
        _Distortion ("Ripple amount", Range(0, 0.03)) = 0.009
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _Drift, _Distortion;
            CBUFFER_END
            struct Output { float4 pos : SV_POSITION; float3 ray : TEXCOORD0; };
            Output Vert(float3 vertex : POSITION)
            {
                Output o;
                o.pos = TransformObjectToHClip(vertex);
                o.ray = vertex;
                return o;
            }
            half4 Frag(Output i) : SV_Target
            {
                float3 ray = normalize(i.ray);
                float time = _Time.y * _Drift;
                float ripple = sin(dot(ray, float3(6, 8, 3)) + time * 0.23);
                float u = frac(atan2(ray.z, ray.x) / PI + time * 0.0025 + ripple * _Distortion);
                float v = saturate(0.28 + asin(clamp(ray.y, -1, 1)) * 0.64
                    + sin(ray.x * 5 - ray.z * 4 + time * 0.19) * _Distortion);
                // Overlap the two edges of the painting and converge on one brush colour at the poles.
                float pole = smoothstep(0.86, 1.0, abs(ray.y));
                float2 uv = float2(lerp(0.12 + u * 0.88, 0.5, pole), v);
                half3 paint = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                float join = smoothstep(0.86, 1.0, u);
                uv.x = lerp(saturate((u - 0.86) / 0.14) * 0.12, 0.5, pole);
                half3 edge = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                return half4(lerp(paint, edge, join), 1);
            }
            ENDHLSL
        }
    }
}
