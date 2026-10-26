Shader "Delphis/Map/Fog" {
	Properties {
		_MainTex ("Main Tex", 2D) = "white" {}
	}

	SubShader {
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }
		GrabPass { "_BackgroundTexture" }
		Pass {
			Tags { "LightMode"="FORWARDBASE" "RenderType"="Transparent" }
			Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			float3 _WorldSpaceCameraPos;
			float4 _ProjectionParams;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _Time;
			float4 _ZBufferParams;
			float4 _MainTex_TexelSize;
			sampler2D _CameraDepthTexture;
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
				float4 t4 : TEXCOORD4;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a1.y;
				temp_1 = v.a0.y;
				temp_2 = v.a1.x;
				temp_3 = v.a0.x;
				temp_4 = v.a1.z;
				temp_5 = v.a0.z;
				temp_6 = v.a0.w;
				temp_7 = temp_0 * unity_ObjectToWorld._m01;
				temp_8 = temp_0 * unity_ObjectToWorld._m11;
				temp_9 = temp_0 * unity_ObjectToWorld._m21;
				temp_10 = temp_1 * unity_ObjectToWorld._m01;
				temp_11 = temp_1 * unity_ObjectToWorld._m21;
				temp_12 = temp_1 * unity_ObjectToWorld._m11;
				temp_13 = temp_1 * unity_ObjectToWorld._m31;
				temp_14 = mad(temp_2, unity_ObjectToWorld._m00, temp_7);
				temp_15 = mad(temp_2, unity_ObjectToWorld._m10, temp_8);
				temp_16 = v.a2.x;
				temp_17 = mad(temp_2, unity_ObjectToWorld._m20, temp_9);
				temp_18 = mad(temp_3, unity_ObjectToWorld._m00, temp_10);
				temp_19 = mad(temp_3, unity_ObjectToWorld._m20, temp_11);
				temp_20 = mad(temp_3, unity_ObjectToWorld._m10, temp_12);
				temp_21 = mad(temp_3, unity_ObjectToWorld._m30, temp_13);
				temp_22 = mad(temp_4, unity_ObjectToWorld._m02, temp_14);
				temp_23 = v.a2.y;
				temp_24 = mad(temp_4, unity_ObjectToWorld._m12, temp_15);
				o.t4.x = _WorldSpaceCameraPos.x;
				temp_25 = mad(temp_5, unity_ObjectToWorld._m02, temp_18);
				o.t4.y = _WorldSpaceCameraPos.y;
				temp_26 = mad(temp_4, unity_ObjectToWorld._m22, temp_17);
				o.t4.z = _WorldSpaceCameraPos.z;
				temp_27 = temp_22 * temp_22;
				temp_28 = mad(temp_5, unity_ObjectToWorld._m12, temp_20);
				temp_29 = mad(temp_6, unity_ObjectToWorld._m03, temp_25);
				temp_30 = mad(temp_5, unity_ObjectToWorld._m22, temp_19);
				temp_31 = mad(temp_5, unity_ObjectToWorld._m32, temp_21);
				temp_32 = temp_25 + unity_ObjectToWorld._m03;
				temp_33 = mad(temp_24, temp_24, temp_27);
				temp_34 = temp_28 + unity_ObjectToWorld._m13;
				temp_35 = mad(temp_6, unity_ObjectToWorld._m13, temp_28);
				temp_36 = 0.0 - _WorldSpaceCameraPos.x;
				temp_37 = temp_29 + temp_36;
				temp_38 = mad(temp_6, unity_ObjectToWorld._m23, temp_30);
				temp_39 = temp_16 * 2.0;
				temp_40 = mad(temp_26, temp_26, temp_33);
				o.t0.x = temp_39;
				temp_41 = 0.0 - _WorldSpaceCameraPos.y;
				temp_42 = temp_35 + temp_41;
				temp_43 = rsqrt(temp_40);
				temp_44 = temp_37 * temp_37;
				temp_45 = 0.0 - _WorldSpaceCameraPos.z;
				temp_46 = temp_38 + temp_45;
				temp_47 = temp_34 * unity_MatrixVP._m21;
				temp_48 = temp_23 * 2.0;
				temp_49 = temp_30 + unity_ObjectToWorld._m23;
				o.t0.y = temp_48;
				temp_50 = mad(temp_42, temp_42, temp_44);
				temp_51 = mad(temp_32, unity_MatrixVP._m20, temp_47);
				temp_52 = temp_22 * temp_43;
				temp_53 = temp_24 * temp_43;
				o.t2.x = temp_52;
				temp_54 = mad(temp_46, temp_46, temp_50);
				o.t2.y = temp_53;
				temp_55 = temp_26 * temp_43;
				temp_56 = rsqrt(temp_54);
				temp_57 = temp_34 * unity_MatrixVP._m31;
				o.t2.z = temp_55;
				temp_58 = temp_31 + unity_ObjectToWorld._m33;
				temp_59 = mad(temp_49, unity_MatrixVP._m22, temp_51);
				temp_60 = mad(temp_32, unity_MatrixVP._m30, temp_57);
				temp_61 = temp_34 * unity_MatrixVP._m11;
				temp_62 = temp_34 * unity_MatrixVP._m01;
				temp_63 = temp_37 * temp_56;
				temp_64 = mad(temp_58, unity_MatrixVP._m23, temp_59);
				o.t3.x = temp_63;
				temp_65 = mad(temp_49, unity_MatrixVP._m32, temp_60);
				o.pos.z = temp_64;
				temp_66 = mad(temp_32, unity_MatrixVP._m10, temp_61);
				o.t1.z = temp_64;
				temp_67 = mad(temp_32, unity_MatrixVP._m00, temp_62);
				temp_68 = mad(temp_58, unity_MatrixVP._m33, temp_65);
				temp_69 = temp_42 * temp_56;
				o.pos.w = temp_68;
				temp_70 = mad(temp_49, unity_MatrixVP._m12, temp_66);
				o.t3.y = temp_69;
				temp_71 = mad(temp_49, unity_MatrixVP._m02, temp_67);
				o.t1.w = temp_68;
				temp_72 = temp_46 * temp_56;
				temp_73 = mad(temp_58, unity_MatrixVP._m13, temp_70);
				o.t3.z = temp_72;
				temp_74 = mad(temp_58, unity_MatrixVP._m03, temp_71);
				o.pos.y = temp_73;
				temp_75 = temp_68 * 0.5;
				o.pos.x = temp_74;
				temp_76 = temp_73 * _ProjectionParams.x;
				temp_77 = mad(temp_74, 0.5, temp_75);
				temp_78 = mad(temp_76, 0.5, temp_75);
				o.t1.x = temp_77;
				o.t1.y = temp_78;
				return o;
			}
			// 0001_fs.sph.bin keywords=[]
			struct f_in {
				float4 fragcoord : SV_POSITION;
				float4 t0 : TEXCOORD0;
				float4 t1 : TEXCOORD1;
				float4 t2 : TEXCOORD2;
				float4 t3 : TEXCOORD3;
				float4 t4 : TEXCOORD4;
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
				temp_0 = i.t1.w;
				temp_1 = i.t0.x;
				temp_2 = i.t0.y;
				temp_3 = i.t4.x;
				temp_4 = i.t1.x;
				temp_5 = i.t1.y;
				temp_6 = i.t4.z;
				temp_7 = 1.0 / temp_0;
				temp_8 = temp_1 + 0.5;
				temp_9 = mad(_Time.x, 0.5, temp_1);
				temp_10 = temp_2 + 0.5;
				temp_11 = mad(_Time.x, 0.5, temp_2);
				temp_12 = mad(_Time.x, 0.800000012, temp_8);
				temp_13 = mad(temp_3, _MainTex_TexelSize.x, temp_9);
				temp_14 = mad(_Time.x, 0.800000012, temp_10);
				temp_15 = 0.0 - _MainTex_TexelSize.y;
				temp_16 = mad(temp_6, temp_15, temp_11);
				temp_17 = mad(temp_3, _MainTex_TexelSize.x, temp_12);
				temp_18 = temp_4 * temp_7;
				temp_19 = temp_5 * temp_7;
				temp_20 = tex2D(_CameraDepthTexture, nvn_nan0(float2(temp_18, temp_19))).x;
				temp_21 = tex2D(_MainTex, nvn_nan0(float2(temp_13, temp_16))).x;
				temp_22 = 0.0 - _MainTex_TexelSize.y;
				temp_23 = mad(temp_6, temp_22, temp_14);
				temp_24 = tex2D(_MainTex, nvn_nan0(float2(temp_17, temp_23))).x;
				temp_25 = i.t1.z;
				temp_26 = 1.0 / _ProjectionParams.y;
				temp_27 = temp_25 * temp_26;
				temp_28 = 0.0 - _ProjectionParams.z;
				temp_29 = mad(temp_27, temp_28, _ProjectionParams.z);
				temp_30 = max(0.0, temp_29);
				temp_31 = mad(temp_20, _ZBufferParams.z, _ZBufferParams.w);
				temp_32 = 1.0 / temp_31;
				temp_33 = temp_21 + temp_24;
				temp_34 = temp_33 * 0.5;
				temp_35 = log2(temp_34);
				temp_36 = 0.0 - temp_30;
				temp_37 = temp_32 + temp_36;
				temp_38 = temp_37 + -14.0;
				temp_39 = temp_35 * 0.199951172;
				temp_40 = temp_38 * 0.0500000007;
				temp_41 = saturate(temp_40);
				temp_42 = temp_41 + -0.200000003;
				temp_43 = exp2(temp_39);
				temp_44 = temp_42 * 1.25;
				temp_45 = max(0.0, temp_44);
				temp_46 = min(temp_43, 1.0);
				temp_47 = mad(temp_45, -2.0, 3.0);
				temp_48 = temp_45 * temp_45;
				temp_49 = temp_46 * 0.700195312;
				temp_50 = temp_47 * temp_48;
				o.c0.x = temp_49;
				o.c0.y = temp_49;
				o.c0.z = temp_49;
				o.c0.w = temp_50;
				return o;
			}
			ENDHLSL
		}
	}
}