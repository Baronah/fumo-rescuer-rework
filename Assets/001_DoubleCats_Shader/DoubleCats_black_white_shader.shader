//author :DoubleCats
//https://twitter.com/doublecats1
//https://www.artstation.com/user-427421
Shader "Effect/DoubleCats/black_white_shader"
{
	Properties
	{
		_min("threshold", Range( 0 , 1)) = 0.5
		_width("smooth", Range( 0 , 0.2)) = 0.001
		_flip("Flip (0.5 as a threshold)", Range( 0 , 1)) = 0.51
		_white_color("white_color", Color) = (1,1,1,0)
		_black_color("black_color", Color) = (0,0,0,0)
		[Toggle(_USE_ALPHA_ON)] _use_alpha("use alpha flip", Float) = 0


		[Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 8


	}
	
	SubShader
	{
		
		
		Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }

		LOD 100

		CGINCLUDE
		#pragma target 2.0
		ENDCG
		Blend Off
		AlphaToMask Off
		Cull Back
		ColorMask RGBA
		ZWrite On
		ZTest [_ZTest]
		Offset 0 , 0
		
		
		GrabPass{ }

		Pass
		{
			Name "Unlit"
			Tags { "LightMode"="ForwardBase" }
			CGPROGRAM

			#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
				#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex);
			#else
				#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex)
			#endif


			#ifndef UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX
				//only defining to not throw compilation error over Unity 5.5
				#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
			#endif
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"
			#pragma shader_feature_local _USE_ALPHA_ON


			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct v2f
			{
				float4 vertex : SV_POSITION;
				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
					float3 worldPos : TEXCOORD0;
				#endif
				float4 ase_color : COLOR;
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			uniform float4 _white_color;
			uniform float4 _black_color;
			uniform float _flip;
			uniform float _min;
			uniform float _width;
			ASE_DECLARE_SCREENSPACE_TEXTURE( _GrabTexture )
			inline float4 ASE_ComputeGrabScreenPos( float4 pos )
			{
				#if UNITY_UV_STARTS_AT_TOP
					float scale = -1.0;
				#else
					float scale = 1.0;
				#endif
				float4 o = pos;
				o.y = pos.w * 0.5f;
				o.y = ( pos.y - o.y ) * _ProjectionParams.x * scale + o.y;
				return o;
			}
			

			
			v2f vert ( appdata v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				UNITY_TRANSFER_INSTANCE_ID(v, o);

				float4 ase_clipPos = UnityObjectToClipPos(v.vertex);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord1 = screenPos;
				
				o.ase_color = v.color;
				float3 vertexValue = float3(0, 0, 0);
				#if ASE_ABSOLUTE_VERTEX_POS
					vertexValue = v.vertex.xyz;
				#endif
				vertexValue = vertexValue;
				#if ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				o.vertex = UnityObjectToClipPos(v.vertex);

				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
					o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				#endif
				return o;
			}
			
			fixed4 frag (v2f i ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				fixed4 finalColor;
				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
					float3 WorldPosition = i.worldPos;
				#endif
				#ifdef _USE_ALPHA_ON
					float staticSwitch27 = i.ase_color.a;
				#else
					float staticSwitch27 = _flip;
				#endif
				float4 screenPos = i.ase_texcoord1;
				float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( screenPos );
				float4 screenColor1 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_GrabTexture,ase_grabScreenPos.xy/ase_grabScreenPos.w);
				float3 desaturateInitialColor2 = screenColor1.rgb;
				float desaturateDot2 = dot( desaturateInitialColor2, float3( 0.299, 0.587, 0.114 ));
				float3 desaturateVar2 = lerp( desaturateInitialColor2, desaturateDot2.xxx, 1.0 );
				float smoothstepResult9 = smoothstep( _min , ( _min + _width ) , desaturateVar2.x);
				float temp_output_8_0 = ( 1.0 - smoothstepResult9 );
				float ifLocalVar13 = 0;
				if( staticSwitch27 >= 0.5 )
				ifLocalVar13 = temp_output_8_0;
				else
				ifLocalVar13 = smoothstepResult9;
				float4 lerpResult18 = lerp( _white_color , _black_color , ifLocalVar13);
				
				
				finalColor = lerpResult18;
				return finalColor;
			}
			ENDCG
		}
	}
	CustomEditor "ASEMaterialInspector"
	
	
}
