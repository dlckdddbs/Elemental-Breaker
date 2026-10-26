using DG.Tweening;
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
        private const string FrostName = "Frost Film";
        private const string BoostRimName = "Boost Rim";
        private const string BoostLabelName = "Boost Label";
        // 체력 2배 벽돌은 깨질 때까지 붉은 테두리가 남아. 벽돌보다 이만큼 크게 뒤에 깔아서 가장자리만 보여.
        private const float BoostRimPadding = 0.035f;
        private static readonly Color BoostRimColor = new Color32(0xFF, 0x3B, 0x4E, 0xFF);
        // 체력 2배 줄이 처음 보일 때 벽돌이 번쩍이고, 위에 "×2"가 톡 커지며 떠올랐다가 사라져.
        private const float BoostFlash = 0.6f;
        private const float BoostPopTime = 0.9f;
        private const float BoostPopRise = 0.18f;
        private const float BoostLabelSize = 2.6f;
        private static readonly Color BoostTextColor = new Color32(0xFF, 0x8A, 0x78, 0xFF);
        private static readonly Color BoostOutlineColor = new Color32(0x3A, 0x0A, 0x12, 0xFF);
        // "×2" 글자 재질은 모든 벽돌이 같이 써.
        private static Material boostLabelMaterial;
        private static readonly int FrostProgressId = Shader.PropertyToID("_Progress");
        private static readonly int FrostGlowId = Shader.PropertyToID("_Glow");
        private static readonly int FrostSheenId = Shader.PropertyToID("_Sheen");
        private static readonly int FrostFadeId = Shader.PropertyToID("_Fade");
        // 맞을 때 찌그러졌다 돌아오는 시간이야.
        private const float HitPoseTime = 0.12f;
        // 맞는 순간 흰색으로 섞이는 비율이야.
        private const float HitFlash = 0.45f;
        // 폭탄에 맞으면 완전히 하얗게 번쩍이고, 실제 시간으로 이만큼만 유지해.
        // 히트 스톱이 길어도 숫자와 속성이 금방 다시 보여.
        private const float BombFlashTime = 0.05f;
        // 폭탄이 터질 때 범위 안 벽돌이 폭발 중심 반대쪽으로 살짝 밀렸다 돌아와. 범위 밖 벽돌은 가만히 있어서 범위가 보여.
        // 게임 시간으로 움직여서 히트 스톱이 풀린 뒤에 밀려.
        private const float ShoveTime = 0.16f;
        private const float ShoveDistance = 0.07f;
        // 가운데 십자 벽돌은 더 세게 맞아서 더 오래 하얘지고 더 멀리 밀려. 따로 설명하지 않아도 센 칸이 보여.
        private const float StrongFlashTime = 0.08f;
        private const float StrongShoveDistance = 0.11f;

        private BoxCollider2D hitbox;
        private SpriteRenderer visual;
        private SpriteRenderer icon;
        // 얼음 아이템의 서리막이야. 처음 얼 때 한 번만 만들어.
        private SpriteRenderer frost;
        private MaterialPropertyBlock frostBlock;
        private bool destroyed;
        private BrickBoard board;
        private Vector2 baseSize;
        private Color baseColor = Color.white;
        private Vector3 labelBaseScale = Vector3.one;
        private bool labelScaleCached;
        private float hitTime = -1f;
        private float hitFlash = HitFlash;
        private float bombFlashStart = -1f;
        private float bombFlashTime = BombFlashTime;
        private Vector3 cellPosition;
        private Vector2 shoveDirection;
        private float shoveDistance = ShoveDistance;
        private float shoveTime = -1f;
        public int Row { get; private set; } = -1;
        public int Column { get; private set; } = -1;
        public int Health { get; private set; }
        public ElementType Element { get; private set; }
        public bool IsFrozen => frost != null && frost.enabled;
        public bool IsBoosted => boostRim != null && boostRim.enabled;
        private SpriteRenderer boostRim;
        private TextMeshPro boostLabel;
        // 보이는 벽돌 크기(월드)야.
        public Vector2 Size => baseSize;

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
            // 그리는 순서: 벽돌(+0) → 얼음 서리막(+1) → 속성 아이콘(+2) → 숫자(+3). 서리가 아이콘과 숫자를 가리지 않아.
            icon.sortingOrder = visual.sortingOrder + 2;
            if (healthLabel != null && healthLabel.TryGetComponent(out Renderer labelRenderer))
            {
                labelRenderer.sortingLayerID = visual.sortingLayerID;
                labelRenderer.sortingOrder = visual.sortingOrder + 3;
            }
        }

        // 얼음 서리막을 보여 줘. progress는 퍼진 정도(0~1.1), glow는 빛남, sheen은 광택 위치(음수면 없음), fade는 투명도야.
        public void SetFrost(Sprite film, Material material, float progress, float glow, float sheen, float fade)
        {
            if (destroyed || film == null)
                return;
            if (frost == null)
            {
                frost = new GameObject(FrostName).AddComponent<SpriteRenderer>();
                frost.transform.SetParent(transform, false);
                frost.sortingLayerID = visual.sortingLayerID;
                frost.sortingOrder = visual.sortingOrder + 1;
                frostBlock = new MaterialPropertyBlock();
            }
            if (frost.sprite != film)
                frost.sprite = film;
            if (frost.sharedMaterial != material)
                frost.sharedMaterial = material;
            // 서리막 이미지는 벽돌과 같은 크기(180x131)라 벽돌 크기에 맞춰 살짝만 늘려.
            Vector2 filmSize = film.rect.size / film.pixelsPerUnit;
            frost.transform.localScale = new Vector3(baseSize.x / filmSize.x, baseSize.y / filmSize.y, 1f);
            frost.GetPropertyBlock(frostBlock);
            frostBlock.SetFloat(FrostProgressId, progress);
            frostBlock.SetFloat(FrostGlowId, glow);
            frostBlock.SetFloat(FrostSheenId, sheen);
            frostBlock.SetFloat(FrostFadeId, fade);
            frost.SetPropertyBlock(frostBlock);
            frost.enabled = true;
        }

        public void HideFrost()
        {
            if (frost != null)
                frost.enabled = false;
        }

        // 체력 2배 디버프로 생긴 벽돌이야. 붉은 테두리를 켜고 끄고, 풀에서 다시 나올 때는 꺼.
        public void SetBoosted(bool boosted)
        {
            if (!boosted)
            {
                if (boostRim != null)
                    boostRim.enabled = false;
                if (boostLabel != null)
                {
                    DOTween.Kill(boostLabel);
                    boostLabel.gameObject.SetActive(false);
                }
                return;
            }
            if (boostRim == null)
            {
                boostRim = new GameObject(BoostRimName).AddComponent<SpriteRenderer>();
                boostRim.transform.SetParent(transform, false);
                boostRim.sharedMaterial = visual.sharedMaterial;
                boostRim.sortingLayerID = visual.sortingLayerID;
                boostRim.sortingOrder = visual.sortingOrder - 1;
                boostRim.drawMode = SpriteDrawMode.Sliced;
                boostRim.color = BoostRimColor;
            }
            boostRim.sprite = visual.sprite;
            boostRim.size = baseSize + Vector2.one * (BoostRimPadding * 2f);
            boostRim.enabled = true;
        }

        // 체력 2배 줄이 처음 보일 때야. 실제 시간으로 움직여.
        public void PlayBoostPop()
        {
            if (destroyed || healthLabel == null)
                return;
            hitTime = 0f;
            hitFlash = BoostFlash;
            if (boostLabel == null)
            {
                boostLabel = new GameObject(BoostLabelName, typeof(RectTransform)).AddComponent<TextMeshPro>();
                boostLabel.transform.SetParent(transform, false);
                boostLabel.rectTransform.sizeDelta = new Vector2(1f, 0.5f);
                boostLabel.font = healthLabel.font;
                if (boostLabelMaterial == null)
                {
                    boostLabelMaterial = new Material(healthLabel.fontSharedMaterial);
                    boostLabelMaterial.SetColor(ShaderUtilities.ID_OutlineColor, BoostOutlineColor);
                }
                boostLabel.fontSharedMaterial = boostLabelMaterial;
                boostLabel.fontSize = BoostLabelSize;
                boostLabel.alignment = TextAlignmentOptions.Center;
                boostLabel.color = BoostTextColor;
                boostLabel.text = "×2";
                if (boostLabel.TryGetComponent(out Renderer labelRenderer))
                {
                    labelRenderer.sortingLayerID = visual.sortingLayerID;
                    labelRenderer.sortingOrder = visual.sortingOrder + 5;
                }
            }
            DOTween.Kill(boostLabel);
            Transform label = boostLabel.transform;
            Vector3 start = new Vector3(baseSize.x * 0.25f, baseSize.y * 0.5f + 0.1f, 0f);
            label.localPosition = start;
            label.localScale = Vector3.one * 0.4f;
            boostLabel.alpha = 1f;
            boostLabel.gameObject.SetActive(true);
            DOTween.Sequence().SetTarget(boostLabel).SetUpdate(true)
                .Append(label.DOScale(1f, 0.2f).SetEase(Ease.OutBack))
                .Join(label.DOLocalMoveY(start.y + BoostPopRise, BoostPopTime).SetEase(Ease.OutQuad))
                .Insert(BoostPopTime * 0.6f, DOTween.To(() => boostLabel.alpha, a => boostLabel.alpha = a, 0f, BoostPopTime * 0.4f))
                .OnComplete(() => boostLabel.gameObject.SetActive(false));
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
            HideFrost();
            SetBoosted(false);

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

            RefreshIcon();
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

        private void RefreshIcon()
        {
            Sprite iconSprite = Element == ElementType.Normal ? null : ElementRules.GetIconSprite(Element);
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
            if (iconSprite == null)
                return;
            Vector2 half = iconSprite.bounds.extents;
            icon.transform.localPosition = new Vector3(-baseSize.x * 0.5f + iconInset + half.x,
                baseSize.y * 0.5f - iconInset - half.y, 0f);
        }

        // 상점의 "벽돌 속성 변경"으로 속성이 바뀔 때야. 한 번 번쩍여서 바뀐 걸 보여 줘.
        // flash는 바뀌는 순간 흰색으로 섞이는 비율이야. 속성 물결은 공에 맞을 때보다 밝게 번쩍여.
        public void SetElement(ElementType element, float flash = HitFlash)
        {
            if (destroyed || element == Element)
                return;
            Element = element;
            baseColor = ElementRules.GetBrickTint(element);
            RefreshIcon();
            hitTime = 0f;
            hitFlash = flash;
        }

        public void SetRow(int row) => Row = row;

        public void SetCellPosition(Vector3 center)
        {
            cellPosition = center;
            transform.position = center;
        }

        public void Despawn()
        {
            destroyed = true;
            hitbox.enabled = false;
            HideFrost();
            Row = Column = -1;
            gameObject.SetActive(false);
        }

        // 공이 아닌 데미지(폭탄 등)는 상성 없이 일반 연출로 처리해.
        public void TakeDamage(int damage) => ApplyDamage(damage, false, ElementType.Normal, transform.position);

        // 공에 맞았을 때야. 유리한 상성이면 공 속성 버스트를 띄워.
        public void TakeHit(int damage, ElementType attacker, Vector2 point) =>
            ApplyDamage(damage, ElementRules.HasAdvantage(attacker, Element), attacker, point, false);

        // 폭탄에 맞았을 때야. 큰 폭발 하나가 대표하니까, 벽돌마다 버스트를 또 터뜨리지 않아.
        // 깨지면 벽돌 조각이 폭발 중심(blastCenter)에서 멀어지는 쪽으로 튀어. strong은 가운데 십자 벽돌이야.
        public void TakeBombHit(int damage, ElementType attacker, Vector2 blastCenter, bool strong)
        {
            bombFlashTime = strong ? StrongFlashTime : BombFlashTime;
            shoveDistance = strong ? StrongShoveDistance : ShoveDistance;
            ApplyDamage(damage, ElementRules.HasAdvantage(attacker, Element), attacker, blastCenter, true);
        }

        // point는 공이면 맞은 자리, 폭탄이면 폭발 중심이야.
        private void ApplyDamage(int damage, bool advantage, ElementType attacker, Vector2 point, bool fromBomb = false)
        {
            if (destroyed || damage <= 0)
                return;

            Health = Mathf.Max(0, Health - damage);
            if (!fromBomb)
            {
                if (Health == 0)
                    GameAudio.Play(SoundCue.Break);
                else
                    GameAudio.Hit(attacker);
            }
            RefreshLabel();
            BrickEffects effects = board != null ? board.Effects : null;
            if (Health > 0)
            {
                hitTime = 0f;
                if (fromBomb)
                {
                    hitFlash = 0f;
                    bombFlashStart = Time.unscaledTime;
                    // 가운데 칸은 밀 방향이 없어서 제자리에 있어.
                    Vector2 away = (Vector2)cellPosition - point;
                    if (away.sqrMagnitude > 0.0001f)
                    {
                        shoveDirection = away.normalized;
                        shoveTime = 0f;
                    }
                    return;
                }
                hitFlash = HitFlash;
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
                effects.PlayBreak(transform.position, baseSize, Element, advantage, attacker, fromBomb, point);
            // 얼어 있던 벽돌은 서리도 같이 사라지며 얼음 조각이 튀어.
            if (IsFrozen && board != null && board.Ice != null)
                board.Ice.PlayShatter(transform.position, baseSize);
            if (board != null)
                board.Release(this);
            else
                Destroy(gameObject);
        }

        private void Update()
        {
            if (hitTime < 0f && bombFlashStart < 0f && shoveTime < 0f)
                return;

            if (shoveTime >= 0f)
            {
                shoveTime += Time.deltaTime;
                float k = shoveTime / ShoveTime;
                Vector2 offset = Vector2.zero;
                if (k >= 1f)
                    shoveTime = -1f;
                else
                    offset = shoveDirection * (shoveDistance * Mathf.Sin(k * Mathf.PI));
                // 콜라이더는 제자리에 두고 보이는 것만 밀어서 공의 충돌에는 영향이 없어.
                transform.position = cellPosition + (Vector3)offset;
                hitbox.offset = -offset;
            }

            float flash = 0f;
            if (bombFlashStart >= 0f)
            {
                // 폭탄 번쩍임은 히트 스톱과 상관없이 실제 시간으로 짧게 끝나.
                flash = 1f - (Time.unscaledTime - bombFlashStart) / bombFlashTime;
                if (flash <= 0f)
                {
                    flash = 0f;
                    bombFlashStart = -1f;
                }
            }
            if (hitTime >= 0f)
            {
                // 콜라이더는 그대로 두고 보이는 크기만 바꿔서 물리에는 영향이 없어.
                hitTime += Time.deltaTime;
                float k = hitTime / HitPoseTime;
                if (k >= 1f)
                {
                    hitTime = -1f;
                    visual.size = baseSize;
                    if (healthLabel != null)
                        healthLabel.transform.localScale = labelBaseScale;
                }
                else
                {
                    float wobble = Mathf.Sin(k * Mathf.PI);
                    visual.size = new Vector2(baseSize.x * (1f + 0.06f * wobble), baseSize.y * (1f - 0.08f * wobble));
                    flash = Mathf.Max(flash, hitFlash * (1f - k));
                    if (healthLabel != null)
                        healthLabel.transform.localScale = labelBaseScale * (1f + 0.35f * wobble);
                }
            }
            visual.color = Color.Lerp(baseColor, Color.white, flash);
        }

        private void ResetHitPose()
        {
            hitTime = -1f;
            bombFlashStart = -1f;
            shoveTime = -1f;
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
