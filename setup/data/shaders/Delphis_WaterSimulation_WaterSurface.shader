Shader "Delphis/WaterSimulation/WaterSurface" {
	Properties {
		_MainTex ("Main Texture", 2D) = "black" {}
		_DynamicNormalMap ("Dynamic Normal Map", 2D) = "bump" {}
		_Reflectivity ("Reflectivity", Range(0, 1)) = 0.03
		_FresnelPower ("Fresnel Power", Range(0.1, 8)) = 5
		_WaterSurfaceColor ("WaterSurfaceColor", Color) = (0.2,0.4,1,1)
		_WaterUpScale ("Water UpVector Scale", Range(0.1, 4)) = 0.25
		_WaterAttenuation ("Water Attenuation", Range(0.1, 8)) = 0.5
		_WaterMaxDensity ("Water Max Density", Range(0.1, 1)) = 0.95
		_SpecularColorIntensity ("Specular Color Intensity", Range(0, 1024)) = 256
		_SpecularCosinePower ("Specular Cosine Power", Range(0.01, 1024)) = 64
		[HideInInspector] _MirrorMap ("Mirror Map", 2D) = "black" {}
	}

	SubShader {
		Tags { "Queue"="Transparent" "RenderType"="Opaque" }
		GrabPass { "_BackgroundTexture" }
		Pass {
			Tags { "LightMode"="FORWARDBASE" "RenderType"="Opaque" }
			ZWrite Off
			Stencil {
				Ref 2
				Comp NotEqual
				Pass Keep
				Fail Keep
				ZFail Keep
			}
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			float3 _WorldSpaceCameraPos;
			float4 _ProjectionParams;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixV;
			float4x4 unity_MatrixVP;
			float4 _ZBufferParams;
			float4 _WorldSpaceLightPos0;
			float4 _LightColor0;
			float4 _WaterSurfaceColor;
			float _SpecularColorIntensity;
			float _SpecularCosinePower;
			float _WaterUpScale;
			float _WaterAttenuation;
			float _WaterMaxDensity;
			float _Reflectivity;
			float _FresnelPower;
			float4 _ReflectorColor;
			sampler2D _DynamicNormalMap;
			sampler2D _OpaqueDepthTexture;
			sampler2D _BackgroundTexture;
			sampler2D _MirrorMap;

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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a0.x;
				temp_2 = v.a0.z;
				temp_3 = v.a0.w;
				temp_4 = v.a1.y;
				temp_5 = v.a1.x;
				temp_6 = temp_0 * unity_ObjectToWorld._m11;
				temp_7 = temp_0 * unity_ObjectToWorld._m01;
				temp_8 = mad(temp_1, unity_ObjectToWorld._m10, temp_6);
				temp_9 = mad(temp_1, unity_ObjectToWorld._m00, temp_7);
				temp_10 = temp_0 * unity_ObjectToWorld._m21;
				o.t0.y = temp_4;
				temp_11 = mad(temp_2, unity_ObjectToWorld._m12, temp_8);
				o.t0.x = temp_5;
				temp_12 = mad(temp_2, unity_ObjectToWorld._m02, temp_9);
				temp_13 = mad(temp_1, unity_ObjectToWorld._m20, temp_10);
				temp_14 = temp_11 + unity_ObjectToWorld._m13;
				temp_15 = mad(temp_3, unity_ObjectToWorld._m13, temp_11);
				temp_16 = temp_0 * unity_ObjectToWorld._m31;
				temp_17 = temp_12 + unity_ObjectToWorld._m03;
				temp_18 = mad(temp_3, unity_ObjectToWorld._m03, temp_12);
				temp_19 = mad(temp_2, unity_ObjectToWorld._m22, temp_13);
				temp_20 = temp_14 * unity_MatrixV._m21;
				temp_21 = temp_15 * unity_MatrixVP._m21;
				temp_22 = mad(temp_1, unity_ObjectToWorld._m30, temp_16);
				temp_23 = 0.0 - _WorldSpaceCameraPos.y;
				temp_24 = temp_15 + temp_23;
				temp_25 = 0.0 - _WorldSpaceCameraPos.x;
				temp_26 = temp_18 + temp_25;
				temp_27 = mad(temp_3, unity_ObjectToWorld._m23, temp_19);
				temp_28 = mad(temp_17, unity_MatrixV._m20, temp_20);
				temp_29 = temp_19 + unity_ObjectToWorld._m23;
				temp_30 = mad(temp_2, unity_ObjectToWorld._m32, temp_22);
				temp_31 = temp_15 * unity_MatrixVP._m01;
				temp_32 = temp_26 * temp_26;
				temp_33 = 0.0 - _WorldSpaceCameraPos.z;
				temp_34 = temp_27 + temp_33;
				temp_35 = mad(temp_18, unity_MatrixVP._m20, temp_21);
				temp_36 = mad(temp_29, unity_MatrixV._m22, temp_28);
				temp_37 = temp_30 + unity_ObjectToWorld._m33;
				temp_38 = temp_15 * unity_MatrixVP._m31;
				temp_39 = temp_15 * unity_MatrixVP._m11;
				temp_40 = mad(temp_24, temp_24, temp_32);
				temp_41 = mad(temp_18, unity_MatrixVP._m00, temp_31);
				temp_42 = mad(temp_27, unity_MatrixVP._m22, temp_35);
				temp_43 = mad(temp_37, unity_MatrixV._m23, temp_36);
				temp_44 = mad(temp_18, unity_MatrixVP._m30, temp_38);
				temp_45 = mad(temp_18, unity_MatrixVP._m10, temp_39);
				temp_46 = mad(temp_34, temp_34, temp_40);
				temp_47 = mad(temp_27, unity_MatrixVP._m02, temp_41);
				temp_48 = rsqrt(temp_46);
				temp_49 = temp_42 + unity_MatrixVP._m23;
				temp_50 = mad(temp_27, unity_MatrixVP._m32, temp_44);
				o.t3.z = temp_49;
				temp_51 = mad(temp_27, unity_MatrixVP._m12, temp_45);
				o.pos.z = temp_49;
				temp_52 = temp_47 + unity_MatrixVP._m03;
				o.t2.z = temp_49;
				temp_53 = temp_50 + unity_MatrixVP._m33;
				o.pos.x = temp_52;
				temp_54 = temp_51 + unity_MatrixVP._m13;
				o.t3.w = temp_53;
				temp_55 = temp_26 * temp_48;
				o.t4.w = temp_53;
				temp_56 = temp_53 * 0.5;
				o.t1.x = temp_55;
				temp_57 = temp_54 * _ProjectionParams.x;
				o.pos.w = temp_53;
				temp_58 = 0.0 - temp_43;
				temp_59 = temp_58 + -0.0;
				o.t2.w = temp_53;
				temp_60 = temp_24 * temp_48;
				o.t4.z = temp_59;
				temp_61 = mad(temp_52, 0.5, temp_56);
				o.t1.y = temp_60;
				temp_62 = mad(temp_57, 0.5, temp_56);
				o.t3.x = temp_61;
				temp_63 = temp_34 * temp_48;
				o.t4.x = temp_61;
				temp_64 = mad(temp_54, -0.5, temp_56);
				o.t1.z = temp_63;
				o.t2.x = temp_61;
				o.pos.y = temp_54;
				o.t3.y = temp_62;
				o.t4.y = temp_62;
				o.t2.y = temp_64;
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
				precise float2 temp_5;
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
				precise float temp_84;
				precise float temp_85;
				precise float temp_86;
				bool temp_87;
				precise float temp_88;
				precise float temp_89;
				precise float temp_90;
				precise float temp_91;
				precise float temp_92;
				precise float3 temp_93;
				precise float temp_94;
				precise float temp_95;
				precise float temp_96;
				precise float3 temp_97;
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
				precise float temp_112;
				precise float temp_113;
				precise float temp_114;
				precise float temp_115;
				precise float temp_116;
				precise float temp_117;
				precise float temp_118;
				precise float temp_119;
				precise float temp_120;
				precise float temp_121;
				precise float temp_122;
				precise float temp_123;
				precise float temp_124;
				precise float temp_125;
				precise float temp_126;
				precise float temp_127;
				precise float temp_128;
				precise float temp_129;
				precise float temp_130;
				precise float temp_131;
				precise float temp_132;
				precise float temp_133;
				precise float temp_134;
				precise float temp_135;
				precise float temp_136;
				precise float temp_137;
				precise float temp_138;
				precise float temp_139;
				precise float temp_140;
				precise float temp_141;
				precise float temp_142;
				precise float temp_143;
				precise float temp_144;
				precise float temp_145;
				precise float temp_146;
				precise float temp_147;
				precise float temp_148;
				precise float temp_149;
				precise float temp_150;
				precise float temp_151;
				precise float temp_152;
				precise float temp_153;
				precise float temp_154;
				precise float temp_155;
				precise float temp_156;
				precise float temp_157;
				precise float temp_158;
				precise float temp_159;
				precise float temp_160;
				precise float temp_161;
				precise float temp_162;
				temp_0 = i.t0.x;
				temp_1 = i.t0.y;
				temp_2 = i.t4.w;
				temp_3 = i.t4.x;
				temp_4 = i.t4.y;
				temp_5 = tex2D(_DynamicNormalMap, nvn_nan0(float2(temp_0, temp_1))).xy;
				temp_6 = temp_5.x;
				temp_7 = temp_5.y;
				temp_8 = 1.0 / temp_2;
				temp_9 = temp_3 * temp_8;
				temp_10 = temp_4 * temp_8;
				temp_11 = tex2D(_OpaqueDepthTexture, nvn_nan0(float2(temp_9, temp_10))).x;
				temp_12 = i.t1.x;
				temp_13 = i.t1.y;
				temp_14 = i.t3.z;
				temp_15 = i.t2.z;
				temp_16 = i.t1.z;
				temp_17 = _WorldSpaceLightPos0.x * _WorldSpaceLightPos0.x;
				temp_18 = mad(temp_6, 2.0, -1.0);
				temp_19 = mad(temp_7, 2.0, -1.0);
				temp_20 = temp_18 * temp_18;
				temp_21 = mad(_WaterUpScale, _WaterUpScale, temp_20);
				temp_22 = mad(temp_19, temp_19, temp_21);
				temp_23 = rsqrt(temp_22);
				temp_24 = temp_18 * temp_23;
				temp_25 = temp_19 * temp_23;
				temp_26 = temp_23 * _WaterUpScale;
				temp_27 = mad(_WorldSpaceLightPos0.y, _WorldSpaceLightPos0.y, temp_17);
				temp_28 = i.t4.z;
				temp_29 = 0.0 - temp_12;
				temp_30 = temp_24 * temp_29;
				temp_31 = temp_24 * 10.0;
				temp_32 = temp_25 * 10.0;
				temp_33 = mad(_WorldSpaceLightPos0.z, _WorldSpaceLightPos0.z, temp_27);
				temp_34 = rsqrt(temp_33);
				temp_35 = 0.0 - temp_13;
				temp_36 = mad(temp_26, temp_35, temp_30);
				temp_37 = temp_31 * temp_14;
				temp_38 = 0.0 - temp_15;
				temp_39 = temp_31 * temp_38;
				temp_40 = temp_32 * temp_14;
				temp_41 = 0.0 - temp_15;
				temp_42 = temp_32 * temp_41;
				temp_43 = 0.0 - temp_16;
				temp_44 = mad(temp_25, temp_43, temp_36);
				temp_45 = mad(temp_11, _ZBufferParams.z, _ZBufferParams.w);
				temp_46 = temp_26 * temp_44;
				temp_47 = 1.0 / temp_45;
				temp_48 = temp_24 * temp_44;
				temp_49 = mad(temp_46, 2.0, temp_13);
				temp_50 = temp_25 * temp_44;
				temp_51 = mad(temp_48, 2.0, temp_12);
				temp_52 = temp_34 * _WorldSpaceLightPos0.x;
				temp_53 = mad(temp_50, 2.0, temp_16);
				temp_54 = 0.0 - temp_28;
				temp_55 = temp_47 + temp_54;
				temp_56 = saturate(temp_55);
				temp_57 = temp_52 * temp_51;
				temp_58 = temp_34 * _WorldSpaceLightPos0.y;
				temp_59 = temp_56 * 0.5;
				temp_60 = mad(temp_58, temp_49, temp_57);
				temp_61 = temp_59 * temp_39;
				temp_62 = temp_59 * temp_42;
				temp_63 = mad(temp_61, 2.0, temp_3);
				temp_64 = mad(temp_62, 2.0, temp_4);
				temp_65 = temp_63 * temp_8;
				temp_66 = temp_64 * temp_8;
				temp_67 = tex2D(_OpaqueDepthTexture, nvn_nan0(float2(temp_65, temp_66))).x;
				temp_68 = i.t3.w;
				temp_69 = i.t3.x;
				temp_70 = i.t3.y;
				temp_71 = temp_37 * temp_59;
				temp_72 = temp_40 * temp_59;
				temp_73 = temp_61 * 2.0;
				temp_74 = temp_62 * 2.0;
				temp_75 = 1.0 / temp_68;
				temp_76 = mad(temp_71, 2.0, temp_69);
				temp_77 = mad(temp_72, 2.0, temp_70);
				temp_78 = i.t2.w;
				temp_79 = temp_76 * temp_75;
				temp_80 = temp_77 * temp_75;
				temp_81 = i.t2.x;
				temp_82 = 1.0 / temp_78;
				temp_83 = mad(temp_67, _ZBufferParams.z, _ZBufferParams.w);
				temp_84 = i.t2.y;
				temp_85 = temp_34 * _WorldSpaceLightPos0.z;
				temp_86 = 1.0 / temp_83;
				temp_87 = temp_86 >= temp_28;
				temp_88 = temp_87 ? 1.0 : 0.0;
				temp_89 = mad(temp_73, temp_88, temp_81);
				temp_90 = temp_89 * temp_82;
				temp_91 = mad(temp_74, temp_88, temp_84);
				temp_92 = temp_91 * temp_82;
				temp_93 = tex2D(_BackgroundTexture, nvn_nan0(float2(temp_90, temp_92))).xyz;
				temp_94 = temp_93.x;
				temp_95 = temp_93.y;
				temp_96 = temp_93.z;
				temp_97 = tex2D(_MirrorMap, nvn_nan0(float2(temp_79, temp_80))).xyz;
				temp_98 = temp_97.x;
				temp_99 = temp_97.y;
				temp_100 = temp_97.z;
				temp_101 = mad(temp_85, temp_53, temp_60);
				temp_102 = saturate(temp_101);
				temp_103 = 0.0 - temp_44;
				temp_104 = temp_103 + 1.0;
				temp_105 = saturate(temp_104);
				temp_106 = log2(temp_102);
				temp_107 = temp_24 * temp_52;
				temp_108 = log2(temp_105);
				temp_109 = temp_56 * 4.0;
				temp_110 = mad(temp_26, temp_58, temp_107);
				temp_111 = 1.0 / _WaterAttenuation;
				temp_112 = temp_106 * _SpecularCosinePower;
				temp_113 = mad(temp_25, temp_85, temp_110);
				temp_114 = temp_108 * _FresnelPower;
				temp_115 = exp2(temp_112);
				temp_116 = mad(temp_113, 0.5, 0.5);
				temp_117 = saturate(temp_116);
				temp_118 = exp2(temp_114);
				temp_119 = temp_59 * temp_111;
				temp_120 = saturate(temp_119);
				temp_121 = 0.0 - _ReflectorColor.y;
				temp_122 = _LightColor0.y + temp_121;
				temp_123 = 0.0 - _ReflectorColor.z;
				temp_124 = _LightColor0.z + temp_123;
				temp_125 = 0.0 - _ReflectorColor.x;
				temp_126 = _LightColor0.x + temp_125;
				temp_127 = min(temp_109, 1.0);
				temp_128 = temp_120 * _WaterMaxDensity;
				temp_129 = temp_115 * 8.0;
				temp_130 = 0.0 - _Reflectivity;
				temp_131 = mad(temp_118, temp_130, temp_118);
				temp_132 = mad(temp_117, temp_126, _ReflectorColor.x);
				temp_133 = mad(temp_117, temp_122, _ReflectorColor.y);
				temp_134 = mad(temp_117, temp_124, _ReflectorColor.z);
				temp_135 = min(temp_129, 1.0);
				temp_136 = temp_131 + _Reflectivity;
				temp_137 = temp_135 * _SpecularColorIntensity;
				temp_138 = temp_136 * temp_127;
				temp_139 = 0.0 - temp_94;
				temp_140 = mad(temp_132, _WaterSurfaceColor.x, temp_139);
				temp_141 = 0.0 - temp_95;
				temp_142 = mad(temp_133, _WaterSurfaceColor.y, temp_141);
				temp_143 = mad(temp_137, temp_132, temp_98);
				temp_144 = mad(temp_137, temp_133, temp_99);
				temp_145 = 0.0 - temp_96;
				temp_146 = mad(temp_134, _WaterSurfaceColor.z, temp_145);
				temp_147 = mad(temp_137, temp_134, temp_100);
				temp_148 = mad(temp_140, temp_128, temp_94);
				temp_149 = mad(temp_142, temp_128, temp_95);
				temp_150 = min(temp_143, 1.5);
				temp_151 = mad(temp_146, temp_128, temp_96);
				temp_152 = min(temp_144, 1.5);
				temp_153 = min(temp_147, 1.5);
				temp_154 = 0.0 - temp_148;
				temp_155 = temp_154 + temp_150;
				temp_156 = 0.0 - temp_149;
				temp_157 = temp_156 + temp_152;
				temp_158 = 0.0 - temp_151;
				temp_159 = temp_158 + temp_153;
				temp_160 = mad(temp_155, temp_138, temp_148);
				temp_161 = mad(temp_157, temp_138, temp_149);
				temp_162 = mad(temp_159, temp_138, temp_151);
				o.c0.x = temp_160;
				o.c0.y = temp_161;
				o.c0.z = temp_162;
				o.c0.w = 1.0;
				return o;
			}
			ENDHLSL
		}
	}
}