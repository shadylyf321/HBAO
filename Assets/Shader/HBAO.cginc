#include "UnityCG.cginc"

#define FLT_EPSILON     1.192092896e-07 // Smallest positive number, such that 1.0 + FLT_EPSILON != 1.0

float PositivePow(float base, float power)
{
    return pow(max(abs(base), float(FLT_EPSILON)), power);
}

inline float FetchDepth(float2 uv)
{
    return SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
}

inline float3 FetchViewPos(float2 uv)
{
    float depth = LinearEyeDepth(FetchDepth(uv));
    return float3((uv * _UV2View.xy + _UV2View.zw) * depth, depth);
}

inline float3 FetchViewNormal(float2 uv)
{
    float3 normal = DecodeViewNormalStereo(tex2D( _CameraDepthNormalsTexture, uv));
    return float3(normal.x, normal.y, -normal.z);
}

inline float FallOff(float dist)
{
    return 1 - dist / _Radius;
}

//https://www.derschmale.com/2013/12/20/an-alternative-implementation-for-hbao-2/
inline float SimpleAO(float3 pos, float3 stepPos, float3 normal)
{
    float3 h = stepPos - pos;
    float dist = sqrt(dot(h, h));
    float sinBlock = dot(normal, h) / dist;
    return saturate(sinBlock - _AngleBias) * saturate(FallOff(dist));
}

//value-noise https://thebookofshaders.com/11/
inline float random(float2 uv) {
    return frac(sin(dot(uv.xy, float2(12.9898, 78.233))) * 43758.5453123);
}

float4 hbao(v2f input) : SV_Target
{
    float ao = 0;
    float3 viewPos = FetchViewPos(input.uv);
    float3 normal = FetchViewNormal(input.uv);
    float stepSize = min((_RadiusPixel / viewPos.z), _MaxRadiusPixel) / (STEPS + 1.0);
    //stepSize至少大于一个像素
    if(stepSize < 1)
        return float4(1, 1, 1, 1);
    //stepSize > 1
    float delta = 2.0 * UNITY_PI / DIRECTION;
    float rnd = random(input.uv * 10);
    float2 xy = float2(1, 0);

    UNITY_UNROLL
    for (int i = 0; i < DIRECTION; ++i)
    {
        float angle = delta * (float(i) + rnd);
        float cos, sin;
        sincos(angle, sin, cos);
        float2 dir = float2(cos, sin);
        float rayPixel = 1;
        UNITY_UNROLL
        for(int j = 0; j < STEPS; ++j)
        {
            float2 stepUV = round(rayPixel * dir) * _TexelSize.xy + input.uv;
            float3 stepViewPos = FetchViewPos(stepUV);
            ao += SimpleAO(viewPos, stepViewPos, normal);
            rayPixel += stepSize;
        }
    }
    ao /= STEPS * DIRECTION;
    ao = PositivePow(ao * _AOStrengh, 0.6);
    float col = saturate(1 - ao);
    return float4(col, col, col, col);
}


