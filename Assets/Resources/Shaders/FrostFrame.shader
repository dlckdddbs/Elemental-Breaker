// 보드 서리 테두리 셰이더야. 9-slice Tiled 스프라이트를 URP 2D Sprite-Unlit-Default처럼 그리고, 월드 위치로 세 가지를 정해.
// 1) 세로 밝기: 위는 진하고 양옆은 아래로 갈수록, 아래쪽은 가장 옅게 (_Fade = 위, 중간, 아래 배율)
// 2) 드러남: 서리 파동이 지나간 원 안만 보여 (_Reveal = 중심 x, y, 반지름, 경계 부드러움)
// 3) 테두리 띠: _Band가 0보다 크면 바깥 가장자리에서 그 거리 안쪽만 그려. 턴 끝 반짝임이 보드 안쪽까지 번지지 않게 해.
// 섞는 방식은 재질마다 정해서 같은 셰이더로 일반 테두리와 더하기 반짝임을 둘 다 그려.
Shader "Elemental Breaker/Frost Frame"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Rect ("Frame Rect (left, bottom, right, top)", Vector) = (-3, -3, 3, 4)
        _Fade ("Fade (top, middle, bottom)", Vector) = (1, 1, 1, 0)
        _Reveal ("Reveal (center x, y, radius, softness)", Vector) = (0, 0, 1000, 0.05)
        _Band ("Edge Band", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendAlpha ("Src Blend Alpha", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendAlpha ("Dst Blend Alpha", Float) = 10

        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex FrameVertex
            #pragma fragment FrameFragment
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Rect;
                float4 _Fade;
                float4 _Reveal;
                float _Band;
                float _SrcBlend;
                float _DstBlend;
                float _SrcBlendAlpha;
                float _DstBlendAlpha;
            CBUFFER_END

            Varyings FrameVertex(Attributes input)
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

            half4 FrameFragment(Varyings input) : SV_Target
            {
                half4 c = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float2 p = input.worldPos.xy;

                // 세로 밝기: 아래(0)에서 위(1)까지. 55% 높이가 중간 배율이야.
                float t = saturate((p.y - _Rect.y) / max(_Rect.w - _Rect.y, 1e-4));
                float fade = t > 0.55 ? lerp(_Fade.y, _Fade.x, (t - 0.55) / 0.45) : lerp(_Fade.z, _Fade.y, t / 0.55);
                c.a *= fade;

                // 드러남: 파동 원 안쪽만 보여.
                float d = distance(p, _Reveal.xy);
                c.a *= saturate((_Reveal.z - d) / max(_Reveal.w, 1e-4));

                // 테두리 띠: 가장 가까운 바깥 가장자리까지의 거리로 잘라.
                float edge = min(min(p.x - _Rect.x, _Rect.z - p.x), min(p.y - _Rect.y, _Rect.w - p.y));
                c.a *= _Band > 0.0 ? saturate((_Band - edge) / (_Band * 0.3)) : 1.0;
                return c;
            }
            ENDHLSL
        }
    }
}
