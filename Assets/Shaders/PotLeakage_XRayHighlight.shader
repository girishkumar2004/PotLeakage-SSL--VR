Shader "Vedanta/PotLeakage_XRayHighlight"
{
    // See-through pulsing highlight: drawn on top of everything so a target sitting inside
    // or behind equipment (e.g. the CBT bar in the beam hole) is always visible.
    Properties
    {
        _HighlightColor ("Highlight Color", Color) = (0.2, 1.0, 0.35, 0.85)
        _PulseSpeed ("Pulse Speed", Range(0.0, 10.0)) = 3.0
        _PulseMin ("Pulse Minimum", Range(0.0, 1.0)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "XRayHighlightPass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            CBUFFER_START(UnityPerMaterial)
                float4 _HighlightColor;
                float _PulseSpeed;
                float _PulseMin;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float pulse = _PulseMin + (1.0 - _PulseMin) * (0.5 * (sin(_Time.y * _PulseSpeed) + 1.0));
                half4 c = _HighlightColor;
                c.a *= pulse;
                return c;
            }
            ENDHLSL
        }
    }
}
