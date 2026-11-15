Shader "TGDC/Forest Canopy"
{
    Properties
    {
        [PerRendererData] _MainTex ("Painting", 2D) = "white" {}
        _WindStrength ("Crown movement (metres)", Range(0, 0.20)) = 0.08
        _WindSpeed ("Wind speed", Range(0, 3)) = 0.8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _WindStrength, _WindSpeed;
            CBUFFER_END
            struct Input { float3 pos : POSITION; float2 uv : TEXCOORD0; float3 motion : TEXCOORD1; };
            struct Output { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half shade : TEXCOORD1; float depth : TEXCOORD2; };
            Output Vert(Input v)
            {
                Output o;
                float3 p = TransformObjectToWorld(v.pos);
                p.xz += normalize(UNITY_MATRIX_V[0].xz + float2(0.00001, 0)) * v.motion.x;
                float phase = dot(p.xz, float2(0.43, 0.31)) + _Time.y * _WindSpeed;
                float breeze = sin(phase) + 0.3 * sin(1.7 * phase + p.y);
                p.xz += float2(0.84, 0.54) * breeze * _WindStrength * v.motion.y;
                o.pos = TransformWorldToHClip(p);
                o.uv = v.uv;
                o.shade = v.motion.z;
                o.depth = -TransformWorldToView(p).z;
                return o;
            }
            half4 Frag(Output i) : SV_Target
            {
                half4 paint = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(paint.a - 0.42h);
                half fog = ComputeFogFactorZ0ToFar(max(0, i.depth - _ProjectionParams.y));
                return half4(MixFog(paint.rgb * i.shade, fog), 1);
            }
            ENDHLSL
        }
    }
}
