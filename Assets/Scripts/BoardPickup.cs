using TMPro;
using UnityEngine;

namespace ElementalBreaker
{
    // 공이 통과하면 보상을 주고 풀로 돌아가.
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public sealed class BoardPickup : MonoBehaviour
    {
        [SerializeField, HideInInspector] private TMP_Text badgeLabel;
        private BrickBoard board;
        private CircleCollider2D hitbox;
        private bool available;
        private Vector3 centerOffset;
        public int Row { get; private set; } = -1;
        public int Column { get; private set; } = -1;
        public ElementType Element { get; private set; }

        public void Spawn(BrickBoard owner, int row, int column, float diameter,
            ElementType element)
        {
            board = owner;
            Row = row;
            Column = column;
            Element = element;
            hitbox = GetComponent<CircleCollider2D>();
            SpriteRenderer visual = GetComponent<SpriteRenderer>();
            Sprite sprite = ElementRules.GetPickupSprite(element);
            if (sprite != null)
                visual.sprite = sprite;
            Bounds bounds = visual.sprite != null ? visual.sprite.bounds : new Bounds(Vector3.zero, Vector3.one);
            float scale = diameter / Mathf.Max(0.001f, Mathf.Max(bounds.size.x, bounds.size.y));
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(scale, scale, 1f);
            centerOffset = bounds.center * scale;
            hitbox.offset = bounds.center;
            hitbox.radius = Mathf.Max(bounds.extents.x, bounds.extents.y);
            hitbox.isTrigger = true;
            hitbox.enabled = true;
            available = true;
            if (badgeLabel != null)
                badgeLabel.enabled = false;
            SetCellPosition(board.GetCellCenter(row, column));
            gameObject.SetActive(true);
        }

        public void SetRow(int row) => Row = row;
        public void SetCellPosition(Vector3 position) => transform.position = position - centerOffset;

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);
        private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        private void TryCollect(Collider2D other)
        {
            if (!available || !other.TryGetComponent(out Ball ball) || !ball.IsFlying)
                return;
            board.Collect(this);
        }

        public void Despawn()
        {
            available = false;
            if (hitbox != null)
                hitbox.enabled = false;
            Row = Column = -1;
            gameObject.SetActive(false);
        }
    }
}
