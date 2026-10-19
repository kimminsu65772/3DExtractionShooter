Shader "SodaTerrain" {
	Properties {
		[HideInInspector] _EmissionColor ("Emission Color", Vector) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff ("Alpha Cutoff ", Range(0, 1)) = 0.5
		[HideInInspector] _Control ("Control", 2D) = "white" {}
		[HideInInspector] _Splat3 ("Splat3", 2D) = "white" {}
		[HideInInspector] _Splat2 ("Splat2", 2D) = "white" {}
		[HideInInspector] _Splat1 ("Splat1", 2D) = "white" {}
		[HideInInspector] _Splat0 ("Splat0", 2D) = "white" {}
		[HideInInspector] _Normal0 ("Normal0", 2D) = "white" {}
		[HideInInspector] _Normal1 ("Normal1", 2D) = "white" {}
		[HideInInspector] _Normal2 ("Normal2", 2D) = "white" {}
		[HideInInspector] _Normal3 ("Normal3", 2D) = "white" {}
		[HideInInspector] _Smoothness3 ("Smoothness3", Range(0, 1)) = 1
		[HideInInspector] _Smoothness1 ("Smoothness1", Range(0, 1)) = 1
		[HideInInspector] _Smoothness0 ("Smoothness0", Range(0, 1)) = 1
		[HideInInspector] _Smoothness2 ("Smoothness2", Range(0, 1)) = 1
		[HideInInspector] [Gamma] _Metallic0 ("Metallic0", Range(0, 1)) = 0
		[HideInInspector] [Gamma] _Metallic2 ("Metallic2", Range(0, 1)) = 0
		[HideInInspector] [Gamma] _Metallic3 ("Metallic3", Range(0, 1)) = 0
		[HideInInspector] [Gamma] _Metallic1 ("Metallic1", Range(0, 1)) = 0
		_Noise ("Noise", 2D) = "white" {}
		_NoiseTile ("NoiseTile", Range(0, 1)) = 1
		_NoiseStrength ("NoiseStrength", Range(0, 0.01)) = 0
		_Power ("Power", Range(0.1, 10)) = 1
		_HeightFactor ("HeightFactor", Range(0, 200)) = 0
		_TerrainHolesTexture ("_TerrainHolesTexture", 2D) = "white" {}
		[HideInInspector] _texcoord ("", 2D) = "white" {}
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