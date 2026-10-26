Shader "Mitake/FireCore1st" {
	Properties {
		[Header(Mask Texture Settings_______________________)] _Blend0Tex ("Blend Texture1", 2D) = "black" {}
		_Blend1Tex ("Blend Texture2", 2D) = "black" {}
		_LerpTex ("Lerp Texture", 2D) = "black" {}
		[Header(Color Settings_______________________)] _BaseColor ("Base Color", Color) = (1,0,0,1)
		_LayerColor ("Layer Color", Color) = (1,1,0,1)
		_ConstantColor0 ("Multiply Color", Color) = (0.5,0.5,0.5,1)
		_ConstantColorVal ("Multiply Factor", Range(0, 1)) = 0
		[Space] [Header(Rim______________________________)] _BaseRimValue ("Rim Value(on BaseColor)", Range(0, 5)) = 1
		_LayerRimValue ("Rim Value(on LayerColor)", Range(0, 5)) = 1
		[Space] [Header(UV Control_______________________)] _Blend0UVTranslateU ("BlendTex1 UV Translate(U)", Float) = 0
		_Blend0UVTranslateV ("BlendTex1 UV Translate(V)", Float) = 0
		_Blend0UVScaleU ("BlendTex1 UV Scale(U)", Float) = 1
		_Blend0UVScaleV ("BlendTex1 UV Scale(V)", Float) = 1
		_Blend1UVTranslateU ("BlendTex2 UV Translate(U)", Float) = 0
		_Blend1UVTranslateV ("BlendTex2 UV Translate(V)", Float) = 0
		_Blend1UVScaleU ("BlendTex2 UV Scale(U)", Float) = 1
		_Blend1UVScaleV ("BlendTex2 UV Scale(V)", Float) = 1
		[Space] [Header(Stencil___________________________)] _Stencil ("MatLink ID", Float) = 0
		[Space] [Header(Z Test____________________________)] [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z Test", Float) = 4
		[MaterialToggle] _ZWrite ("Z Write", Float) = 0
		[Header(Add DPR___________________________)] _FixMultiplierColor ("FixMultiplierColor", Color) = (1,1,1,1)
	}

	SubShader {
		Tags { "Queue"="Geometry+2" "RenderType"="Opaque" }
		Pass {
			Tags { "RenderType"="Opaque" }
			ZTest [_ZTest]
			ZWrite [_ZWrite]
			Stencil {
				Ref [_Stencil]
				Comp Equal
				Pass Keep
				Fail Keep
				ZFail Keep
			}
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			float3 _WorldSpaceCameraPos;
			float4 _Blend0Tex_ST;
			float4 _Blend1Tex_ST;
			float4 _LerpTex_ST;
			float _Blend0UVTranslateU;
			float _Blend0UVTranslateV;
			float _Blend0UVScaleU;
			float _Blend0UVScaleV;
			float _Blend1UVTranslateU;
			float _Blend1UVTranslateV;
			float _Blend1UVScaleU;
			float _Blend1UVScaleV;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_WorldToObject;
			float4x4 unity_MatrixVP;
			float3 _FixMultiplierColor;
			float4 _BaseColor;
			float4 _LayerColor;
			float4 _ConstantColor0;
			float _BaseRimValue;
			float _LayerRimValue;
			float _ConstantColorVal;
			sampler2D _Blend1Tex;
			sampler2D _Blend0Tex;
			sampler2D _LerpTex;

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
				float4 a1 : NORMAL;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a2.x;
				temp_1 = v.a2.y;
				temp_2 = v.a1.x;
				temp_3 = v.a1.y;
				temp_4 = v.a0.y;
				temp_5 = v.a1.z;
				temp_6 = _Blend1UVScaleU * _Blend1UVTranslateU;
				temp_7 = v.a0.x;
				temp_8 = mad(temp_0, _Blend1Tex_ST.x, _Blend1Tex_ST.z);
				temp_9 = mad(temp_0, _Blend0Tex_ST.x, _Blend0Tex_ST.z);
				temp_10 = mad(temp_0, _LerpTex_ST.x, _LerpTex_ST.z);
				temp_11 = mad(temp_1, _Blend1Tex_ST.y, _Blend1Tex_ST.w);
				temp_12 = 0.0 - temp_6;
				temp_13 = mad(temp_8, _Blend1UVScaleU, temp_12);
				temp_14 = temp_2 * unity_WorldToObject._m00;
				temp_15 = _Blend1UVScaleV * _Blend1UVTranslateV;
				temp_16 = v.a0.z;
				temp_17 = mad(temp_1, _Blend0Tex_ST.y, _Blend0Tex_ST.w);
				temp_18 = mad(temp_1, _LerpTex_ST.y, _LerpTex_ST.w);
				temp_19 = _Blend0UVTranslateU * _Blend0UVScaleU;
				temp_20 = temp_2 * unity_WorldToObject._m02;
				temp_21 = temp_2 * unity_WorldToObject._m01;
				temp_22 = mad(temp_3, unity_WorldToObject._m10, temp_14);
				temp_23 = temp_4 * unity_ObjectToWorld._m01;
				temp_24 = 0.0 - temp_19;
				temp_25 = mad(temp_9, _Blend0UVScaleU, temp_24);
				temp_26 = temp_4 * unity_ObjectToWorld._m11;
				temp_27 = mad(temp_3, unity_WorldToObject._m12, temp_20);
				temp_28 = mad(temp_3, unity_WorldToObject._m11, temp_21);
				temp_29 = mad(temp_5, unity_WorldToObject._m20, temp_22);
				temp_30 = _Blend0UVTranslateV * _Blend0UVScaleV;
				temp_31 = mad(temp_7, unity_ObjectToWorld._m00, temp_23);
				temp_32 = mad(temp_7, unity_ObjectToWorld._m10, temp_26);
				temp_33 = 0.0 - temp_15;
				temp_34 = mad(temp_11, _Blend1UVScaleV, temp_33);
				temp_35 = v.a0.w;
				temp_36 = mad(temp_5, unity_WorldToObject._m21, temp_28);
				o.t0.x = temp_10;
				temp_37 = temp_29 * temp_29;
				o.t0.y = temp_18;
				temp_38 = mad(temp_5, unity_WorldToObject._m22, temp_27);
				o.t1.x = temp_25;
				temp_39 = mad(temp_16, unity_ObjectToWorld._m12, temp_32);
				o.t1.z = temp_13;
				temp_40 = mad(temp_36, temp_36, temp_37);
				o.t1.w = temp_34;
				temp_41 = 0.0 - temp_30;
				temp_42 = mad(temp_17, _Blend0UVScaleV, temp_41);
				temp_43 = temp_4 * unity_ObjectToWorld._m21;
				o.t1.y = temp_42;
				temp_44 = mad(temp_38, temp_38, temp_40);
				temp_45 = temp_4 * unity_ObjectToWorld._m31;
				temp_46 = rsqrt(temp_44);
				temp_47 = mad(temp_16, unity_ObjectToWorld._m02, temp_31);
				temp_48 = temp_39 + unity_ObjectToWorld._m13;
				temp_49 = mad(temp_7, unity_ObjectToWorld._m20, temp_43);
				temp_50 = mad(temp_35, unity_ObjectToWorld._m13, temp_39);
				temp_51 = mad(temp_7, unity_ObjectToWorld._m30, temp_45);
				temp_52 = mad(temp_35, unity_ObjectToWorld._m03, temp_47);
				temp_53 = temp_47 + unity_ObjectToWorld._m03;
				temp_54 = mad(temp_16, unity_ObjectToWorld._m22, temp_49);
				temp_55 = temp_48 * unity_MatrixVP._m01;
				temp_56 = 0.0 - temp_50;
				temp_57 = temp_56 + _WorldSpaceCameraPos.y;
				temp_58 = mad(temp_16, unity_ObjectToWorld._m32, temp_51);
				o.t3.y = temp_57;
				temp_59 = temp_29 * temp_46;
				temp_60 = temp_48 * unity_MatrixVP._m11;
				o.t2.x = temp_59;
				temp_61 = temp_48 * unity_MatrixVP._m21;
				temp_62 = temp_48 * unity_MatrixVP._m31;
				temp_63 = mad(temp_35, unity_ObjectToWorld._m23, temp_54);
				temp_64 = mad(temp_53, unity_MatrixVP._m00, temp_55);
				temp_65 = temp_54 + unity_ObjectToWorld._m23;
				temp_66 = mad(temp_53, unity_MatrixVP._m10, temp_60);
				temp_67 = mad(temp_53, unity_MatrixVP._m20, temp_61);
				temp_68 = mad(temp_53, unity_MatrixVP._m30, temp_62);
				temp_69 = temp_58 + unity_ObjectToWorld._m33;
				temp_70 = 0.0 - temp_52;
				temp_71 = temp_70 + _WorldSpaceCameraPos.x;
				temp_72 = mad(temp_65, unity_MatrixVP._m02, temp_64);
				o.t3.x = temp_71;
				temp_73 = mad(temp_65, unity_MatrixVP._m12, temp_66);
				temp_74 = mad(temp_65, unity_MatrixVP._m22, temp_67);
				temp_75 = mad(temp_65, unity_MatrixVP._m32, temp_68);
				temp_76 = 0.0 - temp_63;
				temp_77 = temp_76 + _WorldSpaceCameraPos.z;
				temp_78 = temp_36 * temp_46;
				o.t3.z = temp_77;
				temp_79 = temp_38 * temp_46;
				o.t2.y = temp_78;
				temp_80 = mad(temp_69, unity_MatrixVP._m03, temp_72);
				o.t2.z = temp_79;
				temp_81 = mad(temp_69, unity_MatrixVP._m13, temp_73);
				o.pos.x = temp_80;
				temp_82 = mad(temp_69, unity_MatrixVP._m23, temp_74);
				o.pos.y = temp_81;
				temp_83 = mad(temp_69, unity_MatrixVP._m33, temp_75);
				o.pos.z = temp_82;
				o.pos.w = temp_83;
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
				precise float temp_2;
				precise float temp_3;
				precise float temp_4;
				precise float temp_5;
				precise float temp_6;
				precise float temp_7;
				precise float2 temp_8;
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
				temp_0 = i.t1.z;
				temp_1 = i.t1.w;
				temp_2 = i.t1.x;
				temp_3 = i.t1.y;
				temp_4 = i.t0.x;
				temp_5 = i.t0.y;
				temp_6 = tex2D(_Blend1Tex, nvn_nan0(float2(temp_0, temp_1))).x;
				temp_7 = tex2D(_Blend0Tex, nvn_nan0(float2(temp_2, temp_3))).x;
				temp_8 = tex2D(_LerpTex, nvn_nan0(float2(temp_4, temp_5))).xw;
				temp_9 = temp_8.x;
				temp_10 = temp_8.y;
				temp_11 = i.t3.x;
				temp_12 = i.t2.x;
				temp_13 = i.t3.z;
				temp_14 = i.t2.z;
				temp_15 = i.t2.y;
				temp_16 = i.t3.y;
				temp_17 = temp_11 * temp_11;
				temp_18 = temp_12 * temp_12;
				temp_19 = mad(temp_15, temp_15, temp_18);
				temp_20 = mad(temp_16, temp_16, temp_17);
				temp_21 = mad(temp_14, temp_14, temp_19);
				temp_22 = mad(temp_13, temp_13, temp_20);
				temp_23 = rsqrt(temp_21);
				temp_24 = rsqrt(temp_22);
				temp_25 = temp_12 * temp_23;
				temp_26 = temp_15 * temp_23;
				temp_27 = temp_11 * temp_24;
				temp_28 = temp_16 * temp_24;
				temp_29 = temp_13 * temp_24;
				temp_30 = temp_14 * temp_23;
				temp_31 = temp_27 * temp_25;
				temp_32 = mad(temp_28, temp_26, temp_31);
				temp_33 = mad(temp_29, temp_30, temp_32);
				temp_34 = saturate(temp_33);
				temp_35 = temp_6 + temp_7;
				temp_36 = 0.0 - temp_35;
				temp_37 = mad(temp_10, temp_36, temp_35);
				temp_38 = 0.0 - _BaseRimValue;
				temp_39 = temp_38 + _LayerRimValue;
				temp_40 = temp_37 + temp_9;
				temp_41 = saturate(temp_40);
				temp_42 = mad(temp_39, temp_41, _BaseRimValue);
				temp_43 = 0.0 - _BaseColor.x;
				temp_44 = temp_43 + _LayerColor.x;
				temp_45 = 0.0 - _BaseColor.y;
				temp_46 = temp_45 + _LayerColor.y;
				temp_47 = 0.0 - _BaseColor.z;
				temp_48 = temp_47 + _LayerColor.z;
				temp_49 = 0.0 - temp_42;
				temp_50 = mad(temp_34, temp_49, temp_42);
				temp_51 = mad(temp_44, temp_41, _BaseColor.x);
				temp_52 = mad(temp_46, temp_41, _BaseColor.y);
				temp_53 = mad(temp_48, temp_41, _BaseColor.z);
				temp_54 = temp_50 + temp_51;
				temp_55 = temp_50 + temp_52;
				temp_56 = temp_50 + temp_53;
				temp_57 = _ConstantColor0.w + -1.0;
				temp_58 = 0.0 - temp_54;
				temp_59 = mad(temp_54, _ConstantColor0.x, temp_58);
				temp_60 = 0.0 - temp_55;
				temp_61 = mad(temp_55, _ConstantColor0.y, temp_60);
				temp_62 = 0.0 - temp_56;
				temp_63 = mad(temp_56, _ConstantColor0.z, temp_62);
				temp_64 = mad(temp_57, _ConstantColorVal, 1.0);
				temp_65 = mad(temp_59, _ConstantColorVal, temp_54);
				temp_66 = mad(temp_61, _ConstantColorVal, temp_55);
				temp_67 = mad(temp_63, _ConstantColorVal, temp_56);
				temp_68 = temp_65 * _FixMultiplierColor.x;
				temp_69 = temp_66 * _FixMultiplierColor.y;
				temp_70 = temp_67 * _FixMultiplierColor.z;
				o.c0.x = temp_68;
				o.c0.y = temp_69;
				o.c0.z = temp_70;
				o.c0.w = temp_64;
				return o;
			}
			ENDHLSL
		}
	}
}