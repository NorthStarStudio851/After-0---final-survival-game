Shader "After0/FogWall"
{
    Properties
    {
        _NoiseTex ("Noise (R fine, G mid, B large)", 2D) = "gray" {}

        _BaseColor ("Thin color", Color) = (0.62, 0.66, 0.70, 0.92)
        _DeepColor ("Dense color", Color) = (0.24, 0.27, 0.30, 1)

        _Density ("Density", Range(0, 4)) = 2.2
        _Contrast ("Contrast", Range(0.5, 4)) = 2.1

        _CellsA ("Layer 1 cells around", Float) = 10
        _CellsUpA ("Layer 1 cells up", Float) = 3
        _CellsB ("Layer 2 cells around", Float) = 3
        _CellsUpB ("Layer 2 cells up", Float) = 1.5

        _FlowSpeed ("Flow speed", Range(0, 3)) = 1
        _Scroll1 ("Scroll layer 1 (xy)", Vector) = (0.01, 0.004, 0, 0)
        _Scroll2 ("Scroll layer 2 (xy)", Vector) = (-0.006, 0.003, 0, 0)

        _TopFade ("Top fade", Range(0.05, 1)) = 0.5
        _BottomFade ("Bottom fade", Range(0, 0.4)) = 0.06
        _GroundBoost ("Ground density boost", Range(1, 3)) = 1.8

        _TileX ("Mesh u tiling", Float) = 24
        _TileY ("Mesh v tiling", Float) = 1
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
            Name "FogWall"
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                half4 _BaseColor;
                half4 _DeepColor;
                float4 _Scroll1;
                float4 _Scroll2;
                half _Density;
                half _Contrast;
                float _CellsA;
                float _CellsUpA;
                float _CellsB;
                float _CellsUpB;
                float _FlowSpeed;
                half _TopFade;
                half _BottomFade;
                half _GroundBoost;
                float _TileX;
                float _TileY;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float u = IN.uv.x / max(_TileX, 0.0001);
                float v = saturate(IN.uv.y / max(_TileY, 0.0001));

                // Scroll is in texture tiles per second now, so the numbers mean what they say
                float2 drift1 = _Scroll1.xy * _Time.y * _FlowSpeed;
                float2 drift2 = _Scroll2.xy * _Time.y * _FlowSpeed;

                float2 coordA = float2(u * _CellsA, v * _CellsUpA) + drift1;
                float2 coordB = float2(u * _CellsB, v * _CellsUpB) + drift2;

                half3 sampleA = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, coordA).rgb;
                half3 sampleB = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, coordB).rgb;

                half raw = sampleA.r * 0.55h + sampleB.g * 0.45h;

                // The slow coarse layer also drives the big banks of density
                raw *= lerp(0.6h, 1.35h, sampleB.b);

                half shaped = pow(saturate(raw), _Contrast);
                half density = shaped * _Density * lerp(_GroundBoost, 0.35h, v);

                half top = 1.0h - smoothstep(1.0h - _TopFade, 1.0h, v);
                half bottom = smoothstep(0.0h, _BottomFade, v);

                half3 color = lerp(_BaseColor.rgb, _DeepColor.rgb, shaped);
                half alpha = saturate(density * top * bottom) * _BaseColor.a;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}