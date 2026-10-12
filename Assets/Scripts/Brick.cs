using TMPro;
using UnityEngine;

namespace ElementalBreaker
{
    // 체력과 보드 칸을 관리하고 파괴되면 풀로 돌아가.
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class Brick : MonoBehaviour
    {
        [SerializeField, Min(1)] private int startingHealth = 3;
        [SerializeField] private TMP_Text healthLabel;

        private BoxCollider2D hitbox;
        private bool destroyed;
        private BrickBoard board;
        private Vector3 spriteCenterOffset;
        public int Row { get; private set; } = -1;
        public int Column { get; private set; } = -1;
        public int Health { get; private set; }
        public bool GrantsBall { get; private set; }
        public ElementType Element { get; private set; }

        private void Awake()
        {
            CacheComponents();
            if (board == null)
                Health = Mathf.Max(1, startingHealth);
            RefreshLabel();
        }

        private void CacheComponents()
        {
            hitbox = GetComponent<BoxCollider2D>();
            // 연결이 비어 있으면 자식의 HP 텍스트를 찾아 써.
            if (healthLabel == null)
                healthLabel = GetComponentInChildren<TMP_Text>(true);
        }

        public void Spawn(BrickBoard owner, int row, int column, int health, Vector2 size, bool grantsBall = false,
            ElementType element = ElementType.Normal)
        {
            CacheComponents();
            board = owner;
            GrantsBall = grantsBall;
            Element = element;
            Row = row;
            Column = column;
            Health = Mathf.Max(1, health);
            destroyed = false;
            SpriteRenderer visual = GetComponent<SpriteRenderer>();
            Sprite elementSprite = ElementRules.GetSprite(element, false);
            if (elementSprite != null)
                visual.sprite = elementSprite;
            Bounds bounds = visual.sprite.bounds;
            visual.drawMode = SpriteDrawMode.Simple;
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, 1f);
            spriteCenterOffset = Vector3.Scale(bounds.center, transform.localScale);
            hitbox.offset = bounds.center;
            hitbox.size = bounds.size;
            hitbox.isTrigger = false;
            hitbox.enabled = true;
            SetCellPosition(owner.GetCellCenter(row, column));
            gameObject.SetActive(true);
            RefreshLabel();
        }

        public void SetRow(int row) => Row = row;

        public void SetCellPosition(Vector3 center) => transform.position = center - spriteCenterOffset;

        public void Despawn()
        {
            destroyed = true;
            hitbox.enabled = false;
            Row = Column = -1;
            gameObject.SetActive(false);
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
            if (board != null)
                board.Release(this);
            else
                Destroy(gameObject);
        }

        private void RefreshLabel()
        {
            if (healthLabel != null)
                healthLabel.SetText("{0}", Health);
        }
    }
}
