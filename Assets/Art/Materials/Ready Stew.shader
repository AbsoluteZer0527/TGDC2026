Shader "TGDC/Ready Stew"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _SteamSway ("Steam sway (texels)", Range(0, 12)) = 7
        _Glow ("Surface glow", Range(0, 0.3)) = 0.14
        [Toggle(_SCORCHED_STEW)] _Scorched ("Scorched stew", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _SCORCHED_STEW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                float _SteamSway, _Glow;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 painting : TEXCOORD1;
                half4 color : COLOR;
            };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.painting = v.positionOS.xy;
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                // The wisps sit above the middle of the painted sprite.
                float steam = smoothstep(0.0, 0.9, i.painting.y);
                float phase = 5.0 * i.painting.y + 2.0 * i.painting.x - 3.0 * _Time.y;
                float2 uv = i.uv;
                uv.x += steam * sin(phase) * _SteamSway * _MainTex_TexelSize.x;
                half4 paint = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * i.color;
#if defined(_SCORCHED_STEW)
                float time = _Time.y;
                float ember = 0.5 + 0.5 * sin(3.2 * time + 4.0 * i.painting.x + sin(2.1 * time));
                float warmPaint = saturate((paint.r - paint.b) * 7.0);
                paint.rgb += half3(0.16, 0.036, 0.006) * warmPaint * ember;

                // Three soft charcoal wisps rise from the painted surface and fade at their tips.
                float smoke = 0;
                for (int n = 0; n < 3; n++)
                {
                    float height = (i.painting.y - 0.10) / (0.62 + 0.09 * sin(1.7 * time + n));
                    float center = (n - 1) * 0.64 + height * 0.12 * sin(6.0 * height - 2.1 * time + n * 2.4);
                    float width = 0.025 + 0.065 * saturate(height);
                    float stroke = 1.0 - smoothstep(width * 0.3, width, abs(i.painting.x - center));
                    smoke += stroke * smoothstep(0.0, 0.18, height) * (1.0 - smoothstep(0.42, 1.0, height));
                }
                float smokeAlpha = saturate(smoke) * 0.58;
                float alpha = smokeAlpha + paint.a * (1.0 - smokeAlpha);
                paint.rgb = (half3(0.17, 0.135, 0.12) * smokeAlpha + paint.rgb * paint.a * (1.0 - smokeAlpha)) / max(alpha, 0.0001);
                paint.a = alpha;
#else
                float pulse = 0.5 + 0.5 * sin(2.8 * _Time.y + 1.4 * i.painting.x);
                paint.rgb += paint.rgb * half3(1.0, 0.65, 0.2) * _Glow * pulse * step(i.painting.y, 0.0);
#endif
                return paint;
            }
            ENDHLSL
        }
    }
}
