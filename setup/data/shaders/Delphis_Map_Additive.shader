Shader "Delphis/Map/Additive" {
	Properties {
		_MainTex ("Main Tex", 2D) = "white" {}
	}

	SubShader {
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }
		Pass {
			Tags { "LightMode"="FORWARDBASE" "RenderType"="Transparent" }
			Blend SrcAlpha One, SrcAlpha One
			ZWrite Off
			Cull Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			float4 _ProjectionParams;
			float4 _MainTex_ST;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixV;
			float4x4 unity_MatrixVP;
			float4 _ZBufferParams;
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
				temp_1 = v.a0.x;
				temp_2 = v.a0.z;
				temp_3 = v.a2.y;
				temp_4 = v.a1.x;
				temp_5 = v.a2.x;
				temp_6 = temp_0 * unity_ObjectToWorld._m11;
				temp_7 = v.a1.y;
				temp_8 = temp_0 * unity_ObjectToWorld._m01;
				temp_9 = v.a1.z;
				temp_10 = mad(temp_1, unity_ObjectToWorld._m10, temp_6);
				temp_11 = v.a1.w;
				temp_12 = temp_0 * unity_ObjectToWorld._m21;
				o.t2.x = temp_4;
				temp_13 = mad(temp_1, unity_ObjectToWorld._m00, temp_8);
				o.t2.y = temp_7;
				temp_14 = mad(temp_2, unity_ObjectToWorld._m12, temp_10);
				o.t2.z = temp_9;
				temp_15 = temp_0 * unity_ObjectToWorld._m31;
				o.t2.w = temp_11;
				temp_16 = mad(temp_2, unity_ObjectToWorld._m02, temp_13);
				temp_17 = temp_14 + unity_ObjectToWorld._m13;
				temp_18 = mad(temp_1, unity_ObjectToWorld._m20, temp_12);
				temp_19 = mad(temp_5, _MainTex_ST.x, _MainTex_ST.z);
				temp_20 = mad(temp_1, unity_ObjectToWorld._m30, temp_15);
				o.t0.x = temp_19;
				temp_21 = temp_16 + unity_ObjectToWorld._m03;
				temp_22 = temp_17 * unity_MatrixV._m21;
				temp_23 = mad(temp_2, unity_ObjectToWorld._m22, temp_18);
				temp_24 = temp_17 * unity_MatrixVP._m21;
				temp_25 = mad(temp_2, unity_ObjectToWorld._m32, temp_20);
				temp_26 = mad(temp_21, unity_MatrixV._m20, temp_22);
				temp_27 = temp_23 + unity_ObjectToWorld._m23;
				temp_28 = mad(temp_21, unity_MatrixVP._m20, temp_24);
				temp_29 = temp_17 * unity_MatrixVP._m31;
				temp_30 = temp_25 + unity_ObjectToWorld._m33;
				temp_31 = mad(temp_3, _MainTex_ST.y, _MainTex_ST.w);
				temp_32 = temp_17 * unity_MatrixVP._m11;
				o.t0.y = temp_31;
				temp_33 = mad(temp_27, unity_MatrixV._m22, temp_26);
				temp_34 = mad(temp_27, unity_MatrixVP._m22, temp_28);
				temp_35 = mad(temp_21, unity_MatrixVP._m30, temp_29);
				temp_36 = temp_17 * unity_MatrixVP._m01;
				temp_37 = mad(temp_21, unity_MatrixVP._m10, temp_32);
				temp_38 = mad(temp_30, unity_MatrixV._m23, temp_33);
				temp_39 = mad(temp_30, unity_MatrixVP._m23, temp_34);
				temp_40 = mad(temp_27, unity_MatrixVP._m32, temp_35);
				o.pos.z = temp_39;
				temp_41 = mad(temp_21, unity_MatrixVP._m00, temp_36);
				temp_42 = mad(temp_27, unity_MatrixVP._m12, temp_37);
				temp_43 = 0.0 - temp_38;
				temp_44 = temp_43 + -0.0;
				temp_45 = mad(temp_30, unity_MatrixVP._m33, temp_40);
				o.t1.z = temp_44;
				temp_46 = mad(temp_27, unity_MatrixVP._m02, temp_41);
				o.pos.w = temp_45;
				temp_47 = mad(temp_30, unity_MatrixVP._m13, temp_42);
				o.t1.w = temp_45;
				temp_48 = temp_45 * 0.5;
				o.pos.y = temp_47;
				temp_49 = mad(temp_30, unity_MatrixVP._m03, temp_46);
				temp_50 = temp_47 * _ProjectionParams.x;
				o.pos.x = temp_49;
				temp_51 = mad(temp_49, 0.5, temp_48);
				temp_52 = mad(temp_50, 0.5, temp_48);
				o.t1.x = temp_51;
				o.t1.y = temp_52;
				return o;
			}
			// 0001_fs.sph.bin keywords=[]
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
				precise float temp_4;
				precise float temp_5;
				precise float temp_6;
				precise float temp_7;
				precise float temp_8;
				precise float4 temp_9;
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
				temp_0 = i.t1.w;
				temp_1 = i.t1.x;
				temp_2 = i.t1.y;
				temp_3 = i.t0.x;
				temp_4 = i.t0.y;
				temp_5 = 1.0 / temp_0;
				temp_6 = temp_1 * temp_5;
				temp_7 = temp_2 * temp_5;
				temp_8 = tex2D(_CameraDepthTexture, nvn_nan0(float2(temp_6, temp_7))).x;
				temp_9 = tex2D(_MainTex, nvn_nan0(float2(temp_3, temp_4))).xyzw;
				temp_10 = temp_9.x;
				temp_11 = temp_9.y;
				temp_12 = temp_9.z;
				temp_13 = temp_9.w;
				temp_14 = i.t1.z;
				temp_15 = i.t2.x;
				temp_16 = i.t2.y;
				temp_17 = i.t2.w;
				temp_18 = mad(temp_8, _ZBufferParams.z, _ZBufferParams.w);
				temp_19 = i.t2.z;
				temp_20 = temp_10 * temp_15;
				temp_21 = 1.0 / temp_18;
				temp_22 = temp_11 * temp_16;
				temp_23 = 0.0 - temp_14;
				temp_24 = temp_23 + temp_21;
				temp_25 = temp_24 * 0.5;
				temp_26 = saturate(temp_25);
				temp_27 = temp_13 * temp_26;
				temp_28 = temp_12 * temp_19;
				temp_29 = temp_27 * temp_17;
				o.c0.x = temp_20;
				o.c0.y = temp_22;
				o.c0.z = temp_28;
				o.c0.w = temp_29;
				return o;
			}
			ENDHLSL
		}
	}
}