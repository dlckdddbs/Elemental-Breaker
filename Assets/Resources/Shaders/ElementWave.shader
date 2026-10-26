// 벽돌 속성 변경 물결 셰이더야. 사각형 하나에 월드 위치로 원을 그려서, 물결이 커져도 선 두께가 그대로야.
// 테두리나 재질 없이 밝은 앞선 + 부드러운 빛 + 뒤로 옅게 남는 속성색만 그려.
// 빛을 더하는 방식(SrcAlpha One)으로 그려서 어두운 배경에서 은은하게 빛나. 색은 바뀔 속성에 맞춰 재질에 넣어.
Shader "Elemental Breaker/Element Wave"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Wave ("Wave (center x, y, radius, alpha)", Vector) = (0, 0, 1, 1)
        _Line ("Line (core width, glow width, trail width)", Vector) = (0.035, 0.22, 1.5, 0)
        _MainColor ("Trail Color", Color) = (0.4, 0.87, 0.23, 1)
        _LightColor ("Line Color", Color) = (0.85, 1, 0.6, 1)
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
                half4 _MainColor;
                half4 _LightColor;
            CBUFFER_END

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
                float d = distance(input.worldPos.xy, _Wave.xy);
                float radius = _Wave.z;
                float x = d - radius;
                float aa = max(fwidth(d), 1e-4);
                // 뒤로 남는 빛: 선 안쪽 trail 폭에서 서서히 차오르다 선 바로 앞에서 빠져.
                float h = (d - (radius - _Line.z)) / _Line.z;
                float trail = h <= 0.0 || h >= 1.0 ? 0.0 : (h < 0.85 ? h / 0.85 : (1.0 - h) / 0.15) * 0.28;
                // 부드러운 빛과 밝은 앞선
                float glow = 0.35 * saturate(1.0 - abs(x) / (_Line.y * 0.5));
                float core = 0.85 * saturate((_Line.x * 0.5 - abs(x)) / aa + 0.5);
                float total = trail + glow + core;
                half3 rgb = (_MainColor.rgb * trail + _LightColor.rgb * (glow + core)) / max(total, 1e-4);
                return half4(rgb, saturate(total * _Wave.w)) * input.color;
            }
            ENDHLSL
        }
    }
}
