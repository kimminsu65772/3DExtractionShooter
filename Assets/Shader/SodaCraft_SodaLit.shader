Shader "SodaCraft/SodaLit" {
	Properties {
		[HideInInspector] _AlphaCutoff ("Alpha Cutoff ", Range(0, 1)) = 0.5
		_MainTex ("MainTex", 2D) = "white" {}
		_Tint ("Tint", Vector) = (1,1,1,1)
		_TempratureLerp ("TempratureLerp", Range(0, 1)) = 0
		_MaskTint ("MaskTint", Vector) = (1,1,1,1)
		_NormalMap ("NormalMap", 2D) = "bump" {}
		_NormalScale ("NormalScale", Float) = 1
		_MetallicSmoothness ("MetallicSmoothness", 2D) = "white" {}
		_Metallic ("Metallic", Range(0, 1)) = 0
		_Smoothness ("Smoothness", Range(0, 1)) = 0
		_EmissionMap ("EmissionMap", 2D) = "white" {}
		[HDR] _EmissionColor ("EmissionColor", Vector) = (0,0,0,0)
		[HideInInspector] _texcoord ("", 2D) = "white" {}
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[Header(Coverage)] [Space(5)] [Toggle(_COVERAGEON_ON)] _Coverageon ("Enable", Float) = 0
		[Header(Coverage Maps)] [Space(10)] _CoverageAlbedo ("Coverage Albedo", 2D) = "white" {}
		_CoverageNormal ("Coverage Normal", 2D) = "bump" {}
		_CoverageMask ("Coverage Mask", 2D) = "white" {}
		_CoverageColor ("Coverage Color", Vector) = (1,1,1,0)
		_CoverageNormalScale ("Coverage Normal Scale", Float) = 1
		_ORGNormalIntensity ("Mask Original NormalScale  ", Range(0, 1)) = 0
		_ORGSmoothIntensity ("Mask Original SmoothScale  ", Range(0, 1)) = 0
		_ORGMetallicIntensity ("Mask Original MetallicScale  ", Range(0, 1)) = 0
		[Header(Coverage Overlay)] [Space(5)] [KeywordEnum(World_Normal,Vertex_Position)] _OverlayMethod ("Overlay Method", Float) = 0
		_CoverageLevel ("Level", Float) = 0
		_CoverageFade ("Fade", Float) = 1
		_MaskContrast ("Mask Contrast", Float) = 0
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