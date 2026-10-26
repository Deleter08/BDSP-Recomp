Shader "Mitake/SmokeMask_MT" {
	Properties {
		_Mask0Tex ("Mask Texture1", 2D) = "black" {}
		_FireTex ("Multiply Texture", 2D) = "white" {}
		[Header(Material Value_______________________)] _DiscardValue ("Cut off threshould", Range(0, 1)) = 0.5
		[Space] [Header(UV Control_______________________)] [KeywordEnum(ZERO, ONE, TWO)] MASK_FIRST_UV ("Mask0 UV Index", Float) = 0
		_Mask0UVTranslateU ("BlendTex1 UV Translate(U)", Float) = 0
		_Mask0UVTranslateV ("BlendTex1 UV Translate(V)", Float) = 0
		[Space] [Header(Model Scaling__________________)] _BillboardScale ("Scale Input", Float) = 0.1
		[Space] [Header(Stencil___________________________)] _Stencil ("MatLink ID", Float) = 0
		[Space] [Header(Z Test____________________________)] [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z Test", Float) = 4
		[MaterialToggle] _ZWrite ("Z Write", Float) = 0
	}

	SubShader {
		Tags { "Queue"="Geometry+1" "RenderType"="Opaque" }
		Pass {
			Tags { "RenderType"="Opaque" }
			ColorMask 0
			ZTest [_ZTest]
			ZWrite [_ZWrite]
			Cull Off
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
			#pragma shader_feature MASK_FIRST_UV_ZERO MASK_FIRST_UV_ONE MASK_FIRST_UV_TWO
			float4 _Mask0Tex_ST;
			float4 _FireTex_ST;
			float _Mask0UVTranslateU;
			float _Mask0UVTranslateV;
			float _BillboardScale;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_WorldToObject;
			float4x4 unity_MatrixInvV;
			float4x4 unity_MatrixVP;
			float _DiscardValue;
			sampler2D _Mask0Tex;

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
			#if defined(MASK_FIRST_UV_ZERO)
			// 0000_vs.sph.bin keywords=['MASK_FIRST_UV_ZERO']
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : COLOR;
				float4 a2 : TEXCOORD0;
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
				precise float temp_80;
				precise float temp_81;
				precise float temp_82;
				precise float temp_83;
				precise float temp_84;
				precise float temp_85;
				precise float temp_86;
				precise float temp_87;
				precise float temp_88;
				precise float temp_89;
				precise float temp_90;
				precise float temp_91;
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a1.x;
				temp_1 = v.a1.y;
				temp_2 = v.a1.z;
				temp_3 = unity_MatrixInvV._m11 * unity_WorldToObject._m01;
				temp_4 = unity_MatrixInvV._m11 * unity_WorldToObject._m11;
				temp_5 = unity_MatrixInvV._m11 * unity_WorldToObject._m21;
				temp_6 = mad(unity_MatrixInvV._m01, unity_WorldToObject._m00, temp_3);
				temp_7 = mad(unity_MatrixInvV._m01, unity_WorldToObject._m10, temp_4);
				temp_8 = mad(unity_MatrixInvV._m01, unity_WorldToObject._m20, temp_5);
				temp_9 = unity_MatrixInvV._m10 * unity_WorldToObject._m01;
				temp_10 = unity_MatrixInvV._m10 * unity_WorldToObject._m11;
				temp_11 = unity_MatrixInvV._m10 * unity_WorldToObject._m21;
				temp_12 = mad(unity_MatrixInvV._m21, unity_WorldToObject._m12, temp_7);
				temp_13 = temp_0 * 2.0;
				temp_14 = mad(unity_MatrixInvV._m21, unity_WorldToObject._m02, temp_6);
				temp_15 = mad(unity_MatrixInvV._m21, unity_WorldToObject._m22, temp_8);
				temp_16 = mad(unity_MatrixInvV._m00, unity_WorldToObject._m20, temp_11);
				temp_17 = temp_1 * 2.0;
				temp_18 = mad(unity_MatrixInvV._m31, unity_WorldToObject._m13, temp_12);
				temp_19 = mad(unity_MatrixInvV._m31, unity_WorldToObject._m03, temp_14);
				temp_20 = 0.0 - temp_2;
				temp_21 = mad(temp_13, temp_2, temp_20);
				temp_22 = mad(unity_MatrixInvV._m00, unity_WorldToObject._m00, temp_9);
				temp_23 = v.a0.y;
				temp_24 = 0.0 - temp_2;
				temp_25 = mad(temp_17, temp_2, temp_24);
				temp_26 = mad(unity_MatrixInvV._m00, unity_WorldToObject._m10, temp_10);
				temp_27 = v.a0.x;
				temp_28 = temp_21 * _BillboardScale;
				temp_29 = v.a0.z;
				temp_30 = temp_25 * _BillboardScale;
				temp_31 = mad(unity_MatrixInvV._m20, unity_WorldToObject._m12, temp_26);
				temp_32 = mad(unity_MatrixInvV._m20, unity_WorldToObject._m02, temp_22);
				temp_33 = mad(unity_MatrixInvV._m20, unity_WorldToObject._m22, temp_16);
				temp_34 = mad(unity_MatrixInvV._m31, unity_WorldToObject._m23, temp_15);
				temp_35 = temp_18 * temp_30;
				temp_36 = mad(unity_MatrixInvV._m30, unity_WorldToObject._m13, temp_31);
				temp_37 = mad(unity_MatrixInvV._m30, unity_WorldToObject._m03, temp_32);
				temp_38 = temp_19 * temp_30;
				temp_39 = mad(unity_MatrixInvV._m30, unity_WorldToObject._m23, temp_33);
				temp_40 = temp_34 * temp_30;
				temp_41 = mad(temp_36, temp_28, temp_35);
				temp_42 = mad(temp_37, temp_28, temp_38);
				temp_43 = mad(temp_39, temp_28, temp_40);
				temp_44 = v.a2.y;
				temp_45 = temp_41 + temp_23;
				temp_46 = v.a2.x;
				temp_47 = temp_42 + temp_27;
				temp_48 = temp_43 + temp_29;
				temp_49 = temp_45 * unity_ObjectToWorld._m11;
				temp_50 = temp_45 * unity_ObjectToWorld._m01;
				temp_51 = mad(temp_47, unity_ObjectToWorld._m10, temp_49);
				temp_52 = temp_45 * unity_ObjectToWorld._m21;
				temp_53 = mad(temp_47, unity_ObjectToWorld._m00, temp_50);
				temp_54 = temp_45 * unity_ObjectToWorld._m31;
				temp_55 = mad(temp_48, unity_ObjectToWorld._m12, temp_51);
				temp_56 = mad(temp_47, unity_ObjectToWorld._m20, temp_52);
				temp_57 = mad(temp_48, unity_ObjectToWorld._m02, temp_53);
				temp_58 = mad(temp_47, unity_ObjectToWorld._m30, temp_54);
				temp_59 = mad(temp_44, _Mask0Tex_ST.y, _Mask0Tex_ST.w);
				temp_60 = temp_55 + unity_ObjectToWorld._m13;
				temp_61 = mad(temp_48, unity_ObjectToWorld._m22, temp_56);
				temp_62 = temp_57 + unity_ObjectToWorld._m03;
				temp_63 = mad(temp_46, _Mask0Tex_ST.x, _Mask0Tex_ST.z);
				temp_64 = mad(temp_48, unity_ObjectToWorld._m32, temp_58);
				temp_65 = 0.0 - _Mask0UVTranslateV;
				temp_66 = temp_59 + temp_65;
				temp_67 = temp_60 * unity_MatrixVP._m31;
				o.t0.y = temp_66;
				temp_68 = temp_61 + unity_ObjectToWorld._m23;
				temp_69 = temp_60 * unity_MatrixVP._m21;
				temp_70 = temp_64 + unity_ObjectToWorld._m33;
				temp_71 = 0.0 - _Mask0UVTranslateU;
				temp_72 = temp_63 + temp_71;
				temp_73 = mad(temp_62, unity_MatrixVP._m30, temp_67);
				o.t0.x = temp_72;
				temp_74 = temp_60 * unity_MatrixVP._m01;
				temp_75 = temp_60 * unity_MatrixVP._m11;
				temp_76 = mad(temp_62, unity_MatrixVP._m20, temp_69);
				temp_77 = mad(temp_68, unity_MatrixVP._m32, temp_73);
				temp_78 = mad(temp_62, unity_MatrixVP._m00, temp_74);
				temp_79 = mad(temp_62, unity_MatrixVP._m10, temp_75);
				temp_80 = mad(temp_68, unity_MatrixVP._m22, temp_76);
				temp_81 = mad(temp_70, unity_MatrixVP._m33, temp_77);
				temp_82 = mad(temp_68, unity_MatrixVP._m02, temp_78);
				temp_83 = 1.0 / temp_81;
				temp_84 = mad(temp_68, unity_MatrixVP._m12, temp_79);
				o.pos.w = temp_81;
				temp_85 = mad(temp_70, unity_MatrixVP._m23, temp_80);
				o.pos.z = temp_85;
				temp_86 = mad(temp_70, unity_MatrixVP._m03, temp_82);
				temp_87 = mad(temp_70, unity_MatrixVP._m13, temp_84);
				o.pos.x = temp_86;
				temp_88 = temp_86 * temp_83;
				o.pos.y = temp_87;
				temp_89 = temp_87 * temp_83;
				temp_90 = mad(temp_88, _FireTex_ST.x, _FireTex_ST.z);
				temp_91 = mad(temp_89, _FireTex_ST.y, _FireTex_ST.w);
				o.t0.z = temp_90;
				o.t0.w = temp_91;
				return o;
			}
			#elif defined(MASK_FIRST_UV_ONE)
			// 0001_vs.sph.bin keywords=['MASK_FIRST_UV_ONE']
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : COLOR;
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
				precise float temp_80;
				precise float temp_81;
				precise float temp_82;
				precise float temp_83;
				precise float temp_84;
				precise float temp_85;
				precise float temp_86;
				precise float temp_87;
				precise float temp_88;
				precise float temp_89;
				precise float temp_90;
				precise float temp_91;
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a1.x;
				temp_1 = v.a1.y;
				temp_2 = v.a1.z;
				temp_3 = unity_MatrixInvV._m11 * unity_WorldToObject._m01;
				temp_4 = unity_MatrixInvV._m11 * unity_WorldToObject._m11;
				temp_5 = unity_MatrixInvV._m11 * unity_WorldToObject._m21;
				temp_6 = mad(unity_MatrixInvV._m01, unity_WorldToObject._m00, temp_3);
				temp_7 = mad(unity_MatrixInvV._m01, unity_WorldToObject._m10, temp_4);
				temp_8 = mad(unity_MatrixInvV._m01, unity_WorldToObject._m20, temp_5);
				temp_9 = unity_MatrixInvV._m10 * unity_WorldToObject._m01;
				temp_10 = unity_MatrixInvV._m10 * unity_WorldToObject._m11;
				temp_11 = unity_MatrixInvV._m10 * unity_WorldToObject._m21;
				temp_12 = mad(unity_MatrixInvV._m21, unity_WorldToObject._m12, temp_7);
				temp_13 = temp_0 * 2.0;
				temp_14 = mad(unity_MatrixInvV._m21, unity_WorldToObject._m02, temp_6);
				temp_15 = mad(unity_MatrixInvV._m21, unity_WorldToObject._m22, temp_8);
				temp_16 = mad(unity_MatrixInvV._m00, unity_WorldToObject._m20, temp_11);
				temp_17 = temp_1 * 2.0;
				temp_18 = mad(unity_MatrixInvV._m31, unity_WorldToObject._m13, temp_12);
				temp_19 = mad(unity_MatrixInvV._m31, unity_WorldToObject._m03, temp_14);
				temp_20 = 0.0 - temp_2;
				temp_21 = mad(temp_13, temp_2, temp_20);
				temp_22 = mad(unity_MatrixInvV._m00, unity_WorldToObject._m00, temp_9);
				temp_23 = v.a0.y;
				temp_24 = 0.0 - temp_2;
				temp_25 = mad(temp_17, temp_2, temp_24);
				temp_26 = mad(unity_MatrixInvV._m00, unity_WorldToObject._m10, temp_10);
				temp_27 = v.a0.x;
				temp_28 = temp_21 * _BillboardScale;
				temp_29 = v.a0.z;
				temp_30 = temp_25 * _BillboardScale;
				temp_31 = mad(unity_MatrixInvV._m20, unity_WorldToObject._m12, temp_26);
				temp_32 = mad(unity_MatrixInvV._m20, unity_WorldToObject._m02, temp_22);
				temp_33 = mad(unity_MatrixInvV._m20, unity_WorldToObject._m22, temp_16);
				temp_34 = mad(unity_MatrixInvV._m31, unity_WorldToObject._m23, temp_15);
				temp_35 = temp_18 * temp_30;
				temp_36 = mad(unity_MatrixInvV._m30, unity_WorldToObject._m13, temp_31);
				temp_37 = mad(unity_MatrixInvV._m30, unity_WorldToObject._m03, temp_32);
				temp_38 = temp_19 * temp_30;
				temp_39 = mad(unity_MatrixInvV._m30, unity_WorldToObject._m23, temp_33);
				temp_40 = temp_34 * temp_30;
				temp_41 = mad(temp_36, temp_28, temp_35);
				temp_42 = mad(temp_37, temp_28, temp_38);
				temp_43 = mad(temp_39, temp_28, temp_40);
				temp_44 = v.a2.y;
				temp_45 = temp_41 + temp_23;
				temp_46 = v.a2.x;
				temp_47 = temp_42 + temp_27;
				temp_48 = temp_43 + temp_29;
				temp_49 = temp_45 * unity_ObjectToWorld._m11;
				temp_50 = temp_45 * unity_ObjectToWorld._m01;
				temp_51 = mad(temp_47, unity_ObjectToWorld._m10, temp_49);
				temp_52 = temp_45 * unity_ObjectToWorld._m21;
				temp_53 = mad(temp_47, unity_ObjectToWorld._m00, temp_50);
				temp_54 = temp_45 * unity_ObjectToWorld._m31;
				temp_55 = mad(temp_48, unity_ObjectToWorld._m12, temp_51);
				temp_56 = mad(temp_47, unity_ObjectToWorld._m20, temp_52);
				temp_57 = mad(temp_48, unity_ObjectToWorld._m02, temp_53);
				temp_58 = mad(temp_47, unity_ObjectToWorld._m30, temp_54);
				temp_59 = mad(temp_44, _Mask0Tex_ST.y, _Mask0Tex_ST.w);
				temp_60 = temp_55 + unity_ObjectToWorld._m13;
				temp_61 = mad(temp_48, unity_ObjectToWorld._m22, temp_56);
				temp_62 = temp_57 + unity_ObjectToWorld._m03;
				temp_63 = mad(temp_46, _Mask0Tex_ST.x, _Mask0Tex_ST.z);
				temp_64 = mad(temp_48, unity_ObjectToWorld._m32, temp_58);
				temp_65 = 0.0 - _Mask0UVTranslateV;
				temp_66 = temp_59 + temp_65;
				temp_67 = temp_60 * unity_MatrixVP._m31;
				o.t0.y = temp_66;
				temp_68 = temp_61 + unity_ObjectToWorld._m23;
				temp_69 = temp_60 * unity_MatrixVP._m21;
				temp_70 = temp_64 + unity_ObjectToWorld._m33;
				temp_71 = 0.0 - _Mask0UVTranslateU;
				temp_72 = temp_63 + temp_71;
				temp_73 = mad(temp_62, unity_MatrixVP._m30, temp_67);
				o.t0.x = temp_72;
				temp_74 = temp_60 * unity_MatrixVP._m01;
				temp_75 = temp_60 * unity_MatrixVP._m11;
				temp_76 = mad(temp_62, unity_MatrixVP._m20, temp_69);
				temp_77 = mad(temp_68, unity_MatrixVP._m32, temp_73);
				temp_78 = mad(temp_62, unity_MatrixVP._m00, temp_74);
				temp_79 = mad(temp_62, unity_MatrixVP._m10, temp_75);
				temp_80 = mad(temp_68, unity_MatrixVP._m22, temp_76);
				temp_81 = mad(temp_70, unity_MatrixVP._m33, temp_77);
				temp_82 = mad(temp_68, unity_MatrixVP._m02, temp_78);
				temp_83 = 1.0 / temp_81;
				temp_84 = mad(temp_68, unity_MatrixVP._m12, temp_79);
				o.pos.w = temp_81;
				temp_85 = mad(temp_70, unity_MatrixVP._m23, temp_80);
				o.pos.z = temp_85;
				temp_86 = mad(temp_70, unity_MatrixVP._m03, temp_82);
				temp_87 = mad(temp_70, unity_MatrixVP._m13, temp_84);
				o.pos.x = temp_86;
				temp_88 = temp_86 * temp_83;
				o.pos.y = temp_87;
				temp_89 = temp_87 * temp_83;
				temp_90 = mad(temp_88, _FireTex_ST.x, _FireTex_ST.z);
				temp_91 = mad(temp_89, _FireTex_ST.y, _FireTex_ST.w);
				o.t0.z = temp_90;
				o.t0.w = temp_91;
				return o;
			}
			#else
			// 0002_vs.sph.bin keywords=['MASK_FIRST_UV_TWO']
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : COLOR;
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
				precise float temp_80;
				precise float temp_81;
				precise float temp_82;
				precise float temp_83;
				precise float temp_84;
				precise float temp_85;
				precise float temp_86;
				precise float temp_87;
				precise float temp_88;
				precise float temp_89;
				precise float temp_90;
				precise float temp_91;
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a1.x;
				temp_1 = v.a1.y;
				temp_2 = v.a1.z;
				temp_3 = unity_MatrixInvV._m11 * unity_WorldToObject._m01;
				temp_4 = unity_MatrixInvV._m11 * unity_WorldToObject._m11;
				temp_5 = unity_MatrixInvV._m11 * unity_WorldToObject._m21;
				temp_6 = mad(unity_MatrixInvV._m01, unity_WorldToObject._m00, temp_3);
				temp_7 = mad(unity_MatrixInvV._m01, unity_WorldToObject._m10, temp_4);
				temp_8 = mad(unity_MatrixInvV._m01, unity_WorldToObject._m20, temp_5);
				temp_9 = unity_MatrixInvV._m10 * unity_WorldToObject._m01;
				temp_10 = unity_MatrixInvV._m10 * unity_WorldToObject._m11;
				temp_11 = unity_MatrixInvV._m10 * unity_WorldToObject._m21;
				temp_12 = mad(unity_MatrixInvV._m21, unity_WorldToObject._m12, temp_7);
				temp_13 = temp_0 * 2.0;
				temp_14 = mad(unity_MatrixInvV._m21, unity_WorldToObject._m02, temp_6);
				temp_15 = mad(unity_MatrixInvV._m21, unity_WorldToObject._m22, temp_8);
				temp_16 = mad(unity_MatrixInvV._m00, unity_WorldToObject._m20, temp_11);
				temp_17 = temp_1 * 2.0;
				temp_18 = mad(unity_MatrixInvV._m31, unity_WorldToObject._m13, temp_12);
				temp_19 = mad(unity_MatrixInvV._m31, unity_WorldToObject._m03, temp_14);
				temp_20 = 0.0 - temp_2;
				temp_21 = mad(temp_13, temp_2, temp_20);
				temp_22 = mad(unity_MatrixInvV._m00, unity_WorldToObject._m00, temp_9);
				temp_23 = v.a0.y;
				temp_24 = 0.0 - temp_2;
				temp_25 = mad(temp_17, temp_2, temp_24);
				temp_26 = mad(unity_MatrixInvV._m00, unity_WorldToObject._m10, temp_10);
				temp_27 = v.a0.x;
				temp_28 = temp_21 * _BillboardScale;
				temp_29 = v.a0.z;
				temp_30 = temp_25 * _BillboardScale;
				temp_31 = mad(unity_MatrixInvV._m20, unity_WorldToObject._m12, temp_26);
				temp_32 = mad(unity_MatrixInvV._m20, unity_WorldToObject._m02, temp_22);
				temp_33 = mad(unity_MatrixInvV._m20, unity_WorldToObject._m22, temp_16);
				temp_34 = mad(unity_MatrixInvV._m31, unity_WorldToObject._m23, temp_15);
				temp_35 = temp_18 * temp_30;
				temp_36 = mad(unity_MatrixInvV._m30, unity_WorldToObject._m13, temp_31);
				temp_37 = mad(unity_MatrixInvV._m30, unity_WorldToObject._m03, temp_32);
				temp_38 = temp_19 * temp_30;
				temp_39 = mad(unity_MatrixInvV._m30, unity_WorldToObject._m23, temp_33);
				temp_40 = temp_34 * temp_30;
				temp_41 = mad(temp_36, temp_28, temp_35);
				temp_42 = mad(temp_37, temp_28, temp_38);
				temp_43 = mad(temp_39, temp_28, temp_40);
				temp_44 = v.a2.y;
				temp_45 = temp_41 + temp_23;
				temp_46 = v.a2.x;
				temp_47 = temp_42 + temp_27;
				temp_48 = temp_43 + temp_29;
				temp_49 = temp_45 * unity_ObjectToWorld._m11;
				temp_50 = temp_45 * unity_ObjectToWorld._m01;
				temp_51 = mad(temp_47, unity_ObjectToWorld._m10, temp_49);
				temp_52 = temp_45 * unity_ObjectToWorld._m21;
				temp_53 = mad(temp_47, unity_ObjectToWorld._m00, temp_50);
				temp_54 = temp_45 * unity_ObjectToWorld._m31;
				temp_55 = mad(temp_48, unity_ObjectToWorld._m12, temp_51);
				temp_56 = mad(temp_47, unity_ObjectToWorld._m20, temp_52);
				temp_57 = mad(temp_48, unity_ObjectToWorld._m02, temp_53);
				temp_58 = mad(temp_47, unity_ObjectToWorld._m30, temp_54);
				temp_59 = mad(temp_44, _Mask0Tex_ST.y, _Mask0Tex_ST.w);
				temp_60 = temp_55 + unity_ObjectToWorld._m13;
				temp_61 = mad(temp_48, unity_ObjectToWorld._m22, temp_56);
				temp_62 = temp_57 + unity_ObjectToWorld._m03;
				temp_63 = mad(temp_46, _Mask0Tex_ST.x, _Mask0Tex_ST.z);
				temp_64 = mad(temp_48, unity_ObjectToWorld._m32, temp_58);
				temp_65 = 0.0 - _Mask0UVTranslateV;
				temp_66 = temp_59 + temp_65;
				temp_67 = temp_60 * unity_MatrixVP._m31;
				o.t0.y = temp_66;
				temp_68 = temp_61 + unity_ObjectToWorld._m23;
				temp_69 = temp_60 * unity_MatrixVP._m21;
				temp_70 = temp_64 + unity_ObjectToWorld._m33;
				temp_71 = 0.0 - _Mask0UVTranslateU;
				temp_72 = temp_63 + temp_71;
				temp_73 = mad(temp_62, unity_MatrixVP._m30, temp_67);
				o.t0.x = temp_72;
				temp_74 = temp_60 * unity_MatrixVP._m01;
				temp_75 = temp_60 * unity_MatrixVP._m11;
				temp_76 = mad(temp_62, unity_MatrixVP._m20, temp_69);
				temp_77 = mad(temp_68, unity_MatrixVP._m32, temp_73);
				temp_78 = mad(temp_62, unity_MatrixVP._m00, temp_74);
				temp_79 = mad(temp_62, unity_MatrixVP._m10, temp_75);
				temp_80 = mad(temp_68, unity_MatrixVP._m22, temp_76);
				temp_81 = mad(temp_70, unity_MatrixVP._m33, temp_77);
				temp_82 = mad(temp_68, unity_MatrixVP._m02, temp_78);
				temp_83 = 1.0 / temp_81;
				temp_84 = mad(temp_68, unity_MatrixVP._m12, temp_79);
				o.pos.w = temp_81;
				temp_85 = mad(temp_70, unity_MatrixVP._m23, temp_80);
				o.pos.z = temp_85;
				temp_86 = mad(temp_70, unity_MatrixVP._m03, temp_82);
				temp_87 = mad(temp_70, unity_MatrixVP._m13, temp_84);
				o.pos.x = temp_86;
				temp_88 = temp_86 * temp_83;
				o.pos.y = temp_87;
				temp_89 = temp_87 * temp_83;
				temp_90 = mad(temp_88, _FireTex_ST.x, _FireTex_ST.z);
				temp_91 = mad(temp_89, _FireTex_ST.y, _FireTex_ST.w);
				o.t0.z = temp_90;
				o.t0.w = temp_91;
				return o;
			}
			#endif
			#if defined(MASK_FIRST_UV_ZERO)
			#define NVN_FS_ID 0
			#elif defined(MASK_FIRST_UV_ONE)
			#define NVN_FS_ID 0
			#else
			#define NVN_FS_ID 0
			#endif
			#if NVN_FS_ID == 0
			// unique program 0: shared by 3 keyword variant(s)
			// 0003_fs.sph.bin keywords=['MASK_FIRST_UV_ZERO']
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
				precise float temp_7;
				precise float temp_8;
				precise float temp_9;
				precise float temp_10;
				bool temp_11;
				temp_0 = i.fragcoord.w;
				temp_1 = i.t0.x;
				temp_2 = i.fragcoord.w;
				temp_3 = temp_1 * temp_2;
				temp_4 = i.t0.y;
				temp_5 = i.fragcoord.w;
				temp_6 = temp_4 * temp_5;
				temp_7 = 1.0 / temp_0;
				temp_8 = temp_7 * temp_3;
				temp_9 = temp_7 * temp_6;
				temp_10 = tex2D(_Mask0Tex, nvn_nan0(float2(temp_8, temp_9))).x;
				temp_11 = temp_10 < _DiscardValue;
				if (temp_11)
				{
				discard;
				}
				o.c0.x = 1.0;
				o.c0.y = 1.0;
				o.c0.z = 1.0;
				o.c0.w = temp_10;
				return o;
			}
			#endif
			ENDHLSL
		}
	}
}