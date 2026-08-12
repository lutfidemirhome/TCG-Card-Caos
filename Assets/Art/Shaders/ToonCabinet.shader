Shader "TCG/Toon Cabinet"
{
    Properties
    {
        [Header(Base)]
        [MainColor] _Color ("Tint Color", Color) = (1, 1, 1, 1)
        [MainTexture] _MainTex ("Albedo", 2D) = "white" {}

        [Header(Toon Ramp)]
        _SColor ("Shadow Color", Color) = (0.55, 0.55, 0.58, 1)
        _HColor ("Highlight Color", Color) = (1, 1, 1, 0)
        _RampThreshold ("Shadow Threshold", Range(0, 1)) = 0.55
        _RampSmooth ("Shadow Smooth", Range(0.001, 0.5)) = 0.06

        [Header(Ambient Fill)]
        _AmbientBoost ("Flat Ambient Mix", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _SColor;
                half4 _HColor;
                half _RampThreshold;
                half _RampSmooth;
                half _AmbientBoost;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = normalInputs.normalWS;
                return output;
            }

            half3 ApplyToonRamp(half3 albedo, half3 normalWS, Light light)
            {
                half ndotl = dot(normalize(normalWS), light.direction);
                half shade = smoothstep(
                    _RampThreshold - _RampSmooth,
                    _RampThreshold + _RampSmooth,
                    ndotl * light.shadowAttenuation);

                half3 shadowCol = albedo * _SColor.rgb;
                half3 litCol = lerp(shadowCol, albedo, shade);
                litCol += _HColor.rgb * saturate(ndotl - (1.0h - _RampSmooth * 2.0h));
                return litCol * light.color;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb * _Color.rgb;
                half3 normalWS = normalize(input.normalWS);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 color = ApplyToonRamp(albedo, normalWS, mainLight);

                #ifdef _ADDITIONAL_LIGHTS
                uint additionalLightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < additionalLightCount; i++)
                {
                    Light light = GetAdditionalLight(i, input.positionWS);
                    color += ApplyToonRamp(albedo, normalWS, light) * 0.35h;
                }
                #endif

                half3 ambient = albedo * half3(0.75, 0.75, 0.78);
                color = lerp(color, max(color, ambient), _AmbientBoost);

                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
