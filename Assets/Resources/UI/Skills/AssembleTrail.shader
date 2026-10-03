Shader "TCG/Assemble Trail"
{
    Properties
    {
        _Sparkle("Sparkle Shape", Float) = 0
        _Arrival("Arrival Flash", Float) = 0
        _ArrivalTime("Arrival Progress", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
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
                half _Sparkle;
                half _Arrival;
                float _ArrivalTime;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                if (_Arrival > 0.5)
                {
                    float t = saturate(_ArrivalTime);
                    float envelope = smoothstep(0, 0.16, t) * (1 - smoothstep(0.2, 1, t));
                    float2 q = abs(input.uv - 0.5) - 0.44;
                    float edgeDistance = length(max(q, 0)) + min(max(q.x, q.y), 0) - 0.06;
                    float mask = 1 - smoothstep(-0.015, 0.008, edgeDistance);
                    float rim = smoothstep(-0.1, -0.018, edgeDistance) * mask;
                    float sweep = 1 - smoothstep(0.02, 0.16,
                        abs(input.uv.x + input.uv.y * 0.35 - lerp(-0.2, 1.55, t)));
                    float alpha = (mask * 0.18 + rim * 0.46 + sweep * mask * 0.26) * envelope;
                    return half4(lerp(half3(1, 0.96, 0.74), half3(1, 0.73, 0.28), t), alpha);
                }
                float2 p = abs(input.uv * 2 - 1);
                float ribbon = pow(saturate(1 - p.y), 2);
                float glow = pow(saturate(1 - length(p)), 2);
                float star = pow(saturate(1 - p.x), 14) * pow(saturate(1 - p.y), 2)
                    + pow(saturate(1 - p.y), 14) * pow(saturate(1 - p.x), 2);
                float alpha = lerp(ribbon, saturate(glow * 0.5 + star * 0.8), _Sparkle);
                return half4(input.color.rgb, input.color.a * alpha);
            }
            ENDHLSL
        }
    }
}
