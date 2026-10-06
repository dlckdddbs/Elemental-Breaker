using UnityEngine;

namespace ElementalBreaker
{
    // 하단 영역에 들어온 공을 발사 위치로 회수해.
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class BallReturnZone : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryReturn(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryReturn(other);
        }

        private static void TryReturn(Collider2D other)
        {
            // 발사할 때 영역이 겹쳐도 올라가는 공은 회수하지 않아.
            if (other.TryGetComponent(out Ball ball) && ball.IsDescending)
                ball.ReturnToLaunch();
        }
    }
}
