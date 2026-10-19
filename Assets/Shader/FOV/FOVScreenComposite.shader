Shader "FOV/FOVScreenComposite"
{
    Properties
    {
        _OutsideTint("Outside Tint", Color) = (0.35, 0.45, 0.65, 0.35)
        _OutsideBrightness("Outside Brightness", Range(0.0, 1.0)) = 0.35
        _OutsideSaturation("Outside Saturation", Range(0.0, 1.0)) = 0.25
        _EdgeSoftness("Edge Softness", Range(0.001, 0.5)) = 0.08
        [Toggle] _DebugMask("Debug Mask", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "FOV Screen Composite"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "FOVEyeDepth.hlsl"

            TEXTURE2D(_FOVMaskTexture);
            SAMPLER(sampler_FOVMaskTexture);

            float4x4 _FOVWorldToMaskClip;

            CBUFFER_START(UnityPerMaterial)
                half4 _OutsideTint;
                half _OutsideBrightness;
                half _OutsideSaturation;
                half _EdgeSoftness;
                half _DebugMask;
            CBUFFER_END

            half3 CalculateOutsideColor(half3 originalColor)
            {
                half luminance = dot(
                    originalColor,
                    half3(0.2126h, 0.7152h, 0.0722h)
                );

                half3 grayscaleColor = luminance.xxx;

                half3 desaturatedColor = lerp(
                    grayscaleColor,
                    originalColor,
                    _OutsideSaturation
                );

                half3 multipliedTint =
                    desaturatedColor * _OutsideTint.rgb;

                half3 tintedColor = lerp(
                    desaturatedColor,
                    multipliedTint,
                    _OutsideTint.a
                );

                return tintedColor * _OutsideBrightness;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = input.texcoord.xy;

                half4 sceneColor = SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    screenUV,
                    _BlitMipLevel
                );

                half3 outsideColor =
                    CalculateOutsideColor(sceneColor.rgb);

                #if UNITY_REVERSED_Z
                    float deviceDepth = SampleSceneDepth(screenUV);
                    bool isSkyPixel = deviceDepth <= 0.0001f;
                #else
                    float deviceDepth = lerp(
                        UNITY_NEAR_CLIP_VALUE,
                        1.0f,
                        SampleSceneDepth(screenUV)
                    );
                    bool isSkyPixel = deviceDepth >= 0.9999f;
                #endif

                half maskValue = 0.0h;

                if (!isSkyPixel)
                {
                    float3 worldPosition = ComputeWorldSpacePosition(
                        screenUV,
                        deviceDepth,
                        UNITY_MATRIX_I_VP
                    );

                    float4 maskClipPosition = mul(
                        _FOVWorldToMaskClip,
                        float4(worldPosition, 1.0f)
                    );

                    float2 maskNDC =
                        maskClipPosition.xy / maskClipPosition.w;

                    float2 maskUV =
                        maskNDC * 0.5f + 0.5f;

                    // 일부 그래픽 API는 RenderTexture의 세로 UV 원점이 화면 좌표와 반대다.
                    // 해당 플랫폼에서만 Y축을 뒤집어 월드 방향과 마스크 방향을 일치시킨다.
                    #if UNITY_UV_STARTS_AT_TOP
                        maskUV.y = 1.0f - maskUV.y;
                    #endif

                    float2 insideMinimum =
                        step(float2(0.0f, 0.0f), maskUV);

                    float2 insideMaximum =
                        step(maskUV, float2(1.0f, 1.0f));

                    float isInsideMaskTexture =
                        insideMinimum.x *
                        insideMinimum.y *
                        insideMaximum.x *
                        insideMaximum.y;

                    maskValue = SAMPLE_TEXTURE2D(
                        _FOVMaskTexture,
                        sampler_FOVMaskTexture,
                        saturate(maskUV)
                    ).r;

                    maskValue *= isInsideMaskTexture;
                    if (_FOVEyeEnabled > 0.5)
                    {
                        // 깊이 맵의 픽셀 오차 때문에 표면 자체가 차폐되는 현상을 줄인다.
                        float3 normal = cross(ddy(worldPosition), ddx(worldPosition));
                        normal *= rsqrt(max(dot(normal, normal), 0.000001));
                        normal *= dot(normal, _FOVEyeOrigin - worldPosition) >= 0 ? 1 : -1;
                        maskValue = EyeVisibility(worldPosition + normal * _FOVEyeNormalBias, maskValue);
                    }
                }

                half softenedMask = smoothstep(
                    0.5h - _EdgeSoftness,
                    0.5h + _EdgeSoftness,
                    maskValue
                );

                half3 finalColor = lerp(
                    outsideColor,
                    sceneColor.rgb,
                    softenedMask
                );

                half3 debugMaskColor = softenedMask.xxx;

                half debugMaskEnabled =
                    step(0.5h, _DebugMask);

                half3 outputColor = lerp(
                    finalColor,
                    debugMaskColor,
                    debugMaskEnabled
                );

                return half4(outputColor, sceneColor.a);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
