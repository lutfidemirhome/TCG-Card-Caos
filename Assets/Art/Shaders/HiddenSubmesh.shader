Shader "TCG/HiddenSubmesh"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-1" }

        Pass
        {
            ColorMask 0
            ZWrite Off
            Cull Off
        }
    }
}
