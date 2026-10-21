using DG.Tweening;
using UnityEngine;

namespace ElementalBreaker
{
    // 화면 흔들림은 여기 한 곳에서만 해. 흔들림이 겹쳐도 카메라가 원래 자리를 잃지 않아.
    public static class ScreenShake
    {
        private static Transform target;
        private static Vector3 basePosition;

        // 실제 시간으로 흔들어서 히트 스톱으로 게임이 느려져도 같은 빠르기야. 처음이 가장 세고 점점 약해져.
        public static void Play(float duration, float strength, int vibrato)
        {
            Camera main = Camera.main;
            if (main == null || duration <= 0f || strength <= 0f)
                return;
            if (target != main.transform)
            {
                target = main.transform;
                basePosition = target.position;
            }
            // 흔들리던 중이면 원래 자리로 돌려놓고 새로 시작해.
            target.DOKill();
            target.position = basePosition;
            target.DOShakePosition(duration, new Vector3(strength, strength * 0.8f, 0f), vibrato, 90f, false, true,
                    ShakeRandomnessMode.Harmonic)
                .SetUpdate(true);
        }
    }
}
