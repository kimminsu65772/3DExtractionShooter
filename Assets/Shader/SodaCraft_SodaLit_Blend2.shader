Shader "SodaCraft/SodaLit_Blend2" {
	Properties {
		[HideInInspector] _EmissionColor ("Emission Color", Vector) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff ("Alpha Cutoff ", Range(0, 1)) = 0.5
		[Header(______________________________)] [Header(Texture 1)] [Header(..............................................)] _Tile1 ("Tile 1", Vector) = (1,1,0,0)
		_Tint1 ("Tint 1", Vector) = (1,1,1,1)
		[NoScaleOffset] _MainTex1 ("MainTex 1", 2D) = "white" {}
		[NoScaleOffset] _NormalMap1 ("NormalMap 1", 2D) = "bump" {}
		_NormalScale1 ("NormalScale 1", Float) = 1
		[NoScaleOffset] _MetallicSmoothness1 ("MetallicSmoothness 1", 2D) = "white" {}
		_Metallic1 ("Metallic 1", Range(0, 1)) = 0
		_Smoothness1 ("Smoothness 1", Range(0, 1)) = 0
		[Header(______________________________)] [Header(Texture 2)] [Header(..............................................)] _Tile2 ("Tile 2", Vector) = (1,1,0,0)
		_Tint2 ("Tint 2", Vector) = (1,1,1,1)
		[NoScaleOffset] _MainTex2 ("MainTex 2", 2D) = "white" {}
		[NoScaleOffset] _NormalMap2 ("NormalMap 2", 2D) = "bump" {}
		_NormalScale2 ("NormalScale 2", Float) = 1
		[NoScaleOffset] _MetallicSmoothness2 ("MetallicSmoothness 2", 2D) = "white" {}
		_Metallic2 ("Metallic 2", Range(0, 1)) = 0
		_Smoothness2 ("Smoothness 2", Range(0, 1)) = 0
		[Header(______________________________)] [Header(Water)] [Header(..............................................)] _WaterTint ("WaterTint", Vector) = (1,1,1,0)
		_WaterMetallic ("WaterMetallic", Range(0, 1)) = 0
		_WaterSmoothness ("WaterSmoothness", Range(0, 1)) = 0
		[Header(______________________________)] [Header(Noise)] [Header(..............................................)] [NoScaleOffset] _Noise ("Noise", 2D) = "white" {}
		_NoiseTile ("NoiseTile", Float) = 3
		_NosieStrength ("NosieStrength", Range(0, 0.3)) = 0
		[Header(______________________________)] [Header(Blend)] [Header(..............................................)] [RemapSliders] _Height1 ("Height 1", Vector) = (0,1,0,0)
		[RemapSliders] _Height2 ("Height 2", Vector) = (0,1,0,0)
		_HeightBlend ("HeightBlend", Range(0, 1)) = 0.2
		_WaterHeight ("WaterHeight", Range(0, 2)) = 0
		_WaterFade ("WaterFade", Range(0, 1)) = 0
		_EdgeTint ("EdgeTint", Vector) = (1,1,1,0)
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