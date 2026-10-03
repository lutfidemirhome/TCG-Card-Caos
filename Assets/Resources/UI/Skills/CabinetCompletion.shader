Shader "TCG/Cabinet Completion"
{
    Properties
    {
        _MainTex("Surface", 2D) = "white" {}
        _SourceColor("Source Color", Color) = (1,1,1,1)
        _Progress("Progress", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _SourceColor;
                float _Progress;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float t = saturate(_Progress);
                float envelope = smoothstep(0, 0.14, t) * (1 - smoothstep(0.52, 1, t));
                half4 art = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _SourceColor;
                clip(art.a - 0.01);
                half luminance = dot(art.rgb, half3(0.2126, 0.7152, 0.0722));
                half rim = pow(1 - saturate(abs(dot(normalize(input.normalWS), GetWorldSpaceNormalizeViewDir(input.positionWS)))), 3);
                half3 gold = half3(1.4, 0.94, 0.075);
                half3 color = gold * (lerp(0.28, 1.0, sqrt(saturate(luminance))) + rim * 0.35);
                return half4(color, envelope * art.a * 0.94);
            }
            ENDHLSL
        }
    }
}
