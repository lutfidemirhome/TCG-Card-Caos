Shader "TCG/Insight Beam"
{
    Properties
    {
        _BaseColor("Glow Color", Color) = (0.3, 1, 0.55, 1)
    }
    SubShader
    {
        // Object origin is the emitter; dynamic batching must not replace it with world-space vertices.
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float phase : TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                float3 viewRight = UNITY_MATRIX_V[0].xyz;
                float3 right = float3(viewRight.x, 0, viewRight.z);
                right *= rsqrt(max(dot(right, right), 0.0001));
                // A vertical ribbon, about 1.2 m tall, rooted just above the card.
                float3 positionWS = origin + right * (input.positionOS.x * 0.34)
                    + float3(0, 0.025 + input.positionOS.y * 1.2, 0);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.phase = frac(dot(origin, float3(1.37, 3.19, 2.71)));
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y;
                float x = abs(uv.x - 0.5) * 2;
                float taper = 1 - smoothstep(0.25, 1, uv.y);
                float beam = pow(saturate(1 - x), 3) * taper;
                float stream = 0.85 + 0.15 * sin(uv.y * 22 - time * 3 + input.phase * 6.28);
                float sparkles = 0;
                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    float seed = input.phase + i * 0.137;
                    float y = frac(time * (0.16 + i * 0.012) + seed);
                    float sx = 0.5 + 0.29 * sin(seed * 19.1 + time * 0.7);
                    float2 d = abs(uv - float2(sx, y));
                    float core = saturate(1 - length(d / float2(0.028, 0.01)));
                    float star = saturate(1 - d.x / 0.01 - d.y / 0.035)
                        + saturate(1 - d.x / 0.075 - d.y / 0.004);
                    float fade = smoothstep(0, 0.12, y) * (1 - smoothstep(0.72, 1, y));
                    sparkles += (core + star * 0.8) * fade;
                }
                float opacity = saturate(beam * stream * 0.42 + sparkles * 0.95);
                half3 color = lerp(_BaseColor.rgb, half3(0.9, 1, 0.85), saturate(sparkles));
                return half4(color, opacity * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
