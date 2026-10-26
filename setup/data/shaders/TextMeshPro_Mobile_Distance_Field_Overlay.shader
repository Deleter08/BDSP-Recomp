Shader "TextMeshPro/Mobile/Distance Field Overlay" {
	Properties {
		_FaceColor ("Face Color", Color) = (1,1,1,1)
		_FaceDilate ("Face Dilate", Range(-1, 1)) = 0
		_OutlineColor ("Outline Color", Color) = (0,0,0,1)
		_OutlineWidth ("Outline Thickness", Range(0, 1)) = 0
		_OutlineSoftness ("Outline Softness", Range(0, 1)) = 0
		_UnderlayColor ("Border Color", Color) = (0,0,0,0.5)
		_UnderlayOffsetX ("Border OffsetX", Range(-1, 1)) = 0
		_UnderlayOffsetY ("Border OffsetY", Range(-1, 1)) = 0
		_UnderlayDilate ("Border Dilate", Range(-1, 1)) = 0
		_UnderlaySoftness ("Border Softness", Range(0, 1)) = 0
		_WeightNormal ("Weight Normal", Float) = 0
		_WeightBold ("Weight Bold", Float) = 0.5
		_ShaderFlags ("Flags", Float) = 0
		_ScaleRatioA ("Scale RatioA", Float) = 1
		_ScaleRatioB ("Scale RatioB", Float) = 1
		_ScaleRatioC ("Scale RatioC", Float) = 1
		_MainTex ("Font Atlas", 2D) = "white" {}
		_TextureWidth ("Texture Width", Float) = 512
		_TextureHeight ("Texture Height", Float) = 512
		_GradientScale ("Gradient Scale", Float) = 5
		_ScaleX ("Scale X", Float) = 1
		_ScaleY ("Scale Y", Float) = 1
		_PerspectiveFilter ("Perspective Correction", Range(0, 1)) = 0.875
		_Sharpness ("Sharpness", Range(-1, 1)) = 0
		_VertexOffsetX ("Vertex OffsetX", Float) = 0
		_VertexOffsetY ("Vertex OffsetY", Float) = 0
		_ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
		_MaskSoftnessX ("Mask SoftnessX", Float) = 0
		_MaskSoftnessY ("Mask SoftnessY", Float) = 0
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		_ColorMask ("Color Mask", Float) = 15
	}

	SubShader {
		Tags { "IgnoreProjector"="true" "Queue"="Overlay" "RenderType"="Transparent" }
		Pass {
			Tags { "IgnoreProjector"="true" "RenderType"="Transparent" }
			Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
			ColorMask [_ColorMask]
			ZTest Always
			ZWrite Off
			Cull [_CullMode]
			Stencil {
				Ref [_Stencil]
				ReadMask [_StencilReadMask]
				WriteMask [_StencilWriteMask]
				Comp [_StencilComp]
				Pass [_StencilOp]
				Fail Keep
				ZFail Keep
			}
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			#pragma multi_compile _ UNITY_UI_ALPHACLIP
			#pragma multi_compile _ UNITY_UI_CLIP_RECT
			float3 _WorldSpaceCameraPos;
			float4 _ScreenParams;
			float4 _FaceColor;
			float _FaceDilate;
			float _OutlineSoftness;
			float4 _OutlineColor;
			float _OutlineWidth;
			float _WeightNormal;
			float _WeightBold;
			float _ScaleRatioA;
			float _VertexOffsetX;
			float _VertexOffsetY;
			float4 _ClipRect;
			float _MaskSoftnessX;
			float _MaskSoftnessY;
			float _GradientScale;
			float _ScaleX;
			float _ScaleY;
			float _PerspectiveFilter;
			float _Sharpness;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_WorldToObject;
			float4x4 glstate_matrix_projection;
			float4x4 unity_MatrixVP;
			sampler2D _MainTex;

			static const int undef = 0;
			// TMP vertex layout. These programs read TMP 2.x vertices: uv1.x = the face/outline UV packed as in TMP_Text.PackUV
			// (int(x*511)*4096 + int(y*511)), uv1.y = the SDF scale (negative = bold). TextMeshPro in com.unity.ugui 2.x (Unity
			// 2023.2+ / Unity 6) writes the scale to uv0.w and a plain uv1 - read as the scale, uv1.y (0 or 1) draws no glyphs.
			// Repack those vertices to the 2.x layout; nothing changes for the 2019.4 editor (TMP 2.x).
			#if UNITY_VERSION >= 202320
			#define NVN_TMP_VERTEX(uv0, uv1) uv1.xy = float2(trunc(uv1.x * 511.0) * 4096.0 + trunc(uv1.y * 511.0), uv0.w)
			#else
			#define NVN_TMP_VERTEX(uv0, uv1)
			#endif
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
			#if defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
			#define NVN_VS_ID 0
			#elif defined(UNITY_UI_ALPHACLIP)
			#define NVN_VS_ID 0
			#elif defined(UNITY_UI_CLIP_RECT)
			#define NVN_VS_ID 0
			#else
			#define NVN_VS_ID 0
			#endif
			#if NVN_VS_ID == 0
			// unique program 0: shared by 4 keyword variant(s)
			// 0000_vs.sph.bin keywords=[]
			struct appdata {
				float4 a0 : POSITION;
				float4 a1 : NORMAL;
				float4 a2 : COLOR;
				float4 a3 : TEXCOORD0;
				float4 a4 : TEXCOORD1;
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
				NVN_TMP_VERTEX(v.a3, v.a4);
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
				bool temp_13;
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
				bool temp_76;
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
				precise float temp_164;
				precise float temp_165;
				precise float temp_166;
				precise float temp_167;
				precise float temp_168;
				precise float temp_169;
				precise float temp_170;
				precise float temp_171;
				precise float temp_172;
				precise float temp_173;
				precise float temp_174;
				precise float temp_175;
				precise float temp_176;
				precise float temp_177;
				precise float temp_178;
				precise float temp_179;
				precise float temp_180;
				precise float temp_181;
				precise float temp_182;
				precise float temp_183;
				precise float temp_184;
				precise float temp_185;
				precise float temp_186;
				precise float temp_187;
				precise float temp_188;
				precise float temp_189;
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.y;
				temp_1 = v.a1.x;
				temp_2 = v.a0.x;
				temp_3 = _ScreenParams.y * glstate_matrix_projection._m01;
				temp_4 = v.a1.y;
				temp_5 = _ScreenParams.y * glstate_matrix_projection._m11;
				temp_6 = v.a0.z;
				temp_7 = 0.0 - _WeightNormal;
				temp_8 = temp_7 + _WeightBold;
				temp_9 = v.a1.z;
				temp_10 = mad(_ScreenParams.x, glstate_matrix_projection._m00, temp_3);
				temp_11 = mad(_ScreenParams.x, glstate_matrix_projection._m10, temp_5);
				temp_12 = temp_0 + _VertexOffsetY;
				temp_13 = 0.0 == glstate_matrix_projection._m33;
				temp_14 = temp_1 * unity_WorldToObject._m02;
				temp_15 = temp_1 * unity_WorldToObject._m00;
				temp_16 = temp_1 * unity_WorldToObject._m01;
				temp_17 = temp_12 * unity_ObjectToWorld._m11;
				temp_18 = temp_2 + _VertexOffsetX;
				temp_19 = temp_12 * unity_ObjectToWorld._m01;
				temp_20 = temp_12 * unity_ObjectToWorld._m31;
				temp_21 = abs(temp_11);
				temp_22 = temp_21 + -0.0;
				temp_23 = mad(temp_4, unity_WorldToObject._m12, temp_14);
				temp_24 = mad(temp_4, unity_WorldToObject._m10, temp_15);
				temp_25 = v.a0.w;
				temp_26 = mad(temp_4, unity_WorldToObject._m11, temp_16);
				temp_27 = mad(temp_18, unity_ObjectToWorld._m10, temp_17);
				temp_28 = temp_12 * unity_ObjectToWorld._m21;
				temp_29 = mad(temp_18, unity_ObjectToWorld._m00, temp_19);
				temp_30 = mad(temp_18, unity_ObjectToWorld._m30, temp_20);
				temp_31 = temp_22 * _ScaleY;
				temp_32 = mad(temp_9, unity_WorldToObject._m21, temp_26);
				temp_33 = 1.0 / temp_31;
				temp_34 = mad(temp_6, unity_ObjectToWorld._m12, temp_27);
				temp_35 = mad(temp_18, unity_ObjectToWorld._m20, temp_28);
				temp_36 = mad(temp_6, unity_ObjectToWorld._m02, temp_29);
				temp_37 = mad(temp_6, unity_ObjectToWorld._m32, temp_30);
				temp_38 = mad(temp_9, unity_WorldToObject._m20, temp_24);
				temp_39 = abs(temp_10);
				temp_40 = temp_39 + -0.0;
				temp_41 = temp_34 + unity_ObjectToWorld._m13;
				temp_42 = mad(temp_6, unity_ObjectToWorld._m22, temp_35);
				temp_43 = temp_36 + unity_ObjectToWorld._m03;
				temp_44 = temp_38 * temp_38;
				temp_45 = temp_40 * _ScaleX;
				temp_46 = temp_41 * unity_MatrixVP._m31;
				temp_47 = 1.0 / temp_45;
				temp_48 = mad(temp_9, unity_WorldToObject._m22, temp_23);
				temp_49 = mad(temp_25, unity_ObjectToWorld._m03, temp_36);
				temp_50 = mad(temp_25, unity_ObjectToWorld._m13, temp_34);
				temp_51 = mad(temp_25, unity_ObjectToWorld._m23, temp_42);
				temp_52 = temp_42 + unity_ObjectToWorld._m23;
				temp_53 = mad(temp_43, unity_MatrixVP._m30, temp_46);
				temp_54 = v.a4.y;
				temp_55 = temp_37 + unity_ObjectToWorld._m33;
				temp_56 = 0.0 - temp_49;
				temp_57 = temp_56 + _WorldSpaceCameraPos.x;
				temp_58 = 0.0 - temp_50;
				temp_59 = temp_58 + _WorldSpaceCameraPos.y;
				temp_60 = 0.0 - temp_51;
				temp_61 = temp_60 + _WorldSpaceCameraPos.z;
				temp_62 = mad(temp_32, temp_32, temp_44);
				temp_63 = mad(temp_52, unity_MatrixVP._m32, temp_53);
				temp_64 = temp_57 * temp_57;
				temp_65 = mad(temp_48, temp_48, temp_62);
				temp_66 = mad(temp_55, unity_MatrixVP._m33, temp_63);
				temp_67 = rsqrt(temp_65);
				temp_68 = mad(temp_59, temp_59, temp_64);
				temp_69 = temp_66 * temp_47;
				temp_70 = mad(temp_61, temp_61, temp_68);
				temp_71 = temp_66 * temp_33;
				temp_72 = rsqrt(temp_70);
				temp_73 = abs(temp_54);
				temp_74 = temp_73 + -0.0;
				temp_75 = temp_69 * temp_69;
				temp_76 = temp_54 <= 0.0;
				temp_77 = temp_76 ? 1.0 : 0.0;
				temp_78 = temp_38 * temp_67;
				temp_79 = temp_74 * _GradientScale;
				temp_80 = mad(temp_71, temp_71, temp_75);
				temp_81 = mad(temp_8, temp_77, _WeightNormal);
				temp_82 = rsqrt(temp_80);
				temp_83 = max(-2E+10, _ClipRect.x);
				temp_84 = temp_57 * temp_72;
				temp_85 = mad(temp_79, _Sharpness, temp_79);
				temp_86 = temp_59 * temp_72;
				temp_87 = v.a3.y;
				temp_88 = temp_61 * temp_72;
				temp_89 = min(temp_83, 2e+10);
				temp_90 = temp_84 * temp_78;
				temp_91 = max(-2E+10, _ClipRect.z);
				temp_92 = temp_32 * temp_67;
				temp_93 = v.a3.x;
				temp_94 = temp_48 * temp_67;
				temp_95 = temp_85 * temp_82;
				temp_96 = max(-2E+10, _ClipRect.y);
				temp_97 = 0.0 - temp_89;
				temp_98 = mad(temp_18, 2.0, temp_97);
				temp_99 = min(temp_91, 2e+10);
				temp_100 = v.a2.x;
				temp_101 = mad(temp_86, temp_92, temp_90);
				temp_102 = max(-2E+10, _ClipRect.w);
				temp_103 = abs(temp_95);
				temp_104 = temp_103 + -0.0;
				temp_105 = min(temp_96, 2e+10);
				temp_106 = 0.0 - temp_89;
				temp_107 = temp_18 + temp_106;
				temp_108 = v.a2.w;
				temp_109 = 0.0 - temp_99;
				temp_110 = temp_109 + temp_98;
				temp_111 = mad(temp_88, temp_94, temp_101);
				temp_112 = min(temp_102, 2e+10);
				temp_113 = 0.0 - _PerspectiveFilter;
				temp_114 = mad(temp_104, temp_113, temp_104);
				temp_115 = 0.0 - temp_89;
				temp_116 = temp_115 + temp_99;
				temp_117 = 0.0 - temp_105;
				temp_118 = mad(temp_12, 2.0, temp_117);
				temp_119 = 1.0 / temp_116;
				temp_120 = 0.0 - temp_105;
				temp_121 = temp_12 + temp_120;
				temp_122 = 0.0 - temp_105;
				temp_123 = temp_122 + temp_112;
				temp_124 = abs(temp_111);
				temp_125 = temp_124 + -0.0;
				temp_126 = 1.0 / temp_123;
				temp_127 = 0.0 - temp_114;
				temp_128 = mad(temp_85, temp_82, temp_127);
				temp_129 = v.a2.y;
				temp_130 = 0.0 - temp_112;
				temp_131 = temp_130 + temp_118;
				temp_132 = v.a2.z;
				temp_133 = temp_41 * unity_MatrixVP._m01;
				o.t3.x = temp_93;
				temp_134 = temp_41 * unity_MatrixVP._m11;
				o.pos.w = temp_66;
				temp_135 = temp_41 * unity_MatrixVP._m21;
				o.t3.y = temp_87;
				o.t2.x = temp_110;
				temp_136 = temp_107 * temp_119;
				o.t2.y = temp_131;
				temp_137 = mad(temp_128, temp_125, temp_114);
				o.t3.z = temp_136;
				temp_138 = mad(temp_43, unity_MatrixVP._m00, temp_133);
				temp_139 = mad(temp_43, unity_MatrixVP._m10, temp_134);
				temp_140 = mad(temp_43, unity_MatrixVP._m20, temp_135);
				temp_141 = temp_137;
				[branch] if (!temp_13)
				{
				temp_141 = temp_95;
				}
				temp_142 = temp_141;
				temp_143 = _ScaleRatioA * _OutlineSoftness;
				temp_144 = mad(temp_52, unity_MatrixVP._m02, temp_138);
				temp_145 = mad(temp_52, unity_MatrixVP._m12, temp_139);
				temp_146 = mad(temp_52, unity_MatrixVP._m22, temp_140);
				temp_147 = _ScaleRatioA * _OutlineWidth;
				temp_148 = mad(temp_143, temp_142, 1.0);
				temp_149 = mad(temp_81, 0.25, _FaceDilate);
				temp_150 = 1.0 / temp_148;
				temp_151 = mad(temp_55, unity_MatrixVP._m03, temp_144);
				temp_152 = mad(temp_55, unity_MatrixVP._m13, temp_145);
				o.pos.x = temp_151;
				temp_153 = mad(temp_55, unity_MatrixVP._m23, temp_146);
				o.pos.y = temp_152;
				o.pos.z = temp_153;
				temp_154 = temp_149 * _ScaleRatioA;
				temp_155 = temp_150 * temp_142;
				temp_156 = temp_100 * _FaceColor.x;
				o.t1.x = temp_155;
				temp_157 = mad(temp_154, -0.5, 0.5);
				temp_158 = temp_147 * temp_155;
				temp_159 = temp_129 * _FaceColor.y;
				temp_160 = temp_108 * _FaceColor.w;
				temp_161 = temp_121 * temp_126;
				o.t0.w = temp_160;
				temp_162 = mad(temp_155, temp_157, -0.5);
				o.t3.w = temp_161;
				temp_163 = temp_132 * _FaceColor.z;
				o.t1.w = temp_162;
				temp_164 = mad(0.25, _MaskSoftnessX, temp_69);
				temp_165 = mad(0.25, _MaskSoftnessY, temp_71);
				temp_166 = 1.0 / temp_164;
				temp_167 = mad(temp_158, 0.5, temp_162);
				temp_168 = 1.0 / temp_165;
				temp_169 = mad(temp_158, -0.5, temp_162);
				o.t1.z = temp_167;
				temp_170 = min(temp_158, 1.0);
				o.t1.y = temp_169;
				temp_171 = temp_108 * _OutlineColor.w;
				temp_172 = sqrt(temp_170);
				temp_173 = temp_160 * temp_156;
				temp_174 = temp_160 * temp_159;
				o.t0.x = temp_173;
				temp_175 = temp_160 * temp_163;
				o.t0.y = temp_174;
				temp_176 = 0.0 - temp_173;
				temp_177 = mad(temp_171, _OutlineColor.x, temp_176);
				o.t0.z = temp_175;
				temp_178 = 0.0 - temp_174;
				temp_179 = mad(temp_171, _OutlineColor.y, temp_178);
				temp_180 = 0.0 - temp_175;
				temp_181 = mad(temp_171, _OutlineColor.z, temp_180);
				temp_182 = 0.0 - temp_160;
				temp_183 = mad(temp_108, _OutlineColor.w, temp_182);
				temp_184 = temp_166 * 0.25;
				temp_185 = temp_168 * 0.25;
				o.t2.z = temp_184;
				temp_186 = mad(temp_177, temp_172, temp_173);
				o.t2.w = temp_185;
				temp_187 = mad(temp_179, temp_172, temp_174);
				o.t4.x = temp_186;
				temp_188 = mad(temp_181, temp_172, temp_175);
				o.t4.y = temp_187;
				temp_189 = mad(temp_183, temp_172, temp_160);
				o.t4.z = temp_188;
				o.t4.w = temp_189;
				return o;
			}
			#endif
			#if defined(UNITY_UI_ALPHACLIP) && defined(UNITY_UI_CLIP_RECT)
			// 0007_fs.sph.bin keywords=['UNITY_UI_ALPHACLIP', 'UNITY_UI_CLIP_RECT']
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
				bool temp_32;
				precise float temp_33;
				precise float temp_34;
				precise float temp_35;
				precise float temp_36;
				precise float temp_37;
				precise float temp_38;
				precise float temp_39;
				precise float temp_40;
				temp_0 = i.t3.x;
				temp_1 = i.t3.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).w;
				temp_3 = i.t2.x;
				temp_4 = i.t2.y;
				temp_5 = i.t2.z;
				temp_6 = i.t2.w;
				temp_7 = i.t1.x;
				temp_8 = i.t1.w;
				temp_9 = i.t0.w;
				temp_10 = i.t0.z;
				temp_11 = 0.0 - _ClipRect.x;
				temp_12 = temp_11 + _ClipRect.z;
				temp_13 = 0.0 - _ClipRect.y;
				temp_14 = temp_13 + _ClipRect.w;
				temp_15 = abs(temp_3);
				temp_16 = 0.0 - temp_15;
				temp_17 = temp_16 + temp_12;
				temp_18 = abs(temp_4);
				temp_19 = 0.0 - temp_18;
				temp_20 = temp_19 + temp_14;
				temp_21 = temp_5 * temp_17;
				temp_22 = saturate(temp_21);
				temp_23 = temp_6 * temp_20;
				temp_24 = saturate(temp_23);
				temp_25 = temp_22 * temp_24;
				temp_26 = 0.0 - temp_8;
				temp_27 = mad(temp_2, temp_7, temp_26);
				temp_28 = saturate(temp_27);
				temp_29 = i.t0.y;
				temp_30 = temp_28 * temp_9;
				temp_31 = temp_25 * temp_30;
				temp_32 = temp_31 < 0.00100040436;
				temp_33 = i.t0.x;
				temp_34 = temp_28 * temp_29;
				if (temp_32)
				{
				discard;
				}
				temp_35 = temp_25 * temp_34;
				temp_36 = temp_28 * temp_33;
				temp_37 = temp_28 * temp_10;
				temp_38 = temp_25 * temp_36;
				temp_39 = temp_25 * temp_37;
				temp_40 = temp_25 * temp_30;
				o.c0.x = temp_38;
				o.c0.y = temp_35;
				o.c0.z = temp_39;
				o.c0.w = temp_40;
				return o;
			}
			#elif defined(UNITY_UI_ALPHACLIP)
			// 0005_fs.sph.bin keywords=['UNITY_UI_ALPHACLIP']
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
				bool temp_12;
				precise float temp_13;
				precise float temp_14;
				precise float temp_15;
				precise float temp_16;
				precise float temp_17;
				temp_0 = i.t3.x;
				temp_1 = i.t3.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).w;
				temp_3 = i.t1.x;
				temp_4 = i.t1.w;
				temp_5 = i.t0.w;
				temp_6 = i.t0.z;
				temp_7 = 0.0 - temp_4;
				temp_8 = mad(temp_2, temp_3, temp_7);
				temp_9 = saturate(temp_8);
				temp_10 = i.t0.y;
				temp_11 = temp_9 * temp_5;
				temp_12 = temp_11 < 0.00100040436;
				temp_13 = i.t0.x;
				temp_14 = temp_9 * temp_10;
				if (temp_12)
				{
				discard;
				}
				temp_15 = temp_9 * temp_6;
				temp_16 = temp_9 * temp_5;
				temp_17 = temp_9 * temp_13;
				o.c0.x = temp_17;
				o.c0.y = temp_14;
				o.c0.z = temp_15;
				o.c0.w = temp_16;
				return o;
			}
			#elif defined(UNITY_UI_CLIP_RECT)
			// 0006_fs.sph.bin keywords=['UNITY_UI_CLIP_RECT']
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
				temp_0 = i.t3.x;
				temp_1 = i.t3.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).w;
				temp_3 = i.t2.x;
				temp_4 = i.t2.y;
				temp_5 = i.t1.x;
				temp_6 = i.t2.z;
				temp_7 = i.t2.w;
				temp_8 = i.t1.w;
				temp_9 = i.t0.x;
				temp_10 = i.t0.y;
				temp_11 = i.t0.z;
				temp_12 = i.t0.w;
				temp_13 = 0.0 - _ClipRect.x;
				temp_14 = temp_13 + _ClipRect.z;
				temp_15 = 0.0 - _ClipRect.y;
				temp_16 = temp_15 + _ClipRect.w;
				temp_17 = abs(temp_3);
				temp_18 = 0.0 - temp_17;
				temp_19 = temp_18 + temp_14;
				temp_20 = abs(temp_4);
				temp_21 = 0.0 - temp_20;
				temp_22 = temp_21 + temp_16;
				temp_23 = temp_6 * temp_19;
				temp_24 = saturate(temp_23);
				temp_25 = temp_7 * temp_22;
				temp_26 = saturate(temp_25);
				temp_27 = 0.0 - temp_8;
				temp_28 = mad(temp_2, temp_5, temp_27);
				temp_29 = saturate(temp_28);
				temp_30 = temp_24 * temp_26;
				temp_31 = temp_29 * temp_9;
				temp_32 = temp_29 * temp_10;
				temp_33 = temp_29 * temp_11;
				temp_34 = temp_29 * temp_12;
				temp_35 = temp_30 * temp_31;
				temp_36 = temp_30 * temp_32;
				temp_37 = temp_30 * temp_33;
				temp_38 = temp_30 * temp_34;
				o.c0.x = temp_35;
				o.c0.y = temp_36;
				o.c0.z = temp_37;
				o.c0.w = temp_38;
				return o;
			}
			#else
			// 0004_fs.sph.bin keywords=[]
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
				temp_0 = i.t3.x;
				temp_1 = i.t3.y;
				temp_2 = tex2D(_MainTex, nvn_nan0(float2(temp_0, temp_1))).w;
				temp_3 = i.t1.x;
				temp_4 = i.t1.w;
				temp_5 = i.t0.x;
				temp_6 = i.t0.y;
				temp_7 = i.t0.z;
				temp_8 = i.t0.w;
				temp_9 = 0.0 - temp_4;
				temp_10 = mad(temp_2, temp_3, temp_9);
				temp_11 = saturate(temp_10);
				temp_12 = temp_11 * temp_5;
				temp_13 = temp_11 * temp_6;
				temp_14 = temp_11 * temp_7;
				temp_15 = temp_11 * temp_8;
				o.c0.x = temp_12;
				o.c0.y = temp_13;
				o.c0.z = temp_14;
				o.c0.w = temp_15;
				return o;
			}
			#endif
			ENDHLSL
		}
	}
}