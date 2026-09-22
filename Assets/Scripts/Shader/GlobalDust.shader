Shader "FullScreen/HDRP_GlobalDust"
{
    Properties
    {
        _DustColor("Dust Color", Color) = (0.5, 0.45, 0.4, 1.0)
        _DustIntensity("Dust Intensity", Range(0.0, 2.0)) = 1.0
        _DustScale("Dust Pattern Scale", Range(0.1, 50.0)) = 15.0
        _UpThreshold("Upward Angle (0=Her yer, 1=Sadece tepe)", Range(0.0, 1.0)) = 0.6
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"
    
    // EKLENEN KÜTÜPHANE: HDRP'nin Normal haritasını okumak için gereken çekirdek kütüphanesi
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl"

    float4 _DustColor;
    float _DustIntensity;
    float _DustScale;
    float _UpThreshold;

    float Hash(float2 p) {
        return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
    }
    float Noise(float2 p) {
        float2 i = floor(p);
        float2 f = frac(p);
        f = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(Hash(i + float2(0,0)), Hash(i + float2(1,0)), f.x),
                    lerp(Hash(i + float2(0,1)), Hash(i + float2(1,1)), f.x), f.y);
    }

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        float2 uv = varyings.positionCS.xy * _ScreenSize.zw;
        float depth = SampleCameraDepth(uv);
        
        float3 sceneColor = CustomPassSampleCameraColor(uv, 0);

        #if UNITY_REVERSED_Z
            if (depth == 0.0) return float4(sceneColor, 1.0);
        #else
            if (depth == 1.0) return float4(sceneColor, 1.0);
        #endif

        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
        
        // DÜZELTİLEN KISIM: Normal Buffer'dan veriyi manuel olarak çekip çözümlüyoruz
        NormalData normalData;
        DecodeFromNormalBuffer(uint2(varyings.positionCS.xy), normalData);
        float3 normalWS = normalData.normalWS;

        // Maske ve Toz hesaplamaları
        float upFactor = saturate(dot(normalWS, float3(0, 1, 0)));
        float dustMask = smoothstep(_UpThreshold, 1.0, upFactor);

        float noiseVal = Noise(posInput.positionWS.xz * _DustScale);
        float finalDustAmount = dustMask * noiseVal * _DustIntensity;

        float lum = dot(sceneColor, float3(0.2126, 0.7152, 0.0722));
        float3 adjustedDustColor = _DustColor.rgb * (lum + 0.1); 

        float3 finalColor = lerp(sceneColor, adjustedDustColor, finalDustAmount);

        return float4(finalColor, 1.0);
    }
    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "GlobalDustPass"
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