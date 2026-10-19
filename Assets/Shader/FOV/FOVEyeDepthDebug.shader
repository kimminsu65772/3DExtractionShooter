Shader "Hidden/FOV/EyeDepthDebug"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "FOVEyeDepth.hlsl"
            struct Attributes { float4 position : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                o.position = TransformObjectToHClip(i.position.xyz);
                o.uv = i.uv;
                return o;
            }
            float4 _ProbeWorldPosition;
            float _ProbeMode;
            float4 frag(Varyings i) : SV_Target
            {
                float d = SAMPLE_TEXTURE2D_LOD(_FOVEyeDepth, sampler_FOVEyeDepth, i.uv, 0).r;
                float value = _ProbeMode > 0.5 ? EyeVisibility(_ProbeWorldPosition.xyz, 0) : EyeLinearDepth(d) / _FOVEyeClip.y;
                return float4(value, value, value, 1);
            }
            ENDHLSL
        }
    }
}
