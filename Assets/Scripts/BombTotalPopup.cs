using TMPro;
using UnityEngine;

namespace ElementalBreaker
{
    // 폭탄이 벽돌에서 실제로 깎은 체력의 합을 폭발 자리 위에 잠깐 띄워. [✦ 42]
    // 0부터 빠르게 올라가며 톡 커지고, 잠깐 머물다 떠오르며 사라져. 히트 스톱 중에도 움직여야 해서 실제 시간을 써.
    public sealed class BombTotalPopup : MonoBehaviour
    {
        // 시간은 숫자가 나타난 순간부터 세. 폭발이 터지고(히트 스톱이 끝나고) AppearDelay 뒤에 나타나.
        private const float AppearDelay = 0.05f;
        private const float CountTime = 0.25f;
        private const float SettleTime = 0.15f;
        private const float FadeStart = 0.7f;
        private const float FadeTime = 0.35f;
        // 작게 나타나서 다 올라가는 순간 PopScale까지 커졌다가 1로 돌아와.
        private const float StartScale = 0.6f;
        private const float PopScale = 1.25f;
        // 폭발 자리에서 Raise만큼 위에 띄우고, 사라지면서 Rise만큼 더 떠올라.
        // 맨 위 줄에서 터져도 위쪽 벽에 닿지 않게 보드 위 끝에서 TopMargin 아래까지만 올리고, 양옆은 보드 끝에서 SideMargin 안쪽으로 당겨.
        private const float Raise = 0.45f;
        private const float Rise = 0.4f;
        private const float TopMargin = 0.35f;
        private const float SideMargin = 0.7f;
        // 기준 화면(480x854)에서 글자가 46px쯤이야. 별은 아이템 칸 데미지 알약과 같은 그림이야.
        private const float FontSize = 5.2f;
        private const float StarSize = 0.37f;
        private const float StarGap = 0.05f;
        private const float GlowSize = 1.5f;
        private const float GlowAlpha = 0.45f;
        private const float OutlineWidth = 0.3f;
        private static readonly Color OutlineColor = new Color32(0x1E, 0x1B, 0x3D, 0xFF);
        // 글자 위쪽은 밝은 색, 아래쪽은 폭탄 속성색이고 뒤에 속성색 빛을 깔아. 일반, 불, 물, 풀 순서야.
        private static readonly Color32[] TopColors =
        {
            new Color32(0xFF, 0xFF, 0xFF, 0xFF), new Color32(0xFF, 0xF1, 0xA8, 0xFF),
            new Color32(0xE8, 0xFB, 0xFF, 0xFF), new Color32(0xF6, 0xFF, 0xC4, 0xFF)
        };
        private static readonly Color32[] BottomColors =
        {
            new Color32(0xBF, 0xD0, 0xEE, 0xFF), new Color32(0xFF, 0x7A, 0x2E, 0xFF),
            new Color32(0x3B, 0xB4, 0xFF, 0xFF), new Color32(0x5B, 0xD8, 0x3A, 0xFF)
        };
        private static readonly Color32[] GlowColors =
        {
            new Color32(0xBF, 0xD0, 0xEE, 0xFF), new Color32(0xFF, 0x6E, 0x28, 0xFF),
            new Color32(0x28, 0xA0, 0xFF, 0xFF), new Color32(0x5A, 0xD2, 0x3C, 0xFF)
        };

        private BrickBoard board;
        private Transform body;
        private SpriteRenderer glow;
        private SpriteRenderer star;
        private TextMeshPro label;
        private Color glowColor;
        private Vector3 basePosition;
        private float appearTime = -1f;
        private int total;
        private int shownValue = -1;
        private bool popped;
        private bool visible = true;

