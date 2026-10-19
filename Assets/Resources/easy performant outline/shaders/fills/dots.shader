Shader "Hidden/EPO/Fill/Basic/Dots" {
	Properties {
		_PublicColor ("Color", Vector) = (1,0,0,1)
		_PublicHorizontalSize ("Horizontal size", Float) = 1
		_PublicVerticalSize ("Vertical size", Float) = 1
		_PublicHorizontalGapSize ("Horizontal gap size", Range(-1, 1)) = 0.5
		_PublicVerticalGapSize ("Vertical gap size", Range(-1, 1)) = 0.5
		_PublicHorizontalSoftness ("Horizontal softness", Range(0, 1)) = 0.2
		_PublicVerticalSoftness ("Vertical softness", Range(0, 1)) = 0.2
		_PublicHorizontalSpeed ("Horizontal speed", Float) = 1
		_PublicVerticalSpeed ("Vertical speed", Float) = 1
		_PublicAngle ("Angle", Range(0, 360)) = 0
		_PublicSecondaryAlpha ("Secondary alpha", Range(0, 1)) = 0.2
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
}