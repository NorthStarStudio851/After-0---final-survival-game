Shader "After0/FogPlane"
{
    Properties
    {
        _NoiseTex ("Noise (R fine, G mid, B large)", 2D) = "gray" {}

        _BaseColor ("Thin color", Color) = (0.62, 0.66, 0.70, 0.95)
        _DeepColor ("Dense color", Color) = (0.24, 0.27, 0.30, 1)

        _Density ("Density", Range(0, 4)) = 2.4
        _Contrast ("Contrast", Range(0.5, 4)) = 1.8

        _Thickness ("Thickness floor", Range(0, 1)) = 0.5

        _MetresA ("Layer 1 metres per tile", Float) = 12
        _MetresB ("Layer 2 metres per tile", Float) = 90

        _FlowSpeed ("Flow speed", Range(0, 3)) = 1
        _Scroll1 ("Scroll layer 1 (xy)", Vector) = (0.010, 0.004, 0, 0)
        _Scroll2 ("Scroll layer 2 (xy)", Vector) = (-0.006, 0.003, 0, 0)

        _EdgeSoftness ("Clearing edge softness", Range(0.01, 1)) = 0.35
        _MinAlpha ("Alpha in full light", Range(0, 0.4)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "FogPlane"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            // Written by LightMap.cs. Every pole is already burned into this.
            TEXTURE2D(_LightMap);
            SAMPLER(sampler_LightMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            // Globals, not per material: one fog plane, one set of values
            float4 _LightMapBounds;   // xy = terrain corner, z = size in metres, w = 1/size
            float4 _FogViewer;        // xyz = player position, w = bubble radius

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                half4 _BaseColor;
                half4 _DeepColor;
                float4 _Scroll1;
                float4 _Scroll2;
                half _Density;
                half _Contrast;
                half _Thickness;
                float _MetresA;
                float _MetresB;
                float _FlowSpeed;
                half _EdgeSoftness;
                half _MinAlpha;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 world = IN.positionWS.xz;

                // --- what the poles have already cleared ---
                // LightMap.cs writes the bounds together with the texture, so a zero size means
                // nothing is driving us yet. Without this test an unbound texture reads white,
                // which would say "lit everywhere" and make the fog vanish completely.
                half hasMap = _LightMapBounds.z > 0.0 ? 1.0h : 0.0h;

                float2 lightUV = (world - _LightMapBounds.xy) * _LightMapBounds.w;
                half lit = SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, lightUV).r;

                // Outside the terrain the map clamps, so kill it rather than smear the edge pixel
                half inside = (lightUV.x >= 0 && lightUV.x <= 1 && lightUV.y >= 0 && lightUV.y <= 1)
                    ? 1.0h : 0.0h;
                lit *= inside * hasMap;

                // --- the bubble the player carries, added live so the map never rebuilds ---
                float radius = max(_FogViewer.w, 0.001);
                half bubble = 1.0h - smoothstep(radius * (1.0h - _EdgeSoftness), radius,
                                                distance(world, _FogViewer.xz));

                half clear = max(lit, bubble);

                // --- the fog itself, sampled in metres so tiles never stretch ---
                float2 drift1 = _Scroll1.xy * _Time.y * _FlowSpeed;
                float2 drift2 = _Scroll2.xy * _Time.y * _FlowSpeed;

                float2 coordA = world / max(_MetresA, 0.001) + drift1;
                float2 coordB = world / max(_MetresB, 0.001) + drift2;

                half3 sampleA = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, coordA).rgb;
                half3 sampleB = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, coordB).rgb;

                half raw = sampleA.r * 0.55h + sampleB.g * 0.45h;
                raw *= lerp(0.6h, 1.35h, sampleB.b);

                half shaped = pow(saturate(raw), _Contrast);

                half3 color = lerp(_BaseColor.rgb, _DeepColor.rgb, shaped);

                // Thickness is the floor: how solid the fog stays where the noise is thinnest.
                // Without it those thin patches become windows and the ground reads straight
                // through them, no matter how high the density goes.
                half alpha = saturate(_Thickness + shaped * _Density) * _BaseColor.a;

                // Clear areas win outright, so the edge of a clearing reads as a hard rim
                alpha = lerp(alpha, _MinAlpha, clear);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
