Shader "Ocean Motion/Subproject 05/Water Wireframe"
{
    Properties
    {
        _BaseColor("Water Color", Color) = (0.025, 0.22, 0.38, 1)
        _LineColor("Grid Color", Color) = (0.2, 0.9, 1, 1)
        _GridDensity("Grid Density", Range(4, 256)) = 64
        _LineWidth("Line Width", Range(0.5, 3)) = 1.25
        _FresnelStrength("Edge Highlight", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Wireframe Water"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _LineColor;
                float _GridDensity;
                float _LineWidth;
                float _FresnelStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 gridCoordinates = input.uv * _GridDensity;
                float2 distanceToLine = abs(frac(gridCoordinates - 0.5) - 0.5);
                float2 antiAliasWidth = max(fwidth(gridCoordinates) * _LineWidth, 0.0001);
                float2 cellInterior = smoothstep(antiAliasWidth, antiAliasWidth * 1.5, distanceToLine);
                float gridLine = 1.0 - min(cellInterior.x, cellInterior.y);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirectionWS)), 3.0);
                float normalBrightness = lerp(0.72, 1.08, saturate(normalWS.y));

                float3 waterColor = _BaseColor.rgb * normalBrightness;
                waterColor += _LineColor.rgb * fresnel * _FresnelStrength;
                float3 finalColor = lerp(waterColor, _LineColor.rgb, gridLine * _LineColor.a);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
