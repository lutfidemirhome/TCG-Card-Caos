Shader "TCG/Shelf Completion"
{
    Properties
    {
        _MainTex("Card Art", 2D) = "white" {}
        _Progress("Progress", Float) = 0
        _Aspect("Card Aspect", Float) = 0.714
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Progress;
                float _Aspect;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float t = saturate(_Progress);
                float envelope = smoothstep(0, 0.14, t) * (1 - smoothstep(0.52, 1, t));
                float2 faceUV = (input.uv - 0.5) * 1.32 + 0.5;
                float2 p = (faceUV - 0.5) * float2(_Aspect, 1);
                float radius = 0.018;
                float2 q = abs(p) - float2(_Aspect * 0.5, 0.5) + radius;
                float distance = length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
                float face = 1 - smoothstep(-0.002, 0.002, distance);
                float haloWidth = lerp(0.028, 0.080, smoothstep(0.04, 0.55, t));
                float halo = pow(saturate(1 - max(distance, 0) / haloWidth), 2.4) * (1 - face);
                half3 art = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(faceUV) * _MainTex_ST.xy + _MainTex_ST.zw).rgb;
                half luminance = dot(art, half3(0.2126, 0.7152, 0.0722));
                half3 gold = half3(1.4, 0.94, 0.075);
                half3 color = gold * lerp(0.24, 1.0, sqrt(saturate(luminance)));
                color = lerp(gold * 1.35, color, face);
                half alpha = envelope * (face * 0.94 + halo * 0.55);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
