#ifndef FOV_EYE_DEPTH_INCLUDED
#define FOV_EYE_DEPTH_INCLUDED
TEXTURE2D(_FOVEyeDepth);
SAMPLER(sampler_FOVEyeDepth);
float4x4 _FOVEyeVP;
float3 _FOVEyeOrigin;
float3 _FOVEyeForward;
float4 _FOVEyeSettings;
float4 _FOVEyeClip;
float _FOVEyeEnabled;
float _FOVEyeNormalBias;

float EyeLinearDepth(float rawDepth)
{
    float n = _FOVEyeClip.x;
    float f = _FOVEyeClip.y;
    #if UNITY_REVERSED_Z
        return n * f / max(n + rawDepth * (f - n), 0.00001);
    #else
        return n * f / max(f - rawDepth * (f - n), 0.00001);
    #endif
}

float EyeVisibility(float3 worldPosition, float fallbackMask)
{
    float4 clip = mul(_FOVEyeVP, float4(worldPosition, 1));
    if (clip.w <= 0.0) return fallbackMask;
    float2 uv = clip.xy / clip.w * 0.5 + 0.5;
    #if UNITY_UV_STARTS_AT_TOP
        uv.y = 1.0 - uv.y;
    #endif
    // 전방 시험 카메라의 촬영 범위 밖은 기존 마스크를 사용한다.
    if (any(uv < 0) || any(uv > 1)) return fallbackMask;
    float2 offset = worldPosition.xz - _FOVEyeOrigin.xz;
    float distanceXZ = length(offset);
    float facing = dot(offset / max(distanceXZ, 0.0001), normalize(_FOVEyeForward.xz));
    bool inShape = distanceXZ <= _FOVEyeSettings.y ||
        (distanceXZ <= _FOVEyeSettings.x && facing >= _FOVEyeSettings.z);
    float rawDepth = SAMPLE_TEXTURE2D_LOD(_FOVEyeDepth, sampler_FOVEyeDepth, uv, 0).r;
    return inShape && clip.w <= EyeLinearDepth(rawDepth) + _FOVEyeSettings.w ? 1.0 : 0.0;
}
#endif
