using UnityEngine;

namespace ElementalBreaker
{
    // 이펙트들이 같이 쓰는 움직임 계산이야.
    internal static class EffectMath
    {
        // 처음 속도 velocity로 출발해서 중력(gravity, 아래쪽)과 공기 저항(drag, 0보다 커야 해)을 받는 물체가 t초 동안 움직인 거리야.
        // 매 프레임 더해 가지 않고 시간으로 바로 계산해서, 프레임이 끊겨도 같은 궤적이 나와.
        public static Vector2 Drift(Vector2 velocity, float gravity, float drag, float t)
        {
            float decay = Mathf.Exp(-drag * t);
            float spread = (1f - decay) / drag;
            float terminal = -gravity / drag;
            return new Vector2(velocity.x * spread, (velocity.y - terminal) * spread + terminal * t);
        }

        // t초 뒤의 속도야. 날아가는 방향으로 파편을 돌릴 때 써.
        public static Vector2 VelocityAt(Vector2 velocity, float gravity, float drag, float t)
        {
            float decay = Mathf.Exp(-drag * t);
            float terminal = -gravity / drag;
            return new Vector2(velocity.x * decay, terminal + (velocity.y - terminal) * decay);
        }

        public static float EaseOut(float k)
        {
            k = Mathf.Clamp01(k);
            return 1f - (1f - k) * (1f - k);
        }
    }
}
