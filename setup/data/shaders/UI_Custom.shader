Shader "UI/Custom" {
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
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcColor ("Src Blend Color", Float) = 5
		[Enum(UnityEngine.Rendering.BlendMode)] _DestColor ("Dest Blend Color", Float) = 10
		[Enum(UnityEngine.Rendering.BlendOp)] _BlendOpColor ("Blend Op Color", Float) = 0
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcAlpha ("Src Blend Alpha", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DestAlpha ("Dest Blend Alpha", Float) = 0
		[Enum(UnityEngine.Rendering.BlendOp)] _BlendOpAlpha ("Blend Op Alpha", Float) = 0
		[KeywordEnum(Off, On)] _GrayscaleEnabled ("Glayscale Enabled", Float) = 0
		_GrayscaleAmount ("Grayscale Amount", Range(0, 1)) = 1
		[KeywordEnum(Off, On)] _OverrideTextureAlphaEnabled ("Override Texture Alpha Enabled", Float) = 0
		_OverrideTextureAlpha ("_Override Texture Alpha", Range(0, 1)) = 1
	}

	SubShader {
		Tags { "CanUseSpriteAtlas"="true" "IgnoreProjector"="true" "PreviewType"="Plane" "Queue"="Transparent" "RenderType"="Transparent" }
		Pass {
			Name "Default"
			Tags { "CanUseSpriteAtlas"="true" "IgnoreProjector"="true" "PreviewType"="Plane" "RenderType"="Transparent" }
			Blend [_SrcColor] [_DestColor], [_SrcAlpha] [_DestAlpha]
			BlendOp [_BlendOpColor], [_BlendOpAlpha]
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
			#pragma shader_feature _ _GRAYSCALEENABLED_ON
			#pragma shader_feature _ _OVERRIDETEXTUREALPHAENABLED_ON
			#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
			float4 _Color;
			float4 _MainTex_ST;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _TextureSampleAdd;
			float4 _ClipRect;
			float _OverrideTextureAlpha;
			float _GrayscaleAmount;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a0.x;
				temp_2 = v.a2.x;
				temp_3 = v.a0.z;
				temp_4 = v.a1.z;
				temp_5 = v.a2.y;
				temp_6 = temp_0 * unity_ObjectToWorld._m11;
				temp_7 = v.a1.x;
				temp_8 = temp_0 * unity_ObjectToWorld._m01;
				temp_9 = v.a1.y;
				temp_10 = mad(temp_1, unity_ObjectToWorld._m10, temp_6);
				temp_11 = v.a1.w;
				temp_12 = mad(temp_2, _MainTex_ST.x, _MainTex_ST.z);
				temp_13 = v.a0.w;
				temp_14 = mad(temp_1, unity_ObjectToWorld._m00, temp_8);
				o.t1.z = temp_3;
				temp_15 = temp_0 * unity_ObjectToWorld._m21;
				o.t1.y = temp_0;
				temp_16 = temp_0 * unity_ObjectToWorld._m31;
				o.t2.x = temp_12;
				temp_17 = mad(temp_3, unity_ObjectToWorld._m12, temp_10);
				o.t1.x = temp_1;
				temp_18 = mad(temp_1, unity_ObjectToWorld._m20, temp_15);
				o.t1.w = temp_13;
				temp_19 = mad(temp_1, unity_ObjectToWorld._m30, temp_16);
				temp_20 = temp_17 + unity_ObjectToWorld._m13;
				temp_21 = mad(temp_3, unity_ObjectToWorld._m02, temp_14);
				temp_22 = mad(temp_5, _MainTex_ST.y, _MainTex_ST.w);
				temp_23 = temp_7 * _Color.x;
				o.t2.y = temp_22;
				temp_24 = mad(temp_3, unity_ObjectToWorld._m22, temp_18);
				o.t0.x = temp_23;
				temp_25 = mad(temp_3, unity_ObjectToWorld._m32, temp_19);
				temp_26 = temp_4 * _Color.z;
				temp_27 = temp_20 * unity_MatrixVP._m01;
				o.t0.z = temp_26;
				temp_28 = temp_20 * unity_MatrixVP._m11;
				temp_29 = temp_20 * unity_MatrixVP._m21;
				temp_30 = temp_21 + unity_ObjectToWorld._m03;
				temp_31 = temp_20 * unity_MatrixVP._m31;
				temp_32 = temp_24 + unity_ObjectToWorld._m23;
				temp_33 = temp_25 + unity_ObjectToWorld._m33;
				temp_34 = temp_9 * _Color.y;
				temp_35 = temp_11 * _Color.w;
				o.t0.y = temp_34;
				temp_36 = mad(temp_30, unity_MatrixVP._m00, temp_27);
				o.t0.w = temp_35;
				temp_37 = mad(temp_30, unity_MatrixVP._m10, temp_28);
				temp_38 = mad(temp_30, unity_MatrixVP._m20, temp_29);
				temp_39 = mad(temp_30, unity_MatrixVP._m30, temp_31);
				temp_40 = mad(temp_32, unity_MatrixVP._m02, temp_36);
				temp_41 = mad(temp_32, unity_MatrixVP._m12, temp_37);
				temp_42 = mad(temp_32, unity_MatrixVP._m22, temp_38);
				temp_43 = mad(temp_32, unity_MatrixVP._m32, temp_39);
				temp_44 = mad(temp_33, unity_MatrixVP._m03, temp_40);
				temp_45 = mad(temp_33, unity_MatrixVP._m13, temp_41);
				o.pos.x = temp_44;
				temp_46 = mad(temp_33, unity_MatrixVP._m23, temp_42);
				o.pos.y = temp_45;
				temp_47 = mad(temp_33, unity_MatrixVP._m33, temp_43);
				o.pos.z = temp_46;
				o.pos.w = temp_47;
				return o;
			}
			#endif
			#if defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && defined(_GRAYSCALEENABLED_ON) && defined(_OVERRIDETEXTUREALPHAENABLED_ON)
			// 0019_fs.sph.bin keywords=['_GRAYSCALEENABLED_ON', '_OVERRIDETEXTUREALPHAENABLED_ON'] local=['UNITY_UI_ALPHACLIP', 'UNITY_UI_CLIP_RECT']
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
				bool temp_3;
				precise float temp_4;
				precise float temp_5;
				bool temp_6;
				precise float temp_7;
				precise float temp_8;
				precise float temp_9;
				bool temp_10;
				precise float temp_11;
				bool temp_12;
				precise float temp_13;
				precise float temp_14;
				precise float temp_15;
				precise float temp_16;
				precise float temp_17;
				bool temp_18;
				precise float3 temp_19;
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
				temp_0 = i.t1.x;
				temp_1 = i.t1.y;
				temp_2 = i.t0.w;
				temp_3 = temp_0 >= _ClipRect.x;
				temp_4 = temp_3 ? 1.0 : 0.0;
				temp_5 = i.t2.x;
				temp_6 = temp_0 <= _ClipRect.z;
				temp_7 = temp_6 ? 1.0 : 0.0;
				temp_8 = i.t2.y;
				temp_9 = temp_2 * _OverrideTextureAlpha;
				temp_10 = temp_1 >= _ClipRect.y;
				temp_11 = temp_10 ? 1.0 : 0.0;
				temp_12 = temp_1 <= _ClipRect.w;
				temp_13 = temp_12 ? 1.0 : 0.0;
				temp_14 = temp_7 * temp_4;
				temp_15 = temp_13 * temp_11;
				temp_16 = temp_14 * temp_15;
				temp_17 = temp_9 * temp_16;
				temp_18 = temp_17 < 0.00100000005;
				if (temp_18)
				{
				discard;
				}
				temp_19 = tex2D(_MainTex, nvn_nan0(float2(temp_5, temp_8))).xyz;
				temp_20 = temp_19.x;
				temp_21 = temp_19.y;
				temp_22 = temp_19.z;
				temp_23 = i.t0.x;
				temp_24 = i.t0.y;
				temp_25 = i.t0.z;
				temp_26 = temp_20 + _TextureSampleAdd.x;
				temp_27 = temp_21 + _TextureSampleAdd.y;
				temp_28 = temp_22 + _TextureSampleAdd.z;
				temp_29 = temp_23 * temp_26;
				temp_30 = temp_24 * temp_27;
				temp_31 = temp_25 * temp_28;
				temp_32 = temp_29 * 0.300048828;
				temp_33 = mad(temp_30, 0.58984375, temp_32);
				temp_34 = mad(temp_31, 0.109985352, temp_33);
				temp_35 = 0.0 - temp_26;
				temp_36 = mad(temp_23, temp_35, temp_34);
				temp_37 = 0.0 - temp_27;
				temp_38 = mad(temp_24, temp_37, temp_34);
				temp_39 = 0.0 - temp_28;
				temp_40 = mad(temp_25, temp_39, temp_34);
				temp_41 = mad(temp_36, _GrayscaleAmount, temp_29);
				temp_42 = mad(temp_38, _GrayscaleAmount, temp_30);
				temp_43 = mad(temp_40, _GrayscaleAmount, temp_31);
				o.c0.x = temp_41;
				o.c0.y = temp_42;
				o.c0.z = temp_43;
				o.c0.w = temp_17;
				return o;
			}
			#elif defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && defined(_OVERRIDETEXTUREALPHAENABLED_ON)
			// 0011_fs.sph.bin keywords=['_OVERRIDETEXTUREALPHAENABLED_ON'] local=['UNITY_UI_ALPHACLIP', 'UNITY_UI_CLIP_RECT']
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
				bool temp_3;
				precise float temp_4;
				precise float temp_5;
				bool temp_6;
				precise float temp_7;
				precise float temp_8;
				bool temp_9;
				precise float temp_10;
				bool temp_11;
				precise float temp_12;
				precise float temp_13;
				precise float temp_14;
				precise float temp_15;
				precise float temp_16;
				precise float temp_17;
				bool temp_18;
				precise float3 temp_19;
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
				temp_0 = i.t1.x;
				temp_1 = i.t1.y;
				temp_2 = i.t0.w;
				temp_3 = temp_0 >= _ClipRect.x;
				temp_4 = temp_3 ? 1.0 : 0.0;
				temp_5 = i.t2.y;
				temp_6 = temp_1 >= _ClipRect.y;
				temp_7 = temp_6 ? 1.0 : 0.0;
				temp_8 = temp_2 * _OverrideTextureAlpha;
				temp_9 = temp_0 <= _ClipRect.z;
				temp_10 = temp_9 ? 1.0 : 0.0;
				temp_11 = temp_1 <= _ClipRect.w;
				temp_12 = temp_11 ? 1.0 : 0.0;
				temp_13 = temp_10 * temp_4;
				temp_14 = temp_12 * temp_7;
				temp_15 = i.t2.x;
				temp_16 = temp_13 * temp_14;
				temp_17 = temp_8 * temp_16;
				temp_18 = temp_17 < 0.00100000005;
				if (temp_18)
				{
				discard;
				}
				temp_19 = tex2D(_MainTex, nvn_nan0(float2(temp_15, temp_5))).xyz;
				temp_20 = temp_19.x;
				temp_21 = temp_19.y;
				temp_22 = temp_19.z;
				temp_23 = i.t0.x;
				temp_24 = i.t0.y;
				temp_25 = i.t0.z;
				temp_26 = temp_20 + _TextureSampleAdd.x;
				temp_27 = temp_21 + _TextureSampleAdd.y;
				temp_28 = temp_22 + _TextureSampleAdd.z;
				temp_29 = temp_23 * temp_26;
				temp_30 = temp_24 * temp_27;
				temp_31 = temp_25 * temp_28;
				o.c0.x = temp_29;
				o.c0.y = temp_30;
				o.c0.z = temp_31;
				o.c0.w = temp_17;
				return o;
			}
			#elif defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT) && defined(_GRAYSCALEENABLED_ON)
			// 0015_fs.sph.bin keywords=['_GRAYSCALEENABLED_ON'] local=['UNITY_UI_ALPHACLIP', 'UNITY_UI_CLIP_RECT']
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
				precise float4 temp_2;
				precise float temp_3;
				precise float temp_4;
				precise float temp_5;
				precise float temp_6;
				precise float temp_7;
				precise float temp_8;
				precise float temp_9;
				bool temp_10;
				precise float temp_11;
				bool temp_12;
				precise float temp_13;
				bool temp_14;
				precise float temp_15;
				bool temp_16;
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
				temp_0 = i.t2.x;
				temp_1 = i.t2.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).xyzw;
				temp_3 = temp_2.x;
				temp_4 = temp_2.y;
				temp_5 = temp_2.z;
				temp_6 = temp_2.w;
				temp_7 = i.t1.x;
				temp_8 = i.t1.y;
				temp_9 = i.t0.w;
				temp_10 = temp_7 >= _ClipRect.x;
				temp_11 = temp_10 ? 1.0 : 0.0;
				temp_12 = temp_8 >= _ClipRect.y;
				temp_13 = temp_12 ? 1.0 : 0.0;
				temp_14 = temp_7 <= _ClipRect.z;
				temp_15 = temp_14 ? 1.0 : 0.0;
				temp_16 = temp_8 <= _ClipRect.w;
				temp_17 = temp_16 ? 1.0 : 0.0;
				temp_18 = temp_15 * temp_11;
				temp_19 = i.t0.z;
				temp_20 = temp_13 * temp_17;
				temp_21 = temp_18 * temp_20;
				temp_22 = i.t0.y;
				temp_23 = temp_6 + _TextureSampleAdd.w;
				temp_24 = temp_23 * temp_9;
				temp_25 = temp_24 * temp_21;
				temp_26 = i.t0.x;
				temp_27 = temp_25 < 0.00100000005;
				if (temp_27)
				{
				discard;
				}
				temp_28 = temp_3 + _TextureSampleAdd.x;
				temp_29 = temp_4 + _TextureSampleAdd.y;
				temp_30 = temp_5 + _TextureSampleAdd.z;
				temp_31 = temp_28 * temp_26;
				temp_32 = temp_29 * temp_22;
				temp_33 = temp_30 * temp_19;
				temp_34 = temp_31 * 0.300048828;
				temp_35 = mad(temp_32, 0.58984375, temp_34);
				temp_36 = mad(temp_33, 0.109985352, temp_35);
				temp_37 = 0.0 - temp_26;
				temp_38 = mad(temp_28, temp_37, temp_36);
				temp_39 = 0.0 - temp_22;
				temp_40 = mad(temp_29, temp_39, temp_36);
				temp_41 = 0.0 - temp_19;
				temp_42 = mad(temp_30, temp_41, temp_36);
				temp_43 = mad(temp_38, _GrayscaleAmount, temp_31);
				temp_44 = mad(temp_40, _GrayscaleAmount, temp_32);
				temp_45 = mad(temp_42, _GrayscaleAmount, temp_33);
				o.c0.x = temp_43;
				o.c0.y = temp_44;
				o.c0.z = temp_45;
				o.c0.w = temp_25;
				return o;
			}
			#elif defined(UNITY_UI_ALPHACLIP) && defined(_GRAYSCALEENABLED_ON) && defined(_OVERRIDETEXTUREALPHAENABLED_ON)
			// 0017_fs.sph.bin keywords=['_GRAYSCALEENABLED_ON', '_OVERRIDETEXTUREALPHAENABLED_ON'] local=['UNITY_UI_ALPHACLIP']
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
				bool temp_4;
				precise float3 temp_5;
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
				temp_0 = i.t0.w;
				temp_1 = i.t2.x;
				temp_2 = i.t2.y;
				temp_3 = temp_0 * _OverrideTextureAlpha;
				temp_4 = temp_3 < 0.00100000005;
				if (temp_4)
				{
				discard;
				}
				temp_5 = tex2D(_MainTex, nvn_nan0(float2(temp_1, temp_2))).xyz;
				temp_6 = temp_5.x;
				temp_7 = temp_5.y;
				temp_8 = temp_5.z;
				temp_9 = i.t0.x;
				temp_10 = i.t0.y;
				temp_11 = i.t0.z;
				temp_12 = temp_6 + _TextureSampleAdd.x;
				temp_13 = temp_7 + _TextureSampleAdd.y;
				temp_14 = temp_8 + _TextureSampleAdd.z;
				temp_15 = temp_9 * temp_12;
				temp_16 = temp_10 * temp_13;
				temp_17 = temp_11 * temp_14;
				temp_18 = temp_15 * 0.300048828;
				temp_19 = mad(temp_16, 0.58984375, temp_18);
				temp_20 = mad(temp_17, 0.109985352, temp_19);
				temp_21 = 0.0 - temp_12;
				temp_22 = mad(temp_9, temp_21, temp_20);
				temp_23 = 0.0 - temp_13;
				temp_24 = mad(temp_10, temp_23, temp_20);
				temp_25 = 0.0 - temp_14;
				temp_26 = mad(temp_11, temp_25, temp_20);
				temp_27 = mad(temp_22, _GrayscaleAmount, temp_15);
				temp_28 = mad(temp_24, _GrayscaleAmount, temp_16);
				temp_29 = mad(temp_26, _GrayscaleAmount, temp_17);
				o.c0.x = temp_27;
				o.c0.y = temp_28;
				o.c0.z = temp_29;
				o.c0.w = temp_3;
				return o;
			}
			#elif defined(UNITY_UI_CLIP_RECT) && defined(_GRAYSCALEENABLED_ON) && defined(_OVERRIDETEXTUREALPHAENABLED_ON)
			// 0018_fs.sph.bin keywords=['_GRAYSCALEENABLED_ON', '_OVERRIDETEXTUREALPHAENABLED_ON'] local=['UNITY_UI_CLIP_RECT']
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
				precise float3 temp_2;
				precise float temp_3;
				precise float temp_4;
				precise float temp_5;
				precise float temp_6;
				precise float temp_7;
				precise float temp_8;
				precise float temp_9;
				bool temp_10;
				precise float temp_11;
				bool temp_12;
				precise float temp_13;
				precise float temp_14;
				bool temp_15;
				precise float temp_16;
				bool temp_17;
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
				temp_0 = i.t2.x;
				temp_1 = i.t2.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).xyz;
				temp_3 = temp_2.x;
				temp_4 = temp_2.y;
				temp_5 = temp_2.z;
				temp_6 = i.t1.x;
				temp_7 = i.t1.y;
				temp_8 = i.t0.w;
				temp_9 = i.t0.x;
				temp_10 = temp_6 >= _ClipRect.x;
				temp_11 = temp_10 ? 1.0 : 0.0;
				temp_12 = temp_6 <= _ClipRect.z;
				temp_13 = temp_12 ? 1.0 : 0.0;
				temp_14 = temp_8 * _OverrideTextureAlpha;
				temp_15 = temp_7 >= _ClipRect.y;
				temp_16 = temp_15 ? 1.0 : 0.0;
				temp_17 = temp_7 <= _ClipRect.w;
				temp_18 = temp_17 ? 1.0 : 0.0;
				temp_19 = i.t0.y;
				temp_20 = temp_13 * temp_11;
				temp_21 = i.t0.z;
				temp_22 = temp_16 * temp_18;
				temp_23 = temp_20 * temp_22;
				temp_24 = temp_14 * temp_23;
				temp_25 = temp_3 + _TextureSampleAdd.x;
				temp_26 = temp_4 + _TextureSampleAdd.y;
				temp_27 = temp_5 + _TextureSampleAdd.z;
				temp_28 = temp_9 * temp_25;
				temp_29 = temp_19 * temp_26;
				temp_30 = temp_21 * temp_27;
				temp_31 = temp_28 * 0.300048828;
				temp_32 = mad(temp_29, 0.58984375, temp_31);
				temp_33 = mad(temp_30, 0.109985352, temp_32);
				temp_34 = 0.0 - temp_25;
				temp_35 = mad(temp_9, temp_34, temp_33);
				temp_36 = 0.0 - temp_26;
				temp_37 = mad(temp_19, temp_36, temp_33);
				temp_38 = 0.0 - temp_27;
				temp_39 = mad(temp_21, temp_38, temp_33);
				temp_40 = mad(temp_35, _GrayscaleAmount, temp_28);
				temp_41 = mad(temp_37, _GrayscaleAmount, temp_29);
				temp_42 = mad(temp_39, _GrayscaleAmount, temp_30);
				o.c0.x = temp_40;
				o.c0.y = temp_41;
				o.c0.z = temp_42;
				o.c0.w = temp_24;
				return o;
			}
			#elif defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
			// 0007_fs.sph.bin keywords=[] local=['UNITY_UI_ALPHACLIP', 'UNITY_UI_CLIP_RECT']
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
				precise float4 temp_2;
				precise float temp_3;
				precise float temp_4;
				precise float temp_5;
				precise float temp_6;
				precise float temp_7;
				precise float temp_8;
				precise float temp_9;
				bool temp_10;
				precise float temp_11;
				bool temp_12;
				precise float temp_13;
				bool temp_14;
				precise float temp_15;
				bool temp_16;
				precise float temp_17;
				precise float temp_18;
				precise float temp_19;
				precise float temp_20;
				precise float temp_21;
				precise float temp_22;
				precise float temp_23;
				precise float temp_24;
				precise float temp_25;
				bool temp_26;
				precise float temp_27;
				precise float temp_28;
				precise float temp_29;
				precise float temp_30;
				precise float temp_31;
				precise float temp_32;
				precise float temp_33;
				temp_0 = i.t2.x;
				temp_1 = i.t2.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).xyzw;
				temp_3 = temp_2.x;
				temp_4 = temp_2.y;
				temp_5 = temp_2.z;
				temp_6 = temp_2.w;
				temp_7 = i.t1.x;
				temp_8 = i.t1.y;
				temp_9 = i.t0.w;
				temp_10 = temp_7 >= _ClipRect.x;
				temp_11 = temp_10 ? 1.0 : 0.0;
				temp_12 = temp_7 <= _ClipRect.z;
				temp_13 = temp_12 ? 1.0 : 0.0;
				temp_14 = temp_8 >= _ClipRect.y;
				temp_15 = temp_14 ? 1.0 : 0.0;
				temp_16 = temp_8 <= _ClipRect.w;
				temp_17 = temp_16 ? 1.0 : 0.0;
				temp_18 = temp_13 * temp_11;
				temp_19 = temp_15 * temp_17;
				temp_20 = temp_18 * temp_19;
				temp_21 = temp_6 + _TextureSampleAdd.w;
				temp_22 = temp_9 * temp_21;
				temp_23 = i.t0.y;
				temp_24 = temp_22 * temp_20;
				temp_25 = i.t0.x;
				temp_26 = temp_24 < 0.00100000005;
				temp_27 = i.t0.z;
				if (temp_26)
				{
				discard;
				}
				temp_28 = temp_3 + _TextureSampleAdd.x;
				temp_29 = temp_4 + _TextureSampleAdd.y;
				temp_30 = temp_5 + _TextureSampleAdd.z;
				temp_31 = temp_25 * temp_28;
				temp_32 = temp_23 * temp_29;
				temp_33 = temp_27 * temp_30;
				o.c0.x = temp_31;
				o.c0.y = temp_32;
				o.c0.z = temp_33;
				o.c0.w = temp_24;
				return o;
			}
			#elif defined(UNITY_UI_ALPHACLIP) && defined(_OVERRIDETEXTUREALPHAENABLED_ON)
			// 0009_fs.sph.bin keywords=['_OVERRIDETEXTUREALPHAENABLED_ON'] local=['UNITY_UI_ALPHACLIP']
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
				bool temp_4;
				precise float3 temp_5;
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
				temp_0 = i.t0.w;
				temp_1 = i.t2.x;
				temp_2 = i.t2.y;
				temp_3 = temp_0 * _OverrideTextureAlpha;
				temp_4 = temp_3 < 0.00100000005;
				if (temp_4)
				{
				discard;
				}
				temp_5 = tex2D(_MainTex, nvn_nan0(float2(temp_1, temp_2))).xyz;
				temp_6 = temp_5.x;
				temp_7 = temp_5.y;
				temp_8 = temp_5.z;
				temp_9 = i.t0.x;
				temp_10 = i.t0.y;
				temp_11 = i.t0.z;
				temp_12 = temp_6 + _TextureSampleAdd.x;
				temp_13 = temp_7 + _TextureSampleAdd.y;
				temp_14 = temp_8 + _TextureSampleAdd.z;
				temp_15 = temp_9 * temp_12;
				temp_16 = temp_10 * temp_13;
				temp_17 = temp_11 * temp_14;
				o.c0.x = temp_15;
				o.c0.y = temp_16;
				o.c0.z = temp_17;
				o.c0.w = temp_3;
				return o;
			}
			#elif defined(UNITY_UI_CLIP_RECT) && defined(_OVERRIDETEXTUREALPHAENABLED_ON)
			// 0010_fs.sph.bin keywords=['_OVERRIDETEXTUREALPHAENABLED_ON'] local=['UNITY_UI_CLIP_RECT']
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
				precise float3 temp_2;
				precise float temp_3;
				precise float temp_4;
				precise float temp_5;
				precise float temp_6;
				precise float temp_7;
				precise float temp_8;
				precise float temp_9;
				precise float temp_10;
				bool temp_11;
				precise float temp_12;
				bool temp_13;
				precise float temp_14;
				precise float temp_15;
				precise float temp_16;
				bool temp_17;
				precise float temp_18;
				bool temp_19;
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
				temp_0 = i.t2.x;
				temp_1 = i.t2.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).xyz;
				temp_3 = temp_2.x;
				temp_4 = temp_2.y;
				temp_5 = temp_2.z;
				temp_6 = i.t1.x;
				temp_7 = i.t1.y;
				temp_8 = i.t0.w;
				temp_9 = i.t0.x;
				temp_10 = i.t0.y;
				temp_11 = temp_6 >= _ClipRect.x;
				temp_12 = temp_11 ? 1.0 : 0.0;
				temp_13 = temp_6 <= _ClipRect.z;
				temp_14 = temp_13 ? 1.0 : 0.0;
				temp_15 = i.t0.z;
				temp_16 = temp_8 * _OverrideTextureAlpha;
				temp_17 = temp_7 >= _ClipRect.y;
				temp_18 = temp_17 ? 1.0 : 0.0;
				temp_19 = temp_7 <= _ClipRect.w;
				temp_20 = temp_19 ? 1.0 : 0.0;
				temp_21 = temp_14 * temp_12;
				temp_22 = temp_20 * temp_18;
				temp_23 = temp_21 * temp_22;
				temp_24 = temp_16 * temp_23;
				temp_25 = temp_3 + _TextureSampleAdd.x;
				temp_26 = temp_4 + _TextureSampleAdd.y;
				temp_27 = temp_5 + _TextureSampleAdd.z;
				temp_28 = temp_9 * temp_25;
				temp_29 = temp_10 * temp_26;
				temp_30 = temp_15 * temp_27;
				o.c0.x = temp_28;
				o.c0.y = temp_29;
				o.c0.z = temp_30;
				o.c0.w = temp_24;
				return o;
			}
			#elif defined(UNITY_UI_ALPHACLIP) && defined(_GRAYSCALEENABLED_ON)
			// 0013_fs.sph.bin keywords=['_GRAYSCALEENABLED_ON'] local=['UNITY_UI_ALPHACLIP']
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
				precise float4 temp_2;
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
				bool temp_13;
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
				temp_0 = i.t2.x;
				temp_1 = i.t2.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).xyzw;
				temp_3 = temp_2.x;
				temp_4 = temp_2.y;
				temp_5 = temp_2.z;
				temp_6 = temp_2.w;
				temp_7 = i.t0.w;
				temp_8 = i.t0.x;
				temp_9 = i.t0.z;
				temp_10 = temp_6 + _TextureSampleAdd.w;
				temp_11 = temp_7 * temp_10;
				temp_12 = i.t0.y;
				temp_13 = temp_11 < 0.00100000005;
				if (temp_13)
				{
				discard;
				}
				temp_14 = temp_3 + _TextureSampleAdd.x;
				temp_15 = temp_4 + _TextureSampleAdd.y;
				temp_16 = temp_5 + _TextureSampleAdd.z;
				temp_17 = temp_14 * temp_8;
				temp_18 = temp_15 * temp_12;
				temp_19 = temp_16 * temp_9;
				temp_20 = temp_17 * 0.300048828;
				temp_21 = mad(temp_18, 0.58984375, temp_20);
				temp_22 = mad(temp_19, 0.109985352, temp_21);
				temp_23 = 0.0 - temp_12;
				temp_24 = mad(temp_15, temp_23, temp_22);
				temp_25 = 0.0 - temp_8;
				temp_26 = mad(temp_14, temp_25, temp_22);
				temp_27 = 0.0 - temp_9;
				temp_28 = mad(temp_16, temp_27, temp_22);
				temp_29 = mad(temp_24, _GrayscaleAmount, temp_18);
				temp_30 = mad(temp_26, _GrayscaleAmount, temp_17);
				temp_31 = mad(temp_28, _GrayscaleAmount, temp_19);
				o.c0.x = temp_30;
				o.c0.y = temp_29;
				o.c0.z = temp_31;
				o.c0.w = temp_11;
				return o;
			}
			#elif defined(UNITY_UI_CLIP_RECT) && defined(_GRAYSCALEENABLED_ON)
			// 0014_fs.sph.bin keywords=['_GRAYSCALEENABLED_ON'] local=['UNITY_UI_CLIP_RECT']
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
				precise float4 temp_2;
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
				bool temp_26;
				precise float temp_27;
				precise float temp_28;
				precise float temp_29;
				bool temp_30;
				precise float temp_31;
				precise float temp_32;
				precise float temp_33;
				precise float temp_34;
				precise float temp_35;
				precise float temp_36;
				precise float temp_37;
				bool temp_38;
				precise float temp_39;
				bool temp_40;
				precise float temp_41;
				precise float temp_42;
				precise float temp_43;
				precise float temp_44;
				temp_0 = i.t2.x;
				temp_1 = i.t2.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).xyzw;
				temp_3 = temp_2.x;
				temp_4 = temp_2.y;
				temp_5 = temp_2.z;
				temp_6 = temp_2.w;
				temp_7 = i.t0.x;
				temp_8 = i.t0.y;
				temp_9 = i.t0.w;
				temp_10 = i.t0.z;
				temp_11 = i.t1.x;
				temp_12 = i.t1.y;
				temp_13 = temp_3 + _TextureSampleAdd.x;
				temp_14 = temp_4 + _TextureSampleAdd.y;
				temp_15 = temp_6 + _TextureSampleAdd.w;
				temp_16 = temp_5 + _TextureSampleAdd.z;
				temp_17 = temp_7 * temp_13;
				temp_18 = temp_8 * temp_14;
				temp_19 = temp_9 * temp_15;
				temp_20 = temp_10 * temp_16;
				temp_21 = temp_17 * 0.300048828;
				temp_22 = mad(temp_18, 0.58984375, temp_21);
				temp_23 = mad(temp_20, 0.109985352, temp_22);
				temp_24 = 0.0 - temp_13;
				temp_25 = mad(temp_7, temp_24, temp_23);
				temp_26 = temp_11 >= _ClipRect.x;
				temp_27 = temp_26 ? 1.0 : 0.0;
				temp_28 = 0.0 - temp_14;
				temp_29 = mad(temp_8, temp_28, temp_23);
				temp_30 = temp_11 <= _ClipRect.z;
				temp_31 = temp_30 ? 1.0 : 0.0;
				temp_32 = 0.0 - temp_16;
				temp_33 = mad(temp_10, temp_32, temp_23);
				temp_34 = mad(temp_25, _GrayscaleAmount, temp_17);
				temp_35 = mad(temp_29, _GrayscaleAmount, temp_18);
				temp_36 = temp_31 * temp_27;
				temp_37 = mad(temp_33, _GrayscaleAmount, temp_20);
				temp_38 = temp_12 >= _ClipRect.y;
				temp_39 = temp_38 ? 1.0 : 0.0;
				temp_40 = temp_12 <= _ClipRect.w;
				temp_41 = temp_40 ? 1.0 : 0.0;
				temp_42 = temp_41 * temp_39;
				temp_43 = temp_36 * temp_42;
				temp_44 = temp_19 * temp_43;
				o.c0.x = temp_34;
				o.c0.y = temp_35;
				o.c0.z = temp_37;
				o.c0.w = temp_44;
				return o;
			}
			#elif defined(_GRAYSCALEENABLED_ON) && defined(_OVERRIDETEXTUREALPHAENABLED_ON)
			// 0016_fs.sph.bin keywords=['_GRAYSCALEENABLED_ON', '_OVERRIDETEXTUREALPHAENABLED_ON']
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
				precise float3 temp_2;
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
				temp_0 = i.t2.x;
				temp_1 = i.t2.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).xyz;
				temp_3 = temp_2.x;
				temp_4 = temp_2.y;
				temp_5 = temp_2.z;
				temp_6 = i.t0.x;
				temp_7 = i.t0.y;
				temp_8 = i.t0.z;
				temp_9 = i.t0.w;
				temp_10 = temp_9 * _OverrideTextureAlpha;
				temp_11 = temp_3 + _TextureSampleAdd.x;
				temp_12 = temp_4 + _TextureSampleAdd.y;
				temp_13 = temp_5 + _TextureSampleAdd.z;
				temp_14 = temp_6 * temp_11;
				temp_15 = temp_7 * temp_12;
				temp_16 = temp_8 * temp_13;
				temp_17 = temp_14 * 0.300048828;
				temp_18 = mad(temp_15, 0.58984375, temp_17);
				temp_19 = mad(temp_16, 0.109985352, temp_18);
				temp_20 = 0.0 - temp_11;
				temp_21 = mad(temp_6, temp_20, temp_19);
				temp_22 = 0.0 - temp_12;
				temp_23 = mad(temp_7, temp_22, temp_19);
				temp_24 = 0.0 - temp_13;
				temp_25 = mad(temp_8, temp_24, temp_19);
				temp_26 = mad(temp_21, _GrayscaleAmount, temp_14);
				temp_27 = mad(temp_23, _GrayscaleAmount, temp_15);
				temp_28 = mad(temp_25, _GrayscaleAmount, temp_16);
				o.c0.x = temp_26;
				o.c0.y = temp_27;
				o.c0.z = temp_28;
				o.c0.w = temp_10;
				return o;
			}
			#elif defined(UNITY_UI_ALPHACLIP)
			// 0005_fs.sph.bin keywords=[] local=['UNITY_UI_ALPHACLIP']
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
				precise float4 temp_2;
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
				bool temp_13;
				precise float temp_14;
				precise float temp_15;
				precise float temp_16;
				precise float temp_17;
				precise float temp_18;
				precise float temp_19;
				temp_0 = i.t2.x;
				temp_1 = i.t2.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).xyzw;
				temp_3 = temp_2.x;
				temp_4 = temp_2.y;
				temp_5 = temp_2.z;
				temp_6 = temp_2.w;
				temp_7 = i.t0.w;
				temp_8 = i.t0.y;
				temp_9 = i.t0.z;
				temp_10 = temp_6 + _TextureSampleAdd.w;
				temp_11 = temp_7 * temp_10;
				temp_12 = i.t0.x;
				temp_13 = temp_11 < 0.00100000005;
				if (temp_13)
				{
				discard;
				}
				temp_14 = temp_3 + _TextureSampleAdd.x;
				temp_15 = temp_4 + _TextureSampleAdd.y;
				temp_16 = temp_5 + _TextureSampleAdd.z;
				temp_17 = temp_12 * temp_14;
				temp_18 = temp_8 * temp_15;
				temp_19 = temp_9 * temp_16;
				o.c0.x = temp_17;
				o.c0.y = temp_18;
				o.c0.z = temp_19;
				o.c0.w = temp_11;
				return o;
			}
			#elif defined(UNITY_UI_CLIP_RECT)
			// 0006_fs.sph.bin keywords=[] local=['UNITY_UI_CLIP_RECT']
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
				precise float4 temp_2;
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
				bool temp_13;
				precise float temp_14;
				bool temp_15;
				precise float temp_16;
				bool temp_17;
				precise float temp_18;
				bool temp_19;
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
				temp_0 = i.t2.x;
				temp_1 = i.t2.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).xyzw;
				temp_3 = temp_2.x;
				temp_4 = temp_2.y;
				temp_5 = temp_2.z;
				temp_6 = temp_2.w;
				temp_7 = i.t1.x;
				temp_8 = i.t1.y;
				temp_9 = i.t0.w;
				temp_10 = i.t0.y;
				temp_11 = i.t0.x;
				temp_12 = i.t0.z;
				temp_13 = temp_7 >= _ClipRect.x;
				temp_14 = temp_13 ? 1.0 : 0.0;
				temp_15 = temp_8 >= _ClipRect.y;
				temp_16 = temp_15 ? 1.0 : 0.0;
				temp_17 = temp_7 <= _ClipRect.z;
				temp_18 = temp_17 ? 1.0 : 0.0;
				temp_19 = temp_8 <= _ClipRect.w;
				temp_20 = temp_19 ? 1.0 : 0.0;
				temp_21 = temp_18 * temp_14;
				temp_22 = temp_16 * temp_20;
				temp_23 = temp_21 * temp_22;
				temp_24 = temp_6 + _TextureSampleAdd.w;
				temp_25 = temp_4 + _TextureSampleAdd.y;
				temp_26 = temp_3 + _TextureSampleAdd.x;
				temp_27 = temp_5 + _TextureSampleAdd.z;
				temp_28 = temp_24 * temp_9;
				temp_29 = temp_25 * temp_10;
				temp_30 = temp_26 * temp_11;
				temp_31 = temp_27 * temp_12;
				temp_32 = temp_28 * temp_23;
				o.c0.x = temp_30;
				o.c0.y = temp_29;
				o.c0.z = temp_31;
				o.c0.w = temp_32;
				return o;
			}
			#elif defined(_OVERRIDETEXTUREALPHAENABLED_ON)
			// 0008_fs.sph.bin keywords=['_OVERRIDETEXTUREALPHAENABLED_ON']
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
				precise float3 temp_2;
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
				temp_0 = i.t2.x;
				temp_1 = i.t2.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).xyz;
				temp_3 = temp_2.x;
				temp_4 = temp_2.y;
				temp_5 = temp_2.z;
				temp_6 = i.t0.x;
				temp_7 = i.t0.y;
				temp_8 = i.t0.z;
				temp_9 = i.t0.w;
				temp_10 = temp_3 + _TextureSampleAdd.x;
				temp_11 = temp_4 + _TextureSampleAdd.y;
				temp_12 = temp_5 + _TextureSampleAdd.z;
				temp_13 = temp_6 * temp_10;
				temp_14 = temp_7 * temp_11;
				temp_15 = temp_8 * temp_12;
				temp_16 = temp_9 * _OverrideTextureAlpha;
				o.c0.x = temp_13;
				o.c0.y = temp_14;
				o.c0.z = temp_15;
				o.c0.w = temp_16;
				return o;
			}
			#elif defined(_GRAYSCALEENABLED_ON)
			// 0012_fs.sph.bin keywords=['_GRAYSCALEENABLED_ON']
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
				precise float4 temp_2;
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
				temp_0 = i.t2.x;
				temp_1 = i.t2.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).xyzw;
				temp_3 = temp_2.x;
				temp_4 = temp_2.y;
				temp_5 = temp_2.z;
				temp_6 = temp_2.w;
				temp_7 = i.t0.x;
				temp_8 = i.t0.y;
				temp_9 = i.t0.z;
				temp_10 = i.t0.w;
				temp_11 = temp_3 + _TextureSampleAdd.x;
				temp_12 = temp_4 + _TextureSampleAdd.y;
				temp_13 = temp_5 + _TextureSampleAdd.z;
				temp_14 = temp_6 + _TextureSampleAdd.w;
				temp_15 = temp_7 * temp_11;
				temp_16 = temp_8 * temp_12;
				temp_17 = temp_9 * temp_13;
				temp_18 = temp_10 * temp_14;
				temp_19 = temp_15 * 0.300048828;
				temp_20 = mad(temp_16, 0.58984375, temp_19);
				temp_21 = mad(temp_17, 0.109985352, temp_20);
				temp_22 = 0.0 - temp_11;
				temp_23 = mad(temp_7, temp_22, temp_21);
				temp_24 = 0.0 - temp_12;
				temp_25 = mad(temp_8, temp_24, temp_21);
				temp_26 = 0.0 - temp_13;
				temp_27 = mad(temp_9, temp_26, temp_21);
				temp_28 = mad(temp_23, _GrayscaleAmount, temp_15);
				temp_29 = mad(temp_25, _GrayscaleAmount, temp_16);
				temp_30 = mad(temp_27, _GrayscaleAmount, temp_17);
				o.c0.x = temp_28;
				o.c0.y = temp_29;
				o.c0.z = temp_30;
				o.c0.w = temp_18;
				return o;
			}
			#else
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
				precise float4 temp_2;
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
				temp_0 = i.t2.x;
				temp_1 = i.t2.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).xyzw;
				temp_3 = temp_2.x;
				temp_4 = temp_2.y;
				temp_5 = temp_2.z;
				temp_6 = temp_2.w;
				temp_7 = i.t0.x;
				temp_8 = i.t0.y;
				temp_9 = i.t0.z;
				temp_10 = i.t0.w;
				temp_11 = temp_3 + _TextureSampleAdd.x;
				temp_12 = temp_4 + _TextureSampleAdd.y;
				temp_13 = temp_5 + _TextureSampleAdd.z;
				temp_14 = temp_6 + _TextureSampleAdd.w;
				temp_15 = temp_7 * temp_11;
				temp_16 = temp_8 * temp_12;
				temp_17 = temp_9 * temp_13;
				temp_18 = temp_10 * temp_14;
				o.c0.x = temp_15;
				o.c0.y = temp_16;
				o.c0.z = temp_17;
				o.c0.w = temp_18;
				return o;
			}
			#endif
			ENDHLSL
		}
	}
}