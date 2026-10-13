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
        [Tooltip("벽돌 왼쪽 위 모서리에서 속성 아이콘까지의 여백")]
        [SerializeField, Min(0f)] private float iconInset = 0.04f;

        private const string IconName = "Element Icon";
        // 맞을 때 찌그러졌다 돌아오는 시간이야.
        private const float HitPoseTime = 0.12f;

        private BoxCollider2D hitbox;
        private SpriteRenderer visual;
        private SpriteRenderer icon;
        private bool destroyed;
        private BrickBoard board;
        private Vector2 baseSize;
        private Color baseColor = Color.white;
        private Vector3 labelBaseScale = Vector3.one;
        private bool labelScaleCached;
        private float hitTime = -1f;
        public int Row { get; private set; } = -1;
        public int Column { get; private set; } = -1;
        public int Health { get; private set; }
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
            visual = GetComponent<SpriteRenderer>();
            // 연결이 비어 있으면 자식의 HP 텍스트를 찾아 써.
            if (healthLabel == null)
                healthLabel = GetComponentInChildren<TMP_Text>(true);
            if (healthLabel != null && !labelScaleCached)
            {
                labelBaseScale = healthLabel.transform.localScale;
                labelScaleCached = true;
            }
        }

        private void EnsureIcon()
        {
            if (icon != null)
                return;

            // 하이어라키를 건드리지 않게 풀의 벽돌마다 실행 중에 한 번만 만들어.
            Transform existing = transform.Find(IconName);
            icon = existing != null
                ? existing.GetComponent<SpriteRenderer>()
                : new GameObject(IconName).AddComponent<SpriteRenderer>();
            icon.transform.SetParent(transform, false);
            icon.sharedMaterial = visual.sharedMaterial;
            icon.sortingLayerID = visual.sortingLayerID;
            icon.sortingOrder = visual.sortingOrder + 1;
            // 숫자는 벽돌과 아이콘 위에 그려.
            if (healthLabel != null && healthLabel.TryGetComponent(out Renderer labelRenderer))
            {
                labelRenderer.sortingLayerID = visual.sortingLayerID;
                labelRenderer.sortingOrder = visual.sortingOrder + 2;
            }
        }

        public void Spawn(BrickBoard owner, int row, int column, int health, Vector2 size,
            ElementType element = ElementType.Normal)
        {
            CacheComponents();
            board = owner;
            Element = element;
            Row = row;
            Column = column;
            Health = Mathf.Max(1, health);
            destroyed = false;
            EnsureIcon();

            // 스케일 대신 9-slice로 늘려서 모서리와 아이콘, 숫자가 찌그러지지 않게 해.
            Sprite baseSprite = ElementRules.GetBrickSprite();
            if (baseSprite != null)
                visual.sprite = baseSprite;
            visual.drawMode = SpriteDrawMode.Sliced;
            baseSize = size;
            baseColor = ElementRules.GetBrickTint(element);
            ResetHitPose();
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            hitbox.offset = Vector2.zero;
            hitbox.size = size;
            hitbox.isTrigger = false;
            hitbox.enabled = true;

            Sprite iconSprite = element == ElementType.Normal ? null : ElementRules.GetIconSprite(element);
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
            if (iconSprite != null)
            {
                Vector2 half = iconSprite.bounds.extents;
                icon.transform.localPosition = new Vector3(-size.x * 0.5f + iconInset + half.x,
                    size.y * 0.5f - iconInset - half.y, 0f);
            }
            if (healthLabel != null)
            {
                // 예전 이미지 기준으로 잡힌 텍스트 영역을 벽돌 크기에 맞춰.
                healthLabel.rectTransform.anchoredPosition = Vector2.zero;
                healthLabel.rectTransform.sizeDelta = size;
            }

            SetCellPosition(owner.GetCellCenter(row, column));
            gameObject.SetActive(true);
            RefreshLabel();
        }

        public void SetRow(int row) => Row = row;

        public void SetCellPosition(Vector3 center) => transform.position = center;

        public void Despawn()
        {
            destroyed = true;
            hitbox.enabled = false;
            Row = Column = -1;
            gameObject.SetActive(false);
        }

        // 공이 아닌 데미지(폭탄 등)는 상성 없이 일반 연출로 처리해.
        public void TakeDamage(int damage) => ApplyDamage(damage, false, ElementType.Normal, transform.position);

        // 공에 맞았을 때야. 유리한 상성이면 공 속성 버스트를 띄워.
        public void TakeHit(int damage, ElementType attacker, Vector2 point) =>
            ApplyDamage(damage, ElementRules.HasAdvantage(attacker, Element), attacker, point);

        private void ApplyDamage(int damage, bool advantage, ElementType attacker, Vector2 point)
        {
            if (destroyed || damage <= 0)
                return;

            Health = Mathf.Max(0, Health - damage);
            RefreshLabel();
            BrickEffects effects = board != null ? board.Effects : null;
            if (Health > 0)
            {
                hitTime = 0f;
                if (advantage && effects != null)
                    effects.PlaySmallBurst(attacker, point);
                return;
            }

            // 같은 물리 프레임에 다시 맞아도 파괴는 한 번만 처리해.
            destroyed = true;
            hitbox.enabled = false;
            if (board != null)
                board.AwardBreak(advantage);
            if (effects != null)
                effects.PlayBreak(transform.position, baseSize, Element, advantage, attacker);
            if (board != null)
                board.Release(this);
            else
                Destroy(gameObject);
        }

        private void Update()
        {
            if (hitTime < 0f)
                return;

            // 콜라이더는 그대로 두고 보이는 크기만 바꿔서 물리에는 영향이 없어.
            hitTime += Time.deltaTime;
            float k = hitTime / HitPoseTime;
            if (k >= 1f)
            {
                ResetHitPose();
                return;
            }
            float wobble = Mathf.Sin(k * Mathf.PI);
            visual.size = new Vector2(baseSize.x * (1f + 0.06f * wobble), baseSize.y * (1f - 0.08f * wobble));
            visual.color = Color.Lerp(baseColor, Color.white, 0.45f * (1f - k));
            if (healthLabel != null)
                healthLabel.transform.localScale = labelBaseScale * (1f + 0.35f * wobble);
        }

        private void ResetHitPose()
        {
            hitTime = -1f;
            visual.size = baseSize;
            visual.color = baseColor;
            if (healthLabel != null)
                healthLabel.transform.localScale = labelBaseScale;
        }

        private void RefreshLabel()
        {
            if (healthLabel != null)
                healthLabel.SetText("{0}", Health);
        }
    }
}
