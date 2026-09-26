Shader "TCG/Skill Marker"
{
    Properties { _BaseColor("Color", Color) = (0.25, 1, 0.45, 1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input) { Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz); return output; }
            half4 frag(Varyings input) : SV_Target { return _BaseColor; }
            ENDHLSL
        }
    }
}
