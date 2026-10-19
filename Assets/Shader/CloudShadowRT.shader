Shader "CloudShadowRT" {
	Properties {
		_Noise1 ("Noise1", 2D) = "white" {}
		_Noise2 ("Noise1", 2D) = "white" {}
		[IntRange] _Noise1Tile ("Noise1Tile", Range(1, 10)) = 1
		[IntRange] _Noise2Tile ("Noise2Tile", Range(1, 10)) = 1
		[RemapSliders] _CloudClamp ("CloudClamp", Vector) = (0,1,0,0)
		_Speed12 ("Speed1&2", Vector) = (0,0,0,0)
		_CloudColor ("CloudColor", Vector) = (0,0,0,1)
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
	//CustomEditor "ASEMaterialInspector"
}