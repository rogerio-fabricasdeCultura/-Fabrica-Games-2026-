Shader "Fish Run/Shield Bubble"
{
    Properties
    {
        _ShieldColor ("Shield Color", Color) = (0.12, 0.85, 1, 1)
        _RimColor ("Rim Color", Color) = (0.45, 0.95, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.18
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.55
        _CellStrength ("Hex Cell Strength", Range(0, 1)) = 0.22
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _HexScale ("Hex Scale", Range(1, 40)) = 16
        _PulseSpeed ("Pulse Speed", Range(0, 8)) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShieldColor;
                float4 _RimColor;
                float _Opacity;
                float _RimStrength;
                float _CellStrength;
                float _RimPower;
                float _HexScale;
                float _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirectionWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirectionWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirectionWS = normalize(input.viewDirectionWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirectionWS)), _RimPower);

                float2 patternPosition = input.positionWS.xz * _HexScale;
                float2 cellSize = float2(1.0, 1.7320508);
                float2 cellA = fmod(patternPosition, cellSize) - cellSize * 0.5;
                float2 cellB = fmod(patternPosition + cellSize * 0.5, cellSize) - cellSize * 0.5;
                float2 cell = dot(cellA, cellA) < dot(cellB, cellB) ? cellA : cellB;
                float edgeDistance = min(abs(cell.y), abs(abs(cell.x) * 0.8660254 + abs(cell.y) * 0.5 - 0.5));
                float hexLine = 1.0 - smoothstep(0.018, 0.055, edgeDistance);

                float pulse = 0.88 + 0.12 * sin(_Time.y * _PulseSpeed * 6.2831853);
                float alpha = saturate((_Opacity + fresnel * _RimStrength + hexLine * _CellStrength) * pulse);
                float3 color = lerp(_ShieldColor.rgb, _RimColor.rgb, saturate(fresnel + hexLine * 0.55));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
