Shader "TerrainGrass" {
	Properties {
		[HideInInspector] _EmissionColor ("Emission Color", Vector) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff ("Alpha Cutoff ", Range(0, 1)) = 0.5
		_TopColor1 ("TopColor1", Vector) = (0,0,0,0)
		_TopColor2 ("TopColor2", Vector) = (0,0,0,0)
		_RootColor ("RootColor", Vector) = (0,0,0,0)
		_AlbedoEnhance ("AlbedoEnhance", Range(0, 1)) = 0
		[HDR] _WindHighlightColor ("WindHighlightColor", Vector) = (0,0,0,0)
		_WindHighlightPower ("WindHighlightPower", Range(0.1, 22)) = 1
		_StaticNoise ("StaticNoise", 2D) = "white" {}
		_YScale ("YScale", Range(0, 3)) = 1
		_PosNoise ("PosNoise", Range(0, 1)) = 0
		_StaticNoiseTile ("StaticNoiseTile", Range(0, 1)) = 1
		_XZNoise ("XZNoise", Range(0, 1)) = 0
		_YNoise ("YNoise", Range(0, 3)) = 0
		_WindNoise ("WindNoise", 2D) = "white" {}
		_NoiseTileAndSpeed ("NoiseTileAndSpeed", Vector) = (0.2,0.2,0,0)
		_WindStrength ("WindStrength", Float) = 1
		[RemapSliders] _CharacterInflunce ("CharacterInflunce", Vector) = (0,0.5,0,0)
		[RemapSliders] _CharacterInflunceRange ("CharacterInflunceRange", Vector) = (0,0,0,0)
		_GrassWidth ("GrassWidth", Range(0.1, 2.5)) = 1
		_widthNoise ("widthNoise", Range(0, 1)) = 0
		_Smoothness ("Smoothness", Range(0, 1)) = 0
		_MainTex ("MainTex", 2D) = "white" {}
		_EdgeOffsetX ("EdgeOffsetX", Range(-0.5, 0.5)) = 0
		_EdgeOffsetY ("EdgeOffsetY", Range(-0.5, 0.5)) = 0
		_YFix ("YFix", Range(0, 1)) = 0
		[HideInInspector] _texcoord ("", 2D) = "white" {}
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _MainTex_ST;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy);
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/InternalErrorShader"
	//CustomEditor "UnityEditor.ShaderGraphLitGUI"
}