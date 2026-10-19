Shader "Hidden/Symmetry Break Studio/Tasty Grass Shader/Grass" {
	Properties {
		[PerRendererData] _Tgs_UseAlphaToCoverage ("_Tgs_UseAlphaToCoverage", Int) = 0
		[PerRendererData] _MainTex ("Shape Texture", 2D) = "white" {}
		[Header(Debug)] [PerRendererData] _PositionBoundMin ("BoundsMin", Vector) = (0,0,0,0)
		[PerRendererData] _PositionBoundMax ("BoundsMax", Vector) = (0,0,0,0)
		[Toggle(_DebugShowBarycentric)] _DebugShowBarycentric ("Show Barycentric", Float) = 0
		[Toggle(_DebugDisableWind)] _DebugDisableWind ("Disable Wind", Float) = 0
		[Toggle(_ShowBezierDistance)] _ShowBezierDistance ("Show Bezier Distance", Float) = 0
		_DebugBarycentricIso ("Barycentric Isolation", Vector) = (1,1,1,1)
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
}