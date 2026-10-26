Shader "TextMeshPro/Bitmap" {
	Properties {
		_MainTex ("Font Atlas", 2D) = "white" {}
		_FaceTex ("Font Texture", 2D) = "white" {}
		_FaceColor ("Text Color", Color) = (1,1,1,1)
		_VertexOffsetX ("Vertex OffsetX", Float) = 0
		_VertexOffsetY ("Vertex OffsetY", Float) = 0
		_MaskSoftnessX ("Mask SoftnessX", Float) = 0
		_MaskSoftnessY ("Mask SoftnessY", Float) = 0
		_ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		_ColorMask ("Color Mask", Float) = 15
	}

	SubShader {
		Tags { "IgnoreProjector"="true" "Queue"="Transparent" "RenderType"="Transparent" }
		Pass {
			Tags { "IgnoreProjector"="true" "RenderType"="Transparent" }
			Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
			ColorMask [_ColorMask]
			ZTest [unity_GUIZTestMode]
			ZWrite Off
			Cull [_CullMode]
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
			#pragma multi_compile _ UNITY_UI_ALPHACLIP
			#pragma multi_compile _ UNITY_UI_CLIP_RECT
			float4 _ScreenParams;
			float4 _FaceTex_ST;
			float4 _FaceColor;
			float _VertexOffsetX;
			float _VertexOffsetY;
			float4 _ClipRect;
			float _MaskSoftnessX;
			float _MaskSoftnessY;
			float4x4 unity_ObjectToWorld;
			float4x4 glstate_matrix_projection;
			float4x4 unity_MatrixVP;
			sampler2D _FaceTex;
			sampler2D _MainTex;

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
				float4 a2 : TEXCOORD0;
				float4 a3 : TEXCOORD1;
			};
			struct v2f {
				float4 pos : SV_POSITION;
				float4 t0 : TEXCOORD0;
				float4 t1 : TEXCOORD1;
				float4 t2 : TEXCOORD2;
				float4 t3 : TEXCOORD3;
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
				precise float temp_92;
				precise float temp_93;
				precise float temp_94;
				precise float temp_95;
				precise float temp_96;
				precise float temp_97;
				precise float temp_98;
				precise float temp_99;
				precise float temp_100;
				precise float temp_101;
				precise float temp_102;
				precise float temp_103;
				precise float temp_104;
				precise float temp_105;
				precise float temp_106;
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a3.x;
				temp_1 = 1.0 / _ScreenParams.y;
				temp_2 = v.a0.y;
				temp_3 = 1.0 / _ScreenParams.x;
				temp_4 = max(-2E+10, _ClipRect.z);
				temp_5 = v.a0.w;
				temp_6 = min(temp_4, 2e+10);
				temp_7 = v.a0.x;
				temp_8 = temp_0 * 0.000244140625;
				temp_9 = v.a0.z;
				temp_10 = temp_2 + _VertexOffsetY;
				temp_11 = floor(temp_8);
				temp_12 = temp_1 * temp_5;
				temp_13 = v.a1.z;
				temp_14 = temp_3 * temp_5;
				temp_15 = max(-2E+10, _ClipRect.y);
				temp_16 = mad(temp_11, -4096.0, temp_0);
				temp_17 = mad(temp_12, 0.5, temp_10);
				temp_18 = v.a1.x;
				temp_19 = temp_7 + _VertexOffsetX;
				temp_20 = temp_11 * _FaceTex_ST.x;
				temp_21 = v.a1.y;
				temp_22 = temp_16 * _FaceTex_ST.y;
				temp_23 = min(temp_15, 2e+10);
				temp_24 = mad(temp_14, 0.5, temp_19);
				temp_25 = temp_17 * unity_ObjectToWorld._m11;
				temp_26 = mad(temp_20, 0.001953125, _FaceTex_ST.z);
				temp_27 = temp_17 * unity_ObjectToWorld._m01;
				temp_28 = mad(temp_22, 0.001953125, _FaceTex_ST.w);
				temp_29 = v.a2.x;
				temp_30 = temp_17 * unity_ObjectToWorld._m21;
				temp_31 = mad(temp_24, unity_ObjectToWorld._m10, temp_25);
				temp_32 = 0.0 - temp_23;
				temp_33 = mad(temp_17, 2.0, temp_32);
				temp_34 = temp_17 * unity_ObjectToWorld._m31;
				temp_35 = mad(temp_24, unity_ObjectToWorld._m00, temp_27);
				temp_36 = temp_13 * _FaceColor.z;
				temp_37 = max(-2E+10, _ClipRect.x);
				temp_38 = mad(temp_9, unity_ObjectToWorld._m12, temp_31);
				temp_39 = mad(temp_24, unity_ObjectToWorld._m20, temp_30);
				temp_40 = mad(temp_24, unity_ObjectToWorld._m30, temp_34);
				temp_41 = v.a1.w;
				temp_42 = mad(temp_9, unity_ObjectToWorld._m02, temp_35);
				temp_43 = temp_18 * _FaceColor.x;
				temp_44 = min(temp_37, 2e+10);
				temp_45 = v.a2.y;
				temp_46 = temp_38 + unity_ObjectToWorld._m13;
				o.t1.y = temp_28;
				temp_47 = mad(temp_9, unity_ObjectToWorld._m22, temp_39);
				o.t0.x = temp_43;
				temp_48 = mad(temp_9, unity_ObjectToWorld._m32, temp_40);
				o.t3.x = temp_29;
				temp_49 = temp_42 + unity_ObjectToWorld._m03;
				o.t0.z = temp_36;
				temp_50 = temp_46 * unity_MatrixVP._m31;
				o.t1.x = temp_26;
				temp_51 = 0.0 - temp_44;
				temp_52 = mad(temp_24, 2.0, temp_51);
				o.t3.y = temp_45;
				temp_53 = temp_21 * _FaceColor.y;
				temp_54 = temp_47 + unity_ObjectToWorld._m23;
				o.t0.y = temp_53;
				temp_55 = mad(temp_49, unity_MatrixVP._m30, temp_50);
				temp_56 = 0.0 - temp_6;
				temp_57 = temp_56 + temp_52;
				temp_58 = temp_46 * unity_MatrixVP._m11;
				o.t2.x = temp_57;
				temp_59 = max(-2E+10, _ClipRect.w);
				temp_60 = temp_46 * unity_MatrixVP._m01;
				temp_61 = temp_48 + unity_ObjectToWorld._m33;
				temp_62 = mad(temp_54, unity_MatrixVP._m32, temp_55);
				temp_63 = _ScreenParams.x * glstate_matrix_projection._m00;
				temp_64 = mad(temp_49, unity_MatrixVP._m10, temp_58);
				temp_65 = abs(temp_63);
				temp_66 = 1.0 / temp_65;
				temp_67 = mad(temp_49, unity_MatrixVP._m00, temp_60);
				temp_68 = _ScreenParams.y * glstate_matrix_projection._m11;
				temp_69 = mad(temp_61, unity_MatrixVP._m33, temp_62);
				temp_70 = abs(temp_68);
				temp_71 = 1.0 / temp_70;
				temp_72 = temp_46 * unity_MatrixVP._m21;
				temp_73 = 1.0 / temp_69;
				temp_74 = mad(temp_54, unity_MatrixVP._m02, temp_67);
				o.pos.w = temp_69;
				temp_75 = mad(temp_54, unity_MatrixVP._m12, temp_64);
				temp_76 = _ScreenParams.x * 0.5;
				temp_77 = _ScreenParams.y * 0.5;
				temp_78 = mad(temp_49, unity_MatrixVP._m20, temp_72);
				temp_79 = mad(temp_61, unity_MatrixVP._m03, temp_74);
				temp_80 = mad(temp_61, unity_MatrixVP._m13, temp_75);
				temp_81 = temp_69 * temp_66;
				temp_82 = temp_69 * temp_71;
				temp_83 = min(temp_59, 2e+10);
				temp_84 = mad(temp_54, unity_MatrixVP._m22, temp_78);
				temp_85 = temp_79 * temp_73;
				temp_86 = temp_80 * temp_73;
				temp_87 = mad(0.25, _MaskSoftnessX, temp_81);
				temp_88 = mad(0.25, _MaskSoftnessY, temp_82);
				temp_89 = 1.0 / temp_87;
				temp_90 = temp_41 * _FaceColor.w;
				temp_91 = 1.0 / temp_88;
				temp_92 = temp_76 * temp_85;
				temp_93 = 1.0 / temp_76;
				temp_94 = temp_77 * temp_86;
				temp_95 = 1.0 / temp_77;
				temp_96 = 0.0 - temp_83;
				temp_97 = temp_96 + temp_33;
				temp_98 = nvn_roundEven(temp_92);
				temp_99 = mad(temp_61, unity_MatrixVP._m23, temp_84);
				temp_100 = nvn_roundEven(temp_94);
				temp_101 = temp_89 * 0.25;
				o.t0.w = temp_90;
				temp_102 = temp_91 * 0.25;
				o.t2.y = temp_97;
				temp_103 = temp_98 * temp_93;
				o.pos.z = temp_99;
				temp_104 = temp_100 * temp_95;
				o.t2.z = temp_101;
				temp_105 = temp_69 * temp_103;
				o.t2.w = temp_102;
				temp_106 = temp_69 * temp_104;
				o.pos.x = temp_105;
				o.pos.y = temp_106;
				return o;
			}
			#endif
			#if defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
			// 0007_fs.sph.bin keywords=['UNITY_UI_ALPHACLIP', 'UNITY_UI_CLIP_RECT']
			struct f_in {
				float4 fragcoord : SV_POSITION;
				float4 t0 : TEXCOORD0;
				float4 t1 : TEXCOORD1;
				float4 t2 : TEXCOORD2;
				float4 t3 : TEXCOORD3;
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
				bool temp_27;
				precise float3 temp_28;
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
				temp_0 = i.t3.x;
				temp_1 = i.t3.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).w;
				temp_3 = i.t2.x;
				temp_4 = i.t2.y;
				temp_5 = i.t2.z;
				temp_6 = i.t2.w;
				temp_7 = i.t0.w;
				temp_8 = 0.0 - _ClipRect.x;
				temp_9 = temp_8 + _ClipRect.z;
				temp_10 = 0.0 - _ClipRect.y;
				temp_11 = temp_10 + _ClipRect.w;
				temp_12 = abs(temp_3);
				temp_13 = 0.0 - temp_12;
				temp_14 = temp_9 + temp_13;
				temp_15 = i.t1.x;
				temp_16 = abs(temp_4);
				temp_17 = 0.0 - temp_16;
				temp_18 = temp_11 + temp_17;
				temp_19 = i.t1.y;
				temp_20 = temp_14 * temp_5;
				temp_21 = saturate(temp_20);
				temp_22 = temp_18 * temp_6;
				temp_23 = saturate(temp_22);
				temp_24 = temp_21 * temp_23;
				temp_25 = temp_2 * temp_7;
				temp_26 = temp_24 * temp_25;
				temp_27 = temp_26 < 0.00100040436;
				if (temp_27)
				{
				discard;
				}
				temp_28 = tex2D(_FaceTex, nvn_nan0(float2(temp_15, temp_19))).xyz;
				temp_29 = temp_28.x;
				temp_30 = temp_28.y;
				temp_31 = temp_28.z;
				temp_32 = i.t0.x;
				temp_33 = i.t0.y;
				temp_34 = i.t0.z;
				temp_35 = temp_29 * temp_32;
				temp_36 = temp_30 * temp_33;
				temp_37 = temp_31 * temp_34;
				temp_38 = temp_24 * temp_35;
				temp_39 = temp_24 * temp_36;
				temp_40 = temp_24 * temp_37;
				o.c0.x = temp_38;
				o.c0.y = temp_39;
				o.c0.z = temp_40;
				o.c0.w = temp_26;
				return o;
			}
			#elif defined(UNITY_UI_ALPHACLIP)
			// 0005_fs.sph.bin keywords=['UNITY_UI_ALPHACLIP']
			struct f_in {
				float4 fragcoord : SV_POSITION;
				float4 t0 : TEXCOORD0;
				float4 t1 : TEXCOORD1;
				float4 t2 : TEXCOORD2;
				float4 t3 : TEXCOORD3;
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
				precise float3 temp_8;
				precise float temp_9;
				precise float temp_10;
				precise float temp_11;
				precise float temp_12;
				precise float temp_13;
				precise float temp_14;
				precise float temp_15;
				precise float temp_16;
				precise float temp_17;
				temp_0 = i.t3.x;
				temp_1 = i.t3.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).w;
				temp_3 = i.t0.w;
				temp_4 = i.t1.x;
				temp_5 = i.t1.y;
				temp_6 = temp_2 * temp_3;
				temp_7 = temp_6 < 0.00100040436;
				if (temp_7)
				{
				discard;
				}
				temp_8 = tex2D(_FaceTex, nvn_nan0(float2(temp_4, temp_5))).xyz;
				temp_9 = temp_8.x;
				temp_10 = temp_8.y;
				temp_11 = temp_8.z;
				temp_12 = i.t0.x;
				temp_13 = i.t0.y;
				temp_14 = i.t0.z;
				temp_15 = temp_9 * temp_12;
				temp_16 = temp_10 * temp_13;
				temp_17 = temp_11 * temp_14;
				o.c0.x = temp_15;
				o.c0.y = temp_16;
				o.c0.z = temp_17;
				o.c0.w = temp_6;
				return o;
			}
			#elif defined(UNITY_UI_CLIP_RECT)
			// 0006_fs.sph.bin keywords=['UNITY_UI_CLIP_RECT']
			struct f_in {
				float4 fragcoord : SV_POSITION;
				float4 t0 : TEXCOORD0;
				float4 t1 : TEXCOORD1;
				float4 t2 : TEXCOORD2;
				float4 t3 : TEXCOORD3;
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
				precise float3 temp_4;
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
				temp_0 = i.t1.x;
				temp_1 = i.t1.y;
				temp_2 = i.t3.x;
				temp_3 = i.t3.y;
				temp_4 = tex2D(_FaceTex, nvn_nan0(float2(temp_0, temp_1))).xyz;
				temp_5 = temp_4.x;
				temp_6 = temp_4.y;
				temp_7 = temp_4.z;
				temp_8 = tex2D(_MainTex, nvn_nan0(float2(temp_2, temp_3))).w;
				temp_9 = i.t2.x;
				temp_10 = i.t2.y;
				temp_11 = i.t2.z;
				temp_12 = 0.0 - _ClipRect.x;
				temp_13 = temp_12 + _ClipRect.z;
				temp_14 = i.t0.x;
				temp_15 = i.t0.w;
				temp_16 = abs(temp_9);
				temp_17 = 0.0 - temp_16;
				temp_18 = temp_13 + temp_17;
				temp_19 = 0.0 - _ClipRect.y;
				temp_20 = temp_19 + _ClipRect.w;
				temp_21 = i.t2.w;
				temp_22 = temp_18 * temp_11;
				temp_23 = saturate(temp_22);
				temp_24 = i.t0.y;
				temp_25 = abs(temp_10);
				temp_26 = 0.0 - temp_25;
				temp_27 = temp_20 + temp_26;
				temp_28 = i.t0.z;
				temp_29 = temp_27 * temp_21;
				temp_30 = saturate(temp_29);
				temp_31 = temp_23 * temp_30;
				temp_32 = temp_14 * temp_5;
				temp_33 = temp_24 * temp_6;
				temp_34 = temp_28 * temp_7;
				temp_35 = temp_8 * temp_15;
				temp_36 = temp_31 * temp_32;
				temp_37 = temp_31 * temp_33;
				temp_38 = temp_31 * temp_34;
				temp_39 = temp_31 * temp_35;
				o.c0.x = temp_36;
				o.c0.y = temp_37;
				o.c0.z = temp_38;
				o.c0.w = temp_39;
				return o;
			}
			#else
			// 0004_fs.sph.bin keywords=[]
			struct f_in {
				float4 fragcoord : SV_POSITION;
				float4 t0 : TEXCOORD0;
				float4 t1 : TEXCOORD1;
				float4 t2 : TEXCOORD2;
				float4 t3 : TEXCOORD3;
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
				precise float3 temp_4;
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
				temp_0 = i.t1.x;
				temp_1 = i.t1.y;
				temp_2 = i.t3.x;
				temp_3 = i.t3.y;
				temp_4 = tex2D(_FaceTex, nvn_nan0(float2(temp_0, temp_1))).xyz;
				temp_5 = temp_4.x;
				temp_6 = temp_4.y;
				temp_7 = temp_4.z;
				temp_8 = tex2D(_MainTex, nvn_nan0(float2(temp_2, temp_3))).w;
				temp_9 = i.t0.x;
				temp_10 = i.t0.y;
				temp_11 = i.t0.z;
				temp_12 = i.t0.w;
				temp_13 = temp_9 * temp_5;
				temp_14 = temp_10 * temp_6;
				temp_15 = temp_11 * temp_7;
				temp_16 = temp_8 * temp_12;
				o.c0.x = temp_13;
				o.c0.y = temp_14;
				o.c0.z = temp_15;
				o.c0.w = temp_16;
				return o;
			}
			#endif
			ENDHLSL
		}
	}
}