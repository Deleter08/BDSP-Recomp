Shader "UI/UI-SoundWave" {
	Properties {
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		_ColorMask ("Color Mask", Float) = 15
		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
	}

	SubShader {
		Tags { "CanUseSpriteAtlas"="true" "IgnoreProjector"="true" "PreviewType"="Plane" "Queue"="Transparent" "RenderType"="Transparent" }
		Pass {
			Name "Default"
			Tags { "CanUseSpriteAtlas"="true" "IgnoreProjector"="true" "PreviewType"="Plane" "RenderType"="Transparent" }
			Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
			ColorMask [_ColorMask]
			ZTest [unity_GUIZTestMode]
			ZWrite Off
			Cull Off
			Stencil {
				Ref [_Stencil]
				ReadMask [_StencilReadMask]
				WriteMask [_StencilWriteMask]
				Comp [_StencilComp]
				Pass [_StencilOp]
				Fail Keep
				ZFail Keep
			}
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			#pragma multi_compile_local _ UNITY_UI_ALPHACLIP
			#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
			float4 _ScreenParams;
			float4 _Color;
			float4 _ClipRect;
			float _UIMaskSoftnessX;
			float _UIMaskSoftnessY;
			float4x4 unity_ObjectToWorld;
			float4x4 glstate_matrix_projection;
			float4x4 unity_MatrixVP;

			static const int undef = 0;
			uint nvn_pack_half2x16(float2 v) { return f32tof16(v.x) | (f32tof16(v.y) << 16); }  /* s30: GLSL packHalf2x16 returns uint (was asfloat: garbage when assigned to a uint temp - 1,044 Map/Standard programs) */
			float2 nvn_unpack_half2x16(uint u) { return float2(f16tof32(u & 0xffffu), f16tof32(u >> 16)); }
			float nvn_glsl_mod(float a, float b) { return a - b * floor(a / b); }
			float nvn_roundEven(float x) { float r = round(x); return (abs(frac(x)) == 0.5) ? 2.0 * round(x * 0.5) : r; }
			float nvn_nan0(float v) { return (asuint(v) & 0x7fffffffu) > 0x7f800000u ? 0.0 : v; }
			float2 nvn_nan0(float2 v) { return float2(nvn_nan0(v.x), nvn_nan0(v.y)); }
			float3 nvn_nan0(float3 v) { return float3(nvn_nan0(v.x), nvn_nan0(v.y), nvn_nan0(v.z)); }
			float4 nvn_nan0(float4 v) { return float4(nvn_nan0(v.x), nvn_nan0(v.y), nvn_nan0(v.z), nvn_nan0(v.w)); }
			uint nvn_bfmask(int offset, int bits) { return bits >= 32 ? 0xffffffffu : (((1u << uint(bits)) - 1u) << uint(offset)); }
			int nvn_bitfieldInsert(int b, int s, int offset, int bits) { uint m = nvn_bfmask(offset, bits); return asint((asuint(b) & ~m) | ((asuint(s) << uint(offset)) & m)); }
			uint nvn_bitfieldInsert(uint b, uint s, int offset, int bits) { uint m = nvn_bfmask(offset, bits); return (b & ~m) | ((s << uint(offset)) & m); }
			int nvn_bitfieldExtract(int v, int offset, int bits) { return bits == 0 ? 0 : (v << uint(32 - bits - offset)) >> uint(32 - bits); }
			uint nvn_bitfieldExtract(uint v, int offset, int bits) { return bits == 0 ? 0u : (v << uint(32 - bits - offset)) >> uint(32 - bits); }
			#if defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
			#define NVN_VS_ID 0
			#elif defined(UNITY_UI_ALPHACLIP)
			#define NVN_VS_ID 0
			#elif defined(UNITY_UI_CLIP_RECT)
			#define NVN_VS_ID 0
			#else
			#define NVN_VS_ID 0
			#endif
			#if NVN_VS_ID == 0
			// unique program 0: shared by 4 keyword variant(s)
			// 0000_vs.sph.bin keywords=[]
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : COLOR;
			};
			struct v2f {
				float4 pos : SV_POSITION;
				float4 t0 : TEXCOORD0;
				float4 t1 : TEXCOORD1;
				float4 t2 : TEXCOORD2;
			};
			v2f vert(appdata v) {
				v2f o = (v2f)0;
				precise float temp_0;
				precise float temp_1;
				precise float temp_2;
				precise float temp_3;
				precise float temp_4;
				precise float temp_5;
				precise float temp_6;
				precise float temp_7;
				precise float temp_8;
				precise float temp_9;
				precise float temp_10;
				precise float temp_11;
				precise float temp_12;
				precise float temp_13;
				precise float temp_14;
				precise float temp_15;
				precise float temp_16;
				precise float temp_17;
				precise float temp_18;
				precise float temp_19;
				precise float temp_20;
				precise float temp_21;
				precise float temp_22;
				precise float temp_23;
				precise float temp_24;
				precise float temp_25;
				precise float temp_26;
				precise float temp_27;
				precise float temp_28;
				precise float temp_29;
				precise float temp_30;
				precise float temp_31;
				precise float temp_32;
				precise float temp_33;
				precise float temp_34;
				precise float temp_35;
				precise float temp_36;
				precise float temp_37;
				precise float temp_38;
				precise float temp_39;
				precise float temp_40;
				precise float temp_41;
				precise float temp_42;
				precise float temp_43;
				precise float temp_44;
				precise float temp_45;
				precise float temp_46;
				precise float temp_47;
				precise float temp_48;
				precise float temp_49;
				precise float temp_50;
				precise float temp_51;
				precise float temp_52;
				precise float temp_53;
				precise float temp_54;
				precise float temp_55;
				precise float temp_56;
				precise float temp_57;
				precise float temp_58;
				precise float temp_59;
				precise float temp_60;
				precise float temp_61;
				precise float temp_62;
				precise float temp_63;
				precise float temp_64;
				precise float temp_65;
				precise float temp_66;
				precise float temp_67;
				precise float temp_68;
				precise float temp_69;
				precise float temp_70;
				precise float temp_71;
				precise float temp_72;
				precise float temp_73;
				precise float temp_74;
				precise float temp_75;
				precise float temp_76;
				precise float temp_77;
				precise float temp_78;
				precise float temp_79;
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a0.x;
				temp_2 = _ScreenParams.y * glstate_matrix_projection._m11;
				temp_3 = v.a0.z;
				temp_4 = _ScreenParams.y * glstate_matrix_projection._m01;
				temp_5 = v.a1.x;
				temp_6 = mad(_ScreenParams.x, glstate_matrix_projection._m10, temp_2);
				temp_7 = v.a0.w;
				temp_8 = mad(_ScreenParams.x, glstate_matrix_projection._m00, temp_4);
				temp_9 = abs(temp_6);
				temp_10 = 1.0 / temp_9;
				temp_11 = temp_0 * unity_ObjectToWorld._m11;
				temp_12 = v.a1.y;
				temp_13 = temp_0 * unity_ObjectToWorld._m01;
				temp_14 = temp_0 * unity_ObjectToWorld._m21;
				temp_15 = mad(temp_1, unity_ObjectToWorld._m10, temp_11);
				temp_16 = mad(temp_1, unity_ObjectToWorld._m00, temp_13);
				temp_17 = mad(temp_1, unity_ObjectToWorld._m20, temp_14);
				temp_18 = temp_0 * unity_ObjectToWorld._m31;
				temp_19 = mad(temp_3, unity_ObjectToWorld._m12, temp_15);
				temp_20 = v.a1.z;
				temp_21 = max(-2E+10, _ClipRect.x);
				temp_22 = mad(temp_3, unity_ObjectToWorld._m02, temp_16);
				temp_23 = max(-2E+10, _ClipRect.y);
				temp_24 = mad(temp_1, unity_ObjectToWorld._m30, temp_18);
				temp_25 = max(-2E+10, _ClipRect.z);
				temp_26 = temp_19 + unity_ObjectToWorld._m13;
				temp_27 = v.a1.w;
				temp_28 = min(temp_21, 2e+10);
				o.t1.z = temp_3;
				temp_29 = mad(temp_3, unity_ObjectToWorld._m22, temp_17);
				o.t1.x = temp_1;
				temp_30 = temp_22 + unity_ObjectToWorld._m03;
				o.t1.y = temp_0;
				temp_31 = temp_26 * unity_MatrixVP._m31;
				o.t1.w = temp_7;
				temp_32 = mad(temp_3, unity_ObjectToWorld._m32, temp_24);
				temp_33 = min(temp_23, 2e+10);
				temp_34 = temp_29 + unity_ObjectToWorld._m23;
				temp_35 = abs(temp_8);
				temp_36 = 1.0 / temp_35;
				temp_37 = 0.0 - temp_28;
				temp_38 = mad(temp_1, 2.0, temp_37);
				temp_39 = mad(temp_30, unity_MatrixVP._m30, temp_31);
				temp_40 = temp_32 + unity_ObjectToWorld._m33;
				temp_41 = min(temp_25, 2e+10);
				temp_42 = temp_5 * _Color.x;
				temp_43 = max(-2E+10, _ClipRect.w);
				o.t0.x = temp_42;
				temp_44 = mad(temp_34, unity_MatrixVP._m32, temp_39);
				temp_45 = 0.0 - temp_41;
				temp_46 = temp_45 + temp_38;
				temp_47 = temp_12 * _Color.y;
				o.t2.x = temp_46;
				temp_48 = mad(temp_40, unity_MatrixVP._m33, temp_44);
				o.t0.y = temp_47;
				temp_49 = temp_26 * unity_MatrixVP._m01;
				o.pos.w = temp_48;
				temp_50 = 0.0 - temp_33;
				temp_51 = mad(temp_0, 2.0, temp_50);
				temp_52 = temp_48 * temp_36;
				temp_53 = temp_48 * temp_10;
				temp_54 = temp_20 * _Color.z;
				temp_55 = temp_26 * unity_MatrixVP._m11;
				o.t0.z = temp_54;
				temp_56 = mad(temp_30, unity_MatrixVP._m00, temp_49);
				temp_57 = abs(temp_52);
				temp_58 = temp_57 + -0.0;
				temp_59 = abs(temp_53);
				temp_60 = temp_59 + -0.0;
				temp_61 = min(temp_43, 2e+10);
				temp_62 = temp_26 * unity_MatrixVP._m21;
				temp_63 = mad(temp_30, unity_MatrixVP._m10, temp_55);
				temp_64 = mad(temp_34, unity_MatrixVP._m02, temp_56);
				temp_65 = mad(0.25, _UIMaskSoftnessX, temp_58);
				temp_66 = mad(0.25, _UIMaskSoftnessY, temp_60);
				temp_67 = 1.0 / temp_65;
				temp_68 = mad(temp_30, unity_MatrixVP._m20, temp_62);
				temp_69 = 1.0 / temp_66;
				temp_70 = mad(temp_34, unity_MatrixVP._m12, temp_63);
				temp_71 = temp_27 * _Color.w;
				temp_72 = 0.0 - temp_61;
				temp_73 = temp_72 + temp_51;
				o.t0.w = temp_71;
				temp_74 = mad(temp_34, unity_MatrixVP._m22, temp_68);
				o.t2.y = temp_73;
				temp_75 = temp_67 * 0.25;
				temp_76 = mad(temp_40, unity_MatrixVP._m03, temp_64);
				o.t2.z = temp_75;
				temp_77 = mad(temp_40, unity_MatrixVP._m13, temp_70);
				o.pos.x = temp_76;
				temp_78 = mad(temp_40, unity_MatrixVP._m23, temp_74);
				o.pos.y = temp_77;
				temp_79 = temp_69 * 0.25;
				o.pos.z = temp_78;
				o.t2.w = temp_79;
				return o;
			}
			#endif
			#if defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
			#define NVN_FS_ID 0
			#elif defined(UNITY_UI_ALPHACLIP)
			#define NVN_FS_ID 0
			#elif defined(UNITY_UI_CLIP_RECT)
			#define NVN_FS_ID 0
			#else
			#define NVN_FS_ID 0
			#endif
			#if NVN_FS_ID == 0
			// unique program 0: shared by 4 keyword variant(s)
			// 0004_fs.sph.bin keywords=[]
			struct f_in {
				float4 fragcoord : SV_POSITION;
				float4 t0 : TEXCOORD0;
				float4 t1 : TEXCOORD1;
				float4 t2 : TEXCOORD2;
			};
			struct f_out {
				float4 c0 : SV_Target0;
			};
			f_out frag(f_in i) {
				f_out o = (f_out)0;
				precise float temp_0;
				precise float temp_1;
				precise float temp_2;
				precise float temp_3;
				temp_0 = i.t0.x;
				temp_1 = i.t0.y;
				temp_2 = i.t0.z;
				temp_3 = i.t0.w;
				o.c0.x = temp_0;
				o.c0.y = temp_1;
				o.c0.z = temp_2;
				o.c0.w = temp_3;
				return o;
			}
			#endif
			ENDHLSL
		}
	}
}