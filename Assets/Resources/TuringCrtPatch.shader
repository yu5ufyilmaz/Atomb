Shader "Hidden/TuringCrtPatch"
{
    Properties
    {
        _MainTex ("Original atlas", 2D) = "white" {}
        _Patch ("Number strip", 2D) = "black" {}
        _PatchRect ("Atlas UV rectangle", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _Patch;
            float4 _PatchRect;
            float4 frag(v2f_img input) : SV_Target
            {
                float2 patchUV = (input.uv - _PatchRect.xy) / _PatchRect.zw;
                if (any(patchUV < 0) || any(patchUV > 1)) return tex2D(_MainTex, input.uv);
                return tex2D(_Patch, patchUV);
            }
            ENDCG
        }
    }
    Fallback Off
}
