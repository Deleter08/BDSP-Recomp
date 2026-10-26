Shader "Dpr/PostEffect/FeedbackBlur" {
	Properties {
		[Toggle(_FEEDBACKBLUR_ON)] _FeedbackBlur ("FeedbackBlur Enabled", Float) = 0
		_FeedbackBlurWeight ("Weight", Float) = 0
		_FeedbackBlurRotation ("Rotate", Vector) = (0,0,0,1)
		_FeedbackBlurScale ("Scale", Float) = 1
		_FeedbackBlurHue ("Hue", Float) = 0
		_FeedbackBlurSaturation ("Saturation", Float) = 1
		_FeedbackBlurBrightness ("Brightness", Float) = 1
		_FeedbackBlurContrast ("Contrast", Float) = 1
	}

	SubShader {
		Tags { "RenderType"="Opeque" }
		Pass {
			Name "FeedbackBlur"
			Tags { "RenderType"="Opeque" }
			Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
			ZTest Always
			ZWrite Off
			Cull Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			float4 _FeedbackBlurRotation;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float _FeedbackBlurWeight;
			float _FeedbackBlurHue;
			float _FeedbackBlurSaturation;
			float _FeedbackBlurBrightness;
			float _FeedbackBlurContrast;
			sampler2D _DelayFrameTexture;

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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a1.x;
				temp_2 = v.a0.x;
				temp_3 = v.a1.y;
				temp_4 = v.a0.z;
				temp_5 = temp_0 * unity_ObjectToWorld._m11;
				temp_6 = temp_0 * unity_ObjectToWorld._m01;
				temp_7 = temp_1 * _FeedbackBlurRotation.x;
				temp_8 = temp_1 * _FeedbackBlurRotation.z;
				temp_9 = mad(temp_2, unity_ObjectToWorld._m10, temp_5);
				temp_10 = temp_0 * unity_ObjectToWorld._m21;
				temp_11 = mad(temp_3, _FeedbackBlurRotation.y, temp_7);
				temp_12 = mad(temp_2, unity_ObjectToWorld._m00, temp_6);
				temp_13 = mad(temp_3, _FeedbackBlurRotation.w, temp_8);
				temp_14 = 0.5 * _FeedbackBlurRotation.x;
				temp_15 = mad(temp_4, unity_ObjectToWorld._m12, temp_9);
				temp_16 = 0.5 * _FeedbackBlurRotation.z;
				temp_17 = temp_0 * unity_ObjectToWorld._m31;
				temp_18 = mad(temp_4, unity_ObjectToWorld._m02, temp_12);
				temp_19 = mad(temp_2, unity_ObjectToWorld._m20, temp_10);
				temp_20 = mad(0.5, _FeedbackBlurRotation.y, temp_14);
				temp_21 = temp_15 + unity_ObjectToWorld._m13;
				temp_22 = mad(0.5, _FeedbackBlurRotation.w, temp_16);
				temp_23 = mad(temp_2, unity_ObjectToWorld._m30, temp_17);
				temp_24 = temp_18 + unity_ObjectToWorld._m03;
				temp_25 = mad(temp_4, unity_ObjectToWorld._m22, temp_19);
				temp_26 = 0.0 - temp_20;
				temp_27 = temp_11 + temp_26;
				temp_28 = temp_21 * unity_MatrixVP._m01;
				temp_29 = temp_21 * unity_MatrixVP._m11;
				temp_30 = 0.0 - temp_22;
				temp_31 = temp_13 + temp_30;
				temp_32 = mad(temp_4, unity_ObjectToWorld._m32, temp_23);
				temp_33 = temp_25 + unity_ObjectToWorld._m23;
				temp_34 = temp_27 + 0.5;
				temp_35 = mad(temp_24, unity_MatrixVP._m00, temp_28);
				o.t0.x = temp_34;
				temp_36 = temp_21 * unity_MatrixVP._m21;
				temp_37 = temp_21 * unity_MatrixVP._m31;
				temp_38 = mad(temp_24, unity_MatrixVP._m10, temp_29);
				temp_39 = temp_32 + unity_ObjectToWorld._m33;
				temp_40 = temp_31 + 0.5;
				temp_41 = mad(temp_33, unity_MatrixVP._m02, temp_35);
				o.t0.y = temp_40;
				temp_42 = mad(temp_24, unity_MatrixVP._m20, temp_36);
				temp_43 = mad(temp_24, unity_MatrixVP._m30, temp_37);
				temp_44 = mad(temp_33, unity_MatrixVP._m12, temp_38);
				temp_45 = mad(temp_39, unity_MatrixVP._m03, temp_41);
				temp_46 = mad(temp_33, unity_MatrixVP._m22, temp_42);
				o.pos.x = temp_45;
				temp_47 = mad(temp_33, unity_MatrixVP._m32, temp_43);
				temp_48 = mad(temp_39, unity_MatrixVP._m13, temp_44);
				temp_49 = mad(temp_39, unity_MatrixVP._m23, temp_46);
				o.pos.y = temp_48;
				temp_50 = mad(temp_39, unity_MatrixVP._m33, temp_47);
				o.pos.z = temp_49;
				o.pos.w = temp_50;
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
				precise float3 temp_2;
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
				temp_0 = i.t0.x;
				temp_1 = i.t0.y;
				temp_2 = tex2D(_DelayFrameTexture, nvn_nan0(float2(temp_0, temp_1))).xyz;
				temp_3 = temp_2.x;
				temp_4 = temp_2.y;
				temp_5 = temp_2.z;
				temp_6 = _FeedbackBlurHue * 0.0174560547;
				temp_7 = cos(temp_6);
				temp_8 = _FeedbackBlurSaturation * _FeedbackBlurBrightness;
				temp_9 = sin(temp_6);
				temp_10 = temp_8 * temp_7;
				temp_11 = temp_9 * temp_8;
				temp_12 = temp_10 * 0.300048828;
				temp_13 = temp_10 * 0.587890625;
				temp_14 = temp_10 * 0.886230469;
				temp_15 = temp_10 * 0.413085938;
				temp_16 = temp_10 * 0.586914062;
				temp_17 = temp_10 * 0.114013672;
				temp_18 = temp_10 * 0.299072266;
				temp_19 = 0.0 - temp_12;
				temp_20 = mad(_FeedbackBlurBrightness, 0.298999995, temp_19);
				temp_21 = 0.0 - temp_13;
				temp_22 = mad(_FeedbackBlurBrightness, 0.587000012, temp_21);
				temp_23 = mad(_FeedbackBlurBrightness, 0.114, temp_14);
				temp_24 = mad(_FeedbackBlurBrightness, 0.587000012, temp_15);
				temp_25 = 0.0 - temp_16;
				temp_26 = mad(_FeedbackBlurBrightness, 0.587000012, temp_25);
				temp_27 = 0.0 - temp_17;
				temp_28 = mad(_FeedbackBlurBrightness, 0.114, temp_27);
				temp_29 = 0.0 - temp_18;
				temp_30 = mad(_FeedbackBlurBrightness, 0.298999995, temp_29);
				temp_31 = _FeedbackBlurBrightness * 0.299072266;
				temp_32 = mad(temp_11, -0.20300293, temp_23);
				temp_33 = mad(temp_11, -0.327880859, temp_30);
				temp_34 = mad(temp_10, 0.701171875, temp_31);
				temp_35 = mad(temp_11, 0.330078125, temp_26);
				temp_36 = mad(temp_11, -1.04980469, temp_22);
				temp_37 = mad(temp_11, 1.25, temp_20);
				temp_38 = mad(temp_11, 0.0350036621, temp_24);
				temp_39 = mad(temp_11, 0.16796875, temp_34);
				temp_40 = temp_4 * temp_35;
				temp_41 = temp_3 * temp_33;
				temp_42 = temp_36 * temp_4;
				temp_43 = mad(temp_11, -0.497070312, temp_28);
				temp_44 = mad(temp_11, 0.291992188, temp_28);
				temp_45 = mad(temp_3, temp_39, temp_40);
				temp_46 = mad(temp_4, temp_38, temp_41);
				temp_47 = mad(temp_37, temp_3, temp_42);
				temp_48 = mad(temp_5, temp_43, temp_45);
				temp_49 = mad(temp_5, temp_44, temp_46);
				temp_50 = mad(temp_5, temp_32, temp_47);
				temp_51 = _FeedbackBlurWeight + -0.217651367;
				temp_52 = temp_48 + -0.217637643;
				temp_53 = temp_49 + -0.217637643;
				temp_54 = temp_50 + -0.217637643;
				temp_55 = mad(temp_51, _FeedbackBlurContrast, 0.217637643);
				temp_56 = mad(temp_52, _FeedbackBlurContrast, 0.217637643);
				temp_57 = mad(temp_53, _FeedbackBlurContrast, 0.217637643);
				temp_58 = mad(temp_54, _FeedbackBlurContrast, 0.217637643);
				o.c0.x = temp_56;
				o.c0.y = temp_57;
				o.c0.z = temp_58;
				o.c0.w = temp_55;
				return o;
			}
			ENDHLSL
		}
	}
}