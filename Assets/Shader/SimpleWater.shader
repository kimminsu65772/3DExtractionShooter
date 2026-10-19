Shader "SimpleWater" {
	Properties {
		[HideInInspector] _EmissionColor ("Emission Color", Vector) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff ("Alpha Cutoff ", Range(0, 1)) = 0.5
		_ColorShallow ("ColorShallow", Vector) = (0,0,0,0)
		_ColorDeep ("ColorDeep", Vector) = (0,0,0,0)
		[HDR] _EdgeColor ("EdgeColor", Vector) = (0,0,0,0)
		_Smoothness ("Smoothness", Range(0, 1)) = 0
		_FadeDistance ("FadeDistance", Float) = 1
		_ColorBlendDistance ("ColorBlendDistance", Range(0, 5)) = 0.01
		_EdgeWidth ("EdgeWidth", Range(0, 0.5)) = 0.01
		_Noise ("Noise", 2D) = "white" {}
		_Wave1Normal ("Wave1Normal", 2D) = "bump" {}
		_Wave1Normal1 ("Wave1Normal", 2D) = "bump" {}
		_NoiseTileAndSpeed ("NoiseTileAndSpeed", Vector) = (0,0,0,0)
		_Wave1TileAndSpeed ("Wave1TileAndSpeed", Vector) = (0,0,0,0)
		_Wave2TileAndSpeed ("Wave2TileAndSpeed", Vector) = (0,0,0,0)
		_NoiseAffactDepthFade ("NoiseAffactDepthFade", Float) = 0
		_NoiseAffactWave1 ("NoiseAffactWave1", Float) = 0
		_NoiseAffactWave2 ("NoiseAffactWave2", Float) = 0
		_Normal1Strength ("Normal1Strength", Range(0, 1)) = 0.873248
		_Normal2Strength ("Normal2Strength", Range(0, 1)) = 0.873248
		_FadePower ("FadePower", Range(0.1, 10)) = 1
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
			};

			struct Vertex_Stage_Output
			{
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return float4(1.0, 1.0, 1.0, 1.0); // RGBA
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/InternalErrorShader"
	//CustomEditor "UnityEditor.ShaderGraphLitGUI"
}