Shader "UI/Blur" {
	Properties {
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Size ("Size", Float) = 1
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
	}

	SubShader {
		Tags { "IgnoreProjector"="true" "Queue"="Transparent" "RenderType"="Opaque" }
		GrabPass { }
		Pass {
			Name "blurX"
			Tags { "IgnoreProjector"="true" "RenderType"="Opaque" }
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
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float _Size;
			float4 _GrabTexture_TexelSize;
			sampler2D _GrabTexture;

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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a0.x;
				temp_2 = v.a0.z;
				temp_3 = v.a1.x;
				temp_4 = v.a1.y;
				temp_5 = v.a1.z;
				temp_6 = temp_0 * unity_ObjectToWorld._m11;
				temp_7 = v.a1.w;
				temp_8 = temp_0 * unity_ObjectToWorld._m01;
				temp_9 = mad(temp_1, unity_ObjectToWorld._m10, temp_6);
				o.t1.x = temp_3;
				temp_10 = temp_0 * unity_ObjectToWorld._m21;
				o.t1.y = temp_4;
				temp_11 = mad(temp_1, unity_ObjectToWorld._m00, temp_8);
				o.t1.z = temp_5;
				temp_12 = mad(temp_2, unity_ObjectToWorld._m12, temp_9);
				o.t1.w = temp_7;
				temp_13 = temp_0 * unity_ObjectToWorld._m31;
				temp_14 = mad(temp_1, unity_ObjectToWorld._m20, temp_10);
				temp_15 = mad(temp_2, unity_ObjectToWorld._m02, temp_11);
				temp_16 = temp_12 + unity_ObjectToWorld._m13;
				temp_17 = mad(temp_1, unity_ObjectToWorld._m30, temp_13);
				temp_18 = mad(temp_2, unity_ObjectToWorld._m22, temp_14);
				temp_19 = temp_15 + unity_ObjectToWorld._m03;
				temp_20 = temp_16 * unity_MatrixVP._m21;
				temp_21 = mad(temp_2, unity_ObjectToWorld._m32, temp_17);
				temp_22 = temp_18 + unity_ObjectToWorld._m23;
				temp_23 = temp_16 * unity_MatrixVP._m31;
				temp_24 = mad(temp_19, unity_MatrixVP._m20, temp_20);
				temp_25 = temp_21 + unity_ObjectToWorld._m33;
				temp_26 = mad(temp_19, unity_MatrixVP._m30, temp_23);
				temp_27 = mad(temp_22, unity_MatrixVP._m22, temp_24);
				temp_28 = temp_16 * unity_MatrixVP._m01;
				temp_29 = temp_16 * unity_MatrixVP._m11;
				temp_30 = mad(temp_22, unity_MatrixVP._m32, temp_26);
				temp_31 = mad(temp_25, unity_MatrixVP._m23, temp_27);
				temp_32 = mad(temp_19, unity_MatrixVP._m00, temp_28);
				o.pos.z = temp_31;
				temp_33 = mad(temp_19, unity_MatrixVP._m10, temp_29);
				o.t0.z = temp_31;
				temp_34 = mad(temp_25, unity_MatrixVP._m33, temp_30);
				temp_35 = mad(temp_22, unity_MatrixVP._m02, temp_32);
				o.pos.w = temp_34;
				temp_36 = mad(temp_22, unity_MatrixVP._m12, temp_33);
				o.t0.w = temp_34;
				temp_37 = temp_34 * 0.5;
				temp_38 = mad(temp_25, unity_MatrixVP._m03, temp_35);
				temp_39 = mad(temp_25, unity_MatrixVP._m13, temp_36);
				o.pos.x = temp_38;
				temp_40 = mad(temp_38, 0.5, temp_37);
				o.pos.y = temp_39;
				temp_41 = mad(temp_39, -0.5, temp_37);
				o.t0.x = temp_40;
				o.t0.y = temp_41;
				return o;
			}
			// 0001_fs.sph.bin keywords=[]
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
				precise float4 temp_14;
				precise float temp_15;
				precise float temp_16;
				precise float temp_17;
				precise float temp_18;
				precise float4 temp_19;
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
				precise float4 temp_39;
				precise float temp_40;
				precise float temp_41;
				precise float temp_42;
				precise float temp_43;
				precise float4 temp_44;
				precise float temp_45;
				precise float temp_46;
				precise float temp_47;
				precise float temp_48;
				precise float4 temp_49;
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
				precise float4 temp_64;
				precise float temp_65;
				precise float temp_66;
				precise float temp_67;
				precise float temp_68;
				precise float4 temp_69;
				precise float temp_70;
				precise float temp_71;
				precise float temp_72;
				precise float temp_73;
				precise float temp_74;
				precise float temp_75;
				precise float temp_76;
				precise float4 temp_77;
				precise float temp_78;
				precise float temp_79;
				precise float temp_80;
				precise float temp_81;
				precise float4 temp_82;
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
				temp_0 = _Size * _GrabTexture_TexelSize.x;
				temp_1 = i.t0.w;
				temp_2 = i.t1.w;
				temp_3 = i.t0.x;
				temp_4 = i.t0.y;
				temp_5 = 1.0 / temp_1;
				temp_6 = temp_0 * temp_2;
				temp_7 = mad(temp_6, -3.0, temp_3);
				temp_8 = mad(temp_6, -9.0, temp_3);
				temp_9 = mad(temp_6, -6.0, temp_3);
				temp_10 = mad(temp_6, -12.0, temp_3);
				temp_11 = temp_4 * temp_5;
				temp_12 = temp_8 * temp_5;
				temp_13 = temp_10 * temp_5;
				temp_14 = tex2D(_GrabTexture, nvn_nan0(float2(temp_12, temp_11))).xyzw;
				temp_15 = temp_14.x;
				temp_16 = temp_14.y;
				temp_17 = temp_14.z;
				temp_18 = temp_14.w;
				temp_19 = tex2D(_GrabTexture, nvn_nan0(float2(temp_13, temp_11))).xyzw;
				temp_20 = temp_19.x;
				temp_21 = temp_19.y;
				temp_22 = temp_19.z;
				temp_23 = temp_19.w;
				temp_24 = mad(temp_6, 12.0, temp_3);
				temp_25 = mad(temp_6, 6.0, temp_3);
				temp_26 = mad(temp_6, 9.0, temp_3);
				temp_27 = mad(temp_6, 3.0, temp_3);
				temp_28 = temp_3 * temp_5;
				temp_29 = temp_15 * 0.0900268555;
				temp_30 = temp_16 * 0.0900268555;
				temp_31 = temp_17 * 0.0900268555;
				temp_32 = temp_18 * 0.0900268555;
				temp_33 = mad(temp_20, 0.049987793, temp_29);
				temp_34 = mad(temp_21, 0.049987793, temp_30);
				temp_35 = mad(temp_22, 0.049987793, temp_31);
				temp_36 = mad(temp_23, 0.049987793, temp_32);
				temp_37 = temp_9 * temp_5;
				temp_38 = temp_7 * temp_5;
				temp_39 = tex2D(_GrabTexture, nvn_nan0(float2(temp_37, temp_11))).xyzw;
				temp_40 = temp_39.x;
				temp_41 = temp_39.y;
				temp_42 = temp_39.z;
				temp_43 = temp_39.w;
				temp_44 = tex2D(_GrabTexture, nvn_nan0(float2(temp_38, temp_11))).xyzw;
				temp_45 = temp_44.x;
				temp_46 = temp_44.y;
				temp_47 = temp_44.z;
				temp_48 = temp_44.w;
				temp_49 = tex2D(_GrabTexture, nvn_nan0(float2(temp_28, temp_11))).xyzw;
				temp_50 = temp_49.x;
				temp_51 = temp_49.y;
				temp_52 = temp_49.z;
				temp_53 = temp_49.w;
				temp_54 = mad(temp_40, 0.119995117, temp_33);
				temp_55 = mad(temp_42, 0.119995117, temp_35);
				temp_56 = mad(temp_41, 0.119995117, temp_34);
				temp_57 = temp_27 * temp_5;
				temp_58 = temp_26 * temp_5;
				temp_59 = mad(temp_43, 0.119995117, temp_36);
				temp_60 = mad(temp_45, 0.150024414, temp_54);
				temp_61 = temp_25 * temp_5;
				temp_62 = temp_24 * temp_5;
				temp_63 = mad(temp_46, 0.150024414, temp_56);
				temp_64 = tex2D(_GrabTexture, nvn_nan0(float2(temp_57, temp_11))).xyzw;
				temp_65 = temp_64.x;
				temp_66 = temp_64.y;
				temp_67 = temp_64.z;
				temp_68 = temp_64.w;
				temp_69 = tex2D(_GrabTexture, nvn_nan0(float2(temp_61, temp_11))).xyzw;
				temp_70 = temp_69.x;
				temp_71 = temp_69.y;
				temp_72 = temp_69.z;
				temp_73 = temp_69.w;
				temp_74 = mad(temp_47, 0.150024414, temp_55);
				temp_75 = mad(temp_48, 0.150024414, temp_59);
				temp_76 = mad(temp_50, 0.180053711, temp_60);
				temp_77 = tex2D(_GrabTexture, nvn_nan0(float2(temp_58, temp_11))).xyzw;
				temp_78 = temp_77.x;
				temp_79 = temp_77.y;
				temp_80 = temp_77.z;
				temp_81 = temp_77.w;
				temp_82 = tex2D(_GrabTexture, nvn_nan0(float2(temp_62, temp_11))).xyzw;
				temp_83 = temp_82.x;
				temp_84 = temp_82.y;
				temp_85 = temp_82.z;
				temp_86 = temp_82.w;
				temp_87 = mad(temp_51, 0.180053711, temp_63);
				temp_88 = mad(temp_52, 0.180053711, temp_74);
				temp_89 = mad(temp_53, 0.180053711, temp_75);
				temp_90 = mad(temp_65, 0.150024414, temp_76);
				temp_91 = mad(temp_66, 0.150024414, temp_87);
				temp_92 = mad(temp_67, 0.150024414, temp_88);
				temp_93 = mad(temp_68, 0.150024414, temp_89);
				temp_94 = mad(temp_70, 0.119995117, temp_90);
				temp_95 = mad(temp_71, 0.119995117, temp_91);
				temp_96 = mad(temp_72, 0.119995117, temp_92);
				temp_97 = mad(temp_73, 0.119995117, temp_93);
				temp_98 = mad(temp_78, 0.0900268555, temp_94);
				temp_99 = mad(temp_79, 0.0900268555, temp_95);
				temp_100 = mad(temp_80, 0.0900268555, temp_96);
				temp_101 = mad(temp_81, 0.0900268555, temp_97);
				temp_102 = mad(temp_83, 0.049987793, temp_98);
				temp_103 = mad(temp_84, 0.049987793, temp_99);
				temp_104 = mad(temp_85, 0.049987793, temp_100);
				temp_105 = mad(temp_86, 0.049987793, temp_101);
				o.c0.x = temp_102;
				o.c0.y = temp_103;
				o.c0.z = temp_104;
				o.c0.w = temp_105;
				return o;
			}
			ENDHLSL
		}
		GrabPass { }
		Pass {
			Name "blurY"
			Tags { "IgnoreProjector"="true" "RenderType"="Opaque" }
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
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float _Size;
			float4 _GrabTexture_TexelSize;
			sampler2D _GrabTexture;

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
			// 0002_vs.sph.bin keywords=[]
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : COLOR;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a0.x;
				temp_2 = v.a0.z;
				temp_3 = v.a1.x;
				temp_4 = v.a1.y;
				temp_5 = v.a1.z;
				temp_6 = temp_0 * unity_ObjectToWorld._m11;
				temp_7 = v.a1.w;
				temp_8 = temp_0 * unity_ObjectToWorld._m01;
				temp_9 = mad(temp_1, unity_ObjectToWorld._m10, temp_6);
				o.t1.x = temp_3;
				temp_10 = temp_0 * unity_ObjectToWorld._m21;
				o.t1.y = temp_4;
				temp_11 = mad(temp_1, unity_ObjectToWorld._m00, temp_8);
				o.t1.z = temp_5;
				temp_12 = mad(temp_2, unity_ObjectToWorld._m12, temp_9);
				o.t1.w = temp_7;
				temp_13 = temp_0 * unity_ObjectToWorld._m31;
				temp_14 = mad(temp_1, unity_ObjectToWorld._m20, temp_10);
				temp_15 = mad(temp_2, unity_ObjectToWorld._m02, temp_11);
				temp_16 = temp_12 + unity_ObjectToWorld._m13;
				temp_17 = mad(temp_1, unity_ObjectToWorld._m30, temp_13);
				temp_18 = mad(temp_2, unity_ObjectToWorld._m22, temp_14);
				temp_19 = temp_15 + unity_ObjectToWorld._m03;
				temp_20 = temp_16 * unity_MatrixVP._m21;
				temp_21 = mad(temp_2, unity_ObjectToWorld._m32, temp_17);
				temp_22 = temp_18 + unity_ObjectToWorld._m23;
				temp_23 = temp_16 * unity_MatrixVP._m31;
				temp_24 = mad(temp_19, unity_MatrixVP._m20, temp_20);
				temp_25 = temp_21 + unity_ObjectToWorld._m33;
				temp_26 = mad(temp_19, unity_MatrixVP._m30, temp_23);
				temp_27 = mad(temp_22, unity_MatrixVP._m22, temp_24);
				temp_28 = temp_16 * unity_MatrixVP._m01;
				temp_29 = temp_16 * unity_MatrixVP._m11;
				temp_30 = mad(temp_22, unity_MatrixVP._m32, temp_26);
				temp_31 = mad(temp_25, unity_MatrixVP._m23, temp_27);
				temp_32 = mad(temp_19, unity_MatrixVP._m00, temp_28);
				o.pos.z = temp_31;
				temp_33 = mad(temp_19, unity_MatrixVP._m10, temp_29);
				o.t0.z = temp_31;
				temp_34 = mad(temp_25, unity_MatrixVP._m33, temp_30);
				temp_35 = mad(temp_22, unity_MatrixVP._m02, temp_32);
				o.pos.w = temp_34;
				temp_36 = mad(temp_22, unity_MatrixVP._m12, temp_33);
				o.t0.w = temp_34;
				temp_37 = temp_34 * 0.5;
				temp_38 = mad(temp_25, unity_MatrixVP._m03, temp_35);
				temp_39 = mad(temp_25, unity_MatrixVP._m13, temp_36);
				o.pos.x = temp_38;
				temp_40 = mad(temp_38, 0.5, temp_37);
				o.pos.y = temp_39;
				temp_41 = mad(temp_39, -0.5, temp_37);
				o.t0.x = temp_40;
				o.t0.y = temp_41;
				return o;
			}
			// 0003_fs.sph.bin keywords=[]
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
				precise float4 temp_14;
				precise float temp_15;
				precise float temp_16;
				precise float temp_17;
				precise float temp_18;
				precise float temp_19;
				precise float4 temp_20;
				precise float temp_21;
				precise float temp_22;
				precise float temp_23;
				precise float temp_24;
				precise float4 temp_25;
				precise float temp_26;
				precise float temp_27;
				precise float temp_28;
				precise float temp_29;
				precise float temp_30;
				precise float4 temp_31;
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
				precise float4 temp_50;
				precise float temp_51;
				precise float temp_52;
				precise float temp_53;
				precise float temp_54;
				precise float temp_55;
				precise float temp_56;
				precise float4 temp_57;
				precise float temp_58;
				precise float temp_59;
				precise float temp_60;
				precise float temp_61;
				precise float temp_62;
				precise float temp_63;
				precise float temp_64;
				precise float4 temp_65;
				precise float temp_66;
				precise float temp_67;
				precise float temp_68;
				precise float temp_69;
				precise float4 temp_70;
				precise float temp_71;
				precise float temp_72;
				precise float temp_73;
				precise float temp_74;
				precise float temp_75;
				precise float4 temp_76;
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
				precise float temp_107;
				precise float temp_108;
				precise float temp_109;
				precise float temp_110;
				precise float temp_111;
				temp_0 = _Size * _GrabTexture_TexelSize.y;
				temp_1 = i.t0.w;
				temp_2 = i.t1.w;
				temp_3 = i.t0.y;
				temp_4 = i.t0.x;
				temp_5 = 1.0 / temp_1;
				temp_6 = temp_0 * temp_2;
				temp_7 = mad(temp_6, -9.0, temp_3);
				temp_8 = mad(temp_6, -3.0, temp_3);
				temp_9 = mad(temp_6, -12.0, temp_3);
				temp_10 = mad(temp_6, -6.0, temp_3);
				temp_11 = temp_4 * temp_5;
				temp_12 = temp_7 * temp_5;
				temp_13 = temp_9 * temp_5;
				temp_14 = tex2D(_GrabTexture, nvn_nan0(float2(temp_11, temp_12))).xyzw;
				temp_15 = temp_14.x;
				temp_16 = temp_14.y;
				temp_17 = temp_14.z;
				temp_18 = temp_14.w;
				temp_19 = temp_10 * temp_5;
				temp_20 = tex2D(_GrabTexture, nvn_nan0(float2(temp_11, temp_13))).xyzw;
				temp_21 = temp_20.x;
				temp_22 = temp_20.y;
				temp_23 = temp_20.z;
				temp_24 = temp_20.w;
				temp_25 = tex2D(_GrabTexture, nvn_nan0(float2(temp_11, temp_19))).xyzw;
				temp_26 = temp_25.x;
				temp_27 = temp_25.y;
				temp_28 = temp_25.z;
				temp_29 = temp_25.w;
				temp_30 = temp_8 * temp_5;
				temp_31 = tex2D(_GrabTexture, nvn_nan0(float2(temp_11, temp_30))).xyzw;
				temp_32 = temp_31.x;
				temp_33 = temp_31.y;
				temp_34 = temp_31.z;
				temp_35 = temp_31.w;
				temp_36 = temp_15 * 0.0900268555;
				temp_37 = temp_16 * 0.0900268555;
				temp_38 = temp_17 * 0.0900268555;
				temp_39 = temp_18 * 0.0900268555;
				temp_40 = mad(temp_23, 0.049987793, temp_38);
				temp_41 = mad(temp_21, 0.049987793, temp_36);
				temp_42 = mad(temp_22, 0.049987793, temp_37);
				temp_43 = mad(temp_6, 3.0, temp_3);
				temp_44 = mad(temp_6, 9.0, temp_3);
				temp_45 = mad(temp_6, 6.0, temp_3);
				temp_46 = mad(temp_6, 12.0, temp_3);
				temp_47 = temp_3 * temp_5;
				temp_48 = mad(temp_28, 0.119995117, temp_40);
				temp_49 = mad(temp_26, 0.119995117, temp_41);
				temp_50 = tex2D(_GrabTexture, nvn_nan0(float2(temp_11, temp_47))).xyzw;
				temp_51 = temp_50.x;
				temp_52 = temp_50.y;
				temp_53 = temp_50.z;
				temp_54 = temp_50.w;
				temp_55 = temp_43 * temp_5;
				temp_56 = temp_44 * temp_5;
				temp_57 = tex2D(_GrabTexture, nvn_nan0(float2(temp_11, temp_55))).xyzw;
				temp_58 = temp_57.x;
				temp_59 = temp_57.y;
				temp_60 = temp_57.z;
				temp_61 = temp_57.w;
				temp_62 = temp_45 * temp_5;
				temp_63 = temp_46 * temp_5;
				temp_64 = mad(temp_27, 0.119995117, temp_42);
				temp_65 = tex2D(_GrabTexture, nvn_nan0(float2(temp_11, temp_62))).xyzw;
				temp_66 = temp_65.x;
				temp_67 = temp_65.y;
				temp_68 = temp_65.z;
				temp_69 = temp_65.w;
				temp_70 = tex2D(_GrabTexture, nvn_nan0(float2(temp_11, temp_56))).xyzw;
				temp_71 = temp_70.x;
				temp_72 = temp_70.y;
				temp_73 = temp_70.z;
				temp_74 = temp_70.w;
				temp_75 = mad(temp_24, 0.049987793, temp_39);
				temp_76 = tex2D(_GrabTexture, nvn_nan0(float2(temp_11, temp_63))).xyzw;
				temp_77 = temp_76.x;
				temp_78 = temp_76.y;
				temp_79 = temp_76.z;
				temp_80 = temp_76.w;
				temp_81 = mad(temp_32, 0.150024414, temp_49);
				temp_82 = mad(temp_33, 0.150024414, temp_64);
				temp_83 = mad(temp_34, 0.150024414, temp_48);
				temp_84 = mad(temp_29, 0.119995117, temp_75);
				temp_85 = mad(temp_35, 0.150024414, temp_84);
				temp_86 = mad(temp_51, 0.180053711, temp_81);
				temp_87 = i.t1.y;
				temp_88 = mad(temp_52, 0.180053711, temp_82);
				temp_89 = i.t1.z;
				temp_90 = mad(temp_53, 0.180053711, temp_83);
				temp_91 = i.t1.x;
				temp_92 = mad(temp_54, 0.180053711, temp_85);
				temp_93 = mad(temp_58, 0.150024414, temp_86);
				temp_94 = mad(temp_59, 0.150024414, temp_88);
				temp_95 = mad(temp_60, 0.150024414, temp_90);
				temp_96 = mad(temp_61, 0.150024414, temp_92);
				temp_97 = mad(temp_66, 0.119995117, temp_93);
				temp_98 = mad(temp_67, 0.119995117, temp_94);
				temp_99 = mad(temp_68, 0.119995117, temp_95);
				temp_100 = mad(temp_69, 0.119995117, temp_96);
				temp_101 = mad(temp_71, 0.0900268555, temp_97);
				temp_102 = mad(temp_72, 0.0900268555, temp_98);
				temp_103 = mad(temp_73, 0.0900268555, temp_99);
				temp_104 = mad(temp_74, 0.0900268555, temp_100);
				temp_105 = mad(temp_77, 0.049987793, temp_101);
				temp_106 = mad(temp_78, 0.049987793, temp_102);
				temp_107 = mad(temp_79, 0.049987793, temp_103);
				temp_108 = mad(temp_80, 0.049987793, temp_104);
				temp_109 = temp_106 * temp_87;
				temp_110 = temp_105 * temp_91;
				temp_111 = temp_107 * temp_89;
				o.c0.x = temp_110;
				o.c0.y = temp_109;
				o.c0.z = temp_111;
				o.c0.w = temp_108;
				return o;
			}
			ENDHLSL
		}
	}
}