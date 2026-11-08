// Unlit soft-blob particle for HDRP: rendered through HDRP's SRPDefaultUnlit compatibility pass, tinted by the
// particle's vertex colour, alpha falls off from the quad centre so no texture is needed (cigarette smoke, ember).
Shader "Senzora/Soft Particle Unlit"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Softness ("Edge Softness", Range(0.01, 0.5)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            float4 _Color;
            float _Softness;

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float d = distance(input.uv, float2(0.5, 0.5));
                float alpha = smoothstep(0.5, 0.5 - _Softness, d);
                return float4(input.color.rgb, input.color.a * alpha);
            }
            ENDHLSL
        }
    }
}
