Shader "TextMeshPro/Mobile/Bitmap" {
	Properties {
		_MainTex ("Font Atlas", 2D) = "white" {}
		_Color ("Text Color", Color) = (1,1,1,1)
		_DiffusePower ("Diffuse Power", Range(1, 4)) = 1
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
			#pragma multi_compile _ UNITY_UI_ALPHACLIP
			#pragma multi_compile _ UNITY_UI_CLIP_RECT
			float4 _ScreenParams;
			float4 _Color;
			float _DiffusePower;
			float _VertexOffsetX;
			float _VertexOffsetY;
			float4 _ClipRect;
			float _MaskSoftnessX;
			float _MaskSoftnessY;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
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
			};
			struct v2f {
				float4 pos : SV_POSITION;
				float4 t0 : TEXCOORD0;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = 1.0 / _ScreenParams.y;
				temp_2 = v.a0.w;
				temp_3 = max(-2E+10, _ClipRect.x);
				temp_4 = v.a0.x;
				temp_5 = _ScreenParams.y * 0.5;
				temp_6 = 1.0 / _ScreenParams.x;
				temp_7 = max(-2E+10, _ClipRect.y);
				temp_8 = v.a0.z;
				temp_9 = max(-2E+10, _ClipRect.z);
				temp_10 = min(temp_3, 2e+10);
				temp_11 = max(-2E+10, _ClipRect.w);
				temp_12 = _ScreenParams.x * 0.5;
				temp_13 = min(temp_7, 2e+10);
				temp_14 = temp_0 + _VertexOffsetY;
				temp_15 = min(temp_11, 2e+10);
				temp_16 = temp_1 * temp_2;
				temp_17 = temp_6 * temp_2;
				temp_18 = temp_4 + _VertexOffsetX;
				temp_19 = mad(temp_16, 0.5, temp_14);
				temp_20 = v.a1.w;
				temp_21 = mad(temp_17, 0.5, temp_18);
				temp_22 = v.a1.x;
				temp_23 = temp_19 * unity_ObjectToWorld._m11;
				temp_24 = temp_19 * unity_ObjectToWorld._m01;
				temp_25 = temp_19 * unity_ObjectToWorld._m21;
				temp_26 = temp_19 * unity_ObjectToWorld._m31;
				temp_27 = 0.0 - temp_13;
				temp_28 = mad(temp_19, 2.0, temp_27);
				temp_29 = v.a2.y;
				temp_30 = 0.0 - temp_10;
				temp_31 = mad(temp_21, 2.0, temp_30);
				temp_32 = mad(temp_21, unity_ObjectToWorld._m10, temp_23);
				temp_33 = v.a1.y;
				temp_34 = mad(temp_21, unity_ObjectToWorld._m00, temp_24);
				temp_35 = v.a2.x;
				temp_36 = mad(temp_21, unity_ObjectToWorld._m20, temp_25);
				temp_37 = mad(temp_8, unity_ObjectToWorld._m12, temp_32);
				temp_38 = mad(temp_8, unity_ObjectToWorld._m02, temp_34);
				temp_39 = temp_37 + unity_ObjectToWorld._m13;
				temp_40 = mad(temp_21, unity_ObjectToWorld._m30, temp_26);
				temp_41 = min(temp_9, 2e+10);
				temp_42 = mad(temp_8, unity_ObjectToWorld._m22, temp_36);
				temp_43 = v.a1.z;
				temp_44 = temp_38 + unity_ObjectToWorld._m03;
				o.t3.y = temp_29;
				temp_45 = temp_39 * unity_MatrixVP._m31;
				o.t3.x = temp_35;
				temp_46 = mad(temp_8, unity_ObjectToWorld._m32, temp_40);
				temp_47 = temp_42 + unity_ObjectToWorld._m23;
				temp_48 = 0.0 - temp_15;
				temp_49 = temp_48 + temp_28;
				temp_50 = temp_39 * unity_MatrixVP._m01;
				o.t2.y = temp_49;
				temp_51 = mad(temp_44, unity_MatrixVP._m30, temp_45);
				temp_52 = 0.0 - temp_41;
				temp_53 = temp_52 + temp_31;
				temp_54 = temp_39 * unity_MatrixVP._m11;
				o.t2.x = temp_53;
				temp_55 = temp_20 * _Color.w;
				temp_56 = temp_46 + unity_ObjectToWorld._m33;
				o.t0.w = temp_55;
				temp_57 = mad(temp_47, unity_MatrixVP._m32, temp_51);
				temp_58 = mad(temp_44, unity_MatrixVP._m00, temp_50);
				temp_59 = mad(temp_44, unity_MatrixVP._m10, temp_54);
				temp_60 = temp_22 * _Color.x;
				temp_61 = temp_33 * _Color.y;
				temp_62 = temp_39 * unity_MatrixVP._m21;
				temp_63 = mad(temp_56, unity_MatrixVP._m33, temp_57);
				temp_64 = mad(temp_47, unity_MatrixVP._m02, temp_58);
				temp_65 = 1.0 / temp_63;
				temp_66 = mad(temp_47, unity_MatrixVP._m12, temp_59);
				o.pos.w = temp_63;
				temp_67 = mad(temp_44, unity_MatrixVP._m20, temp_62);
				temp_68 = temp_43 * _Color.z;
				temp_69 = mad(temp_56, unity_MatrixVP._m03, temp_64);
				temp_70 = temp_60 * _DiffusePower;
				temp_71 = mad(temp_56, unity_MatrixVP._m13, temp_66);
				o.t0.x = temp_70;
				temp_72 = mad(0.25, _MaskSoftnessX, temp_63);
				temp_73 = temp_69 * temp_65;
				temp_74 = 1.0 / temp_72;
				temp_75 = mad(0.25, _MaskSoftnessY, temp_63);
				temp_76 = mad(temp_47, unity_MatrixVP._m22, temp_67);
				temp_77 = 1.0 / temp_75;
				temp_78 = temp_71 * temp_65;
				temp_79 = temp_61 * _DiffusePower;
				temp_80 = temp_12 * temp_73;
				temp_81 = 1.0 / temp_12;
				temp_82 = temp_68 * _DiffusePower;
				temp_83 = nvn_roundEven(temp_80);
				temp_84 = mad(temp_56, unity_MatrixVP._m23, temp_76);
				o.t0.y = temp_79;
				temp_85 = temp_5 * temp_78;
				temp_86 = 1.0 / temp_5;
				temp_87 = temp_74 * 0.25;
				temp_88 = nvn_roundEven(temp_85);
				temp_89 = temp_77 * 0.25;
				o.t0.z = temp_82;
				temp_90 = temp_83 * temp_81;
				o.pos.z = temp_84;
				temp_91 = temp_88 * temp_86;
				o.t2.z = temp_87;
				temp_92 = temp_63 * temp_90;
				o.t2.w = temp_89;
				temp_93 = temp_63 * temp_91;
				o.pos.x = temp_92;
				o.pos.y = temp_93;
				return o;
			}
			#endif
			#if defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
			// 0007_fs.sph.bin keywords=['UNITY_UI_ALPHACLIP', 'UNITY_UI_CLIP_RECT']
			struct f_in {
				float4 fragcoord : SV_POSITION;
				float4 t0 : TEXCOORD0;
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
				precise float temp_27;
				precise float temp_28;
				bool temp_29;
				precise float temp_30;
				precise float temp_31;
				temp_0 = i.t3.x;
				temp_1 = i.t3.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).w;
				temp_3 = i.t2.x;
				temp_4 = i.t2.y;
				temp_5 = i.t2.z;
				temp_6 = i.t2.w;
				temp_7 = i.t0.w;
				temp_8 = i.t0.y;
				temp_9 = 0.0 - _ClipRect.x;
				temp_10 = temp_9 + _ClipRect.z;
				temp_11 = 0.0 - _ClipRect.y;
				temp_12 = temp_11 + _ClipRect.w;
				temp_13 = abs(temp_3);
				temp_14 = 0.0 - temp_13;
				temp_15 = temp_10 + temp_14;
				temp_16 = i.t0.z;
				temp_17 = abs(temp_4);
				temp_18 = 0.0 - temp_17;
				temp_19 = temp_12 + temp_18;
				temp_20 = temp_15 * temp_5;
				temp_21 = saturate(temp_20);
				temp_22 = temp_19 * temp_6;
				temp_23 = saturate(temp_22);
				temp_24 = temp_21 * temp_23;
				temp_25 = temp_24 * temp_8;
				temp_26 = temp_2 * temp_7;
				temp_27 = i.t0.x;
				temp_28 = temp_24 * temp_26;
				temp_29 = temp_28 < 0.00100000005;
				if (temp_29)
				{
				discard;
				}
				temp_30 = temp_24 * temp_27;
				temp_31 = temp_24 * temp_16;
				o.c0.x = temp_30;
				o.c0.y = temp_25;
				o.c0.z = temp_31;
				o.c0.w = temp_28;
				return o;
			}
			#elif defined(UNITY_UI_ALPHACLIP)
			// 0005_fs.sph.bin keywords=['UNITY_UI_ALPHACLIP']
			struct f_in {
				float4 fragcoord : SV_POSITION;
				float4 t0 : TEXCOORD0;
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
				bool temp_8;
				temp_0 = i.t3.x;
				temp_1 = i.t3.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).w;
				temp_3 = i.t0.w;
				temp_4 = i.t0.x;
				temp_5 = i.t0.y;
				temp_6 = i.t0.z;
				temp_7 = temp_2 * temp_3;
				temp_8 = temp_7 < 0.00100040436;
				if (temp_8)
				{
				discard;
				}
				o.c0.x = temp_4;
				o.c0.y = temp_5;
				o.c0.z = temp_6;
				o.c0.w = temp_7;
				return o;
			}
			#elif defined(UNITY_UI_CLIP_RECT)
			// 0006_fs.sph.bin keywords=['UNITY_UI_CLIP_RECT']
			struct f_in {
				float4 fragcoord : SV_POSITION;
				float4 t0 : TEXCOORD0;
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
				precise float temp_27;
				precise float temp_28;
				precise float temp_29;
				precise float temp_30;
				temp_0 = i.t3.x;
				temp_1 = i.t3.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).w;
				temp_3 = i.t2.x;
				temp_4 = i.t2.y;
				temp_5 = i.t2.z;
				temp_6 = i.t2.w;
				temp_7 = i.t0.w;
				temp_8 = i.t0.x;
				temp_9 = i.t0.y;
				temp_10 = 0.0 - _ClipRect.x;
				temp_11 = temp_10 + _ClipRect.z;
				temp_12 = i.t0.z;
				temp_13 = 0.0 - _ClipRect.y;
				temp_14 = temp_13 + _ClipRect.w;
				temp_15 = abs(temp_3);
				temp_16 = 0.0 - temp_15;
				temp_17 = temp_11 + temp_16;
				temp_18 = abs(temp_4);
				temp_19 = 0.0 - temp_18;
				temp_20 = temp_14 + temp_19;
				temp_21 = temp_17 * temp_5;
				temp_22 = saturate(temp_21);
				temp_23 = temp_20 * temp_6;
				temp_24 = saturate(temp_23);
				temp_25 = temp_22 * temp_24;
				temp_26 = temp_25 * temp_8;
				temp_27 = temp_25 * temp_9;
				temp_28 = temp_25 * temp_12;
				temp_29 = temp_7 * temp_2;
				temp_30 = temp_25 * temp_29;
				o.c0.x = temp_26;
				o.c0.y = temp_27;
				o.c0.z = temp_28;
				o.c0.w = temp_30;
				return o;
			}
			#else
			// 0004_fs.sph.bin keywords=[]
			struct f_in {
				float4 fragcoord : SV_POSITION;
				float4 t0 : TEXCOORD0;
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
				temp_0 = i.fragcoord.w;
				temp_1 = i.t3.x;
				temp_2 = i.fragcoord.w;
				temp_3 = temp_1 * temp_2;
				temp_4 = i.t3.y;
				temp_5 = i.fragcoord.w;
				temp_6 = temp_4 * temp_5;
				temp_7 = 1.0 / temp_0;
				temp_8 = temp_7 * temp_3;
				temp_9 = temp_7 * temp_6;
				temp_10 = tex2D(_MainTex, nvn_nan0(float2(temp_8, temp_9))).w;
				temp_11 = i.t0.w;
				temp_12 = i.t0.x;
				temp_13 = i.t0.y;
				temp_14 = i.t0.z;
				temp_15 = temp_10 * temp_11;
				o.c0.x = temp_12;
				o.c0.y = temp_13;
				o.c0.z = temp_14;
				o.c0.w = temp_15;
				return o;
			}
			#endif
			ENDHLSL
		}
	}
	SubShader {
		Tags { "IgnoreProjector"="true" "Queue"="Transparent" "RenderType"="Transparent" }
		Pass {
			Tags { "IgnoreProjector"="true" "RenderType"="Transparent" }
			Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
			ZTest Always
			ZWrite Off
			Cull Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			float4 _MainTex_ST;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _Color;
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
			// 0008_vs.sph.bin keywords=[]
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : COLOR;
				float4 a2 : TEXCOORD0;
			};
			struct v2f {
				float4 pos : SV_POSITION;
				float4 t0 : TEXCOORD0;
				float4 t1 : TEXCOORD1;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a0.x;
				temp_2 = v.a0.z;
				temp_3 = v.a2.y;
				temp_4 = v.a2.x;
				temp_5 = v.a1.x;
				temp_6 = temp_0 * unity_ObjectToWorld._m11;
				temp_7 = v.a1.y;
				temp_8 = temp_0 * unity_ObjectToWorld._m01;
				temp_9 = v.a1.z;
				temp_10 = mad(temp_1, unity_ObjectToWorld._m10, temp_6);
				temp_11 = temp_0 * unity_ObjectToWorld._m21;
				temp_12 = mad(temp_1, unity_ObjectToWorld._m00, temp_8);
				temp_13 = v.a1.w;
				temp_14 = mad(temp_2, unity_ObjectToWorld._m12, temp_10);
				temp_15 = temp_0 * unity_ObjectToWorld._m31;
				temp_16 = mad(temp_1, unity_ObjectToWorld._m20, temp_11);
				temp_17 = mad(temp_2, unity_ObjectToWorld._m02, temp_12);
				temp_18 = mad(temp_4, _MainTex_ST.x, _MainTex_ST.z);
				temp_19 = temp_14 + unity_ObjectToWorld._m13;
				o.t1.x = temp_18;
				temp_20 = mad(temp_1, unity_ObjectToWorld._m30, temp_15);
				temp_21 = mad(temp_3, _MainTex_ST.y, _MainTex_ST.w);
				temp_22 = temp_17 + unity_ObjectToWorld._m03;
				o.t1.y = temp_21;
				temp_23 = temp_19 * unity_MatrixVP._m01;
				temp_24 = mad(temp_2, unity_ObjectToWorld._m22, temp_16);
				temp_25 = mad(temp_2, unity_ObjectToWorld._m32, temp_20);
				temp_26 = temp_5 + -0.0;
				temp_27 = saturate(temp_26);
				temp_28 = temp_19 * unity_MatrixVP._m11;
				o.t0.x = temp_27;
				temp_29 = mad(temp_22, unity_MatrixVP._m00, temp_23);
				temp_30 = temp_19 * unity_MatrixVP._m21;
				temp_31 = temp_19 * unity_MatrixVP._m31;
				temp_32 = temp_13 + -0.0;
				temp_33 = saturate(temp_32);
				temp_34 = temp_24 + unity_ObjectToWorld._m23;
				o.t0.w = temp_33;
				temp_35 = mad(temp_22, unity_MatrixVP._m10, temp_28);
				temp_36 = mad(temp_22, unity_MatrixVP._m20, temp_30);
				temp_37 = mad(temp_22, unity_MatrixVP._m30, temp_31);
				temp_38 = temp_25 + unity_ObjectToWorld._m33;
				temp_39 = temp_7 + -0.0;
				temp_40 = saturate(temp_39);
				temp_41 = mad(temp_34, unity_MatrixVP._m02, temp_29);
				o.t0.y = temp_40;
				temp_42 = mad(temp_34, unity_MatrixVP._m12, temp_35);
				temp_43 = mad(temp_34, unity_MatrixVP._m22, temp_36);
				temp_44 = mad(temp_34, unity_MatrixVP._m32, temp_37);
				temp_45 = temp_9 + -0.0;
				temp_46 = saturate(temp_45);
				temp_47 = mad(temp_38, unity_MatrixVP._m03, temp_41);
				o.t0.z = temp_46;
				temp_48 = mad(temp_38, unity_MatrixVP._m13, temp_42);
				o.pos.x = temp_47;
				temp_49 = mad(temp_38, unity_MatrixVP._m23, temp_43);
				o.pos.y = temp_48;
				temp_50 = mad(temp_38, unity_MatrixVP._m33, temp_44);
				o.pos.z = temp_49;
				o.pos.w = temp_50;
				return o;
			}
			// 0009_fs.sph.bin keywords=[]
			struct f_in {
				float4 fragcoord : SV_POSITION;
				float4 t0 : TEXCOORD0;
				float4 t1 : TEXCOORD1;
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
				temp_0 = i.fragcoord.w;
				temp_1 = i.t1.x;
				temp_2 = i.fragcoord.w;
				temp_3 = temp_1 * temp_2;
				temp_4 = i.t1.y;
				temp_5 = i.fragcoord.w;
				temp_6 = temp_4 * temp_5;
				temp_7 = 1.0 / temp_0;
				temp_8 = temp_7 * temp_3;
				temp_9 = temp_7 * temp_6;
				temp_10 = tex2D(_MainTex, nvn_nan0(float2(temp_8, temp_9))).w;
				temp_11 = i.t0.x;
				temp_12 = i.t0.y;
				temp_13 = i.t0.z;
				temp_14 = temp_11 * _Color.x;
				temp_15 = temp_12 * _Color.y;
				temp_16 = temp_13 * _Color.z;
				temp_17 = temp_10 * _Color.w;
				o.c0.x = temp_14;
				o.c0.y = temp_15;
				o.c0.z = temp_16;
				o.c0.w = temp_17;
				return o;
			}
			ENDHLSL
		}
	}
}