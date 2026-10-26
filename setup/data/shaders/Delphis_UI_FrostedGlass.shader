Shader "Delphis/UI/FrostedGlass" {
	Properties {
		_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
		_NoiseTex ("Noise Texture", 2D) = "bump" {}
		_NoiseScale ("Noise Scale", Range(0, 20)) = 2
		_BackgroundTex ("Background", 2D) = "white" {}
		_ScreenOffset ("Screen Offset", Vector) = (0,0,0,0)
		_Color ("Color", Color) = (1,1,1,1)
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		_ColorMask ("Color Mask", Float) = 15
	}

	SubShader {
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }
		Pass {
			Tags { "RenderType"="Transparent" }
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
			float4 _ProjectionParams;
			float4 _MainTex_ST;
			float4 _NoiseTex_ST;
			float4 _Color;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float _NoiseScale;
			float4 _ScreenOffset;
			float4 _BackgroundTex_TexelSize;
			sampler2D _MainTex;
			sampler2D _NoiseTex;
			sampler2D _BackgroundTex;

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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a2.x;
				temp_2 = v.a0.x;
				temp_3 = v.a0.w;
				temp_4 = temp_0 * unity_ObjectToWorld._m11;
				temp_5 = v.a0.z;
				temp_6 = mad(temp_1, _NoiseTex_ST.x, _NoiseTex_ST.z);
				temp_7 = v.a1.y;
				temp_8 = mad(temp_1, _MainTex_ST.x, _MainTex_ST.z);
				temp_9 = v.a1.w;
				temp_10 = temp_0 * unity_ObjectToWorld._m01;
				temp_11 = v.a2.y;
				temp_12 = mad(temp_2, unity_ObjectToWorld._m10, temp_4);
				temp_13 = v.a1.x;
				temp_14 = temp_0 * unity_ObjectToWorld._m21;
				temp_15 = v.a1.z;
				temp_16 = mad(temp_5, unity_ObjectToWorld._m12, temp_12);
				o.t2.w = temp_3;
				temp_17 = mad(temp_2, unity_ObjectToWorld._m20, temp_14);
				o.t3.z = temp_6;
				temp_18 = temp_16 + unity_ObjectToWorld._m13;
				o.t3.x = temp_8;
				temp_19 = mad(temp_2, unity_ObjectToWorld._m00, temp_10);
				o.t2.z = temp_5;
				temp_20 = temp_0 * unity_ObjectToWorld._m31;
				o.t2.y = temp_0;
				temp_21 = temp_18 * unity_MatrixVP._m21;
				o.t2.x = temp_2;
				temp_22 = mad(temp_5, unity_ObjectToWorld._m02, temp_19);
				temp_23 = mad(temp_2, unity_ObjectToWorld._m30, temp_20);
				temp_24 = mad(temp_5, unity_ObjectToWorld._m22, temp_17);
				temp_25 = temp_7 * _Color.y;
				temp_26 = temp_15 * _Color.z;
				o.t0.y = temp_25;
				temp_27 = temp_22 + unity_ObjectToWorld._m03;
				o.t0.z = temp_26;
				temp_28 = mad(temp_5, unity_ObjectToWorld._m32, temp_23);
				temp_29 = temp_9 * _Color.w;
				temp_30 = temp_18 * unity_MatrixVP._m11;
				o.t0.w = temp_29;
				temp_31 = mad(temp_27, unity_MatrixVP._m20, temp_21);
				temp_32 = temp_18 * unity_MatrixVP._m31;
				temp_33 = temp_24 + unity_ObjectToWorld._m23;
				temp_34 = temp_18 * unity_MatrixVP._m01;
				temp_35 = temp_28 + unity_ObjectToWorld._m33;
				temp_36 = mad(temp_27, unity_MatrixVP._m10, temp_30);
				temp_37 = mad(temp_11, _NoiseTex_ST.y, _NoiseTex_ST.w);
				temp_38 = mad(temp_27, unity_MatrixVP._m30, temp_32);
				o.t3.w = temp_37;
				temp_39 = mad(temp_27, unity_MatrixVP._m00, temp_34);
				temp_40 = mad(temp_33, unity_MatrixVP._m12, temp_36);
				temp_41 = mad(temp_33, unity_MatrixVP._m22, temp_31);
				temp_42 = mad(temp_11, _MainTex_ST.y, _MainTex_ST.w);
				temp_43 = mad(temp_33, unity_MatrixVP._m32, temp_38);
				o.t3.y = temp_42;
				temp_44 = mad(temp_33, unity_MatrixVP._m02, temp_39);
				temp_45 = mad(temp_35, unity_MatrixVP._m13, temp_40);
				temp_46 = mad(temp_35, unity_MatrixVP._m23, temp_41);
				o.pos.y = temp_45;
				temp_47 = mad(temp_35, unity_MatrixVP._m33, temp_43);
				o.pos.z = temp_46;
				temp_48 = mad(temp_35, unity_MatrixVP._m03, temp_44);
				o.t1.z = temp_46;
				temp_49 = temp_45 * _ProjectionParams.x;
				o.pos.w = temp_47;
				temp_50 = temp_47 * 0.5;
				o.t1.w = temp_47;
				temp_51 = temp_13 * _Color.x;
				o.pos.x = temp_48;
				temp_52 = mad(temp_48, 0.5, temp_50);
				o.t0.x = temp_51;
				temp_53 = mad(temp_49, 0.5, temp_50);
				o.t1.x = temp_52;
				o.t1.y = temp_53;
				return o;
			}
			// 0001_fs.sph.bin keywords=[]
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
				bool temp_16;
				precise float temp_17;
				precise float temp_18;
				precise float temp_19;
				precise float2 temp_20;
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
				precise float3 temp_45;
				precise float temp_46;
				precise float temp_47;
				precise float temp_48;
				precise float3 temp_49;
				precise float temp_50;
				precise float temp_51;
				precise float temp_52;
				precise float3 temp_53;
				precise float temp_54;
				precise float temp_55;
				precise float temp_56;
				precise float3 temp_57;
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
				temp_0 = i.t3.x;
				temp_1 = i.t3.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).xyzw;
				temp_3 = temp_2.x;
				temp_4 = temp_2.y;
				temp_5 = temp_2.z;
				temp_6 = temp_2.w;
				temp_7 = i.t0.w;
				temp_8 = i.t1.w;
				temp_9 = i.t1.x;
				temp_10 = i.t1.y;
				temp_11 = i.t3.w;
				temp_12 = 1.0 / temp_8;
				temp_13 = temp_11 * temp_10;
				temp_14 = temp_13 * temp_12;
				temp_15 = temp_6 * temp_7;
				temp_16 = temp_15 < 0.00100040436;
				temp_17 = i.t3.z;
				if (temp_16)
				{
				discard;
				}
				temp_18 = temp_17 * temp_9;
				temp_19 = temp_18 * temp_12;
				temp_20 = tex2D(_NoiseTex, nvn_nan0(float2(temp_19, temp_14))).xy;
				temp_21 = temp_20.x;
				temp_22 = temp_20.y;
				temp_23 = i.t0.x;
				temp_24 = i.t0.y;
				temp_25 = i.t0.z;
				temp_26 = temp_3 * temp_23;
				temp_27 = temp_4 * temp_24;
				temp_28 = temp_5 * temp_25;
				temp_29 = mad(temp_21, 2.0, -1.0);
				temp_30 = mad(temp_22, 2.0, -1.0);
				temp_31 = mad(temp_29, _NoiseScale, _ScreenOffset.x);
				temp_32 = mad(temp_30, _NoiseScale, _ScreenOffset.y);
				temp_33 = temp_31 + 1.0;
				temp_34 = temp_32 + -1.0;
				temp_35 = temp_32 + 1.0;
				temp_36 = temp_31 + -1.0;
				temp_37 = mad(temp_33, _BackgroundTex_TexelSize.x, temp_9);
				temp_38 = mad(temp_34, _BackgroundTex_TexelSize.y, temp_10);
				temp_39 = mad(temp_35, _BackgroundTex_TexelSize.y, temp_10);
				temp_40 = mad(temp_36, _BackgroundTex_TexelSize.x, temp_9);
				temp_41 = temp_37 * temp_12;
				temp_42 = temp_38 * temp_12;
				temp_43 = temp_39 * temp_12;
				temp_44 = temp_40 * temp_12;
				temp_45 = tex2D(_BackgroundTex, nvn_nan0(float2(temp_41, temp_42))).xyz;
				temp_46 = temp_45.x;
				temp_47 = temp_45.y;
				temp_48 = temp_45.z;
				temp_49 = tex2D(_BackgroundTex, nvn_nan0(float2(temp_41, temp_43))).xyz;
				temp_50 = temp_49.x;
				temp_51 = temp_49.y;
				temp_52 = temp_49.z;
				temp_53 = tex2D(_BackgroundTex, nvn_nan0(float2(temp_44, temp_43))).xyz;
				temp_54 = temp_53.x;
				temp_55 = temp_53.y;
				temp_56 = temp_53.z;
				temp_57 = tex2D(_BackgroundTex, nvn_nan0(float2(temp_44, temp_42))).xyz;
				temp_58 = temp_57.x;
				temp_59 = temp_57.y;
				temp_60 = temp_57.z;
				temp_61 = temp_46 + temp_50;
				temp_62 = temp_48 + temp_52;
				temp_63 = temp_47 + temp_51;
				temp_64 = temp_54 + temp_61;
				temp_65 = temp_56 + temp_62;
				temp_66 = temp_55 + temp_63;
				temp_67 = temp_58 + temp_64;
				temp_68 = temp_60 + temp_65;
				temp_69 = temp_59 + temp_66;
				temp_70 = 0.0 - temp_26;
				temp_71 = mad(temp_67, 0.25, temp_70);
				temp_72 = 0.0 - temp_28;
				temp_73 = mad(temp_68, 0.25, temp_72);
				temp_74 = 0.0 - temp_27;
				temp_75 = mad(temp_69, 0.25, temp_74);
				temp_76 = mad(temp_71, 0.5, temp_26);
				temp_77 = mad(temp_73, 0.5, temp_28);
				temp_78 = mad(temp_75, 0.5, temp_27);
				o.c0.x = temp_76;
				o.c0.y = temp_78;
				o.c0.z = temp_77;
				o.c0.w = 1.0;
				return o;
			}
			ENDHLSL
		}
	}
}