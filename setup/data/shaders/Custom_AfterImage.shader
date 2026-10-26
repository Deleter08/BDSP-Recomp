Shader "Custom/AfterImage" {
	Properties {
		_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
		_PrevFrameTex ("Previous Frame Image", 2D) = "black" {}
		_Angle ("Angle", Float) = 0
		_ReciprocalScale ("Reciprocal Scale", Float) = 1
		_BlendWeight ("Blend Weight", Float) = 1
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
			float _Angle;
			float _ReciprocalScale;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float _BlendWeight;
			sampler2D _MainTex;
			sampler2D _PrevFrameTex;

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
				precise float temp_42;
				precise float temp_43;
				precise float temp_44;
				precise float temp_45;
				precise float temp_46;
				precise float temp_47;
				precise float temp_48;
				precise float temp_49;
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = cos(_Angle);
				temp_2 = sin(_Angle);
				temp_3 = v.a0.x;
				temp_4 = v.a1.x;
				temp_5 = v.a0.z;
				temp_6 = v.a1.y;
				temp_7 = temp_0 * unity_ObjectToWorld._m11;
				o.t0.x = temp_4;
				temp_8 = temp_0 * unity_ObjectToWorld._m01;
				o.t0.y = temp_6;
				temp_9 = mad(temp_3, unity_ObjectToWorld._m10, temp_7);
				temp_10 = temp_4 + -0.5;
				temp_11 = temp_6 + -0.5;
				temp_12 = temp_0 * unity_ObjectToWorld._m21;
				temp_13 = temp_0 * unity_ObjectToWorld._m31;
				temp_14 = mad(temp_3, unity_ObjectToWorld._m00, temp_8);
				temp_15 = mad(temp_5, unity_ObjectToWorld._m12, temp_9);
				temp_16 = temp_10 * _ReciprocalScale;
				temp_17 = temp_11 * _ReciprocalScale;
				temp_18 = mad(temp_3, unity_ObjectToWorld._m20, temp_12);
				temp_19 = mad(temp_3, unity_ObjectToWorld._m30, temp_13);
				temp_20 = mad(temp_5, unity_ObjectToWorld._m02, temp_14);
				temp_21 = temp_15 + unity_ObjectToWorld._m13;
				temp_22 = temp_16 * temp_1;
				temp_23 = temp_16 * temp_2;
				temp_24 = mad(temp_5, unity_ObjectToWorld._m22, temp_18);
				temp_25 = mad(temp_5, unity_ObjectToWorld._m32, temp_19);
				temp_26 = temp_20 + unity_ObjectToWorld._m03;
				temp_27 = temp_21 * unity_MatrixVP._m01;
				temp_28 = 0.0 - temp_2;
				temp_29 = mad(temp_17, temp_28, temp_22);
				temp_30 = mad(temp_17, temp_1, temp_23);
				temp_31 = temp_21 * unity_MatrixVP._m11;
				temp_32 = temp_24 + unity_ObjectToWorld._m23;
				temp_33 = temp_25 + unity_ObjectToWorld._m33;
				temp_34 = mad(temp_26, unity_MatrixVP._m00, temp_27);
				temp_35 = temp_21 * unity_MatrixVP._m21;
				temp_36 = temp_21 * unity_MatrixVP._m31;
				temp_37 = mad(temp_26, unity_MatrixVP._m10, temp_31);
				temp_38 = temp_29 + 0.5;
				temp_39 = temp_30 + 0.5;
				o.t1.x = temp_38;
				temp_40 = mad(temp_26, unity_MatrixVP._m20, temp_35);
				o.t1.y = temp_39;
				temp_41 = mad(temp_26, unity_MatrixVP._m30, temp_36);
				temp_42 = mad(temp_32, unity_MatrixVP._m02, temp_34);
				temp_43 = mad(temp_32, unity_MatrixVP._m12, temp_37);
				temp_44 = mad(temp_32, unity_MatrixVP._m22, temp_40);
				temp_45 = mad(temp_32, unity_MatrixVP._m32, temp_41);
				temp_46 = mad(temp_33, unity_MatrixVP._m03, temp_42);
				temp_47 = mad(temp_33, unity_MatrixVP._m13, temp_43);
				o.pos.x = temp_46;
				temp_48 = mad(temp_33, unity_MatrixVP._m23, temp_44);
				o.pos.y = temp_47;
				temp_49 = mad(temp_33, unity_MatrixVP._m33, temp_45);
				o.pos.z = temp_48;
				o.pos.w = temp_49;
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
				precise float4 temp_6;
				precise float temp_7;
				precise float temp_8;
				precise float temp_9;
				precise float temp_10;
				precise float4 temp_11;
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
				temp_0 = i.t0.y;
				temp_1 = i.t1.x;
				temp_2 = i.t1.y;
				temp_3 = i.t0.x;
				temp_4 = 0.0 - temp_0;
				temp_5 = temp_4 + 1.0;
				temp_6 = tex2D(_MainTex, nvn_nan0(float2(temp_1, temp_2))).xyzw;
				temp_7 = temp_6.x;
				temp_8 = temp_6.y;
				temp_9 = temp_6.z;
				temp_10 = temp_6.w;
				temp_11 = tex2D(_PrevFrameTex, nvn_nan0(float2(temp_3, temp_5))).xyzw;
				temp_12 = temp_11.x;
				temp_13 = temp_11.y;
				temp_14 = temp_11.z;
				temp_15 = temp_11.w;
				temp_16 = 0.0 - temp_14;
				temp_17 = temp_9 + temp_16;
				temp_18 = 0.0 - temp_12;
				temp_19 = temp_7 + temp_18;
				temp_20 = 0.0 - temp_13;
				temp_21 = temp_8 + temp_20;
				temp_22 = 0.0 - temp_15;
				temp_23 = temp_10 + temp_22;
				temp_24 = mad(temp_17, _BlendWeight, temp_14);
				temp_25 = mad(temp_19, _BlendWeight, temp_12);
				temp_26 = mad(temp_21, _BlendWeight, temp_13);
				temp_27 = mad(temp_23, _BlendWeight, temp_15);
				o.c0.x = temp_25;
				o.c0.y = temp_26;
				o.c0.z = temp_24;
				o.c0.w = temp_27;
				return o;
			}
			ENDHLSL
		}
	}
}