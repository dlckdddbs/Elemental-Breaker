using UnityEngine;

namespace ElementalBreaker
{
    // 벽돌 체력을 관리하고 체력이 0이면 없애.
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class Brick : MonoBehaviour
    {
        [SerializeField, Min(1)] private int startingHealth = 3;
        [SerializeField] private TextMesh healthLabel;

        private BoxCollider2D hitbox;
        private bool destroyed;
        public int Health { get; private set; }

        private void Awake()
        {
            hitbox = GetComponent<BoxCollider2D>();
            if (healthLabel == null)
                healthLabel = GetComponentInChildren<TextMesh>();
            Health = Mathf.Max(1, startingHealth);
            RefreshLabel();
        }

        public void TakeDamage(int damage)
        {
            if (destroyed || damage <= 0)
                return;

            Health = Mathf.Max(0, Health - damage);
            RefreshLabel();
            if (Health > 0)
                return;

            // 같은 물리 프레임에 다시 맞아도 파괴는 한 번만 처리해.
            destroyed = true;
            hitbox.enabled = false;
            Destroy(gameObject);
        }

        private void RefreshLabel()
        {
            if (healthLabel != null)
                healthLabel.text = Health.ToString();
        }
    }
}
