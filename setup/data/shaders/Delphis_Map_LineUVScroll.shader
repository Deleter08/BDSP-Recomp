Shader "Delphis/Map/LineUVScroll" {
	Properties {
		[NoScaleOffset] _MainTex ("Main Tex", 2D) = "white" {}
		_PatternH ("Horizontal Pattern", Float) = 1
		_PatternV ("Vertical Pattern", Float) = 1
		_StartFrameIndex ("Start Frame Index", Float) = 0
		_SwitchingTime ("Switching Time", Float) = 15
		[MaterialToggle] _AutoSwitch ("AutoSwitch", Float) = 1
		_BlinkCycle ("Blink Cycle", Float) = 1.5
		_BlinkWidth ("Blink Width", Range(0.1, 1)) = 0.1
		_ScrollSpeed ("Scroll Speed", Float) = 0.1
		_LineAlpha ("Line Alpha", Float) = 0.1
		[MaterialToggle] _Transparent ("Transparent", Float) = 0
	}

	SubShader {
		Tags { "Queue"="Transparent" "RenderType"="Opaque" }
		Pass {
			Tags { "LightMode"="FORWARDBASE" "RenderType"="Opaque" }
			Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			float4 _Time;
			int _PatternH;
			int _PatternV;
			float _StartFrameIndex;
			float _BlinkCycle;
			float _BlinkWidth;
			float _SwitchingTime;
			float _AutoSwitch;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float _ScrollSpeed;
			float _LineAlpha;
			float _Transparent;
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
			};
			v2f vert(appdata v) {
				v2f o = (v2f)0;
				precise float temp_0;
				precise float temp_1;
				int temp_2;
				int temp_3;
				int temp_4;
				precise float temp_5;
				int temp_6;
				uint temp_7;
				int temp_8;
				int temp_9;
				int temp_10;
				int temp_11;
				uint temp_12;
				uint temp_13;
				int temp_14;
				int temp_15;
				int temp_16;
				int temp_17;
				int temp_18;
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
				bool temp_33;
				precise float temp_34;
				precise float temp_35;
				precise float temp_36;
				precise float temp_37;
				precise float temp_38;
				precise float temp_39;
				int temp_40;
				precise float temp_41;
				int temp_42;
				bool temp_43;
				bool temp_44;
				precise float temp_45;
				precise float temp_46;
				precise float temp_47;
				precise float temp_48;
				precise float temp_49;
				int temp_50;
				bool temp_51;
				bool temp_52;
				precise float temp_53;
				int temp_54;
				int temp_55;
				int temp_56;
				bool temp_57;
				int temp_58;
				precise float temp_59;
				int temp_60;
				bool temp_61;
				precise float temp_62;
				int temp_63;
				precise float temp_64;
				precise float temp_65;
				precise float temp_66;
				uint temp_67;
				int temp_68;
				int temp_69;
				int temp_70;
				precise float temp_71;
				precise float temp_72;
				precise float temp_73;
				precise float temp_74;
				precise float temp_75;
				uint temp_76;
				int temp_77;
				precise float temp_78;
				int temp_79;
				precise float temp_80;
				precise float temp_81;
				precise float temp_82;
				precise float temp_83;
				int temp_84;
				int temp_85;
				precise float temp_86;
				precise float temp_87;
				precise float temp_88;
				precise float temp_89;
				bool temp_90;
				precise float temp_91;
				precise float temp_92;
				precise float temp_93;
				precise float temp_94;
				precise float temp_95;
				int temp_96;
				int temp_97;
				precise float temp_98;
				precise float temp_99;
				precise float temp_100;
				precise float temp_101;
				int temp_102;
				int temp_103;
				int temp_104;
				precise float temp_105;
				precise float temp_106;
				precise float temp_107;
				precise float temp_108;
				precise float temp_109;
				int temp_110;
				precise float temp_111;
				precise float temp_112;
				precise float temp_113;
				precise float temp_114;
				precise float temp_115;
				precise float temp_116;
				int temp_117;
				int temp_118;
				int temp_119;
				precise float temp_120;
				precise float temp_121;
				precise float temp_122;
				precise float temp_123;
				precise float temp_124;
				precise float temp_125;
				precise float temp_126;
				int temp_127;
				int temp_128;
				int temp_129;
				int temp_130;
				precise float temp_131;
				int temp_132;
				int temp_133;
				int temp_134;
				precise float temp_135;
				precise float temp_136;
				precise float temp_137;
				precise float temp_138;
				precise float temp_139;
				precise float temp_140;
				precise float temp_141;
				int temp_142;
				int temp_143;
				int temp_144;
				int temp_145;
				int temp_146;
				precise float temp_147;
				int temp_148;
				int temp_149;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.x;
				temp_1 = v.a0.z;
				temp_2 = asint(_PatternV) & 0xFFFF;
				temp_3 = asint(_PatternH) & 0xFFFF;
				temp_4 = temp_2 * temp_3;
				temp_5 = 1.0 / _BlinkCycle;
				temp_6 = asint(_PatternV) & 0xFFFF;
				temp_7 = asuint(_PatternH) >> 16;
				temp_8 = temp_6 * int(temp_7);
				temp_9 = temp_8 & 0xFFFF;
				temp_10 = asint(_PatternH) << 16;
				temp_11 = temp_9 | temp_10;
				temp_12 = asuint(_PatternV) >> 16;
				temp_13 = uint(temp_11) >> 16;
				temp_14 = int(temp_12) * int(temp_13);
				temp_15 = temp_14 << 16;
				temp_16 = temp_11 << 16;
				temp_17 = temp_4 + temp_16;
				temp_18 = temp_15 + temp_17;
				temp_19 = float(temp_18);
				temp_20 = temp_5 * _Time.y;
				temp_21 = floor(temp_20);
				temp_22 = temp_19 * _SwitchingTime;
				temp_23 = 1.0 / temp_22;
				temp_24 = temp_23 * _Time.y;
				temp_25 = floor(temp_24);
				temp_26 = 0.0 - temp_25;
				temp_27 = temp_24 + temp_26;
				temp_28 = temp_19 * temp_27;
				temp_29 = floor(temp_28);
				temp_30 = mad(temp_29, _AutoSwitch, _StartFrameIndex);
				temp_31 = temp_19 * temp_30;
				temp_32 = 0.0 - temp_31;
				temp_33 = temp_31 >= temp_32;
				temp_34 = temp_19;
				[branch] if (!temp_33)
				{
				temp_35 = 0.0 - temp_19;
				temp_36 = temp_35 + -0.0;
				temp_34 = temp_36;
				}
				temp_37 = temp_34;
				temp_38 = 1.0 / temp_37;
				temp_39 = temp_30 * temp_38;
				temp_40 = 0 - asint(_PatternH);
				temp_41 = floor(temp_39);
				temp_42 = max(temp_40, asint(_PatternH));
				temp_43 = temp_42 == 0;
				temp_44 = temp_42 < 0;
				temp_45 = 0.0 - temp_41;
				temp_46 = temp_39 + temp_45;
				temp_47 = temp_46 * temp_37;
				temp_48 = float(uint(temp_42));
				temp_49 = trunc(temp_47);
				temp_50 = int(temp_49);
				temp_51 = !temp_43;
				temp_52 = temp_44 || temp_51;
				temp_53 = 1.0 / temp_48;
				temp_54 = 0 - temp_50;
				temp_55 = temp_50 ^ asint(_PatternH);
				temp_56 = temp_50 & asint(-0.0);
				temp_57 = temp_56 != 0;
				temp_58 = max(temp_50, temp_54);
				temp_59 = v.a2.y;
				temp_60 = temp_55 & asint(-0.0);
				temp_61 = temp_60 != 0;
				temp_62 = float(uint(temp_58));
				temp_63 = asint(temp_53) + -2;
				temp_64 = asfloat(temp_63) * temp_62;
				temp_65 = trunc(temp_64);
				temp_66 = max(temp_65, 0.0);
				temp_67 = uint(temp_66);
				temp_68 = temp_42 * int(temp_67);
				temp_69 = 0 - temp_68;
				temp_70 = temp_58 + temp_69;
				temp_71 = float(uint(temp_70));
				temp_72 = asfloat(temp_63) * temp_71;
				temp_73 = v.a0.y;
				temp_74 = trunc(temp_72);
				temp_75 = max(temp_74, 0.0);
				temp_76 = uint(temp_75);
				temp_77 = int(temp_67) + int(temp_76);
				temp_78 = temp_73 * unity_ObjectToWorld._m01;
				temp_79 = temp_42 * temp_77;
				temp_80 = temp_73 * unity_ObjectToWorld._m11;
				temp_81 = temp_73 * unity_ObjectToWorld._m21;
				temp_82 = temp_73 * unity_ObjectToWorld._m31;
				temp_83 = mad(temp_0, unity_ObjectToWorld._m00, temp_78);
				temp_84 = 0 - temp_79;
				temp_85 = temp_58 + temp_84;
				temp_86 = mad(temp_0, unity_ObjectToWorld._m10, temp_80);
				temp_87 = mad(temp_0, unity_ObjectToWorld._m20, temp_81);
				temp_88 = mad(temp_0, unity_ObjectToWorld._m30, temp_82);
				temp_89 = mad(temp_1, unity_ObjectToWorld._m02, temp_83);
				temp_90 = uint(temp_85) >= uint(temp_42);
				temp_91 = mad(temp_1, unity_ObjectToWorld._m12, temp_86);
				temp_92 = mad(temp_1, unity_ObjectToWorld._m22, temp_87);
				temp_93 = mad(temp_1, unity_ObjectToWorld._m32, temp_88);
				temp_94 = 0.0 - temp_21;
				temp_95 = temp_20 + temp_94;
				temp_96 = 0 - (temp_90 ? -1 : 0);
				temp_97 = temp_77 + temp_96;
				temp_98 = temp_91 + unity_ObjectToWorld._m13;
				temp_99 = temp_89 + unity_ObjectToWorld._m03;
				temp_100 = temp_92 + unity_ObjectToWorld._m23;
				temp_101 = temp_95 * 6.28318548;
				temp_102 = temp_42 & 0xFFFF;
				temp_103 = temp_97 & 0xFFFF;
				temp_104 = temp_102 * temp_103;
				temp_105 = temp_98 * unity_MatrixVP._m11;
				temp_106 = temp_98 * unity_MatrixVP._m01;
				temp_107 = temp_98 * unity_MatrixVP._m21;
				temp_108 = temp_98 * unity_MatrixVP._m31;
				temp_109 = mad(temp_99, unity_MatrixVP._m10, temp_105);
				temp_110 = temp_42 * temp_97;
				temp_111 = v.a1.y;
				temp_112 = mad(temp_99, unity_MatrixVP._m00, temp_106);
				temp_113 = v.a1.x;
				temp_114 = mad(temp_99, unity_MatrixVP._m20, temp_107);
				temp_115 = mad(temp_99, unity_MatrixVP._m30, temp_108);
				temp_116 = v.a1.z;
				temp_117 = temp_52 ? temp_97 : -1;
				temp_118 = 0 - temp_110;
				temp_119 = temp_58 + temp_118;
				temp_120 = v.a1.w;
				temp_121 = temp_93 + unity_ObjectToWorld._m33;
				temp_122 = v.a2.x;
				temp_123 = mad(temp_100, unity_MatrixVP._m02, temp_112);
				temp_124 = mad(temp_100, unity_MatrixVP._m12, temp_109);
				temp_125 = mad(temp_100, unity_MatrixVP._m22, temp_114);
				temp_126 = mad(temp_100, unity_MatrixVP._m32, temp_115);
				temp_127 = temp_104;
				temp_128 = temp_117;
				[branch] if (temp_61)
				{
				temp_129 = 0 - temp_117;
				temp_127 = temp_129;
				}
				temp_130 = temp_127;
				temp_131 = float(asint(_PatternH));
				temp_132 = temp_130;
				[branch] if (!temp_61)
				{
				temp_132 = temp_117;
				}
				temp_133 = temp_132;
				temp_134 = temp_52 ? temp_119 : -1;
				temp_135 = float(asint(_PatternV));
				temp_136 = mad(temp_121, unity_MatrixVP._m03, temp_123);
				temp_137 = mad(temp_121, unity_MatrixVP._m13, temp_124);
				o.pos.x = temp_136;
				temp_138 = mad(temp_121, unity_MatrixVP._m23, temp_125);
				o.pos.y = temp_137;
				temp_139 = mad(temp_121, unity_MatrixVP._m33, temp_126);
				temp_140 = 1.0 / temp_131;
				temp_141 = 1.0 / temp_135;
				[branch] if (temp_57)
				{
				temp_142 = 0 - temp_134;
				temp_128 = temp_142;
				}
				temp_143 = temp_128;
				o.pos.z = temp_138;
				temp_144 = 0 - temp_133;
				temp_145 = temp_144 + asint(_PatternV);
				temp_146 = temp_145 + -1;
				temp_147 = cos(temp_101);
				temp_148 = temp_143;
				[branch] if (!temp_57)
				{
				temp_148 = temp_134;
				}
				temp_149 = temp_148;
				temp_150 = float(temp_146);
				o.pos.w = temp_139;
				temp_151 = float(temp_149);
				temp_152 = temp_147 * _BlinkWidth;
				temp_153 = temp_150 + temp_59;
				temp_154 = temp_151 + temp_122;
				temp_155 = mad(temp_152, temp_113, temp_113);
				temp_156 = mad(temp_152, temp_111, temp_111);
				o.t1.x = temp_155;
				temp_157 = mad(temp_152, temp_116, temp_116);
				o.t1.y = temp_156;
				temp_158 = mad(temp_152, temp_120, temp_120);
				o.t1.z = temp_157;
				temp_159 = temp_153 * temp_141;
				o.t1.w = temp_158;
				temp_160 = temp_154 * temp_140;
				o.t0.y = temp_159;
				o.t0.x = temp_160;
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
				precise float4 temp_2;
				precise float temp_3;
				precise float temp_4;
				precise float temp_5;
				precise float temp_6;
				precise float temp_7;
				precise float temp_8;
				bool temp_9;
				precise float temp_10;
				precise float temp_11;
				bool temp_12;
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
				int temp_45;
				int temp_46;
				precise float temp_47;
				temp_0 = i.t0.y;
				temp_1 = i.t0.x;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_1, temp_0))).xyzw;
				temp_3 = temp_2.x;
				temp_4 = temp_2.y;
				temp_5 = temp_2.z;
				temp_6 = temp_2.w;
				temp_7 = i.t1.w;
				temp_8 = i.t1.x;
				temp_9 = 0.0 == _Transparent;
				temp_10 = 0.0 - _ScrollSpeed;
				temp_11 = mad(_Time.y, temp_10, temp_0);
				temp_12 = 1.0 == _Transparent;
				temp_13 = temp_11 * 200.0;
				temp_14 = i.t1.y;
				temp_15 = i.t1.z;
				temp_16 = sin(temp_13);
				temp_17 = 0.0 - _LineAlpha;
				temp_18 = mad(temp_3, temp_17, temp_3);
				temp_19 = 0.0 - _LineAlpha;
				temp_20 = mad(temp_6, temp_19, temp_6);
				temp_21 = 0.0 - _LineAlpha;
				temp_22 = mad(temp_5, temp_21, temp_5);
				temp_23 = 0.0 - _LineAlpha;
				temp_24 = mad(temp_4, temp_23, temp_4);
				temp_25 = mad(temp_16, _LineAlpha, temp_18);
				temp_26 = mad(temp_16, _LineAlpha, temp_20);
				temp_27 = mad(temp_16, _LineAlpha, temp_22);
				temp_28 = mad(temp_16, _LineAlpha, temp_24);
				temp_29 = temp_25 * 0.333000004;
				temp_30 = temp_26;
				temp_31 = temp_27;
				temp_32 = temp_28;
				temp_33 = temp_25;
				if (temp_12)
				{
				temp_34 = 0.0 - temp_25;
				temp_35 = temp_34 + 1.0;
				temp_30 = temp_35;
				}
				temp_36 = temp_30;
				if (temp_12)
				{
				temp_37 = temp_27 * 0.333000004;
				temp_31 = temp_37;
				}
				temp_38 = temp_31;
				if (temp_12)
				{
				temp_39 = temp_28 * 0.333000004;
				temp_32 = temp_39;
				}
				temp_40 = temp_32;
				if (temp_12)
				{
				temp_33 = temp_29;
				}
				temp_41 = temp_33;
				temp_42 = temp_36 * temp_7;
				temp_43 = temp_38 * temp_15;
				temp_44 = temp_40 * temp_14;
				temp_45 = asint(temp_42);
				if (temp_9)
				{
				temp_45 = 0x3F800000;
				}
				temp_46 = temp_45;
				temp_47 = temp_41 * temp_8;
				o.c0.x = temp_47;
				o.c0.y = temp_44;
				o.c0.z = temp_43;
				o.c0.w = asfloat(temp_46);
				return o;
			}
			ENDHLSL
		}
	}
}