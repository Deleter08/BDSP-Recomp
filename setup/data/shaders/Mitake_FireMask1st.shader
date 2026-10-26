Shader "Mitake/FireMask1st" {
	Properties {
		_Mask0Tex ("Mask Texture1", 2D) = "white" {}
		_Mask1Tex ("Mask Texture2", 2D) = "black" {}
		[Header(Material Value_______________________)] [KeywordEnum(ADD, MULTIPLY)] MASK_CALC_MODE ("Mask Blend Mode", Float) = 0
		_DiscardValue ("Cut off threshould", Range(0, 1)) = 0.5
		[Space] [Header(UV Control_______________________)] [KeywordEnum(ZERO, ONE, TWO)] MASK_FIRST_UV ("Mask0 UV Index", Float) = 0
		_Mask0UVTranslateU ("BlendTex1 UV Translate(U)", Float) = 0
		_Mask0UVTranslateV ("BlendTex1 UV Translate(V)", Float) = 0
		_Mask0UVScaleU ("BlendTex1 UV Scale(U)", Float) = 1
		_Mask0UVScaleV ("BlendTex1 UV Scale(V)", Float) = 1
		[KeywordEnum(ZERO, ONE, TWO)] MASK_SECOND_UV ("Mask1 UV Index", Float) = 0
		_Mask1UVTranslateU ("BlendTex2 UV Translate(U)", Float) = 0
		_Mask1UVTranslateV ("BlendTex2 UV Translate(V)", Float) = 0
		_Mask1UVScaleU ("BlendTex2 UV Scale(U)", Float) = 1
		_Mask1UVScaleV ("BlendTex2 UV Scale(V)", Float) = 1
		[Space] [Header(Culling___________________________)] _Stencil ("MatLink ID", Float) = 0
		[Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Culling Mode", Float) = 0
		[Space] [Header(Z Test____________________________)] [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z Test", Float) = 4
		[MaterialToggle] _ZWrite ("Z Write", Float) = 1
	}

	SubShader {
		Tags { "Queue"="Geometry+1" "RenderType"="Opaque" }
		Pass {
			Tags { "RenderType"="Opaque" }
			ColorMask 0
			ZTest [_ZTest]
			ZWrite [_ZWrite]
			Cull [_CullMode]
			Stencil {
				Ref [_Stencil]
				ReadMask 127
				Comp NotEqual
				Pass Replace
				Fail Keep
				ZFail Keep
			}
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			#pragma shader_feature MASK_CALC_MODE_ADD MASK_CALC_MODE_MULTIPLY
			#pragma shader_feature MASK_FIRST_UV_ZERO MASK_FIRST_UV_ONE MASK_FIRST_UV_TWO
			#pragma shader_feature MASK_SECOND_UV_ZERO MASK_SECOND_UV_ONE MASK_SECOND_UV_TWO
			float4 _Mask0Tex_ST;
			float4 _Mask1Tex_ST;
			float _Mask0UVTranslateU;
			float _Mask0UVTranslateV;
			float _Mask0UVScaleU;
			float _Mask0UVScaleV;
			float _Mask1UVTranslateU;
			float _Mask1UVTranslateV;
			float _Mask1UVScaleU;
			float _Mask1UVScaleV;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float _DiscardValue;
			sampler2D _Mask0Tex;
			sampler2D _Mask1Tex;

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
			#if defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_ZERO) && defined(MASK_SECOND_UV_ZERO)
			#define NVN_VS_ID 0
			#elif defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_ZERO) && defined(MASK_SECOND_UV_ONE)
			#define NVN_VS_ID 1
			#elif defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_ZERO) && defined(MASK_SECOND_UV_TWO)
			#define NVN_VS_ID 2
			#elif defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_ONE) && defined(MASK_SECOND_UV_ZERO)
			#define NVN_VS_ID 3
			#elif defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_ONE) && defined(MASK_SECOND_UV_ONE)
			#define NVN_VS_ID 4
			#elif defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_ONE) && defined(MASK_SECOND_UV_TWO)
			#define NVN_VS_ID 5
			#elif defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_TWO) && defined(MASK_SECOND_UV_ZERO)
			#define NVN_VS_ID 6
			#elif defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_TWO) && defined(MASK_SECOND_UV_ONE)
			#define NVN_VS_ID 7
			#elif defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_TWO) && defined(MASK_SECOND_UV_TWO)
			#define NVN_VS_ID 8
			#elif defined(MASK_CALC_MODE_MULTIPLY) && defined(MASK_FIRST_UV_ZERO) && defined(MASK_SECOND_UV_ZERO)
			#define NVN_VS_ID 0
			#elif defined(MASK_CALC_MODE_MULTIPLY) && defined(MASK_FIRST_UV_ZERO) && defined(MASK_SECOND_UV_ONE)
			#define NVN_VS_ID 1
			#elif defined(MASK_CALC_MODE_MULTIPLY) && defined(MASK_FIRST_UV_ZERO) && defined(MASK_SECOND_UV_TWO)
			#define NVN_VS_ID 2
			#elif defined(MASK_CALC_MODE_MULTIPLY) && defined(MASK_FIRST_UV_ONE) && defined(MASK_SECOND_UV_ZERO)
			#define NVN_VS_ID 3
			#elif defined(MASK_CALC_MODE_MULTIPLY) && defined(MASK_FIRST_UV_ONE) && defined(MASK_SECOND_UV_ONE)
			#define NVN_VS_ID 4
			#elif defined(MASK_CALC_MODE_MULTIPLY) && defined(MASK_FIRST_UV_ONE) && defined(MASK_SECOND_UV_TWO)
			#define NVN_VS_ID 5
			#elif defined(MASK_CALC_MODE_MULTIPLY) && defined(MASK_FIRST_UV_TWO) && defined(MASK_SECOND_UV_ZERO)
			#define NVN_VS_ID 6
			#elif defined(MASK_CALC_MODE_MULTIPLY) && defined(MASK_FIRST_UV_TWO) && defined(MASK_SECOND_UV_ONE)
			#define NVN_VS_ID 7
			#else
			#define NVN_VS_ID 8
			#endif
			#if NVN_VS_ID == 0
			// unique program 0: shared by 2 keyword variant(s)
			// 0000_vs.sph.bin keywords=['MASK_CALC_MODE_ADD', 'MASK_FIRST_UV_ZERO', 'MASK_SECOND_UV_ZERO']
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : TEXCOORD0;
			};
			struct v2f {
				float4 pos : SV_POSITION;
				float4 t0 : TEXCOORD0;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a1.x;
				temp_2 = v.a0.x;
				temp_3 = v.a0.z;
				temp_4 = v.a1.y;
				temp_5 = temp_0 * unity_ObjectToWorld._m11;
				temp_6 = temp_0 * unity_ObjectToWorld._m21;
				temp_7 = mad(temp_1, _Mask1Tex_ST.x, _Mask1Tex_ST.z);
				temp_8 = mad(temp_1, _Mask0Tex_ST.x, _Mask0Tex_ST.z);
				temp_9 = temp_0 * unity_ObjectToWorld._m01;
				temp_10 = mad(temp_2, unity_ObjectToWorld._m10, temp_5);
				temp_11 = temp_0 * unity_ObjectToWorld._m31;
				temp_12 = mad(temp_2, unity_ObjectToWorld._m20, temp_6);
				temp_13 = mad(temp_2, unity_ObjectToWorld._m00, temp_9);
				temp_14 = 0.0 - _Mask1UVTranslateU;
				temp_15 = temp_7 + temp_14;
				temp_16 = mad(temp_3, unity_ObjectToWorld._m12, temp_10);
				temp_17 = mad(temp_2, unity_ObjectToWorld._m30, temp_11);
				temp_18 = mad(temp_4, _Mask1Tex_ST.y, _Mask1Tex_ST.w);
				temp_19 = mad(temp_4, _Mask0Tex_ST.y, _Mask0Tex_ST.w);
				temp_20 = mad(temp_3, unity_ObjectToWorld._m02, temp_13);
				temp_21 = 0.0 - _Mask0UVTranslateU;
				temp_22 = temp_8 + temp_21;
				temp_23 = temp_16 + unity_ObjectToWorld._m13;
				temp_24 = mad(temp_3, unity_ObjectToWorld._m22, temp_12);
				temp_25 = 0.0 - _Mask1UVTranslateV;
				temp_26 = temp_18 + temp_25;
				temp_27 = 0.0 - _Mask0UVTranslateV;
				temp_28 = temp_19 + temp_27;
				temp_29 = temp_20 + unity_ObjectToWorld._m03;
				temp_30 = temp_15 * _Mask1UVScaleU;
				temp_31 = temp_23 * unity_MatrixVP._m01;
				o.t0.z = temp_30;
				temp_32 = temp_22 * _Mask0UVScaleU;
				temp_33 = temp_23 * unity_MatrixVP._m11;
				o.t0.x = temp_32;
				temp_34 = mad(temp_29, unity_MatrixVP._m00, temp_31);
				temp_35 = temp_23 * unity_MatrixVP._m21;
				temp_36 = temp_23 * unity_MatrixVP._m31;
				temp_37 = mad(temp_3, unity_ObjectToWorld._m32, temp_17);
				temp_38 = temp_26 * _Mask1UVScaleV;
				temp_39 = temp_24 + unity_ObjectToWorld._m23;
				o.t0.w = temp_38;
				temp_40 = mad(temp_29, unity_MatrixVP._m10, temp_33);
				temp_41 = mad(temp_29, unity_MatrixVP._m20, temp_35);
				temp_42 = mad(temp_29, unity_MatrixVP._m30, temp_36);
				temp_43 = temp_37 + unity_ObjectToWorld._m33;
				temp_44 = temp_28 * _Mask0UVScaleV;
				temp_45 = mad(temp_39, unity_MatrixVP._m02, temp_34);
				o.t0.y = temp_44;
				temp_46 = mad(temp_39, unity_MatrixVP._m12, temp_40);
				temp_47 = mad(temp_39, unity_MatrixVP._m22, temp_41);
				temp_48 = mad(temp_39, unity_MatrixVP._m32, temp_42);
				temp_49 = mad(temp_43, unity_MatrixVP._m03, temp_45);
				temp_50 = mad(temp_43, unity_MatrixVP._m13, temp_46);
				o.pos.x = temp_49;
				temp_51 = mad(temp_43, unity_MatrixVP._m23, temp_47);
				o.pos.y = temp_50;
				temp_52 = mad(temp_43, unity_MatrixVP._m33, temp_48);
				o.pos.z = temp_51;
				o.pos.w = temp_52;
				return o;
			}
			#elif NVN_VS_ID == 1
			// unique program 1: shared by 2 keyword variant(s)
			// 0001_vs.sph.bin keywords=['MASK_CALC_MODE_ADD', 'MASK_FIRST_UV_ZERO', 'MASK_SECOND_UV_ONE']
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : TEXCOORD0;
				float4 a2 : TEXCOORD1;
			};
			struct v2f {
				float4 pos : SV_POSITION;
				float4 t0 : TEXCOORD0;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a2.y;
				temp_2 = v.a0.x;
				temp_3 = v.a0.z;
				temp_4 = v.a2.x;
				temp_5 = v.a1.x;
				temp_6 = v.a1.y;
				temp_7 = temp_0 * unity_ObjectToWorld._m11;
				temp_8 = mad(temp_1, _Mask1Tex_ST.y, _Mask1Tex_ST.w);
				temp_9 = temp_0 * unity_ObjectToWorld._m01;
				temp_10 = mad(temp_2, unity_ObjectToWorld._m10, temp_7);
				temp_11 = temp_0 * unity_ObjectToWorld._m31;
				temp_12 = mad(temp_2, unity_ObjectToWorld._m00, temp_9);
				temp_13 = mad(temp_3, unity_ObjectToWorld._m12, temp_10);
				temp_14 = mad(temp_2, unity_ObjectToWorld._m30, temp_11);
				temp_15 = mad(temp_4, _Mask1Tex_ST.x, _Mask1Tex_ST.z);
				temp_16 = temp_0 * unity_ObjectToWorld._m21;
				temp_17 = mad(temp_3, unity_ObjectToWorld._m02, temp_12);
				temp_18 = temp_13 + unity_ObjectToWorld._m13;
				temp_19 = mad(temp_3, unity_ObjectToWorld._m32, temp_14);
				temp_20 = 0.0 - _Mask1UVTranslateU;
				temp_21 = temp_15 + temp_20;
				temp_22 = mad(temp_2, unity_ObjectToWorld._m20, temp_16);
				temp_23 = mad(temp_5, _Mask0Tex_ST.x, _Mask0Tex_ST.z);
				temp_24 = mad(temp_6, _Mask0Tex_ST.y, _Mask0Tex_ST.w);
				temp_25 = 0.0 - _Mask1UVTranslateV;
				temp_26 = temp_8 + temp_25;
				temp_27 = temp_17 + unity_ObjectToWorld._m03;
				temp_28 = temp_18 * unity_MatrixVP._m01;
				temp_29 = mad(temp_3, unity_ObjectToWorld._m22, temp_22);
				temp_30 = 0.0 - _Mask0UVTranslateU;
				temp_31 = temp_23 + temp_30;
				temp_32 = 0.0 - _Mask0UVTranslateV;
				temp_33 = temp_24 + temp_32;
				temp_34 = temp_21 * _Mask1UVScaleU;
				temp_35 = temp_26 * _Mask1UVScaleV;
				o.t0.z = temp_34;
				temp_36 = mad(temp_27, unity_MatrixVP._m00, temp_28);
				o.t0.w = temp_35;
				temp_37 = temp_18 * unity_MatrixVP._m11;
				temp_38 = temp_18 * unity_MatrixVP._m21;
				temp_39 = temp_18 * unity_MatrixVP._m31;
				temp_40 = temp_31 * _Mask0UVScaleU;
				temp_41 = temp_29 + unity_ObjectToWorld._m23;
				o.t0.x = temp_40;
				temp_42 = mad(temp_27, unity_MatrixVP._m10, temp_37);
				temp_43 = mad(temp_27, unity_MatrixVP._m20, temp_38);
				temp_44 = mad(temp_27, unity_MatrixVP._m30, temp_39);
				temp_45 = temp_19 + unity_ObjectToWorld._m33;
				temp_46 = temp_33 * _Mask0UVScaleV;
				temp_47 = mad(temp_41, unity_MatrixVP._m02, temp_36);
				o.t0.y = temp_46;
				temp_48 = mad(temp_41, unity_MatrixVP._m12, temp_42);
				temp_49 = mad(temp_41, unity_MatrixVP._m22, temp_43);
				temp_50 = mad(temp_41, unity_MatrixVP._m32, temp_44);
				temp_51 = mad(temp_45, unity_MatrixVP._m03, temp_47);
				temp_52 = mad(temp_45, unity_MatrixVP._m13, temp_48);
				o.pos.x = temp_51;
				temp_53 = mad(temp_45, unity_MatrixVP._m23, temp_49);
				o.pos.y = temp_52;
				temp_54 = mad(temp_45, unity_MatrixVP._m33, temp_50);
				o.pos.z = temp_53;
				o.pos.w = temp_54;
				return o;
			}
			#elif NVN_VS_ID == 2
			// unique program 2: shared by 2 keyword variant(s)
			// 0002_vs.sph.bin keywords=['MASK_CALC_MODE_ADD', 'MASK_FIRST_UV_ZERO', 'MASK_SECOND_UV_TWO']
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : TEXCOORD0;
				float4 a2 : TEXCOORD2;
			};
			struct v2f {
				float4 pos : SV_POSITION;
				float4 t0 : TEXCOORD0;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a2.y;
				temp_2 = v.a0.x;
				temp_3 = v.a0.z;
				temp_4 = v.a2.x;
				temp_5 = v.a1.x;
				temp_6 = v.a1.y;
				temp_7 = temp_0 * unity_ObjectToWorld._m11;
				temp_8 = mad(temp_1, _Mask1Tex_ST.y, _Mask1Tex_ST.w);
				temp_9 = temp_0 * unity_ObjectToWorld._m01;
				temp_10 = mad(temp_2, unity_ObjectToWorld._m10, temp_7);
				temp_11 = temp_0 * unity_ObjectToWorld._m31;
				temp_12 = mad(temp_2, unity_ObjectToWorld._m00, temp_9);
				temp_13 = mad(temp_3, unity_ObjectToWorld._m12, temp_10);
				temp_14 = mad(temp_2, unity_ObjectToWorld._m30, temp_11);
				temp_15 = mad(temp_4, _Mask1Tex_ST.x, _Mask1Tex_ST.z);
				temp_16 = temp_0 * unity_ObjectToWorld._m21;
				temp_17 = mad(temp_3, unity_ObjectToWorld._m02, temp_12);
				temp_18 = temp_13 + unity_ObjectToWorld._m13;
				temp_19 = mad(temp_3, unity_ObjectToWorld._m32, temp_14);
				temp_20 = 0.0 - _Mask1UVTranslateU;
				temp_21 = temp_15 + temp_20;
				temp_22 = mad(temp_2, unity_ObjectToWorld._m20, temp_16);
				temp_23 = mad(temp_5, _Mask0Tex_ST.x, _Mask0Tex_ST.z);
				temp_24 = mad(temp_6, _Mask0Tex_ST.y, _Mask0Tex_ST.w);
				temp_25 = 0.0 - _Mask1UVTranslateV;
				temp_26 = temp_8 + temp_25;
				temp_27 = temp_17 + unity_ObjectToWorld._m03;
				temp_28 = temp_18 * unity_MatrixVP._m01;
				temp_29 = mad(temp_3, unity_ObjectToWorld._m22, temp_22);
				temp_30 = 0.0 - _Mask0UVTranslateU;
				temp_31 = temp_23 + temp_30;
				temp_32 = 0.0 - _Mask0UVTranslateV;
				temp_33 = temp_24 + temp_32;
				temp_34 = temp_21 * _Mask1UVScaleU;
				temp_35 = temp_26 * _Mask1UVScaleV;
				o.t0.z = temp_34;
				temp_36 = mad(temp_27, unity_MatrixVP._m00, temp_28);
				o.t0.w = temp_35;
				temp_37 = temp_18 * unity_MatrixVP._m11;
				temp_38 = temp_18 * unity_MatrixVP._m21;
				temp_39 = temp_18 * unity_MatrixVP._m31;
				temp_40 = temp_31 * _Mask0UVScaleU;
				temp_41 = temp_29 + unity_ObjectToWorld._m23;
				o.t0.x = temp_40;
				temp_42 = mad(temp_27, unity_MatrixVP._m10, temp_37);
				temp_43 = mad(temp_27, unity_MatrixVP._m20, temp_38);
				temp_44 = mad(temp_27, unity_MatrixVP._m30, temp_39);
				temp_45 = temp_19 + unity_ObjectToWorld._m33;
				temp_46 = temp_33 * _Mask0UVScaleV;
				temp_47 = mad(temp_41, unity_MatrixVP._m02, temp_36);
				o.t0.y = temp_46;
				temp_48 = mad(temp_41, unity_MatrixVP._m12, temp_42);
				temp_49 = mad(temp_41, unity_MatrixVP._m22, temp_43);
				temp_50 = mad(temp_41, unity_MatrixVP._m32, temp_44);
				temp_51 = mad(temp_45, unity_MatrixVP._m03, temp_47);
				temp_52 = mad(temp_45, unity_MatrixVP._m13, temp_48);
				o.pos.x = temp_51;
				temp_53 = mad(temp_45, unity_MatrixVP._m23, temp_49);
				o.pos.y = temp_52;
				temp_54 = mad(temp_45, unity_MatrixVP._m33, temp_50);
				o.pos.z = temp_53;
				o.pos.w = temp_54;
				return o;
			}
			#elif NVN_VS_ID == 3
			// unique program 3: shared by 2 keyword variant(s)
			// 0003_vs.sph.bin keywords=['MASK_CALC_MODE_ADD', 'MASK_FIRST_UV_ONE', 'MASK_SECOND_UV_ZERO']
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : TEXCOORD0;
				float4 a2 : TEXCOORD1;
			};
			struct v2f {
				float4 pos : SV_POSITION;
				float4 t0 : TEXCOORD0;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a1.y;
				temp_2 = v.a0.x;
				temp_3 = v.a0.z;
				temp_4 = v.a1.x;
				temp_5 = v.a2.x;
				temp_6 = v.a2.y;
				temp_7 = temp_0 * unity_ObjectToWorld._m11;
				temp_8 = mad(temp_1, _Mask1Tex_ST.y, _Mask1Tex_ST.w);
				temp_9 = temp_0 * unity_ObjectToWorld._m01;
				temp_10 = mad(temp_2, unity_ObjectToWorld._m10, temp_7);
				temp_11 = temp_0 * unity_ObjectToWorld._m31;
				temp_12 = mad(temp_2, unity_ObjectToWorld._m00, temp_9);
				temp_13 = mad(temp_3, unity_ObjectToWorld._m12, temp_10);
				temp_14 = mad(temp_2, unity_ObjectToWorld._m30, temp_11);
				temp_15 = mad(temp_4, _Mask1Tex_ST.x, _Mask1Tex_ST.z);
				temp_16 = temp_0 * unity_ObjectToWorld._m21;
				temp_17 = mad(temp_3, unity_ObjectToWorld._m02, temp_12);
				temp_18 = temp_13 + unity_ObjectToWorld._m13;
				temp_19 = mad(temp_3, unity_ObjectToWorld._m32, temp_14);
				temp_20 = 0.0 - _Mask1UVTranslateU;
				temp_21 = temp_15 + temp_20;
				temp_22 = mad(temp_2, unity_ObjectToWorld._m20, temp_16);
				temp_23 = mad(temp_5, _Mask0Tex_ST.x, _Mask0Tex_ST.z);
				temp_24 = mad(temp_6, _Mask0Tex_ST.y, _Mask0Tex_ST.w);
				temp_25 = 0.0 - _Mask1UVTranslateV;
				temp_26 = temp_8 + temp_25;
				temp_27 = temp_17 + unity_ObjectToWorld._m03;
				temp_28 = temp_18 * unity_MatrixVP._m01;
				temp_29 = mad(temp_3, unity_ObjectToWorld._m22, temp_22);
				temp_30 = 0.0 - _Mask0UVTranslateU;
				temp_31 = temp_23 + temp_30;
				temp_32 = 0.0 - _Mask0UVTranslateV;
				temp_33 = temp_24 + temp_32;
				temp_34 = temp_21 * _Mask1UVScaleU;
				temp_35 = temp_26 * _Mask1UVScaleV;
				o.t0.z = temp_34;
				temp_36 = mad(temp_27, unity_MatrixVP._m00, temp_28);
				o.t0.w = temp_35;
				temp_37 = temp_18 * unity_MatrixVP._m11;
				temp_38 = temp_18 * unity_MatrixVP._m21;
				temp_39 = temp_18 * unity_MatrixVP._m31;
				temp_40 = temp_31 * _Mask0UVScaleU;
				temp_41 = temp_29 + unity_ObjectToWorld._m23;
				o.t0.x = temp_40;
				temp_42 = mad(temp_27, unity_MatrixVP._m10, temp_37);
				temp_43 = mad(temp_27, unity_MatrixVP._m20, temp_38);
				temp_44 = mad(temp_27, unity_MatrixVP._m30, temp_39);
				temp_45 = temp_19 + unity_ObjectToWorld._m33;
				temp_46 = temp_33 * _Mask0UVScaleV;
				temp_47 = mad(temp_41, unity_MatrixVP._m02, temp_36);
				o.t0.y = temp_46;
				temp_48 = mad(temp_41, unity_MatrixVP._m12, temp_42);
				temp_49 = mad(temp_41, unity_MatrixVP._m22, temp_43);
				temp_50 = mad(temp_41, unity_MatrixVP._m32, temp_44);
				temp_51 = mad(temp_45, unity_MatrixVP._m03, temp_47);
				temp_52 = mad(temp_45, unity_MatrixVP._m13, temp_48);
				o.pos.x = temp_51;
				temp_53 = mad(temp_45, unity_MatrixVP._m23, temp_49);
				o.pos.y = temp_52;
				temp_54 = mad(temp_45, unity_MatrixVP._m33, temp_50);
				o.pos.z = temp_53;
				o.pos.w = temp_54;
				return o;
			}
			#elif NVN_VS_ID == 4
			// unique program 4: shared by 2 keyword variant(s)
			// 0004_vs.sph.bin keywords=['MASK_CALC_MODE_ADD', 'MASK_FIRST_UV_ONE', 'MASK_SECOND_UV_ONE']
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : TEXCOORD1;
			};
			struct v2f {
				float4 pos : SV_POSITION;
				float4 t0 : TEXCOORD0;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a1.x;
				temp_2 = v.a0.x;
				temp_3 = v.a0.z;
				temp_4 = v.a1.y;
				temp_5 = temp_0 * unity_ObjectToWorld._m11;
				temp_6 = temp_0 * unity_ObjectToWorld._m21;
				temp_7 = mad(temp_1, _Mask1Tex_ST.x, _Mask1Tex_ST.z);
				temp_8 = mad(temp_1, _Mask0Tex_ST.x, _Mask0Tex_ST.z);
				temp_9 = temp_0 * unity_ObjectToWorld._m01;
				temp_10 = mad(temp_2, unity_ObjectToWorld._m10, temp_5);
				temp_11 = temp_0 * unity_ObjectToWorld._m31;
				temp_12 = mad(temp_2, unity_ObjectToWorld._m20, temp_6);
				temp_13 = mad(temp_2, unity_ObjectToWorld._m00, temp_9);
				temp_14 = 0.0 - _Mask1UVTranslateU;
				temp_15 = temp_7 + temp_14;
				temp_16 = mad(temp_3, unity_ObjectToWorld._m12, temp_10);
				temp_17 = mad(temp_2, unity_ObjectToWorld._m30, temp_11);
				temp_18 = mad(temp_4, _Mask1Tex_ST.y, _Mask1Tex_ST.w);
				temp_19 = mad(temp_4, _Mask0Tex_ST.y, _Mask0Tex_ST.w);
				temp_20 = mad(temp_3, unity_ObjectToWorld._m02, temp_13);
				temp_21 = 0.0 - _Mask0UVTranslateU;
				temp_22 = temp_8 + temp_21;
				temp_23 = temp_16 + unity_ObjectToWorld._m13;
				temp_24 = mad(temp_3, unity_ObjectToWorld._m22, temp_12);
				temp_25 = 0.0 - _Mask1UVTranslateV;
				temp_26 = temp_18 + temp_25;
				temp_27 = 0.0 - _Mask0UVTranslateV;
				temp_28 = temp_19 + temp_27;
				temp_29 = temp_20 + unity_ObjectToWorld._m03;
				temp_30 = temp_15 * _Mask1UVScaleU;
				temp_31 = temp_23 * unity_MatrixVP._m01;
				o.t0.z = temp_30;
				temp_32 = temp_22 * _Mask0UVScaleU;
				temp_33 = temp_23 * unity_MatrixVP._m11;
				o.t0.x = temp_32;
				temp_34 = mad(temp_29, unity_MatrixVP._m00, temp_31);
				temp_35 = temp_23 * unity_MatrixVP._m21;
				temp_36 = temp_23 * unity_MatrixVP._m31;
				temp_37 = mad(temp_3, unity_ObjectToWorld._m32, temp_17);
				temp_38 = temp_26 * _Mask1UVScaleV;
				temp_39 = temp_24 + unity_ObjectToWorld._m23;
				o.t0.w = temp_38;
				temp_40 = mad(temp_29, unity_MatrixVP._m10, temp_33);
				temp_41 = mad(temp_29, unity_MatrixVP._m20, temp_35);
				temp_42 = mad(temp_29, unity_MatrixVP._m30, temp_36);
				temp_43 = temp_37 + unity_ObjectToWorld._m33;
				temp_44 = temp_28 * _Mask0UVScaleV;
				temp_45 = mad(temp_39, unity_MatrixVP._m02, temp_34);
				o.t0.y = temp_44;
				temp_46 = mad(temp_39, unity_MatrixVP._m12, temp_40);
				temp_47 = mad(temp_39, unity_MatrixVP._m22, temp_41);
				temp_48 = mad(temp_39, unity_MatrixVP._m32, temp_42);
				temp_49 = mad(temp_43, unity_MatrixVP._m03, temp_45);
				temp_50 = mad(temp_43, unity_MatrixVP._m13, temp_46);
				o.pos.x = temp_49;
				temp_51 = mad(temp_43, unity_MatrixVP._m23, temp_47);
				o.pos.y = temp_50;
				temp_52 = mad(temp_43, unity_MatrixVP._m33, temp_48);
				o.pos.z = temp_51;
				o.pos.w = temp_52;
				return o;
			}
			#elif NVN_VS_ID == 5
			// unique program 5: shared by 2 keyword variant(s)
			// 0005_vs.sph.bin keywords=['MASK_CALC_MODE_ADD', 'MASK_FIRST_UV_ONE', 'MASK_SECOND_UV_TWO']
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : TEXCOORD1;
				float4 a2 : TEXCOORD2;
			};
			struct v2f {
				float4 pos : SV_POSITION;
				float4 t0 : TEXCOORD0;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a2.y;
				temp_2 = v.a0.x;
				temp_3 = v.a0.z;
				temp_4 = v.a2.x;
				temp_5 = v.a1.x;
				temp_6 = v.a1.y;
				temp_7 = temp_0 * unity_ObjectToWorld._m11;
				temp_8 = mad(temp_1, _Mask1Tex_ST.y, _Mask1Tex_ST.w);
				temp_9 = temp_0 * unity_ObjectToWorld._m01;
				temp_10 = mad(temp_2, unity_ObjectToWorld._m10, temp_7);
				temp_11 = temp_0 * unity_ObjectToWorld._m31;
				temp_12 = mad(temp_2, unity_ObjectToWorld._m00, temp_9);
				temp_13 = mad(temp_3, unity_ObjectToWorld._m12, temp_10);
				temp_14 = mad(temp_2, unity_ObjectToWorld._m30, temp_11);
				temp_15 = mad(temp_4, _Mask1Tex_ST.x, _Mask1Tex_ST.z);
				temp_16 = temp_0 * unity_ObjectToWorld._m21;
				temp_17 = mad(temp_3, unity_ObjectToWorld._m02, temp_12);
				temp_18 = temp_13 + unity_ObjectToWorld._m13;
				temp_19 = mad(temp_3, unity_ObjectToWorld._m32, temp_14);
				temp_20 = 0.0 - _Mask1UVTranslateU;
				temp_21 = temp_15 + temp_20;
				temp_22 = mad(temp_2, unity_ObjectToWorld._m20, temp_16);
				temp_23 = mad(temp_5, _Mask0Tex_ST.x, _Mask0Tex_ST.z);
				temp_24 = mad(temp_6, _Mask0Tex_ST.y, _Mask0Tex_ST.w);
				temp_25 = 0.0 - _Mask1UVTranslateV;
				temp_26 = temp_8 + temp_25;
				temp_27 = temp_17 + unity_ObjectToWorld._m03;
				temp_28 = temp_18 * unity_MatrixVP._m01;
				temp_29 = mad(temp_3, unity_ObjectToWorld._m22, temp_22);
				temp_30 = 0.0 - _Mask0UVTranslateU;
				temp_31 = temp_23 + temp_30;
				temp_32 = 0.0 - _Mask0UVTranslateV;
				temp_33 = temp_24 + temp_32;
				temp_34 = temp_21 * _Mask1UVScaleU;
				temp_35 = temp_26 * _Mask1UVScaleV;
				o.t0.z = temp_34;
				temp_36 = mad(temp_27, unity_MatrixVP._m00, temp_28);
				o.t0.w = temp_35;
				temp_37 = temp_18 * unity_MatrixVP._m11;
				temp_38 = temp_18 * unity_MatrixVP._m21;
				temp_39 = temp_18 * unity_MatrixVP._m31;
				temp_40 = temp_31 * _Mask0UVScaleU;
				temp_41 = temp_29 + unity_ObjectToWorld._m23;
				o.t0.x = temp_40;
				temp_42 = mad(temp_27, unity_MatrixVP._m10, temp_37);
				temp_43 = mad(temp_27, unity_MatrixVP._m20, temp_38);
				temp_44 = mad(temp_27, unity_MatrixVP._m30, temp_39);
				temp_45 = temp_19 + unity_ObjectToWorld._m33;
				temp_46 = temp_33 * _Mask0UVScaleV;
				temp_47 = mad(temp_41, unity_MatrixVP._m02, temp_36);
				o.t0.y = temp_46;
				temp_48 = mad(temp_41, unity_MatrixVP._m12, temp_42);
				temp_49 = mad(temp_41, unity_MatrixVP._m22, temp_43);
				temp_50 = mad(temp_41, unity_MatrixVP._m32, temp_44);
				temp_51 = mad(temp_45, unity_MatrixVP._m03, temp_47);
				temp_52 = mad(temp_45, unity_MatrixVP._m13, temp_48);
				o.pos.x = temp_51;
				temp_53 = mad(temp_45, unity_MatrixVP._m23, temp_49);
				o.pos.y = temp_52;
				temp_54 = mad(temp_45, unity_MatrixVP._m33, temp_50);
				o.pos.z = temp_53;
				o.pos.w = temp_54;
				return o;
			}
			#elif NVN_VS_ID == 6
			// unique program 6: shared by 2 keyword variant(s)
			// 0006_vs.sph.bin keywords=['MASK_CALC_MODE_ADD', 'MASK_FIRST_UV_TWO', 'MASK_SECOND_UV_ZERO']
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : TEXCOORD0;
				float4 a2 : TEXCOORD2;
			};
			struct v2f {
				float4 pos : SV_POSITION;
				float4 t0 : TEXCOORD0;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a1.y;
				temp_2 = v.a0.x;
				temp_3 = v.a0.z;
				temp_4 = v.a1.x;
				temp_5 = v.a2.x;
				temp_6 = v.a2.y;
				temp_7 = temp_0 * unity_ObjectToWorld._m11;
				temp_8 = mad(temp_1, _Mask1Tex_ST.y, _Mask1Tex_ST.w);
				temp_9 = temp_0 * unity_ObjectToWorld._m01;
				temp_10 = mad(temp_2, unity_ObjectToWorld._m10, temp_7);
				temp_11 = temp_0 * unity_ObjectToWorld._m31;
				temp_12 = mad(temp_2, unity_ObjectToWorld._m00, temp_9);
				temp_13 = mad(temp_3, unity_ObjectToWorld._m12, temp_10);
				temp_14 = mad(temp_2, unity_ObjectToWorld._m30, temp_11);
				temp_15 = mad(temp_4, _Mask1Tex_ST.x, _Mask1Tex_ST.z);
				temp_16 = temp_0 * unity_ObjectToWorld._m21;
				temp_17 = mad(temp_3, unity_ObjectToWorld._m02, temp_12);
				temp_18 = temp_13 + unity_ObjectToWorld._m13;
				temp_19 = mad(temp_3, unity_ObjectToWorld._m32, temp_14);
				temp_20 = 0.0 - _Mask1UVTranslateU;
				temp_21 = temp_15 + temp_20;
				temp_22 = mad(temp_2, unity_ObjectToWorld._m20, temp_16);
				temp_23 = mad(temp_5, _Mask0Tex_ST.x, _Mask0Tex_ST.z);
				temp_24 = mad(temp_6, _Mask0Tex_ST.y, _Mask0Tex_ST.w);
				temp_25 = 0.0 - _Mask1UVTranslateV;
				temp_26 = temp_8 + temp_25;
				temp_27 = temp_17 + unity_ObjectToWorld._m03;
				temp_28 = temp_18 * unity_MatrixVP._m01;
				temp_29 = mad(temp_3, unity_ObjectToWorld._m22, temp_22);
				temp_30 = 0.0 - _Mask0UVTranslateU;
				temp_31 = temp_23 + temp_30;
				temp_32 = 0.0 - _Mask0UVTranslateV;
				temp_33 = temp_24 + temp_32;
				temp_34 = temp_21 * _Mask1UVScaleU;
				temp_35 = temp_26 * _Mask1UVScaleV;
				o.t0.z = temp_34;
				temp_36 = mad(temp_27, unity_MatrixVP._m00, temp_28);
				o.t0.w = temp_35;
				temp_37 = temp_18 * unity_MatrixVP._m11;
				temp_38 = temp_18 * unity_MatrixVP._m21;
				temp_39 = temp_18 * unity_MatrixVP._m31;
				temp_40 = temp_31 * _Mask0UVScaleU;
				temp_41 = temp_29 + unity_ObjectToWorld._m23;
				o.t0.x = temp_40;
				temp_42 = mad(temp_27, unity_MatrixVP._m10, temp_37);
				temp_43 = mad(temp_27, unity_MatrixVP._m20, temp_38);
				temp_44 = mad(temp_27, unity_MatrixVP._m30, temp_39);
				temp_45 = temp_19 + unity_ObjectToWorld._m33;
				temp_46 = temp_33 * _Mask0UVScaleV;
				temp_47 = mad(temp_41, unity_MatrixVP._m02, temp_36);
				o.t0.y = temp_46;
				temp_48 = mad(temp_41, unity_MatrixVP._m12, temp_42);
				temp_49 = mad(temp_41, unity_MatrixVP._m22, temp_43);
				temp_50 = mad(temp_41, unity_MatrixVP._m32, temp_44);
				temp_51 = mad(temp_45, unity_MatrixVP._m03, temp_47);
				temp_52 = mad(temp_45, unity_MatrixVP._m13, temp_48);
				o.pos.x = temp_51;
				temp_53 = mad(temp_45, unity_MatrixVP._m23, temp_49);
				o.pos.y = temp_52;
				temp_54 = mad(temp_45, unity_MatrixVP._m33, temp_50);
				o.pos.z = temp_53;
				o.pos.w = temp_54;
				return o;
			}
			#elif NVN_VS_ID == 7
			// unique program 7: shared by 2 keyword variant(s)
			// 0007_vs.sph.bin keywords=['MASK_CALC_MODE_ADD', 'MASK_FIRST_UV_TWO', 'MASK_SECOND_UV_ONE']
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : TEXCOORD1;
				float4 a2 : TEXCOORD2;
			};
			struct v2f {
				float4 pos : SV_POSITION;
				float4 t0 : TEXCOORD0;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a1.y;
				temp_2 = v.a0.x;
				temp_3 = v.a0.z;
				temp_4 = v.a1.x;
				temp_5 = v.a2.x;
				temp_6 = v.a2.y;
				temp_7 = temp_0 * unity_ObjectToWorld._m11;
				temp_8 = mad(temp_1, _Mask1Tex_ST.y, _Mask1Tex_ST.w);
				temp_9 = temp_0 * unity_ObjectToWorld._m01;
				temp_10 = mad(temp_2, unity_ObjectToWorld._m10, temp_7);
				temp_11 = temp_0 * unity_ObjectToWorld._m31;
				temp_12 = mad(temp_2, unity_ObjectToWorld._m00, temp_9);
				temp_13 = mad(temp_3, unity_ObjectToWorld._m12, temp_10);
				temp_14 = mad(temp_2, unity_ObjectToWorld._m30, temp_11);
				temp_15 = mad(temp_4, _Mask1Tex_ST.x, _Mask1Tex_ST.z);
				temp_16 = temp_0 * unity_ObjectToWorld._m21;
				temp_17 = mad(temp_3, unity_ObjectToWorld._m02, temp_12);
				temp_18 = temp_13 + unity_ObjectToWorld._m13;
				temp_19 = mad(temp_3, unity_ObjectToWorld._m32, temp_14);
				temp_20 = 0.0 - _Mask1UVTranslateU;
				temp_21 = temp_15 + temp_20;
				temp_22 = mad(temp_2, unity_ObjectToWorld._m20, temp_16);
				temp_23 = mad(temp_5, _Mask0Tex_ST.x, _Mask0Tex_ST.z);
				temp_24 = mad(temp_6, _Mask0Tex_ST.y, _Mask0Tex_ST.w);
				temp_25 = 0.0 - _Mask1UVTranslateV;
				temp_26 = temp_8 + temp_25;
				temp_27 = temp_17 + unity_ObjectToWorld._m03;
				temp_28 = temp_18 * unity_MatrixVP._m01;
				temp_29 = mad(temp_3, unity_ObjectToWorld._m22, temp_22);
				temp_30 = 0.0 - _Mask0UVTranslateU;
				temp_31 = temp_23 + temp_30;
				temp_32 = 0.0 - _Mask0UVTranslateV;
				temp_33 = temp_24 + temp_32;
				temp_34 = temp_21 * _Mask1UVScaleU;
				temp_35 = temp_26 * _Mask1UVScaleV;
				o.t0.z = temp_34;
				temp_36 = mad(temp_27, unity_MatrixVP._m00, temp_28);
				o.t0.w = temp_35;
				temp_37 = temp_18 * unity_MatrixVP._m11;
				temp_38 = temp_18 * unity_MatrixVP._m21;
				temp_39 = temp_18 * unity_MatrixVP._m31;
				temp_40 = temp_31 * _Mask0UVScaleU;
				temp_41 = temp_29 + unity_ObjectToWorld._m23;
				o.t0.x = temp_40;
				temp_42 = mad(temp_27, unity_MatrixVP._m10, temp_37);
				temp_43 = mad(temp_27, unity_MatrixVP._m20, temp_38);
				temp_44 = mad(temp_27, unity_MatrixVP._m30, temp_39);
				temp_45 = temp_19 + unity_ObjectToWorld._m33;
				temp_46 = temp_33 * _Mask0UVScaleV;
				temp_47 = mad(temp_41, unity_MatrixVP._m02, temp_36);
				o.t0.y = temp_46;
				temp_48 = mad(temp_41, unity_MatrixVP._m12, temp_42);
				temp_49 = mad(temp_41, unity_MatrixVP._m22, temp_43);
				temp_50 = mad(temp_41, unity_MatrixVP._m32, temp_44);
				temp_51 = mad(temp_45, unity_MatrixVP._m03, temp_47);
				temp_52 = mad(temp_45, unity_MatrixVP._m13, temp_48);
				o.pos.x = temp_51;
				temp_53 = mad(temp_45, unity_MatrixVP._m23, temp_49);
				o.pos.y = temp_52;
				temp_54 = mad(temp_45, unity_MatrixVP._m33, temp_50);
				o.pos.z = temp_53;
				o.pos.w = temp_54;
				return o;
			}
			#elif NVN_VS_ID == 8
			// unique program 8: shared by 2 keyword variant(s)
			// 0008_vs.sph.bin keywords=['MASK_CALC_MODE_ADD', 'MASK_FIRST_UV_TWO', 'MASK_SECOND_UV_TWO']
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : TEXCOORD2;
			};
			struct v2f {
				float4 pos : SV_POSITION;
				float4 t0 : TEXCOORD0;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a1.x;
				temp_2 = v.a0.x;
				temp_3 = v.a0.z;
				temp_4 = v.a1.y;
				temp_5 = temp_0 * unity_ObjectToWorld._m11;
				temp_6 = temp_0 * unity_ObjectToWorld._m21;
				temp_7 = mad(temp_1, _Mask1Tex_ST.x, _Mask1Tex_ST.z);
				temp_8 = mad(temp_1, _Mask0Tex_ST.x, _Mask0Tex_ST.z);
				temp_9 = temp_0 * unity_ObjectToWorld._m01;
				temp_10 = mad(temp_2, unity_ObjectToWorld._m10, temp_5);
				temp_11 = temp_0 * unity_ObjectToWorld._m31;
				temp_12 = mad(temp_2, unity_ObjectToWorld._m20, temp_6);
				temp_13 = mad(temp_2, unity_ObjectToWorld._m00, temp_9);
				temp_14 = 0.0 - _Mask1UVTranslateU;
				temp_15 = temp_7 + temp_14;
				temp_16 = mad(temp_3, unity_ObjectToWorld._m12, temp_10);
				temp_17 = mad(temp_2, unity_ObjectToWorld._m30, temp_11);
				temp_18 = mad(temp_4, _Mask1Tex_ST.y, _Mask1Tex_ST.w);
				temp_19 = mad(temp_4, _Mask0Tex_ST.y, _Mask0Tex_ST.w);
				temp_20 = mad(temp_3, unity_ObjectToWorld._m02, temp_13);
				temp_21 = 0.0 - _Mask0UVTranslateU;
				temp_22 = temp_8 + temp_21;
				temp_23 = temp_16 + unity_ObjectToWorld._m13;
				temp_24 = mad(temp_3, unity_ObjectToWorld._m22, temp_12);
				temp_25 = 0.0 - _Mask1UVTranslateV;
				temp_26 = temp_18 + temp_25;
				temp_27 = 0.0 - _Mask0UVTranslateV;
				temp_28 = temp_19 + temp_27;
				temp_29 = temp_20 + unity_ObjectToWorld._m03;
				temp_30 = temp_15 * _Mask1UVScaleU;
				temp_31 = temp_23 * unity_MatrixVP._m01;
				o.t0.z = temp_30;
				temp_32 = temp_22 * _Mask0UVScaleU;
				temp_33 = temp_23 * unity_MatrixVP._m11;
				o.t0.x = temp_32;
				temp_34 = mad(temp_29, unity_MatrixVP._m00, temp_31);
				temp_35 = temp_23 * unity_MatrixVP._m21;
				temp_36 = temp_23 * unity_MatrixVP._m31;
				temp_37 = mad(temp_3, unity_ObjectToWorld._m32, temp_17);
				temp_38 = temp_26 * _Mask1UVScaleV;
				temp_39 = temp_24 + unity_ObjectToWorld._m23;
				o.t0.w = temp_38;
				temp_40 = mad(temp_29, unity_MatrixVP._m10, temp_33);
				temp_41 = mad(temp_29, unity_MatrixVP._m20, temp_35);
				temp_42 = mad(temp_29, unity_MatrixVP._m30, temp_36);
				temp_43 = temp_37 + unity_ObjectToWorld._m33;
				temp_44 = temp_28 * _Mask0UVScaleV;
				temp_45 = mad(temp_39, unity_MatrixVP._m02, temp_34);
				o.t0.y = temp_44;
				temp_46 = mad(temp_39, unity_MatrixVP._m12, temp_40);
				temp_47 = mad(temp_39, unity_MatrixVP._m22, temp_41);
				temp_48 = mad(temp_39, unity_MatrixVP._m32, temp_42);
				temp_49 = mad(temp_43, unity_MatrixVP._m03, temp_45);
				temp_50 = mad(temp_43, unity_MatrixVP._m13, temp_46);
				o.pos.x = temp_49;
				temp_51 = mad(temp_43, unity_MatrixVP._m23, temp_47);
				o.pos.y = temp_50;
				temp_52 = mad(temp_43, unity_MatrixVP._m33, temp_48);
				o.pos.z = temp_51;
				o.pos.w = temp_52;
				return o;
			}
			#endif
			#if defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_ZERO) && defined(MASK_SECOND_UV_ZERO)
			#define NVN_FS_ID 0
			#elif defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_ZERO) && defined(MASK_SECOND_UV_ONE)
			#define NVN_FS_ID 0
			#elif defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_ZERO) && defined(MASK_SECOND_UV_TWO)
			#define NVN_FS_ID 0
			#elif defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_ONE) && defined(MASK_SECOND_UV_ZERO)
			#define NVN_FS_ID 0
			#elif defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_ONE) && defined(MASK_SECOND_UV_ONE)
			#define NVN_FS_ID 0
			#elif defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_ONE) && defined(MASK_SECOND_UV_TWO)
			#define NVN_FS_ID 0
			#elif defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_TWO) && defined(MASK_SECOND_UV_ZERO)
			#define NVN_FS_ID 0
			#elif defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_TWO) && defined(MASK_SECOND_UV_ONE)
			#define NVN_FS_ID 0
			#elif defined(MASK_CALC_MODE_ADD) && defined(MASK_FIRST_UV_TWO) && defined(MASK_SECOND_UV_TWO)
			#define NVN_FS_ID 0
			#elif defined(MASK_CALC_MODE_MULTIPLY) && defined(MASK_FIRST_UV_ZERO) && defined(MASK_SECOND_UV_ZERO)
			#define NVN_FS_ID 1
			#elif defined(MASK_CALC_MODE_MULTIPLY) && defined(MASK_FIRST_UV_ZERO) && defined(MASK_SECOND_UV_ONE)
			#define NVN_FS_ID 1
			#elif defined(MASK_CALC_MODE_MULTIPLY) && defined(MASK_FIRST_UV_ZERO) && defined(MASK_SECOND_UV_TWO)
			#define NVN_FS_ID 1
			#elif defined(MASK_CALC_MODE_MULTIPLY) && defined(MASK_FIRST_UV_ONE) && defined(MASK_SECOND_UV_ZERO)
			#define NVN_FS_ID 1
			#elif defined(MASK_CALC_MODE_MULTIPLY) && defined(MASK_FIRST_UV_ONE) && defined(MASK_SECOND_UV_ONE)
			#define NVN_FS_ID 1
			#elif defined(MASK_CALC_MODE_MULTIPLY) && defined(MASK_FIRST_UV_ONE) && defined(MASK_SECOND_UV_TWO)
			#define NVN_FS_ID 1
			#elif defined(MASK_CALC_MODE_MULTIPLY) && defined(MASK_FIRST_UV_TWO) && defined(MASK_SECOND_UV_ZERO)
			#define NVN_FS_ID 1
			#elif defined(MASK_CALC_MODE_MULTIPLY) && defined(MASK_FIRST_UV_TWO) && defined(MASK_SECOND_UV_ONE)
			#define NVN_FS_ID 1
			#else
			#define NVN_FS_ID 1
			#endif
			#if NVN_FS_ID == 0
			// unique program 0: shared by 9 keyword variant(s)
			// 0018_fs.sph.bin keywords=['MASK_CALC_MODE_ADD', 'MASK_FIRST_UV_ZERO', 'MASK_SECOND_UV_ZERO']
			struct f_in {
				float4 fragcoord : SV_POSITION;
				float4 t0 : TEXCOORD0;
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
				precise float temp_4;
				precise float temp_5;
				precise float temp_6;
				bool temp_7;
				temp_0 = i.t0.x;
				temp_1 = i.t0.y;
				temp_2 = i.t0.w;
				temp_3 = i.t0.z;
				temp_4 = tex2D(_Mask0Tex, nvn_nan0(float2(temp_0, temp_1))).x;
				temp_5 = tex2D(_Mask1Tex, nvn_nan0(float2(temp_3, temp_2))).x;
				temp_6 = temp_5 + temp_4;
				temp_7 = temp_6 < _DiscardValue;
				if (temp_7)
				{
				discard;
				}
				o.c0.x = 1.0;
				o.c0.y = 1.0;
				o.c0.z = 1.0;
				o.c0.w = temp_6;
				return o;
			}
			#elif NVN_FS_ID == 1
			// unique program 1: shared by 9 keyword variant(s)
			// 0027_fs.sph.bin keywords=['MASK_CALC_MODE_MULTIPLY', 'MASK_FIRST_UV_ZERO', 'MASK_SECOND_UV_ZERO']
			struct f_in {
				float4 fragcoord : SV_POSITION;
				float4 t0 : TEXCOORD0;
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
				precise float temp_4;
				precise float temp_5;
				precise float temp_6;
				bool temp_7;
				temp_0 = i.t0.x;
				temp_1 = i.t0.y;
				temp_2 = i.t0.w;
				temp_3 = i.t0.z;
				temp_4 = tex2D(_Mask0Tex, nvn_nan0(float2(temp_0, temp_1))).x;
				temp_5 = tex2D(_Mask1Tex, nvn_nan0(float2(temp_3, temp_2))).x;
				temp_6 = temp_5 * temp_4;
				temp_7 = temp_6 < _DiscardValue;
				if (temp_7)
				{
				discard;
				}
				o.c0.x = 1.0;
				o.c0.y = 1.0;
				o.c0.z = 1.0;
				o.c0.w = temp_6;
				return o;
			}
			#endif
			ENDHLSL
		}
	}
}