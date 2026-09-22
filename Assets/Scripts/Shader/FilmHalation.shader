Shader "FullScreen/HDRP_FilmHalation"
{
    Properties
    {
        _HalationThreshold("Highlight Threshold", Range(0.5, 5.0)) = 1.0
        _HalationIntensity("Intensity", Range(0.0, 5.0)) = 1.8
        _HalationRadius("Scatter Radius", Range(0.001, 0.03)) = 0.007
        _HalationColor("Halation Tint", Color) = (1.0, 0.16, 0.04, 1.0)
        _SpreadFalloff("Spread Falloff", Range(1.0, 4.0)) = 2.0
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float _HalationThreshold;
    float _HalationIntensity;
    float _HalationRadius;
    float4 _HalationColor;
    float _SpreadFalloff;

    static const float2 KERNEL_SAMPLES[12] = {
        float2( 0.0,     1.0),
        float2( 0.7071,  0.7071),
        float2( 1.0,     0.0),
        float2( 0.7071, -0.7071),
        float2( 0.0,    -1.0),
        float2(-0.7071, -0.7071),
        float2(-1.0,     0.0),
        float2(-0.7071,  0.7071),
        float2( 0.3826,  0.9238) * 1.7,
        float2( 0.9238, -0.3826) * 1.7,
        float2(-0.3826, -0.9238) * 1.7,
        float2(-0.9238,  0.3826) * 1.7
    };

    float GetLuminance(float3 color)
    {
        return dot(color, float3(0.2126, 0.7152, 0.0722));
    }

    float3 ExtractHighlights(float3 color)
    {
        float lum = GetLuminance(color);
        float factor = max(lum - _HalationThreshold, 0.0);
        return color * (factor / max(lum, 0.0001));
    }

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        float2 uv = varyings.positionCS.xy * _ScreenSize.zw;
        
        // float4 yerine doğrudan float3 olarak alınıyor
        float3 baseColor = CustomPassSampleCameraColor(uv, 0);

        float2 aspectCorrection = float2(_ScreenSize.y * _ScreenSize.z, 1.0);

        float3 halationAccum = 0.0;
        float totalWeight = 0.0;

        [unroll]
        for (int i = 0; i < 12; i++)
        {
            float2 sampleOffset = (KERNEL_SAMPLES[i] * aspectCorrection) * _HalationRadius;
            float2 sampleUV = uv + sampleOffset;

            float3 sampleCol = CustomPassSampleCameraColor(sampleUV, 0);
            float3 highlight = ExtractHighlights(sampleCol);

            float dist = length(KERNEL_SAMPLES[i]);
            float weight = 1.0 / pow(dist + 0.3, _SpreadFalloff);

            halationAccum += highlight * weight;
            totalWeight += weight;
        }

        halationAccum /= max(totalWeight, 0.0001);
        float3 halationFinal = halationAccum * _HalationColor.rgb * _HalationIntensity;

        // Çıkış rengi HDRP render hedefi için float4 olarak paketleniyor
        return float4(baseColor + halationFinal, 1.0);
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "FilmHalationPass"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FullScreenPass
            ENDHLSL
        }
    }
    Fallback Off
}