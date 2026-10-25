// 얼음 아이템의 서리 파동 셰이더야. 사각형 하나에 월드 위치로 원을 그려서, 파동이 커져도 선 두께가 그대로야.
// 뾰족한 결정 없이 얇은 서리선 + 부드러운 빛 + 안쪽으로 옅게 번지는 냉기 + 선 위의 아주 작은 서리 알갱이만 그려.
// 빛을 더하는 방식(SrcAlpha One)으로 그려서 어두운 배경에서 은은하게 빛나.
Shader "Elemental Breaker/Frost Wave"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Wave ("Wave (center x, y, radius, alpha)", Vector) = (0, 0, 1, 1)
        _Line ("Line (core width, glow width, haze width)", Vector) = (0.02, 0.1, 0.9, 0)

        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha One, Zero One
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex WaveVertex
            #pragma fragment WaveFragment
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
                float3 worldPos : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Wave;
                float4 _Line;
            CBUFFER_END

            // 파동 색: 냉기와 빛은 옅은 하늘색, 서리선은 거의 흰색이야.
            static const half3 HazeColor = half3(0.75, 0.94, 1.0);
            static const half3 CoreColor = half3(0.91, 0.98, 1.0);
            // 선 위 서리 알갱이 수와 크기(월드)야.
            static const float GrainCount = 36.0;

            float Hash(float n) { return frac(sin(n * 12.9898) * 43758.5453); }

            Varyings WaveVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.worldPos = TransformObjectToWorld(input.positionOS);
                o.uv = input.uv;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 WaveFragment(Varyings input) : SV_Target
            {
                float2 p = input.worldPos.xy - _Wave.xy;
                float d = length(p);
                float radius = _Wave.z;
                float x = d - radius;
                float aa = max(fwidth(d), 1e-4);

                // 안쪽 냉기: 선 안쪽 haze 폭에서 서서히 차오르다 선 바로 앞에서 빠져.
                float h = (d - (radius - _Line.z)) / _Line.z;
                float haze = h <= 0.0 || h >= 1.0 ? 0.0 : (h < 0.85 ? h / 0.85 : (1.0 - h) / 0.15) * 0.07;
                // 부드러운 빛과 얇은 서리선
                float glow = 0.16 * saturate(1.0 - abs(x) / (_Line.y * 0.5));
                float core = 0.6 * saturate((_Line.x * 0.5 - abs(x)) / aa + 0.5);

                // 서리 알갱이: 원을 36칸으로 나눠 칸마다 하나씩, 자리와 크기는 칸 번호로 정해서 커져도 그대로야.
                float angle = atan2(p.y, p.x) + PI;
                float cell = floor(angle / TWO_PI * GrainCount);
                float grain = 0.0;
                for (int k = -1; k <= 1; k++)
                {
                    float c = fmod(cell + k + GrainCount, GrainCount);
                    float a = (c + Hash(c)) / GrainCount * TWO_PI - PI;
                    float r = radius + (Hash(c + 17.3) - 0.5) * 0.05;
                    float size = 0.0075 + Hash(c + 41.7) * 0.01;
                    float gd = distance(p, float2(cos(a), sin(a)) * r);
                    grain = max(grain, 0.45 * saturate((size - gd) / aa + 0.5));
                }

                float total = haze + glow + core + grain;
                half3 rgb = (HazeColor * (haze + glow) + CoreColor * core + grain) / max(total, 1e-4);
                return half4(rgb, saturate(total * _Wave.w)) * input.color;
            }
            ENDHLSL
        }
    }
}
