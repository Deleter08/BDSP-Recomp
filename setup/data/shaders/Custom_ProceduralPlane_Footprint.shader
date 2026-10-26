Shader "Custom/ProceduralPlane/Footprint" {
	Properties {
		[NoScaleOffset] _MainTex ("Main Texture", 2D) = "black" {}
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Factor", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Factor", Float) = 0
		[Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("BlendOp", Float) = 0
		[HideInInspector] _GridX ("Grid Position X", Float) = 0
		[HideInInspector] _GridY ("Grid Position Z", Float) = 0
		_FootScale ("Foot Scale", Vector) = (1,1,1,1)
		_FootAngle ("Foot Angle", Float) = 0
		[HideInInspector] _Subduction ("Subduction", Float) = 1
	}

	SubShader {
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }
		Pass {
			Tags { "RenderType"="Transparent" }
			Blend [_SrcBlend] [_DstBlend], [_SrcBlend] [_DstBlend]
			BlendOp [_BlendOp], [_BlendOp]
			ZWrite Off
			Cull Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			float _RcpGridSize;
			float _GridX;
			float _GridY;
			float2 _FootScale;
			float _FootAngle;
			float _Subduction;
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
				o.pos.x = 0.0;
				o.pos.y = 0.0;
				o.pos.z = 0.0;
				o.pos.w = 1.0;
				temp_0 = v.a0.x;
				temp_1 = cos(_FootAngle);
				temp_2 = sin(_FootAngle);
				temp_3 = v.a0.y;
				temp_4 = v.a1.x;
				temp_5 = v.a1.y;
				temp_6 = temp_0 * _FootScale.x;
				o.t0.x = temp_4;
				temp_7 = temp_3 * _FootScale.y;
				o.t0.y = temp_5;
				temp_8 = temp_6 * temp_1;
				temp_9 = temp_2 * temp_6;
				temp_10 = 0.0 - temp_7;
				temp_11 = mad(temp_2, temp_10, temp_8);
				temp_12 = mad(temp_7, temp_1, temp_9);
				temp_13 = mad(2.0, _GridX, temp_11);
				temp_14 = mad(2.0, _GridY, temp_12);
				temp_15 = temp_13 * _RcpGridSize;
				temp_16 = temp_14 * _RcpGridSize;
				o.pos.x = temp_15;
				o.pos.y = temp_16;
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
				precise float temp_10;
				precise float temp_11;
				temp_0 = i.fragcoord.w;
				temp_1 = i.t0.x;
				temp_2 = i.fragcoord.w;
				temp_3 = temp_1 * temp_2;
				temp_4 = i.t0.y;
				temp_5 = i.fragcoord.w;
				temp_6 = temp_4 * temp_5;
				temp_7 = 1.0 / temp_0;
				temp_8 = temp_7 * temp_3;
				temp_9 = temp_7 * temp_6;
				temp_10 = tex2D(_MainTex, nvn_nan0(float2(temp_8, temp_9))).x;
				temp_11 = min(temp_10, _Subduction);
				o.c0.x = temp_11;
				o.c0.y = 0.0;
				o.c0.z = 0.0;
				o.c0.w = 1.0;
				return o;
			}
			ENDHLSL
		}
	}
}