Shader "Delphis/Map/Sky" {
	Properties {
		_MainTex ("Gradient Lookup (RGB)", 2D) = "white" {}
		_UpperColor ("Upper Color (RGB)", Color) = (0.5,0.5,1,1)
		_LowerColor ("Lower Color (RGB)", Color) = (0.8,0.8,1,1)
		_StarlightTex ("Starlight Texture (RGB)", 2D) = "black" {}
		_StarlightPower ("Starlight Power", Range(0, 100)) = 0
		_FarCloudTex ("Far Cloud (RGB)", 2D) = "white" {}
		_FarCloudSpeed ("Far Cloud Speed", Float) = 0.05
		_SceneryTex ("Scenery (RGB)", 2D) = "white" {}
		_SceneryColor ("Scenery Color (RGB)", Color) = (1,1,1,1)
		[NoScaleOffset] _SceneryDepthTex ("Scenery Depth (RGB)", 2D) = "white" {}
		_NearCloudTex ("Near Cloud (RGB)", 2D) = "black" {}
		_NearCloudSpeed ("Near Cloud Speed", Float) = 0.075
		_CloudColor ("Cloud Color (RGB)", Color) = (1,1,1,1)
		_ShadowColor ("Cloud Shadow Color (RGB)", Color) = (1,1,1,1)
	}

	SubShader {
		Tags { "Queue"="Geometry" "RenderType"="Opaque" }
		Pass {
			Name "Sky"
			Tags { "LightMode"="FORWARDBASE" "RenderType"="Opaque" "SHADOWSUPPORT"="true" }
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			#pragma multi_compile DIRECTIONAL
			#pragma multi_compile _ SHADOWS_SCREEN
			float4 _Time;
			float4 _MainTex_ST;
			float4 _StarlightTex_ST;
			float4 _FarCloudTex_ST;
			float4 _NearCloudTex_ST;
			float4 _SceneryTex_ST;
			float _FarCloudSpeed;
			float _NearCloudSpeed;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _UpperColor;
			float4 _LowerColor;
			float4 _SceneryColor;
			float4 _CloudColor;
			float4 _ShadowColor;
			float _StarlightPower;
			sampler2D _MainTex;
			sampler2D _FarCloudTex;
			sampler2D _StarlightTex;
			sampler2D _SceneryDepthTex;
			sampler2D _SceneryTex;
			sampler2D _NearCloudTex;

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
			#if defined(DIRECTIONAL) && defined(SHADOWS_SCREEN)
			#define NVN_VS_ID 0
			#else
			#define NVN_VS_ID 0
			#endif
			#if NVN_VS_ID == 0
			// unique program 0: shared by 2 keyword variant(s)
			// 0000_vs.sph.bin keywords=['DIRECTIONAL']
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : TEXCOORD0;
				float4 a2 : TEXCOORD1;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a1.x;
				temp_2 = v.a0.x;
				temp_3 = v.a1.y;
				temp_4 = v.a0.z;
				temp_5 = v.a2.x;
				temp_6 = v.a2.y;
				temp_7 = temp_0 * unity_ObjectToWorld._m11;
				temp_8 = mad(temp_1, _FarCloudTex_ST.x, _FarCloudTex_ST.z);
				temp_9 = mad(temp_1, _NearCloudTex_ST.x, _NearCloudTex_ST.z);
				temp_10 = mad(temp_1, _MainTex_ST.x, _MainTex_ST.z);
				o.t0.x = temp_10;
				temp_11 = mad(temp_1, _SceneryTex_ST.x, _SceneryTex_ST.z);
				temp_12 = temp_0 * unity_ObjectToWorld._m01;
				o.t2.x = temp_11;
				temp_13 = mad(temp_2, unity_ObjectToWorld._m10, temp_7);
				temp_14 = mad(temp_3, _FarCloudTex_ST.y, _FarCloudTex_ST.w);
				temp_15 = mad(temp_3, _NearCloudTex_ST.y, _NearCloudTex_ST.w);
				temp_16 = mad(temp_3, _MainTex_ST.y, _MainTex_ST.w);
				temp_17 = mad(temp_3, _SceneryTex_ST.y, _SceneryTex_ST.w);
				o.t0.y = temp_16;
				temp_18 = temp_0 * unity_ObjectToWorld._m21;
				o.t2.y = temp_17;
				temp_19 = mad(temp_2, unity_ObjectToWorld._m00, temp_12);
				temp_20 = mad(temp_4, unity_ObjectToWorld._m12, temp_13);
				temp_21 = mad(temp_5, _StarlightTex_ST.x, _StarlightTex_ST.z);
				o.t4.x = temp_21;
				temp_22 = mad(temp_2, unity_ObjectToWorld._m20, temp_18);
				temp_23 = mad(temp_4, unity_ObjectToWorld._m02, temp_19);
				temp_24 = temp_20 + unity_ObjectToWorld._m13;
				temp_25 = temp_0 * unity_ObjectToWorld._m31;
				temp_26 = mad(temp_6, _StarlightTex_ST.y, _StarlightTex_ST.w);
				temp_27 = mad(_Time.y, _FarCloudSpeed, temp_8);
				o.t4.y = temp_26;
				temp_28 = mad(temp_4, unity_ObjectToWorld._m22, temp_22);
				o.t1.x = temp_27;
				temp_29 = temp_23 + unity_ObjectToWorld._m03;
				temp_30 = temp_24 * unity_MatrixVP._m01;
				temp_31 = mad(temp_2, unity_ObjectToWorld._m30, temp_25);
				temp_32 = mad(_Time.y, _NearCloudSpeed, temp_9);
				temp_33 = temp_28 + unity_ObjectToWorld._m23;
				o.t3.x = temp_32;
				temp_34 = mad(temp_29, unity_MatrixVP._m00, temp_30);
				temp_35 = mad(temp_4, unity_ObjectToWorld._m32, temp_31);
				temp_36 = 0.0 + temp_14;
				temp_37 = temp_24 * unity_MatrixVP._m11;
				o.t1.y = temp_36;
				temp_38 = temp_24 * unity_MatrixVP._m21;
				temp_39 = temp_24 * unity_MatrixVP._m31;
				temp_40 = mad(temp_33, unity_MatrixVP._m02, temp_34);
				temp_41 = 0.0 + temp_15;
				temp_42 = temp_35 + unity_ObjectToWorld._m33;
				o.t3.y = temp_41;
				temp_43 = mad(temp_29, unity_MatrixVP._m10, temp_37);
				temp_44 = mad(temp_29, unity_MatrixVP._m20, temp_38);
				temp_45 = mad(temp_29, unity_MatrixVP._m30, temp_39);
				temp_46 = mad(temp_42, unity_MatrixVP._m03, temp_40);
				temp_47 = mad(temp_33, unity_MatrixVP._m12, temp_43);
				o.pos.x = temp_46;
				temp_48 = mad(temp_33, unity_MatrixVP._m22, temp_44);
				temp_49 = mad(temp_33, unity_MatrixVP._m32, temp_45);
				temp_50 = mad(temp_42, unity_MatrixVP._m13, temp_47);
				temp_51 = mad(temp_42, unity_MatrixVP._m23, temp_48);
				o.pos.y = temp_50;
				temp_52 = mad(temp_42, unity_MatrixVP._m33, temp_49);
				o.pos.z = temp_51;
				o.pos.w = temp_52;
				return o;
			}
			#endif
			#if defined(DIRECTIONAL) && defined(SHADOWS_SCREEN)
			#define NVN_FS_ID 0
			#else
			#define NVN_FS_ID 0
			#endif
			#if NVN_FS_ID == 0
			// unique program 0: shared by 2 keyword variant(s)
			// 0002_fs.sph.bin keywords=['DIRECTIONAL']
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
				precise float2 temp_10;
				precise float temp_11;
				precise float temp_12;
				precise float2 temp_13;
				precise float temp_14;
				precise float temp_15;
				precise float3 temp_16;
				precise float temp_17;
				precise float temp_18;
				precise float temp_19;
				precise float temp_20;
				precise float4 temp_21;
				precise float temp_22;
				precise float temp_23;
				precise float temp_24;
				precise float temp_25;
				precise float2 temp_26;
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
				precise float temp_163;
				temp_0 = i.t0.x;
				temp_1 = i.t0.y;
				temp_2 = i.t1.x;
				temp_3 = i.t1.y;
				temp_4 = i.t4.x;
				temp_5 = i.t4.y;
				temp_6 = i.t2.x;
				temp_7 = i.t2.y;
				temp_8 = i.t3.x;
				temp_9 = i.t3.y;
				temp_10 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).xw;
				temp_11 = temp_10.x;
				temp_12 = temp_10.y;
				temp_13 = tex2D(_FarCloudTex, nvn_nan0(float2(temp_2, temp_3))).xw;
				temp_14 = temp_13.x;
				temp_15 = temp_13.y;
				temp_16 = tex2D(_StarlightTex, nvn_nan0(float2(temp_4, temp_5))).xyz;
				temp_17 = temp_16.x;
				temp_18 = temp_16.y;
				temp_19 = temp_16.z;
				temp_20 = tex2D(_SceneryDepthTex, nvn_nan0(float2(temp_6, temp_7))).x;
				temp_21 = tex2D(_SceneryTex, nvn_nan0(float2(temp_6, temp_7))).xyzw;
				temp_22 = temp_21.x;
				temp_23 = temp_21.y;
				temp_24 = temp_21.z;
				temp_25 = temp_21.w;
				temp_26 = tex2D(_NearCloudTex, nvn_nan0(float2(temp_8, temp_9))).xw;
				temp_27 = temp_26.x;
				temp_28 = temp_26.y;
				temp_29 = 0.0 - _UpperColor.y;
				temp_30 = temp_29 + _LowerColor.y;
				temp_31 = 0.0 - _ShadowColor.x;
				temp_32 = temp_31 + _CloudColor.x;
				temp_33 = 0.0 - _UpperColor.x;
				temp_34 = temp_33 + _LowerColor.x;
				temp_35 = 0.0 - _UpperColor.z;
				temp_36 = temp_35 + _LowerColor.z;
				temp_37 = 0.0 - _ShadowColor.y;
				temp_38 = temp_37 + _CloudColor.y;
				temp_39 = 0.0 - _ShadowColor.z;
				temp_40 = temp_39 + _CloudColor.z;
				temp_41 = 0.0 - _UpperColor.w;
				temp_42 = temp_41 + _LowerColor.w;
				temp_43 = temp_11 * temp_11;
				temp_44 = 0.0 - _ShadowColor.w;
				temp_45 = temp_44 + _CloudColor.w;
				temp_46 = temp_12 + -0.0;
				temp_47 = saturate(temp_46);
				temp_48 = mad(temp_14, temp_38, _ShadowColor.y);
				temp_49 = mad(temp_14, temp_32, _ShadowColor.x);
				temp_50 = min(temp_43, 1.0);
				temp_51 = mad(temp_14, temp_45, _ShadowColor.w);
				temp_52 = mad(temp_14, temp_40, _ShadowColor.z);
				temp_53 = temp_20 * 0.5;
				temp_54 = mad(temp_30, temp_50, _UpperColor.y);
				temp_55 = mad(temp_34, temp_50, _UpperColor.x);
				temp_56 = 0.0 - temp_47;
				temp_57 = temp_51 + temp_56;
				temp_58 = mad(temp_42, temp_50, _UpperColor.w);
				temp_59 = mad(temp_18, _StarlightPower, temp_54);
				temp_60 = mad(temp_36, temp_50, _UpperColor.z);
				temp_61 = mad(temp_17, _StarlightPower, temp_55);
				temp_62 = mad(temp_15, temp_57, temp_47);
				temp_63 = 0.0 - _SceneryColor.w;
				temp_64 = mad(temp_25, temp_63, temp_58);
				temp_65 = 0.0 - _SceneryColor.y;
				temp_66 = mad(temp_23, temp_65, temp_54);
				temp_67 = temp_59 + -0.0;
				temp_68 = saturate(temp_67);
				temp_69 = mad(temp_19, _StarlightPower, temp_60);
				temp_70 = temp_61 + -0.0;
				temp_71 = saturate(temp_70);
				temp_72 = 0.0 - temp_12;
				temp_73 = temp_72 + temp_62;
				temp_74 = temp_23 * _SceneryColor.y;
				temp_75 = 0.0 - _SceneryColor.z;
				temp_76 = mad(temp_24, temp_75, temp_60);
				temp_77 = 0.0 - temp_68;
				temp_78 = temp_48 + temp_77;
				temp_79 = temp_69 + -0.0;
				temp_80 = saturate(temp_79);
				temp_81 = temp_15 * _CloudColor.w;
				temp_82 = 0.0 - temp_71;
				temp_83 = temp_49 + temp_82;
				temp_84 = mad(temp_66, temp_53, temp_74);
				temp_85 = mad(temp_15, temp_78, temp_68);
				temp_86 = 0.0 - temp_80;
				temp_87 = temp_52 + temp_86;
				temp_88 = mad(temp_73, temp_81, temp_12);
				temp_89 = mad(temp_15, temp_83, temp_71);
				temp_90 = temp_25 * _SceneryColor.w;
				temp_91 = 0.0 - temp_59;
				temp_92 = temp_91 + temp_85;
				temp_93 = mad(temp_15, temp_87, temp_80);
				temp_94 = temp_22 * _SceneryColor.x;
				temp_95 = 0.0 - temp_61;
				temp_96 = temp_95 + temp_89;
				temp_97 = mad(temp_64, temp_53, temp_90);
				temp_98 = mad(temp_92, temp_81, temp_59);
				temp_99 = 0.0 - _SceneryColor.x;
				temp_100 = mad(temp_22, temp_99, temp_55);
				temp_101 = 0.0 - temp_69;
				temp_102 = temp_101 + temp_93;
				temp_103 = temp_24 * _SceneryColor.z;
				temp_104 = mad(temp_96, temp_81, temp_61);
				temp_105 = 0.0 - temp_88;
				temp_106 = temp_105 + temp_97;
				temp_107 = mad(temp_100, temp_53, temp_94);
				temp_108 = mad(temp_102, temp_81, temp_69);
				temp_109 = mad(temp_76, temp_53, temp_103);
				temp_110 = 0.0 - temp_98;
				temp_111 = temp_110 + temp_84;
				temp_112 = 0.0 - temp_104;
				temp_113 = temp_112 + temp_107;
				temp_114 = mad(temp_90, temp_106, temp_88);
				temp_115 = 0.0 - temp_108;
				temp_116 = temp_115 + temp_109;
				temp_117 = 0.0 - _ShadowColor.x;
				temp_118 = temp_117 + _CloudColor.x;
				temp_119 = 0.0 - _ShadowColor.y;
				temp_120 = temp_119 + _CloudColor.y;
				temp_121 = 0.0 - _ShadowColor.z;
				temp_122 = temp_121 + _CloudColor.z;
				temp_123 = mad(temp_90, temp_113, temp_104);
				temp_124 = mad(temp_90, temp_111, temp_98);
				temp_125 = mad(temp_90, temp_116, temp_108);
				temp_126 = mad(temp_118, temp_27, _ShadowColor.x);
				temp_127 = mad(temp_120, temp_27, _ShadowColor.y);
				temp_128 = mad(temp_122, temp_27, _ShadowColor.z);
				temp_129 = mad(temp_45, temp_27, _ShadowColor.w);
				temp_130 = temp_114 + -0.0;
				temp_131 = saturate(temp_130);
				temp_132 = temp_123 + -0.0;
				temp_133 = saturate(temp_132);
				temp_134 = temp_124 + -0.0;
				temp_135 = saturate(temp_134);
				temp_136 = temp_125 + -0.0;
				temp_137 = saturate(temp_136);
				temp_138 = temp_58 * temp_28;
				temp_139 = 0.0 - temp_131;
				temp_140 = temp_139 + temp_129;
				temp_141 = 0.0 - temp_133;
				temp_142 = temp_141 + temp_126;
				temp_143 = 0.0 - temp_135;
				temp_144 = temp_143 + temp_127;
				temp_145 = 0.0 - temp_137;
				temp_146 = temp_145 + temp_128;
				temp_147 = mad(temp_140, temp_138, temp_131);
				temp_148 = mad(temp_142, temp_138, temp_133);
				temp_149 = mad(temp_144, temp_138, temp_135);
				temp_150 = mad(temp_146, temp_138, temp_137);
				temp_151 = temp_58 * _CloudColor.w;
				temp_152 = 0.0 - temp_114;
				temp_153 = temp_152 + temp_147;
				temp_154 = 0.0 - temp_123;
				temp_155 = temp_154 + temp_148;
				temp_156 = 0.0 - temp_124;
				temp_157 = temp_156 + temp_149;
				temp_158 = 0.0 - temp_125;
				temp_159 = temp_158 + temp_150;
				temp_160 = mad(temp_153, temp_151, temp_114);
				temp_161 = mad(temp_155, temp_151, temp_123);
				temp_162 = mad(temp_157, temp_151, temp_124);
				temp_163 = mad(temp_159, temp_151, temp_125);
				o.c0.x = temp_161;
				o.c0.y = temp_162;
				o.c0.z = temp_163;
				o.c0.w = temp_160;
				return o;
			}
			#endif
			ENDHLSL
		}
		Pass {
			Name "ShadowCaster"
			Tags { "LightMode"="SHADOWCASTER" "RenderType"="Opaque" "SHADOWSUPPORT"="true" }
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			#pragma multi_compile SHADOWS_CUBE SHADOWS_DEPTH
			float4 _WorldSpaceLightPos0;
			float4 unity_LightShadowBias;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_WorldToObject;
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
			#if defined(SHADOWS_DEPTH)
			// 0004_vs.sph.bin keywords=['SHADOWS_DEPTH']
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : NORMAL;
			};
			struct v2f {
				float4 pos : SV_POSITION;
			};
			v2f vert(appdata v) {
				v2f o = (v2f)0;
				precise float temp_0;
				bool temp_1;
				bool temp_2;
				bool temp_3;
				bool temp_4;
				bool temp_5;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = 0.0 != unity_LightShadowBias.z;
				temp_2 = isnan(0.0);
				temp_3 = temp_1 || temp_2;
				temp_4 = isnan(unity_LightShadowBias.z);
				temp_5 = temp_3 || temp_4;
				temp_6 = v.a0.x;
				temp_7 = v.a1.x;
				temp_8 = v.a0.z;
				temp_9 = v.a1.y;
				temp_10 = v.a0.w;
				temp_11 = v.a1.z;
				temp_12 = temp_0 * unity_ObjectToWorld._m01;
				temp_13 = temp_0 * unity_ObjectToWorld._m11;
				temp_14 = mad(temp_6, unity_ObjectToWorld._m00, temp_12);
				temp_15 = temp_0 * unity_ObjectToWorld._m21;
				temp_16 = temp_7 * unity_WorldToObject._m00;
				temp_17 = mad(temp_6, unity_ObjectToWorld._m10, temp_13);
				temp_18 = temp_7 * unity_WorldToObject._m01;
				temp_19 = temp_0 * unity_ObjectToWorld._m31;
				temp_20 = mad(temp_8, unity_ObjectToWorld._m02, temp_14);
				temp_21 = mad(temp_6, unity_ObjectToWorld._m20, temp_15);
				temp_22 = mad(temp_9, unity_WorldToObject._m10, temp_16);
				temp_23 = mad(temp_8, unity_ObjectToWorld._m12, temp_17);
				temp_24 = temp_7 * unity_WorldToObject._m02;
				temp_25 = mad(temp_9, unity_WorldToObject._m11, temp_18);
				temp_26 = mad(temp_10, unity_ObjectToWorld._m03, temp_20);
				temp_27 = mad(temp_8, unity_ObjectToWorld._m22, temp_21);
				temp_28 = mad(temp_6, unity_ObjectToWorld._m30, temp_19);
				temp_29 = mad(temp_10, unity_ObjectToWorld._m13, temp_23);
				temp_30 = mad(temp_11, unity_WorldToObject._m20, temp_22);
				temp_31 = mad(temp_9, unity_WorldToObject._m12, temp_24);
				temp_32 = 0.0 - _WorldSpaceLightPos0.w;
				temp_33 = mad(temp_26, temp_32, _WorldSpaceLightPos0.x);
				temp_34 = mad(temp_11, unity_WorldToObject._m21, temp_25);
				temp_35 = mad(temp_10, unity_ObjectToWorld._m23, temp_27);
				temp_36 = 0.0 - _WorldSpaceLightPos0.w;
				temp_37 = mad(temp_29, temp_36, _WorldSpaceLightPos0.y);
				temp_38 = temp_30 * temp_30;
				temp_39 = mad(temp_11, unity_WorldToObject._m22, temp_31);
				temp_40 = temp_33 * temp_33;
				temp_41 = mad(temp_8, unity_ObjectToWorld._m32, temp_28);
				temp_42 = 0.0 - _WorldSpaceLightPos0.w;
				temp_43 = mad(temp_35, temp_42, _WorldSpaceLightPos0.z);
				temp_44 = mad(temp_34, temp_34, temp_38);
				temp_45 = mad(temp_37, temp_37, temp_40);
				temp_46 = mad(temp_10, unity_ObjectToWorld._m33, temp_41);
				temp_47 = mad(temp_39, temp_39, temp_44);
				temp_48 = mad(temp_43, temp_43, temp_45);
				temp_49 = rsqrt(temp_47);
				temp_50 = rsqrt(temp_48);
				temp_51 = temp_30 * temp_49;
				temp_52 = temp_34 * temp_49;
				temp_53 = temp_33 * temp_50;
				temp_54 = temp_37 * temp_50;
				temp_55 = temp_43 * temp_50;
				temp_56 = temp_53 * temp_51;
				temp_57 = temp_39 * temp_49;
				temp_58 = mad(temp_54, temp_52, temp_56);
				temp_59 = mad(temp_55, temp_57, temp_58);
				temp_60 = 0.0 - temp_59;
				temp_61 = mad(temp_59, temp_60, 1.0);
				temp_62 = sqrt(temp_61);
				temp_63 = temp_62 * unity_LightShadowBias.z;
				temp_64 = temp_29;
				temp_65 = temp_26;
				temp_66 = temp_35;
				[branch] if (temp_5)
				{
				temp_67 = 0.0 - temp_63;
				temp_68 = mad(temp_52, temp_67, temp_29);
				temp_64 = temp_68;
				}
				temp_69 = temp_64;
				[branch] if (temp_5)
				{
				temp_70 = 0.0 - temp_63;
				temp_71 = mad(temp_51, temp_70, temp_26);
				temp_65 = temp_71;
				}
				temp_72 = temp_65;
				[branch] if (temp_5)
				{
				temp_73 = 0.0 - temp_63;
				temp_74 = mad(temp_57, temp_73, temp_35);
				temp_66 = temp_74;
				}
				temp_75 = temp_66;
				temp_76 = temp_69 * unity_MatrixVP._m31;
				temp_77 = mad(temp_72, unity_MatrixVP._m30, temp_76);
				temp_78 = mad(temp_75, unity_MatrixVP._m32, temp_77);
				temp_79 = mad(temp_46, unity_MatrixVP._m33, temp_78);
				temp_80 = temp_69 * unity_MatrixVP._m21;
				temp_81 = 1.0 / temp_79;
				temp_82 = mad(temp_72, unity_MatrixVP._m20, temp_80);
				o.pos.w = temp_79;
				temp_83 = mad(temp_75, unity_MatrixVP._m22, temp_82);
				temp_84 = temp_81 * unity_LightShadowBias.x;
				temp_85 = temp_69 * unity_MatrixVP._m01;
				temp_86 = temp_69 * unity_MatrixVP._m11;
				temp_87 = mad(temp_46, unity_MatrixVP._m23, temp_83);
				temp_88 = min(0.0, temp_84);
				temp_89 = mad(temp_72, unity_MatrixVP._m00, temp_85);
				temp_90 = mad(temp_72, unity_MatrixVP._m10, temp_86);
				temp_91 = max(temp_88, -1.0);
				temp_92 = mad(temp_75, unity_MatrixVP._m02, temp_89);
				temp_93 = mad(temp_75, unity_MatrixVP._m12, temp_90);
				temp_94 = temp_87 + temp_91;
				temp_95 = mad(temp_46, unity_MatrixVP._m03, temp_92);
				temp_96 = mad(temp_46, unity_MatrixVP._m13, temp_93);
				o.pos.x = temp_95;
				temp_97 = min(temp_79, temp_94);
				o.pos.y = temp_96;
				temp_98 = 0.0 - temp_94;
				temp_99 = temp_98 + temp_97;
				temp_100 = mad(temp_99, unity_LightShadowBias.y, temp_94);
				o.pos.z = temp_100;
				return o;
			}
			#else
			// 0005_vs.sph.bin keywords=['SHADOWS_CUBE']
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : NORMAL;
			};
			struct v2f {
				float4 pos : SV_POSITION;
			};
			v2f vert(appdata v) {
				v2f o = (v2f)0;
				precise float temp_0;
				bool temp_1;
				bool temp_2;
				bool temp_3;
				bool temp_4;
				bool temp_5;
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
				precise float temp_87;
				precise float temp_88;
				precise float temp_89;
				precise float temp_90;
				precise float temp_91;
				precise float temp_92;
				precise float temp_93;
				precise float temp_94;
				precise float temp_95;
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = 0.0 != unity_LightShadowBias.z;
				temp_2 = isnan(0.0);
				temp_3 = temp_1 || temp_2;
				temp_4 = isnan(unity_LightShadowBias.z);
				temp_5 = temp_3 || temp_4;
				temp_6 = v.a0.x;
				temp_7 = v.a1.x;
				temp_8 = v.a0.z;
				temp_9 = v.a1.y;
				temp_10 = v.a0.w;
				temp_11 = v.a1.z;
				temp_12 = temp_0 * unity_ObjectToWorld._m01;
				temp_13 = temp_0 * unity_ObjectToWorld._m11;
				temp_14 = mad(temp_6, unity_ObjectToWorld._m00, temp_12);
				temp_15 = temp_0 * unity_ObjectToWorld._m21;
				temp_16 = temp_7 * unity_WorldToObject._m00;
				temp_17 = mad(temp_6, unity_ObjectToWorld._m10, temp_13);
				temp_18 = temp_7 * unity_WorldToObject._m01;
				temp_19 = temp_0 * unity_ObjectToWorld._m31;
				temp_20 = mad(temp_8, unity_ObjectToWorld._m02, temp_14);
				temp_21 = mad(temp_6, unity_ObjectToWorld._m20, temp_15);
				temp_22 = mad(temp_9, unity_WorldToObject._m10, temp_16);
				temp_23 = mad(temp_8, unity_ObjectToWorld._m12, temp_17);
				temp_24 = temp_7 * unity_WorldToObject._m02;
				temp_25 = mad(temp_9, unity_WorldToObject._m11, temp_18);
				temp_26 = mad(temp_10, unity_ObjectToWorld._m03, temp_20);
				temp_27 = mad(temp_8, unity_ObjectToWorld._m22, temp_21);
				temp_28 = mad(temp_6, unity_ObjectToWorld._m30, temp_19);
				temp_29 = mad(temp_10, unity_ObjectToWorld._m13, temp_23);
				temp_30 = mad(temp_11, unity_WorldToObject._m20, temp_22);
				temp_31 = mad(temp_9, unity_WorldToObject._m12, temp_24);
				temp_32 = 0.0 - _WorldSpaceLightPos0.w;
				temp_33 = mad(temp_26, temp_32, _WorldSpaceLightPos0.x);
				temp_34 = mad(temp_11, unity_WorldToObject._m21, temp_25);
				temp_35 = mad(temp_10, unity_ObjectToWorld._m23, temp_27);
				temp_36 = 0.0 - _WorldSpaceLightPos0.w;
				temp_37 = mad(temp_29, temp_36, _WorldSpaceLightPos0.y);
				temp_38 = temp_30 * temp_30;
				temp_39 = mad(temp_11, unity_WorldToObject._m22, temp_31);
				temp_40 = temp_33 * temp_33;
				temp_41 = mad(temp_8, unity_ObjectToWorld._m32, temp_28);
				temp_42 = 0.0 - _WorldSpaceLightPos0.w;
				temp_43 = mad(temp_35, temp_42, _WorldSpaceLightPos0.z);
				temp_44 = mad(temp_34, temp_34, temp_38);
				temp_45 = mad(temp_37, temp_37, temp_40);
				temp_46 = mad(temp_10, unity_ObjectToWorld._m33, temp_41);
				temp_47 = mad(temp_39, temp_39, temp_44);
				temp_48 = mad(temp_43, temp_43, temp_45);
				temp_49 = rsqrt(temp_47);
				temp_50 = rsqrt(temp_48);
				temp_51 = temp_30 * temp_49;
				temp_52 = temp_34 * temp_49;
				temp_53 = temp_33 * temp_50;
				temp_54 = temp_37 * temp_50;
				temp_55 = temp_43 * temp_50;
				temp_56 = temp_53 * temp_51;
				temp_57 = temp_39 * temp_49;
				temp_58 = mad(temp_54, temp_52, temp_56);
				temp_59 = mad(temp_55, temp_57, temp_58);
				temp_60 = 0.0 - temp_59;
				temp_61 = mad(temp_59, temp_60, 1.0);
				temp_62 = sqrt(temp_61);
				temp_63 = temp_62 * unity_LightShadowBias.z;
				temp_64 = temp_29;
				temp_65 = temp_26;
				temp_66 = temp_35;
				[branch] if (temp_5)
				{
				temp_67 = 0.0 - temp_63;
				temp_68 = mad(temp_52, temp_67, temp_29);
				temp_64 = temp_68;
				}
				temp_69 = temp_64;
				[branch] if (temp_5)
				{
				temp_70 = 0.0 - temp_63;
				temp_71 = mad(temp_51, temp_70, temp_26);
				temp_65 = temp_71;
				}
				temp_72 = temp_65;
				[branch] if (temp_5)
				{
				temp_73 = 0.0 - temp_63;
				temp_74 = mad(temp_57, temp_73, temp_35);
				temp_66 = temp_74;
				}
				temp_75 = temp_66;
				temp_76 = temp_69 * unity_MatrixVP._m21;
				temp_77 = temp_69 * unity_MatrixVP._m31;
				temp_78 = temp_69 * unity_MatrixVP._m01;
				temp_79 = temp_69 * unity_MatrixVP._m11;
				temp_80 = mad(temp_72, unity_MatrixVP._m20, temp_76);
				temp_81 = mad(temp_72, unity_MatrixVP._m30, temp_77);
				temp_82 = mad(temp_72, unity_MatrixVP._m00, temp_78);
				temp_83 = mad(temp_72, unity_MatrixVP._m10, temp_79);
				temp_84 = mad(temp_75, unity_MatrixVP._m22, temp_80);
				temp_85 = mad(temp_75, unity_MatrixVP._m32, temp_81);
				temp_86 = mad(temp_75, unity_MatrixVP._m02, temp_82);
				temp_87 = mad(temp_75, unity_MatrixVP._m12, temp_83);
				temp_88 = mad(temp_46, unity_MatrixVP._m23, temp_84);
				temp_89 = mad(temp_46, unity_MatrixVP._m33, temp_85);
				temp_90 = mad(temp_46, unity_MatrixVP._m03, temp_86);
				o.pos.w = temp_89;
				temp_91 = mad(temp_46, unity_MatrixVP._m13, temp_87);
				o.pos.x = temp_90;
				temp_92 = min(temp_88, temp_89);
				o.pos.y = temp_91;
				temp_93 = 0.0 - temp_88;
				temp_94 = temp_93 + temp_92;
				temp_95 = mad(temp_94, unity_LightShadowBias.y, temp_88);
				o.pos.z = temp_95;
				return o;
			}
			#endif
			#if defined(SHADOWS_DEPTH)
			#define NVN_FS_ID 0
			#else
			#define NVN_FS_ID 0
			#endif
			#if NVN_FS_ID == 0
			// unique program 0: shared by 2 keyword variant(s)
			// 0006_fs.sph.bin keywords=['SHADOWS_DEPTH']
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
			#endif
			ENDHLSL
		}
	}
}