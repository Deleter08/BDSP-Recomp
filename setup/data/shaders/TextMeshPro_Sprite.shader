Shader "TextMeshPro/Sprite" {
	Properties {
		_MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		_ColorMask ("Color Mask", Float) = 15
		_ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
	}

	SubShader {
		Tags { "CanUseSpriteAtlas"="true" "IgnoreProjector"="true" "PreviewType"="Plane" "Queue"="Transparent" "RenderType"="Transparent" }
		Pass {
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
			#pragma multi_compile _ UNITY_UI_ALPHACLIP
			#pragma multi_compile _ UNITY_UI_CLIP_RECT
			float4 _Color;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _TextureSampleAdd;
			float4 _ClipRect;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a0.x;
				temp_2 = v.a0.z;
				temp_3 = v.a0.w;
				temp_4 = v.a1.w;
				temp_5 = v.a1.x;
				temp_6 = temp_0 * unity_ObjectToWorld._m11;
				temp_7 = v.a1.y;
				temp_8 = temp_0 * unity_ObjectToWorld._m01;
				temp_9 = v.a2.x;
				temp_10 = mad(temp_1, unity_ObjectToWorld._m10, temp_6);
				temp_11 = v.a1.z;
				temp_12 = temp_0 * unity_ObjectToWorld._m21;
				temp_13 = v.a2.y;
				temp_14 = mad(temp_1, unity_ObjectToWorld._m00, temp_8);
				o.t1.y = temp_0;
				temp_15 = mad(temp_2, unity_ObjectToWorld._m12, temp_10);
				o.t1.x = temp_1;
				temp_16 = temp_0 * unity_ObjectToWorld._m31;
				o.t1.w = temp_3;
				temp_17 = mad(temp_2, unity_ObjectToWorld._m02, temp_14);
				o.t1.z = temp_2;
				temp_18 = temp_15 + unity_ObjectToWorld._m13;
				o.t2.x = temp_9;
				temp_19 = mad(temp_1, unity_ObjectToWorld._m20, temp_12);
				o.t2.y = temp_13;
				temp_20 = temp_17 + unity_ObjectToWorld._m03;
				temp_21 = temp_18 * unity_MatrixVP._m01;
				temp_22 = mad(temp_1, unity_ObjectToWorld._m30, temp_16);
				temp_23 = temp_18 * unity_MatrixVP._m11;
				temp_24 = temp_5 * _Color.x;
				temp_25 = mad(temp_2, unity_ObjectToWorld._m22, temp_19);
				o.t0.x = temp_24;
				temp_26 = mad(temp_20, unity_MatrixVP._m00, temp_21);
				temp_27 = temp_18 * unity_MatrixVP._m21;
				temp_28 = temp_18 * unity_MatrixVP._m31;
				temp_29 = mad(temp_2, unity_ObjectToWorld._m32, temp_22);
				temp_30 = mad(temp_20, unity_MatrixVP._m10, temp_23);
				temp_31 = temp_25 + unity_ObjectToWorld._m23;
				temp_32 = temp_4 * _Color.w;
				temp_33 = mad(temp_20, unity_MatrixVP._m20, temp_27);
				o.t0.w = temp_32;
				temp_34 = mad(temp_20, unity_MatrixVP._m30, temp_28);
				temp_35 = temp_29 + unity_ObjectToWorld._m33;
				temp_36 = mad(temp_31, unity_MatrixVP._m02, temp_26);
				temp_37 = mad(temp_31, unity_MatrixVP._m12, temp_30);
				temp_38 = temp_7 * _Color.y;
				temp_39 = mad(temp_31, unity_MatrixVP._m22, temp_33);
				o.t0.y = temp_38;
				temp_40 = mad(temp_31, unity_MatrixVP._m32, temp_34);
				temp_41 = temp_11 * _Color.z;
				temp_42 = mad(temp_35, unity_MatrixVP._m03, temp_36);
				o.t0.z = temp_41;
				temp_43 = mad(temp_35, unity_MatrixVP._m13, temp_37);
				o.pos.x = temp_42;
				temp_44 = mad(temp_35, unity_MatrixVP._m23, temp_39);
				o.pos.y = temp_43;
				temp_45 = mad(temp_35, unity_MatrixVP._m33, temp_40);
				o.pos.z = temp_44;
				o.pos.w = temp_45;
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
				o.c0.x = temp_31;
				o.c0.y = temp_32;
				o.c0.z = temp_33;
				o.c0.w = temp_25;
				return o;
			}
			#elif defined(UNITY_UI_ALPHACLIP)
			// 0005_fs.sph.bin keywords=['UNITY_UI_ALPHACLIP']
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
			// 0006_fs.sph.bin keywords=['UNITY_UI_CLIP_RECT']
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