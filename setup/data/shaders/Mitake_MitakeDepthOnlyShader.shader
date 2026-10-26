Shader "Mitake/MitakeDepthOnlyShader" {
	Properties {

	}

	SubShader {
		Pass {
			Tags { "LightMode"="FORWARDBASE" }
			ColorMask 0
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;

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
			};
			struct v2f {
				float4 pos : SV_POSITION;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a0.x;
				temp_2 = v.a0.z;
				temp_3 = temp_0 * unity_ObjectToWorld._m11;
				temp_4 = temp_0 * unity_ObjectToWorld._m01;
				temp_5 = mad(temp_1, unity_ObjectToWorld._m10, temp_3);
				temp_6 = mad(temp_1, unity_ObjectToWorld._m00, temp_4);
				temp_7 = temp_0 * unity_ObjectToWorld._m21;
				temp_8 = temp_0 * unity_ObjectToWorld._m31;
				temp_9 = mad(temp_2, unity_ObjectToWorld._m12, temp_5);
				temp_10 = mad(temp_2, unity_ObjectToWorld._m02, temp_6);
				temp_11 = mad(temp_1, unity_ObjectToWorld._m20, temp_7);
				temp_12 = mad(temp_1, unity_ObjectToWorld._m30, temp_8);
				temp_13 = temp_9 + unity_ObjectToWorld._m13;
				temp_14 = temp_10 + unity_ObjectToWorld._m03;
				temp_15 = mad(temp_2, unity_ObjectToWorld._m22, temp_11);
				temp_16 = mad(temp_2, unity_ObjectToWorld._m32, temp_12);
				temp_17 = temp_13 * unity_MatrixVP._m01;
				temp_18 = temp_13 * unity_MatrixVP._m11;
				temp_19 = temp_15 + unity_ObjectToWorld._m23;
				temp_20 = temp_16 + unity_ObjectToWorld._m33;
				temp_21 = mad(temp_14, unity_MatrixVP._m00, temp_17);
				temp_22 = temp_13 * unity_MatrixVP._m21;
				temp_23 = temp_13 * unity_MatrixVP._m31;
				temp_24 = mad(temp_14, unity_MatrixVP._m10, temp_18);
				temp_25 = mad(temp_19, unity_MatrixVP._m02, temp_21);
				temp_26 = mad(temp_14, unity_MatrixVP._m20, temp_22);
				temp_27 = mad(temp_14, unity_MatrixVP._m30, temp_23);
				temp_28 = mad(temp_19, unity_MatrixVP._m12, temp_24);
				temp_29 = mad(temp_20, unity_MatrixVP._m03, temp_25);
				temp_30 = mad(temp_19, unity_MatrixVP._m22, temp_26);
				o.pos.x = temp_29;
				temp_31 = mad(temp_19, unity_MatrixVP._m32, temp_27);
				temp_32 = mad(temp_20, unity_MatrixVP._m13, temp_28);
				temp_33 = mad(temp_20, unity_MatrixVP._m23, temp_30);
				o.pos.y = temp_32;
				temp_34 = mad(temp_20, unity_MatrixVP._m33, temp_31);
				o.pos.z = temp_33;
				o.pos.w = temp_34;
				return o;
			}
			// 0001_fs.sph.bin keywords=[]
			struct f_in {
				float4 fragcoord : SV_POSITION;
			};
			struct f_out {
				float4 c0 : SV_Target0;
			};
			f_out frag(f_in i) {
				f_out o = (f_out)0;
				o.c0.x = 0.0;
				o.c0.y = 0.0;
				o.c0.z = 0.0;
				o.c0.w = 0.0;
				return o;
			}
			ENDHLSL
		}
	}
}