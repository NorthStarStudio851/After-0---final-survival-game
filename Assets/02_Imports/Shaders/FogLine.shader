Shader "After0/FogLine"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.85, 0.35, 1)
        _Dashes ("Dashes", Float) = 1
        _DashRatio ("Dash length", Range(0.05, 0.95)) = 0.55
        _Speed ("Flow speed", Float) = 0.25
        _Pulse ("Pulse", Range(0, 1)) = 0.25
        _EdgeSoftness ("Edge softness", Range(0.01, 0.5)) = 0.25
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
        }

        Pass
        {
            Name "FogLine"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Dashes;
                half _DashRatio;
                float _Speed;
                half _Pulse;
                half _EdgeSoftness;
                float _ZTest;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float travel = frac(IN.uv.x * _Dashes - _Time.y * _Speed);
                half dash = 1.0h - smoothstep(_DashRatio, _DashRatio + 0.08h, travel);

                // Fade the ribbon towards its two long edges so it does not look like tape
                half across = 1.0h - smoothstep(0.5h - _EdgeSoftness, 0.5h, abs(IN.uv.y - 0.5h));

                half breathe = 1.0h - _Pulse * (0.5h + 0.5h * sin(_Time.y * 2.2h));

                half alpha = dash * across * breathe * _Color.a * IN.color.a;

                return half4(_Color.rgb * IN.color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}