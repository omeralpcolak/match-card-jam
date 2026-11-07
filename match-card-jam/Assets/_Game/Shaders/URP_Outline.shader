Shader "Custom/URP_Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.03)) = 0.005
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+10"
        }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On
            ZTest LEqual
            Offset 1, 1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                // 1. Kartın kalınlık (Y) eksenindeki genişlemeyi sıfırla!
                // Kart sadece X ve Z (çevre ve yuvarlak köşeler) boyunca genişlemelidir.
                // Eğer Y genişlerse kart balon gibi şişer ve altındaki/üstündeki kartların içine saplanır.
                float3 normal = input.normalOS;
                normal.y = 0.0;

                float len = length(normal.xz);
                if (len > 0.001)
                {
                    normal.xz /= len;
                }
                else
                {
                    normal = float3(0, 0, 0);
                }

                // 2. Kamera mesafesine göre genişlik telafisi
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float dist = length(posWS - _WorldSpaceCameraPos.xyz);
                float offset = _OutlineWidth * dist;

                // 3. Yalnızca kartın çevresi boyunca dışa doğru genişlet
                float3 extrudedPosOS = input.positionOS.xyz + normal * offset;

                output.positionCS = TransformObjectToHClip(extrudedPosOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
