Shader "FullScreen/HDRP_GateWeave"
{
    Properties
    {
        _WeaveSpeed("Weave Speed (FPS)", Range(1.0, 60.0)) = 24.0
        _WeaveAmplitudeX("Horizontal Jitter", Range(0.0, 0.01)) = 0.001
        _WeaveAmplitudeY("Vertical Jitter", Range(0.0, 0.01)) = 0.0015
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float _WeaveSpeed;
    float _WeaveAmplitudeX;
    float _WeaveAmplitudeY;

    // Zaman ve eksen bazlı rastgele sapma (Noise) üreten fonksiyon
    float Hash(float2 p)
    {
        return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
    }

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        // Ekranın standart UV koordinatını al
        float2 uv = varyings.positionCS.xy * _ScreenSize.zw;

        // Zamanı akıcı değil, film makinesi gibi kare kare (snapped) ilerlet
        float timeSnap = floor(_Time.y * _WeaveSpeed);

        // X ve Y eksenlerinde birbirinden bağımsız, mekanik dişli sarsıntısı hesapla
        float offsetX = (Hash(float2(timeSnap, 0.0)) - 0.5) * _WeaveAmplitudeX;
        float offsetY = (Hash(float2(timeSnap, 1.0)) - 0.5) * _WeaveAmplitudeY;

        // Orijinal UV'ye bu sapmayı ekle
        float2 distortedUV = uv + float2(offsetX, offsetY);

        // Kameranın görüntüsünü titretilmiş (kaydırılmış) koordinatlardan çek
        float3 color = CustomPassSampleCameraColor(distortedUV, 0);

        return float4(color, 1.0);
    }
    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "FilmGateWeavePass"
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