        // labelStyle은 벽돌 숫자야. 같은 글꼴과 외곽선 재질을 복사해서 외곽선만 진하고 두껍게 바꿔.
        public void Initialize(BrickBoard owner, Material material, TMP_Text labelStyle, int sortingLayerId, int baseOrder)
        {
            board = owner;
            // 빛은 그대로 두고 별과 숫자만 커졌다 작아지게, 둘을 따로 묶어.
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            glow = CreateRenderer("Glow", transform, material, sortingLayerId, baseOrder);
            glow.sprite = ElementRules.LoadEffectSprite("puff");
            if (glow.sprite != null)
                glow.transform.localScale = Vector3.one * (GlowSize / (glow.sprite.rect.width / glow.sprite.pixelsPerUnit));
            star = CreateRenderer("Star", body, material, sortingLayerId, baseOrder + 1);
            star.sprite = ElementRules.LoadEffectSprite("bomb_star");
            if (star.sprite != null)
                star.transform.localScale = Vector3.one * (StarSize / (star.sprite.rect.width / star.sprite.pixelsPerUnit));

            label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshPro>();
            label.transform.SetParent(body, false);
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(3f, 1f);
            if (labelStyle != null)
            {
                label.font = labelStyle.font;
                Material outline = new Material(labelStyle.fontSharedMaterial);
                outline.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);
                outline.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
                label.fontSharedMaterial = outline;
            }
            label.fontSize = FontSize;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableVertexGradient = true;
            label.color = Color.white;
            if (label.TryGetComponent(out Renderer labelRenderer))
            {
                labelRenderer.sortingLayerID = sortingLayerId;
                labelRenderer.sortingOrder = baseOrder + 1;
            }
            SetVisible(false);
        }

        // center는 폭발 자리, delay는 누른 순간부터 폭발이 터질 때까지(히트 스톱) 걸리는 시간이야.
        public void Play(Vector2 center, ElementType element, int damage, float delay)
        {
            total = damage;
            shownValue = -1;
            popped = false;
            if (total <= 0)
            {
                appearTime = -1f;
                SetVisible(false);
                return;
            }
            Rect rect = board.BoardRect;
            float x = Mathf.Clamp(center.x, rect.xMin + SideMargin, rect.xMax - SideMargin);
            float y = Mathf.Min(center.y + Raise, rect.yMax - TopMargin);
            basePosition = new Vector3(x, y, transform.position.z);
            label.colorGradient = new VertexGradient(TopColors[(int)element], TopColors[(int)element],
                BottomColors[(int)element], BottomColors[(int)element]);
            glowColor = GlowColors[(int)element];
            appearTime = Time.unscaledTime + delay + AppearDelay;
            Update();
        }

        private void Update()
        {
            if (appearTime < 0f)
                return;
            float t = Time.unscaledTime - appearTime;
            if (t < 0f)
            {
                SetVisible(false);
                return;
            }
            if (t >= FadeStart + FadeTime)
            {
                appearTime = -1f;
                SetVisible(false);
                return;
            }
            SetVisible(true);

            float count = EffectMath.EaseOut(t / CountTime);
            SetValue(Mathf.RoundToInt(total * count));
            // 다 올라가서 톡 커지는 순간 한 번 소리를 내.
            if (t >= CountTime && !popped)
            {
                popped = true;
                GameAudio.Play(SoundCue.BombTotal);
            }
            float scale = t < CountTime
                ? Mathf.Lerp(StartScale, PopScale, count)
                : Mathf.Lerp(PopScale, 1f, EffectMath.EaseOut((t - CountTime) / SettleTime));
            body.localScale = new Vector3(scale, scale, 1f);

            float fade = Mathf.Clamp01((t - FadeStart) / FadeTime);
            transform.position = basePosition + Vector3.up * (Rise * EffectMath.EaseOut(fade));
            float alpha = 1f - fade;
            label.alpha = alpha;
            star.color = new Color(1f, 1f, 1f, alpha);
            glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, GlowAlpha * alpha);
        }

        // 숫자가 바뀔 때만 다시 재서 [별 숫자] 묶음이 가운데에 오게 놓아.
        private void SetValue(int value)
        {
            if (value == shownValue)
                return;
            shownValue = value;
            string text = value.ToString();
            label.text = text;
            float width = label.GetPreferredValues(text).x;
            float left = -(StarSize + StarGap + width) * 0.5f;
            star.transform.localPosition = new Vector3(left + StarSize * 0.5f, 0f, 0f);
            label.rectTransform.localPosition = new Vector3(left + StarSize + StarGap, 0f, 0f);
        }

        private void SetVisible(bool on)
        {
            if (visible == on)
                return;
            visible = on;
            // 글자는 컴포넌트만 끄면 마지막 모양이 남을 수 있어서 별과 숫자 묶음째 켜고 꺼.
            glow.enabled = on;
            body.gameObject.SetActive(on);
        }

        private static SpriteRenderer CreateRenderer(string objectName, Transform parent, Material material, int layerId, int order)
        {
            SpriteRenderer renderer = new GameObject(objectName).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = layerId;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
