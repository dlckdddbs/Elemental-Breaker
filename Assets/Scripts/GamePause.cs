using UnityEngine;

namespace ElementalBreaker
{
    // 일시 정지야. 게임 시간을 멈추고, 풀 때 멈추기 전 속도(히트 스톱 중이면 느린 속도)로 돌려놔.
    // 히트 스톱은 실제 시간으로 끝나서, 멈춰 있는 동안에는 시간을 풀지 않게 이 상태를 확인해.
    public static class GamePause
    {
        private static float resumeScale = 1f;

        public static bool IsPaused { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsPaused = false;
            resumeScale = 1f;
        }

        public static void Pause()
        {
            if (IsPaused)
                return;
            IsPaused = true;
            resumeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        public static void Resume()
        {
            if (!IsPaused)
                return;
            IsPaused = false;
            Time.timeScale = resumeScale;
        }

        // 씬을 떠날 때야. 게임 시간은 씬이 바뀌어도 남아서, 보통 속도로 꼭 돌려놔.
        public static void Clear()
        {
            IsPaused = false;
            resumeScale = 1f;
            Time.timeScale = 1f;
        }
    }
}
