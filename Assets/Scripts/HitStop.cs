using UnityEngine;

namespace ElementalBreaker
{
    // 아주 잠깐 게임 시간을 느리게 해서 타격감을 줘. 실제 시간 기준으로 끝나서 멈춘 채로 남지 않아.
    public sealed class HitStop : MonoBehaviour
    {
        private float remaining;
        private float restoreScale = 1f;
        private bool active;

        public void Play(float duration, float timeScale)
        {
            if (duration <= 0f)
                return;
            if (!active)
                restoreScale = Time.timeScale;
            active = true;
            remaining = Mathf.Max(remaining, duration);
            Time.timeScale = Mathf.Clamp01(timeScale);
        }

        private void Update()
        {
            if (!active)
                return;
            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0f)
                Restore();
        }

        private void Restore()
        {
            active = false;
            remaining = 0f;
            Time.timeScale = restoreScale;
        }

        private void OnDisable()
        {
            if (active)
                Restore();
        }
    }
}
