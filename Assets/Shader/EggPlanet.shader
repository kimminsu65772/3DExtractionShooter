Shader "EggPlanet" {
	Properties {
		[HideInInspector] _EmissionColor ("Emission Color", Vector) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff ("Alpha Cutoff ", Range(0, 1)) = 0.5
		_Noise1 ("Noise1", 2D) = "white" {}
		_Noise2 ("Noise2", 2D) = "white" {}
		_Normal1 ("Normal1", 2D) = "bump" {}
		_Normal2 ("Normal2", 2D) = "bump" {}
		_NoiseNormalStrength ("NoiseNormalStrength", Float) = 1
		_TilingOffset1 ("Tiling&Offset1", Vector) = (1,1,0,0)
		_TilingOffset2 ("Tiling&Offset1", Vector) = (1,1,0,0)
		_Strength1 ("Strength1", Range(0, 1)) = 0
		_Strength2 ("Strength2", Range(0, 1)) = 0
		_WaterColor ("WaterColor", Vector) = (0,0,0,0)
		[HDR] _BaseColor ("BaseColor", Vector) = (0,0,0,0)
		_LevelFade ("LevelFade", Range(0, 0.1)) = 0
		_WaterLevel ("WaterLevel", Range(0, 1)) = 0
		_VertexHeight ("VertexHeight", Float) = 0
		_WaterSmooth ("WaterSmooth", Range(0, 1)) = 0.4891304
		_GroundSmooth ("GroundSmooth", Range(0, 1)) = 0.4891304
		_GroundTexture ("GroundTexture", 2D) = "white" {}
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