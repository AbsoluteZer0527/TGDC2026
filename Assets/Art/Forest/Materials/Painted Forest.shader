Shader "TGDC/Painted Forest"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _BaseMap ("Ground Painting", 2D) = "white" {}
        _TileSize ("Tile Size (m)", Range(0.25, 24)) = 4
        _GrassShade ("Grass Green", Color) = (0.1647, 0.2510, 0.2118, 1)
        _GrassLight ("Grass Olive", Color) = (0.3216, 0.3490, 0.2824, 1)
        _FlowerBlue ("Flower Blue", Color) = (0.1373, 0.2039, 0.3255, 1)
        _FlowerPale ("Flower Off-White", Color) = (0.4510, 0.4824, 0.5216, 1)
        _FlowerRed ("Flower Red", Color) = (0.2902, 0.1765, 0.1490, 1)
        [HideInInspector] _IsGrass ("Grass geometry", Float) = 0
        [HideInInspector] _BladeHeight ("Blade height", Float) = 0.19
        [HideInInspector] _WindStrength ("Wind strength", Float) = 0.24
        [HideInInspector] _DrawDistance ("Draw distance", Float) = 24
        [HideInInspector] _Cull ("Shadow culling", Float) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float _TileSize, _IsGrass, _BladeHeight, _WindStrength, _DrawDistance;
            half4 _GrassShade, _GrassLight, _FlowerBlue, _FlowerPale, _FlowerRed;
        CBUFFER_END

        struct Attributes
        {
            float3 positionOS : POSITION;
            // On grass meshes: root XZ, weight from root to tip, palette index.
            float4 blade : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float2 paintXZ : TEXCOORD1;
            half4 tint : TEXCOORD2;
        };

        Varyings ForestVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            float3 positionWS = TransformObjectToWorld(input.positionOS);
            output.paintXZ = positionWS.xz;
            if (_IsGrass > 0.5)
            {
                float3 rootOS = float3(input.blade.x, 0, input.blade.y);
                float3 rootWS = TransformObjectToWorld(rootOS);
                float3 bladeWS = mul((float3x3)unity_ObjectToWorld, input.positionOS - rootOS) * _BladeHeight;
                float weight = input.blade.z;
                float wave = sin(dot(rootWS.xz, float2(0.8, 0.55)) + _Time.y * 1.7)
                           + 0.28 * sin(dot(rootWS.xz, float2(-1.4, 1.9)) + _Time.y * 2.6);
                bladeWS.xz += float2(0.89, 0.45) * wave * _WindStrength * _BladeHeight * weight * weight;
                float fade = 1.0 - smoothstep(_DrawDistance * 0.65, _DrawDistance, distance(rootWS, GetCameraPositionWS()));
                positionWS = rootWS + bladeWS * fade;
                output.paintXZ = rootWS.xz;

                float palette = input.blade.w;
                half3 tint = lerp(_GrassShade.rgb, _GrassLight.rgb, saturate(palette));
                if (palette > 1.5)
                    tint = palette < 2.5 ? _FlowerBlue.rgb : (palette < 3.5 ? _FlowerPale.rgb : _FlowerRed.rgb);
                output.tint = half4(tint, weight);
            }
            output.positionWS = positionWS;
            output.positionCS = TransformWorldToHClip(positionWS);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "ForestForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ForestVertex
            #pragma fragment ForestFragment
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half3 SamplePatch(float2 cell, float2 offset, float2 dx, float2 dy)
            {
                // Stay inside the artwork even when sampling across a shared border.
                float3 hash = frac(float3(cell.xyx) * float3(0.1031, 0.1030, 0.0973));
                hash += dot(hash, hash.yzx + 33.33);
                hash = frac((hash.xxy + hash.yzz) * hash.zyx);
                if (hash.z > 0.5)
                {
                    offset = offset.yx;
                    dx = dx.yx;
                    dy = dy.yx;
                }
                float2 scale = (step(0.5, hash.xy) * 2.0 - 1.0) * 0.70;
                float2 uv = 0.5 + offset * scale + (hash.yz - 0.5) * 0.14;
                return SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, uv, dx * scale, dy * scale).rgb;
            }

            half3 GroundPaint(float2 worldXZ)
            {
                float2 tile = worldXZ / max(_TileSize, 0.01);
                // Derivatives of the continuous coordinates keep mip levels stable at tile edges.
                float2 dx = ddx(tile), dy = ddy(tile);
                float2 cell = floor(tile);
                float2 offset = frac(tile) - 0.5;
                float2 neighbor = sign(offset);
                float2 blend = smoothstep(0.40, 0.60, abs(offset));
                half3 row = SamplePatch(cell, offset, dx, dy);
                if (blend.x > 0)
                    row = lerp(row, SamplePatch(cell + float2(neighbor.x, 0), offset - float2(neighbor.x, 0), dx, dy), blend.x);
                if (blend.y > 0)
                {
                    float2 nextCell = cell + float2(0, neighbor.y);
                    float2 nextOffset = offset - float2(0, neighbor.y);
                    half3 nextRow = SamplePatch(nextCell, nextOffset, dx, dy);
                    if (blend.x > 0)
                        nextRow = lerp(nextRow, SamplePatch(cell + neighbor, offset - neighbor, dx, dy), blend.x);
                    row = lerp(row, nextRow, blend.y);
                }
                return row;
            }

            half4 ForestFragment(Varyings input) : SV_Target
            {
                half3 paint = lerp(GroundPaint(input.paintXZ), input.tint.rgb, input.tint.a);
                half3 up = half3(0, 1, 0);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 light = SampleSH(up) + sun.color * saturate(dot(up, sun.direction)) * sun.shadowAttenuation;
                float eyeDepth = -TransformWorldToView(input.positionWS).z;
                half fog = ComputeFogFactorZ0ToFar(max(0.0, eyeDepth - _ProjectionParams.y));
                return half4(MixFog(paint * light, fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex ForestVertex
            #pragma fragment DepthFragment
            half DepthFragment(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma vertex ForestVertex
            #pragma fragment NormalFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalFragment(Varyings input) : SV_Target
            {
                float3 normalWS = float3(0, 1, 0);
                #if defined(_GBUFFER_NORMALS_OCT)
                    return half4(PackFloat2To888(PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5), 0);
                #else
                    return half4(normalWS, 0);
                #endif
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
