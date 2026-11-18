Shader "Custom/URP_IceOverlay"
{
    Properties
    {
        _BaseMap ("Ice Texture", 2D) = "white" {}
        _IceColor ("Ice Tint", Color) = (0.68, 0.92, 1.0, 1.0)
        _RimColor ("Rim Frost Color", Color) = (0.94, 0.99, 1.0, 1.0)
        _RimPower ("Rim Power", Range(0.5, 6.0)) = 2.0
        _RimIntensity ("Rim Intensity", Range(0.0, 2.0)) = 1.2
        _CenterAlpha ("Center Opacity", Range(0.0, 1.0)) = 0.22
        _TextureAlpha ("Texture Frost Weight", Range(0.0, 1.0)) = 0.62
        _ShimmerSpeed ("Shimmer Speed", Range(0.0, 4.0)) = 0.85
        _ShimmerIntensity ("Shimmer Intensity", Range(0.0, 2.0)) = 0.5
        _MeltProgress ("Melt Progress", Range(0.0, 1.0)) = 0.0
        _MeltEdgeWidth ("Melt Edge Width", Range(0.01, 0.25)) = 0.075
        _MeltEdgeColor ("Melt Edge Color", Color) = (0.95, 1.0, 1.0, 1.0)
        _MeltAxis ("Melt Axis (0=X,1=Y,2=Z)", Float) = 1.0
        _BoundsMin ("Bounds Min (Melt Axis)", Float) = -0.5
        _BoundsMax ("Bounds Max (Melt Axis)", Float) = 0.5
        _FlashAmount ("Flash Amount", Range(0.0, 1.0)) = 0.0
        _AlphaMultiplier ("Alpha Multiplier", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "UnlitIceOverlay"
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS   : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _IceColor;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half _CenterAlpha;
                half _TextureAlpha;
                half _ShimmerSpeed;
                half _ShimmerIntensity;
                half _MeltProgress;
                half _MeltEdgeWidth;
                half4 _MeltEdgeColor;
                half _MeltAxis;
                half _BoundsMin;
                half _BoundsMax;
                half _FlashAmount;
                half _AlphaMultiplier;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = normInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Normalized position along the melt axis (0 = bottom/anchor, 1 = top/melts first)
                float axisCoord = (_MeltAxis < 0.5) ? input.positionOS.x : ((_MeltAxis < 1.5) ? input.positionOS.y : input.positionOS.z);
                float normPos = saturate((axisCoord - _BoundsMin) / max(0.0001, _BoundsMax - _BoundsMin));

                // 2. Organic wavy melt boundary
                float wave = sin(input.uv.x * 16.0 + input.uv.y * 8.0) * 0.032 +
                             sin(input.uv.x * 31.0 - input.uv.y * 19.0) * 0.014;
                float meltThreshold = 1.0 - _MeltProgress;

                // Keep full coverage when _MeltProgress <= 0.001
                float iceMask = (_MeltProgress <= 0.001)
                    ? 1.0
                    : (1.0 - smoothstep(meltThreshold - 0.02, meltThreshold + 0.015, normPos + wave));

                float edgeLine = (_MeltProgress <= 0.001)
                    ? 0.0
                    : smoothstep(meltThreshold - _MeltEdgeWidth, meltThreshold - 0.008, normPos + wave) * iceMask;

                // 3. Fresnel rim frost (keeps center clear while highlighting edges)
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float ndotv = saturate(dot(normalize(input.normalWS), viewDirWS));
                float fresnel = pow(1.0 - ndotv, _RimPower) * _RimIntensity;

                // 4. Subtle animated diagonal ice shimmer
                float diag = input.uv.x + input.uv.y;
                float shimmerWave = frac(diag * 0.5 - _Time.y * _ShimmerSpeed * 0.35);
                float shimmer = smoothstep(0.42, 0.5, shimmerWave) * (1.0 - smoothstep(0.5, 0.58, shimmerWave)) * _ShimmerIntensity;

                // 5. Sample ice frost/cracks texture
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                half3 color = _IceColor.rgb * tex.rgb;
                color += _RimColor.rgb * fresnel;
                color += half3(1.0, 1.0, 1.0) * shimmer;
                color += _MeltEdgeColor.rgb * edgeLine * 0.85;
                color += half3(1.0, 1.0, 1.0) * _FlashAmount * 0.6;

                half alpha = saturate(_CenterAlpha + tex.a * _TextureAlpha + fresnel * 0.65 + edgeLine * 0.55 + shimmer * 0.35 + _FlashAmount * 0.35);
                alpha *= iceMask * _AlphaMultiplier;

                clip(alpha - 0.01);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
