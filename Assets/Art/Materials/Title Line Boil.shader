Shader "TGDC/UI/Title Line Boil"
{
    Properties
    {
        [PerRendererData] _MainTex ("Menu artwork", 2D) = "white" {}
        _Amplitude ("Wobble (texels)", Range(0, 2)) = 1
        _FrameRate ("Drawings per second", Range(1, 12)) = 6
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                float _Amplitude;
                float _FrameRate;
            CBUFFER_END

            struct Attributes
            {
                float3 position : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = TransformObjectToHClip(input.position);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Cycle through three held drawings, with neighboring strokes moving together.
                float phase = fmod(floor(_Time.y * _FrameRate), 3.0) * 2.0943951;
                float2 pixel = input.uv * _MainTex_TexelSize.zw;
                float2 wobble = 0.65 * sin(pixel.yx * 0.10 + phase);
                wobble += 0.35 * sin((pixel.xy + pixel.yx * 0.5) * 0.19 - phase);
                float2 uv = input.uv + wobble * _Amplitude * _MainTex_TexelSize.xy;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * input.color;
            }
            ENDHLSL
        }
    }
}
