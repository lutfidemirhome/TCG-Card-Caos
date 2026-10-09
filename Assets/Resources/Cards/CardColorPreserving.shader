Shader "TCG/Card Color Preserving"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [HideInInspector] _CardBackMap("Card Back", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Cutoff("AlphaCutout", Range(0, 1)) = 0.5
        _Surface("__surface", Float) = 0
        _Blend("__mode", Float) = 0
        _Cull("__cull", Float) = 2
        [ToggleUI] _AlphaClip("__clip", Float) = 0
        [HideInInspector] _BlendOp("__blendop", Float) = 0
        [HideInInspector] _SrcBlend("__src", Float) = 1
        [HideInInspector] _DstBlend("__dst", Float) = 0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0
        [HideInInspector] _ZWrite("__zw", Float) = 1
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0
        _QueueOffset("Queue offset", Float) = 0
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _SampleGI("SampleGI", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "IgnoreProjector"="True" "UniversalMaterialType"="Unlit" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
        ZWrite [_ZWrite]
        Cull [_Cull]

        Pass
        {
            Name "Unlit"
            AlphaToMask [_AlphaToMask]
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex UnlitPassVertex
            #pragma fragment CardArtFragment
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAMODULATE_ON
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma shader_feature_local _CARD_SINGLE_PASS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/UnlitInput.hlsl"
            #define UnlitPassFragment OriginalUnlitPassFragment
            #include "CardColorPreservingForward.hlsl"
            #undef UnlitPassFragment

            // Globals keep UnityPerMaterial identical to the stock depth/normals passes
            // and preserve SRP batching and GPU instancing.
            float _CardArtGradingEnabled;
            float4 _CardArtInverseGrading;

            void CardArtFragment(Varyings input, out half4 outColor : SV_Target0
                #ifdef _WRITE_RENDERING_LAYERS
                , out float4 outRenderingLayers : SV_Target1
                #endif
            )
            {
                OriginalUnlitPassFragment(input, outColor
                    #ifdef _WRITE_RENDERING_LAYERS
                    , outRenderingLayers
                    #endif
                );
                UNITY_BRANCH
                if (_CardArtGradingEnabled > 0.5)
                {
                    float3 color = outColor.rgb;
                    float luma = dot(color, float3(0.2126729, 0.7151522, 0.0721750));
                    color = max(0.0, luma + (color - luma) * _CardArtInverseGrading.y);
                    color = (pow(color * 5.555556 + 0.047996, _CardArtInverseGrading.x)
                        * _CardArtInverseGrading.z - 0.047996) * (0.18 * _CardArtInverseGrading.w);
                    outColor.rgb = max(0.0, color);
                }
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Unlit/DepthOnly"
        UsePass "Universal Render Pipeline/Unlit/DepthNormalsOnly"
        UsePass "Universal Render Pipeline/Unlit/Meta"
        UsePass "Universal Render Pipeline/Unlit/MotionVectors"
        UsePass "Universal Render Pipeline/Unlit/XRMotionVectors"
    }
    FallBack "Universal Render Pipeline/Unlit"
}
