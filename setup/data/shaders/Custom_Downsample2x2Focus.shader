Shader "Custom/Downsample2x2Focus" {
	Properties {
		_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	}

	SubShader {
		Tags { "RenderType"="Opaque" }
		Pass {
			Tags { "RenderType"="Opaque" }
			ZTest Always
			ZWrite Off
			Cull Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _MainTex_TexelSize;
			sampler2D _MainTex;
			sampler2D _FocusTex;

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
				o.t0.x = temp_3;
				temp_8 = mad(temp_1, unity_ObjectToWorld._m00, temp_6);
				o.t0.y = temp_4;
				temp_9 = temp_0 * unity_ObjectToWorld._m21;
				temp_10 = mad(temp_2, unity_ObjectToWorld._m12, temp_7);
				temp_11 = temp_0 * unity_ObjectToWorld._m31;
				temp_12 = mad(temp_2, unity_ObjectToWorld._m02, temp_8);
				temp_13 = mad(temp_1, unity_ObjectToWorld._m20, temp_9);
				temp_14 = temp_10 + unity_ObjectToWorld._m13;
				temp_15 = mad(temp_1, unity_ObjectToWorld._m30, temp_11);
				temp_16 = temp_12 + unity_ObjectToWorld._m03;
				temp_17 = mad(temp_2, unity_ObjectToWorld._m22, temp_13);
				temp_18 = temp_14 * unity_MatrixVP._m01;
				temp_19 = mad(temp_2, unity_ObjectToWorld._m32, temp_15);
				temp_20 = temp_14 * unity_MatrixVP._m11;
				temp_21 = temp_17 + unity_ObjectToWorld._m23;
				temp_22 = mad(temp_16, unity_MatrixVP._m00, temp_18);
				temp_23 = temp_14 * unity_MatrixVP._m21;
				temp_24 = temp_14 * unity_MatrixVP._m31;
				temp_25 = mad(temp_16, unity_MatrixVP._m10, temp_20);
				temp_26 = temp_19 + unity_ObjectToWorld._m33;
				temp_27 = mad(temp_21, unity_MatrixVP._m02, temp_22);
				temp_28 = mad(temp_16, unity_MatrixVP._m20, temp_23);
				temp_29 = mad(temp_16, unity_MatrixVP._m30, temp_24);
				temp_30 = mad(temp_21, unity_MatrixVP._m12, temp_25);
				temp_31 = mad(temp_26, unity_MatrixVP._m03, temp_27);
				temp_32 = mad(temp_21, unity_MatrixVP._m22, temp_28);
				o.pos.x = temp_31;
				temp_33 = mad(temp_21, unity_MatrixVP._m32, temp_29);
				temp_34 = mad(temp_26, unity_MatrixVP._m13, temp_30);
				temp_35 = mad(temp_26, unity_MatrixVP._m23, temp_32);
				o.pos.y = temp_34;
				temp_36 = mad(temp_26, unity_MatrixVP._m33, temp_33);
				o.pos.z = temp_35;
				o.pos.w = temp_36;
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
				precise float temp_3;
				precise float temp_4;
				precise float temp_5;
				precise float temp_6;
				precise float temp_7;
				precise float temp_8;
				precise float temp_9;
				precise float3 temp_10;
				precise float temp_11;
				precise float temp_12;
				precise float temp_13;
				precise float3 temp_14;
				precise float temp_15;
				precise float temp_16;
				precise float temp_17;
				precise float3 temp_18;
				precise float temp_19;
				precise float temp_20;
				precise float temp_21;
				precise float3 temp_22;
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
				temp_0 = i.t0.x;
				temp_1 = i.t0.y;
				temp_2 = i.t0.y;
				temp_3 = i.t0.x;
				temp_4 = i.t0.x;
				temp_5 = i.t0.y;
				temp_6 = 0.0 - _MainTex_TexelSize.x;
				temp_7 = temp_0 + temp_6;
				temp_8 = 0.0 - _MainTex_TexelSize.y;
				temp_9 = temp_1 + temp_8;
				temp_10 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_2))).xyz;
				temp_11 = temp_10.x;
				temp_12 = temp_10.y;
				temp_13 = temp_10.z;
				temp_14 = tex2D(_MainTex, nvn_nan0(float2(temp_7, temp_1))).xyz;
				temp_15 = temp_14.x;
				temp_16 = temp_14.y;
				temp_17 = temp_14.z;
				temp_18 = tex2D(_MainTex, nvn_nan0(float2(temp_3, temp_9))).xyz;
				temp_19 = temp_18.x;
				temp_20 = temp_18.y;
				temp_21 = temp_18.z;
				temp_22 = tex2D(_MainTex, nvn_nan0(float2(temp_7, temp_9))).xyz;
				temp_23 = temp_22.x;
				temp_24 = temp_22.y;
				temp_25 = temp_22.z;
				temp_26 = tex2D(_FocusTex, nvn_nan0(float2(temp_4, temp_5))).x;
				temp_27 = temp_11 + temp_15;
				temp_28 = temp_12 + temp_16;
				temp_29 = temp_13 + temp_17;
				temp_30 = temp_27 + temp_19;
				temp_31 = temp_28 + temp_20;
				temp_32 = temp_29 + temp_21;
				temp_33 = temp_23 + temp_30;
				temp_34 = temp_24 + temp_31;
				temp_35 = temp_25 + temp_32;
				temp_36 = temp_33 * 0.25;
				temp_37 = temp_34 * 0.25;
				temp_38 = temp_35 * 0.25;
				o.c0.x = temp_36;
				o.c0.y = temp_37;
				o.c0.z = temp_38;
				o.c0.w = temp_26;
				return o;
			}
			ENDHLSL
		}
	}
}