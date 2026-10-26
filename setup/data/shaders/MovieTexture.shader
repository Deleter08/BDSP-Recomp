Shader "MovieTexture" {
	Properties {
		_MainTex ("Luminance Texture", 2D) = "black" {}
		_ChromaTex ("Chroma Texture", 2D) = "green" {}
	}

	SubShader {
		Pass {
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			float4 _MainTex_ST;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			sampler2D _MainTex;
			sampler2D _ChromaTex;

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
				float4 a1 : TEXCOORD0;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a0.x;
				temp_2 = v.a0.z;
				temp_3 = v.a1.x;
				temp_4 = v.a1.y;
				temp_5 = temp_0 * unity_ObjectToWorld._m11;
				temp_6 = temp_0 * unity_ObjectToWorld._m01;
				temp_7 = mad(temp_1, unity_ObjectToWorld._m10, temp_5);
				temp_8 = mad(temp_1, unity_ObjectToWorld._m00, temp_6);
				temp_9 = temp_0 * unity_ObjectToWorld._m21;
				temp_10 = temp_0 * unity_ObjectToWorld._m31;
				temp_11 = mad(temp_2, unity_ObjectToWorld._m12, temp_7);
				temp_12 = mad(temp_2, unity_ObjectToWorld._m02, temp_8);
				temp_13 = mad(temp_1, unity_ObjectToWorld._m20, temp_9);
				temp_14 = mad(temp_1, unity_ObjectToWorld._m30, temp_10);
				temp_15 = mad(temp_3, _MainTex_ST.x, _MainTex_ST.z);
				temp_16 = temp_11 + unity_ObjectToWorld._m13;
				o.t0.x = temp_15;
				temp_17 = temp_12 + unity_ObjectToWorld._m03;
				temp_18 = mad(temp_2, unity_ObjectToWorld._m22, temp_13);
				temp_19 = mad(temp_2, unity_ObjectToWorld._m32, temp_14);
				temp_20 = temp_16 * unity_MatrixVP._m01;
				temp_21 = temp_16 * unity_MatrixVP._m11;
				temp_22 = temp_18 + unity_ObjectToWorld._m23;
				temp_23 = temp_19 + unity_ObjectToWorld._m33;
				temp_24 = mad(temp_17, unity_MatrixVP._m00, temp_20);
				temp_25 = temp_16 * unity_MatrixVP._m21;
				temp_26 = temp_16 * unity_MatrixVP._m31;
				temp_27 = mad(temp_17, unity_MatrixVP._m10, temp_21);
				temp_28 = mad(temp_4, _MainTex_ST.y, _MainTex_ST.w);
				temp_29 = mad(temp_22, unity_MatrixVP._m02, temp_24);
				o.t0.y = temp_28;
				temp_30 = mad(temp_17, unity_MatrixVP._m20, temp_25);
				temp_31 = mad(temp_17, unity_MatrixVP._m30, temp_26);
				temp_32 = mad(temp_22, unity_MatrixVP._m12, temp_27);
				temp_33 = mad(temp_23, unity_MatrixVP._m03, temp_29);
				temp_34 = mad(temp_22, unity_MatrixVP._m22, temp_30);
				o.pos.x = temp_33;
				temp_35 = mad(temp_22, unity_MatrixVP._m32, temp_31);
				temp_36 = mad(temp_23, unity_MatrixVP._m13, temp_32);
				temp_37 = mad(temp_23, unity_MatrixVP._m23, temp_34);
				o.pos.y = temp_36;
				temp_38 = mad(temp_23, unity_MatrixVP._m33, temp_35);
				o.pos.z = temp_37;
				o.pos.w = temp_38;
				return o;
			}
			// 0001_fs.sph.bin keywords=[]
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
				precise float2 temp_3;
				precise float temp_4;
				precise float temp_5;
				bool temp_6;
				bool temp_7;
				bool temp_8;
				bool temp_9;
				bool temp_10;
				bool temp_11;
				bool temp_12;
				precise float temp_13;
				precise float temp_14;
				precise float temp_15;
				precise float temp_16;
				precise float temp_17;
				precise float temp_18;
				precise float temp_19;
				int temp_20;
				int temp_21;
				int temp_22;
				int temp_23;
				precise float temp_24;
				int temp_25;
				int temp_26;
				temp_0 = i.t0.x;
				temp_1 = i.t0.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).x;
				temp_3 = tex2D(_ChromaTex, nvn_nan0(float2(temp_0, temp_1))).xy;
				temp_4 = temp_3.x;
				temp_5 = temp_3.y;
				temp_6 = temp_0 < 0.0;
				temp_7 = temp_1 < 0.0;
				temp_8 = temp_7 || temp_6;
				temp_9 = temp_0 > 1.0;
				temp_10 = temp_9 || temp_8;
				temp_11 = temp_1 > 1.0;
				temp_12 = temp_11 || temp_10;
				temp_13 = temp_2 + -0.0625;
				temp_14 = temp_4 + -0.5;
				temp_15 = temp_5 + -0.5;
				temp_16 = temp_13 * 1.16439998;
				temp_17 = mad(temp_14, -0.213300005, temp_16);
				temp_18 = mad(temp_14, 2.11240005, temp_16);
				temp_19 = mad(temp_15, 1.79270005, temp_16);
				temp_20 = asint(temp_19);
				temp_21 = asint(temp_18);
				if (temp_12)
				{
				temp_20 = 0x3F800000;
				}
				temp_22 = temp_20;
				if (temp_12)
				{
				temp_21 = 0;
				}
				temp_23 = temp_21;
				temp_24 = mad(temp_15, -0.532899976, temp_17);
				temp_25 = asint(temp_24);
				if (temp_12)
				{
				temp_25 = 0;
				}
				temp_26 = temp_25;
				o.c0.x = asfloat(temp_22);
				o.c0.y = asfloat(temp_26);
				o.c0.z = asfloat(temp_23);
				o.c0.w = 1.0;
				return o;
			}
			ENDHLSL
		}
	}
}