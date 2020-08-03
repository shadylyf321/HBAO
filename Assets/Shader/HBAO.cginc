#include "UnityCG.cginc"

inline float FetchDepth(float2 uv)
{
    return SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
}

/*
inline float3 FetchViewPos(float2 uv)
{
    float depth = LinearEyeDepth(FetchDepth(uv));
    return float3((uv * _UV2View.xy + _UV2View.zw) * depth, depth);
}
*/

inline float3 FetchViewNormal(float2 uv)
{
    return DecodeViewNormalStereo(
        UNITY_SAMPLE_SCREENSPACE_TEXTURE(
            _CameraDepthNormalsTexture, uv));
}

