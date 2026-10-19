Shader "ExtractionShooter/Environment/RestoredWater"
{
    Properties
    {
        // 추출된 원본 머티리얼의 프로퍼티 이름을 유지해 색상과 물결 설정을 재사용한다.
        _ColorShallow ("Shallow Color", Color) = (0.32, 0.50, 0.52, 1)
        _ColorDeep ("Deep Color", Color) = (0.13, 0.29, 0.34, 1)
        [HDR] _EdgeColor ("Foam Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _FadeDistance ("Transparency Depth", Float) = 4.5
        _ColorBlendDistance ("Color Blend Depth", Float) = 4.5
        _EdgeWidth ("Foam Depth", Range(0, 0.5)) = 0.078
        _Noise ("Foam Noise", 2D) = "white" {}
        [Normal] _Wave1Normal ("Wave Normal 1", 2D) = "bump" {}
        [Normal] _Wave1Normal1 ("Wave Normal 2", 2D) = "bump" {}
        _NoiseTileAndSpeed ("Noise Tiling XY / Speed ZW", Vector) = (0.15, 0.15, 0.2, 0.3)
        _Wave1TileAndSpeed ("Wave 1 Tiling XY / Speed ZW", Vector) = (0.2, 0.2, 0.1, -0.15)
        _Wave2TileAndSpeed ("Wave 2 Tiling XY / Speed ZW", Vector) = (0.1, 0.1, -0.03, 0.1)
        _NoiseAffactDepthFade ("Noise Depth Offset", Float) = -0.15
        _NoiseAffactWave1 ("Wave 1 Noise Offset", Float) = 0.05
        _NoiseAffactWave2 ("Wave 2 Noise Offset", Float) = 0.05
        _Normal1Strength ("Wave 1 Strength", Range(0, 1)) = 1
        _Normal2Strength ("Wave 2 Strength", Range(0, 1)) = 1
        _FadePower ("Transparency Falloff", Range(0.1, 10)) = 0.5
        _Opacity ("Maximum Opacity", Range(0, 1)) = 0.82
        _FoamIntensity ("Foam Intensity", Range(0, 1)) = 0.25
        _SpecularIntensity ("Specular Intensity", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_Noise); SAMPLER(sampler_Noise);
            TEXTURE2D(_Wave1Normal); SAMPLER(sampler_Wave1Normal);
            TEXTURE2D(_Wave1Normal1); SAMPLER(sampler_Wave1Normal1);

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorShallow;
                half4 _ColorDeep;
                half4 _EdgeColor;
                float4 _NoiseTileAndSpeed;
                float4 _Wave1TileAndSpeed;
                float4 _Wave2TileAndSpeed;
                float _FadeDistance;
                float _ColorBlendDistance;
                float _EdgeWidth;
                float _NoiseAffactDepthFade;
                float _NoiseAffactWave1;
                float _NoiseAffactWave2;
                half _Smoothness;
                half _Normal1Strength;
                half _Normal2Strength;
                half _FadePower;
                half _Opacity;
                half _FoamIntensity;
                half _SpecularIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                #if !UNITY_REVERSED_Z
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                // 수면과 불투명한 바닥의 높이 차를 써서 시선 각도에 따라 수심이 달라지는 것을 막는다.
                float3 sceneWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                float depth = max(0.0, input.positionWS.y - sceneWS.y);
                float2 worldUV = input.positionWS.xz;
                float2 noiseUV = worldUV * _NoiseTileAndSpeed.xy + _Time.y * _NoiseTileAndSpeed.zw;
                half noise = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, noiseUV).r;

                // 월드 좌표로 타일링해 크기가 다른 물 평면에서도 잔물결 크기를 유지한다.
                float2 waveUV1 = worldUV * _Wave1TileAndSpeed.xy + _Time.y * _Wave1TileAndSpeed.zw;
                float2 waveUV2 = worldUV * _Wave2TileAndSpeed.xy + _Time.y * _Wave2TileAndSpeed.zw;
                half3 normal1 = UnpackNormal(SAMPLE_TEXTURE2D(_Wave1Normal, sampler_Wave1Normal,
                    waveUV1 + (noise - 0.5) * _NoiseAffactWave1));
                half3 normal2 = UnpackNormal(SAMPLE_TEXTURE2D(_Wave1Normal1, sampler_Wave1Normal1,
                    waveUV2 + (noise - 0.5) * _NoiseAffactWave2));
                half2 slope = normal1.xy * _Normal1Strength + normal2.xy * _Normal2Strength;
                half3 normalWS = normalize(half3(slope.x, 1.0h, slope.y));
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light mainLight = GetMainLight();

                half deepBlend = saturate(depth / max(_ColorBlendDistance, 0.001));
                half3 color = lerp(_ColorShallow.rgb, _ColorDeep.rgb, deepBlend);
                half diffuse = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = max(SampleSH(half3(0, 1, 0)), half3(0.15h, 0.15h, 0.15h));
                color *= ambient + mainLight.color * (0.45h + 0.35h * diffuse);

                half fresnel = pow(1.0h - saturate(dot(normalWS, viewWS)), 4.0h);
                color = lerp(color, _ColorShallow.rgb, fresnel * 0.3h);
                half3 halfDirection = SafeNormalize(mainLight.direction + viewWS);
                half specular = pow(saturate(dot(normalWS, halfDirection)), lerp(32.0h, 256.0h, _Smoothness));
                color += mainLight.color * specular * _SpecularIntensity;

                float noisyDepth = max(0.0, depth + (noise - 0.5) * _NoiseAffactDepthFade);
                half foam = (1.0h - smoothstep(0.0, max(_EdgeWidth, 0.001), noisyDepth))
                    * smoothstep(0.25h, 0.75h, noise);
                color = lerp(color, _EdgeColor.rgb, foam * _FoamIntensity);

                // 원본 색상 알파는 0이므로 투명도는 수심과 별도 상한값으로 계산한다.
                half depthFade = pow(saturate(depth / max(_FadeDistance, 0.001)), _FadePower);
                half alpha = saturate(depthFade * _Opacity + foam * _FoamIntensity);
                return half4(MixFog(color, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
