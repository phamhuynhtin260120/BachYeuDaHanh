// Ink-wash look for bought cartoon models, so they sit in the painted arena:
// the albedo is pulled toward grey and tinted with paper, light falls in two soft bands (lit / ink shadow),
// a faint rim keeps the silhouette readable, and a second pass draws a dark brush outline (inverted hull).
// The global _InkStyle (-1 light .. 0 default .. +1 bold, set from Cài đặt > Nét quái) shifts every material at once.
Shader "BachYeu/InkCharacter" {
  Properties {
    _BaseMap ("Albedo", 2D) = "white" {}
    _BaseColor ("Color", Color) = (1,1,1,1)
    _Desat ("Desaturate", Range(0,1)) = .4
    _Paper ("Paper tint", Color) = (0.96,0.92,0.84,1)
    _Shade ("Shadow tint", Color) = (0.42,0.42,0.5,1)
    _Band ("Light band", Range(0,1)) = .48
    _Soft ("Band softness", Range(0.01,.5)) = .08
    _Rim ("Rim", Range(0,1)) = .18
    _OutlineColor ("Outline", Color) = (0.11,0.1,0.1,1)
    _OutlineWidth ("Outline width (px)", Range(0,6)) = 2.2
    [HideInInspector] _Cutoff ("Cutoff", Float) = .5
  }
  SubShader {
    Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    CBUFFER_START(UnityPerMaterial)
      float4 _BaseMap_ST;
      half4 _BaseColor, _Paper, _Shade, _OutlineColor;
      half _Desat, _Band, _Soft, _Rim, _OutlineWidth, _Cutoff;
    CBUFFER_END
    TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
    float _InkStyle; // global, not per material: unset = 0 = the material's own look
    ENDHLSL

    Pass {
      Name "ForwardInk"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
      #pragma multi_compile_fragment _ _SHADOWS_SOFT
      #pragma multi_compile_fog
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

      struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
      struct V { float4 cs : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 ws : TEXCOORD2; half fog : TEXCOORD3; };

      V vert (A i) {
        V o;
        VertexPositionInputs p = GetVertexPositionInputs(i.pos.xyz);
        o.cs = p.positionCS; o.ws = p.positionWS;
        o.n = TransformObjectToWorldNormal(i.n);
        o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
        o.fog = ComputeFogFactor(p.positionCS.z);
        return o;
      }

      half4 frag (V i) : SV_Target {
        half3 alb = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
        half lum = dot(alb, half3(.3, .59, .11));
        half s = clamp(_InkStyle, -1, 1);
        alb = lerp(alb, lum.xxx, saturate(_Desat - s * .27)) * _Paper.rgb;

        float3 n = normalize(i.n);
        Light L = GetMainLight(TransformWorldToShadowCoord(i.ws));
        half ndl = dot(n, L.direction) * .5 + .5;
        half band = _Band - s * .08, soft = _Soft * (1 - s * .45);
        half lit = smoothstep(band - soft, band + soft, ndl * lerp(.6, 1, L.shadowAttenuation));
        half3 light = lerp(_Shade.rgb * (1 - s * .25), 1, lit) * lerp(1, L.color, .35) + SampleSH(n) * .25;

        half3 v = normalize(GetWorldSpaceViewDir(i.ws));
        half rim = pow(1 - saturate(dot(n, v)), 3) * _Rim;
        half3 c = alb * light + rim * _Paper.rgb;
        c = MixFog(c, i.fog);
        return half4(c, 1);
      }
      ENDHLSL
    }

    Pass {
      Name "InkOutline"
      Tags { "LightMode"="SRPDefaultUnlit" }
      Cull Front
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_fog
      struct A { float4 pos : POSITION; float3 n : NORMAL; };
      struct V { float4 cs : SV_POSITION; half fog : TEXCOORD0; };
      V vert (A i) {
        V o;
        float4 cs = TransformObjectToHClip(i.pos.xyz);
        float3 nCS = mul((float3x3)UNITY_MATRIX_VP, TransformObjectToWorldNormal(i.n));
        float2 dir = normalize(nCS.xy + 1e-5);
        float width = _OutlineWidth * exp2(clamp(_InkStyle, -1, 1) * .54); // x0.69 light .. x1.45 bold
        cs.xy += dir / _ScreenParams.xy * 2 * width * cs.w; // constant width in pixels
        o.cs = cs;
        o.fog = ComputeFogFactor(cs.z);
        return o;
      }
      half4 frag (V i) : SV_Target { return half4(MixFog(_OutlineColor.rgb, i.fog), 1); }
      ENDHLSL
    }

    UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    UsePass "Universal Render Pipeline/Lit/DepthOnly"
    UsePass "Universal Render Pipeline/Lit/DepthNormals"
  }
  FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
