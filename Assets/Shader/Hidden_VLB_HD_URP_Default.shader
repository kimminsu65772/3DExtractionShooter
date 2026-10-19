Shader "Hidden/VLB_HD_URP_Default" {
	Properties {
		_ConeRadius ("Cone Radius", Vector) = (0,0,0,0)
		_ConeGeomProps ("Cone Geom Props", Vector) = (0,0,0,0)
		_ColorFlat ("Color", Vector) = (1,1,1,1)
		_HDRPExposureWeight ("HDRP Exposure Weight", Range(0, 1)) = 0
		_DistanceFallOff ("Distance Fall Off", Vector) = (0,1,1,0)
		_NoiseVelocityAndScale ("Noise Velocity And Scale", Vector) = (0,0,0,0)
		_NoiseParam ("Noise Param", Vector) = (0,0,0,0)
		_BlendSrcFactor ("BlendSrcFactor", Float) = 1
		_BlendDstFactor ("BlendDstFactor", Float) = 1
		_ZTest ("ZTest", Float) = 4
		_ConeSlopeCosSin ("Cone Slope Cos Sin", Vector) = (0,0,0,0)
		_AlphaInside ("Alpha Inside", Range(0, 1)) = 1
		_AlphaOutside ("Alpha Outside", Range(0, 1)) = 1
		_DistanceCamClipping ("Camera Clipping Distance", Float) = 0.5
		_FadeOutFactor ("FadeOutFactor", Float) = 1
		_AttenuationLerpLinearQuad ("Lerp between attenuation linear and quad", Float) = 0.5
		_DepthBlendDistance ("Depth Blend Distance", Float) = 2
		_FresnelPow ("Fresnel Pow", Range(0, 15)) = 1
		_GlareFrontal ("Glare Frontal", Range(0, 1)) = 0.5
		_GlareBehind ("Glare from Behind", Range(0, 1)) = 0.5
		_DrawCap ("Draw Cap", Float) = 1
		_CameraParams ("Camera Params", Vector) = (0,0,0,0)
		_DynamicOcclusionClippingPlaneWS ("Dynamic Occlusion Clipping Plane WS", Vector) = (0,0,0,0)
		_DynamicOcclusionClippingPlaneProps ("Dynamic Occlusion Clipping Plane Props", Float) = 0.25
		_DynamicOcclusionDepthTexture ("DynamicOcclusionDepthTexture", 2D) = "white" {}
		_DynamicOcclusionDepthProps ("DynamicOcclusionDepthProps", Vector) = (1,1,0.25,1)
		_LocalForwardDirection ("LocalForwardDirection", Vector) = (0,0,1,1)
		_TiltVector ("TiltVector", Vector) = (0,0,0,0)
		_AdditionalClippingPlaneWS ("AdditionalClippingPlaneWS", Vector) = (0,0,0,0)
		_Intensity ("Intensity", Range(0, 8)) = 1
		_SideSoftness ("SideSoftness", Range(0, 15)) = 1
		_Jittering ("Jittering", Vector) = (0,0,0,0)
		_CameraForwardOS ("Camera Forward OS", Vector) = (0,0,0,1)
		_CameraForwardWS ("Camera Forward WS", Vector) = (0,0,0,1)
		_TransformScale ("Transform Scale", Vector) = (0,0,0,1)
		_CookieTexture ("CookieTexture", 2D) = "white" {}
		_CookieProperties ("CookieProperties", Vector) = (0,0,0,0)
		_CookiePosAndScale ("CookiePosAndScale", Vector) = (0,0,1,1)
		_ShadowDepthTexture ("ShadowDepthTexture", 2D) = "white" {}
		_ShadowProps ("ShadowProps", Vector) = (1,1,1,1)
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