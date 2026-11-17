Shader "Custom/URP_Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.03)) = 0.005
        [Toggle] _UseRadialExtrusion ("Use 3D Radial Extrusion", Float) = 0
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
                float _UseRadialExtrusion;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 extrudeDir;
                if (_UseRadialExtrusion > 0.5)
                {
                    // Full 3D radial extrusion from object center: prevents hard-edge corner splitting on 3D dice/cubes
                    float posLen = length(input.positionOS.xyz);
                    extrudeDir = (posLen > 0.0001) ? (input.positionOS.xyz / posLen) : float3(0, 0, 0);
                }
                else
                {
                    // Flat XZ-plane normal extrusion for cards
                    extrudeDir = float3(input.normalOS.x, 0.0, input.normalOS.z);
                    float len = length(extrudeDir.xz);
                    extrudeDir = (len > 0.001) ? (extrudeDir / len) : float3(0, 0, 0);
                }

                // Distance-compensated outline width
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float dist = length(posWS - _WorldSpaceCameraPos.xyz);
                float offset = _OutlineWidth * dist;

                float3 extrudedPosOS = input.positionOS.xyz + extrudeDir * offset;

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
