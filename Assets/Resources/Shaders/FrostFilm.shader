// 벽돌 얼음막 셰이더야. URP 2D의 Sprite-Unlit-Default와 같은 방식으로 그리고, 얼음 아이템 연출 세 가지를 더했어.
// 1) 퍼짐: 양 위 모서리부터 윗변을 따라 가운데로, 그다음 아래쪽 냉기로 번져 (_Progress 0 → 1.1)
// 2) 빛남: 굳는 순간과 턴 끝에 얼음막만 하얗게 빛나 (_Glow)
// 3) 광택: 굳는 순간 매끈한 빛이 벽돌 위를 비스듬히 훑고 지나가 (_Sheen 0 → 1, 음수면 꺼짐)
// 서리막 이미지가 아틀라스에 들어가도 이미지 칸 안의 위치를 알 수 있게 _FilmRect(아틀라스 안 UV 사각형)를 받아.
Shader "Elemental Breaker/Frost Film"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _FilmRect ("Film UV Rect (x, y, width, height)", Vector) = (0, 0, 1, 1)
        _Progress ("Progress", Float) = 1.1
        _Glow ("Glow", Float) = 0
        _Fade ("Fade", Float) = 1
        _Sheen ("Sheen", Float) = -1

        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex FrostVertex
            #pragma fragment FrostFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _FilmRect;
                float _Progress;
                float _Glow;
                float _Fade;
                float _Sheen;
            CBUFFER_END

            // 서리막 이미지 크기(px)야. 퍼짐 순서와 광택 위치를 이 크기 기준으로 계산해.
            static const float2 FilmSize = float2(180.0, 131.0);
            // 벽돌 이미지의 둥근 모서리 반지름과 아래 턱 두께(px)야. 광택이 벽돌 밖으로 나가지 않게 잘라.
            static const float CornerRadius = 20.0;
            static const float Lip = 8.0;

            Varyings FrostVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 FrostFragment(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // 이미지 칸 안의 위치(px). x는 왼쪽부터, y는 위에서부터 재.
                float2 local = (input.uv - _FilmRect.xy) / max(_FilmRect.zw, 1e-5);
                float px = local.x * FilmSize.x;
                float py = (1.0 - local.y) * FilmSize.y;

                // 퍼짐 순서: 양 위 모서리에서 가까울수록 먼저 얼어. 세로는 1.6배로 쳐서 윗변을 따라 먼저 번져.
                float fromLeft = length(float2(px, py * 1.6));
                float fromRight = length(float2(FilmSize.x - px, py * 1.6));
                float reveal = saturate(min(fromLeft, fromRight) / 119.5) * 0.85 + saturate(py / FilmSize.y) * 0.15;
                half alpha = tex.a * saturate((_Progress - reveal) / 0.08);

                // 빛남: 얼음막 색을 하얗게 올려. 서리가 없는 곳은 빛나지 않아.
                half3 rgb = min(1.0, tex.rgb * (1.0 + _Glow));
                alpha = saturate(alpha * (1.0 + 0.5 * _Glow));

                // 광택: 벽돌 모양 안에서만 비스듬한 빛 띠가 왼쪽에서 오른쪽으로 지나가.
                half sheen = 0;
                if (_Sheen >= 0.0)
                {
                    float2 half_ = float2(FilmSize.x, FilmSize.y - Lip) * 0.5;
                    float2 q = abs(float2(px, py) - half_) - (half_ - CornerRadius);
                    float inside = saturate(0.5 - (length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - CornerRadius));
                    float center = -63.0 + 288.0 * _Sheen + FilmSize.y * (0.4 - 0.8 * py / FilmSize.y);
                    float band = saturate(1.0 - abs(px - center) / 28.8);
                    sheen = 0.35 * (1.0 - _Sheen) * band * inside;
                }
                half outAlpha = alpha + sheen * (1.0 - alpha);
                rgb = (rgb * alpha + sheen * (1.0 - alpha)) / max(outAlpha, 1e-4);
                return half4(rgb, outAlpha * _Fade) * input.color;
            }
            ENDHLSL
        }
    }
}
