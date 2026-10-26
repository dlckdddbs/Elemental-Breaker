using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker
{
    // 왼쪽 아래 아이템 칸이야. 들고 있는 아이템을 보여 주고, 쓸 수 있을 때 누르면 써.
    public sealed class ItemSlotButton : MonoBehaviour
    {
        // 쓸 수 있을 때 테두리 빛이 숨 쉬듯 깜빡여. 밝음 → 어두움까지 걸리는 시간과 가장 어두울 때 투명도야.
        private const float GlowPulseTime = 0.5f;
        private const float GlowMinAlpha = 0.1f;
        // 아이템이 칸에 들어올 때 톡 튀는 크기와 시간이야.
        private const float ArrivePopScale = 0.18f;
        private const float ArrivePopTime = 0.28f;
        private static readonly Color32 ReadyLabelColor = new Color32(0xFF, 0xE4, 0x8A, 0xFF);
        // 폭탄이 취소되면 칸이 옆으로 흔들리고, 빛이 빨갛게 번쩍였다가 금색으로 돌아와.
        private const float ShakeDistance = 6f;
        private const float ShakeTime = 0.3f;
        private const float DeniedFlashTime = 0.6f;
        private static readonly Color DeniedGlowColor = new Color32(0xFF, 0x6B, 0x6B, 0xFF);
        // 취소 알림은 살짝 올라오며 나타나서 잠깐 머물다 사라져.
        private const float ToastRise = 8f;
        private const float ToastInTime = 0.15f;
        private const float ToastHoldTime = 1.3f;
        private const float ToastOutTime = 0.3f;

        [SerializeField] private BrickBoard board;
        [SerializeField] private BallLauncher launcher;
        [SerializeField] private Button button;
        [SerializeField] private Image emptyFrame;
        [SerializeField] private Image readyGlow;
        [SerializeField] private Image itemBody;
        [Tooltip("예전 폭탄 심지 이미지 자리야. 새 폭탄 아이콘은 심지까지 한 장이라 숨겨 둬.")]
        [SerializeField] private Image itemOverlay;
        [SerializeField] private TMP_Text label;
        [Header("아이템 이미지 (폭탄은 속성별 아이콘을 코드에서 불러와)")]
        [SerializeField] private Sprite iceIcon;
        [Header("폭탄 데미지 (아이템 칸 아래 테두리의 작은 알약)")]
        [Tooltip("폭탄을 들고 있을 때만 보여. 가운데 십자 데미지야.")]
        [SerializeField] private RectTransform damageBadge;
        [SerializeField] private TMP_Text damageLabel;
        [Header("폭탄 취소 알림 (아이템 칸 위 말풍선)")]
        [Tooltip("맨 앞 공 주변에 벽돌이 없어 폭탄을 쓰지 않았을 때 잠깐 보여.")]
        [SerializeField] private CanvasGroup cancelToast;

        private ItemType shownItem = (ItemType)(-1);
        private ElementType shownElement;
        private int shownUsable = -1;
        private int shownDamage = -1;
        private Color glowColor;
        private Vector2 toastPosition;
        private Tween glowFlash;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(OnClick);
            if (readyGlow != null)
                glowColor = readyGlow.color;
            if (cancelToast != null)
            {
                toastPosition = ((RectTransform)cancelToast.transform).anchoredPosition;
                cancelToast.gameObject.SetActive(false);
            }
        }

        private void OnClick()
        {
            if (board == null)
                return;
            if (board.UseHeldItem())
            {
                GameAudio.Play(SoundCue.Click);
                return;
            }
            GameAudio.Play(SoundCue.Denied);
            // 폭탄은 맨 앞 공 주변에 벽돌이 없으면 쓰지 않고 칸에 남아. 왜 안 터졌는지 알려 줘.
            if (board.Items.Held == ItemType.Bomb)
                ShowCanceled();
        }

        private void ShowCanceled()
        {
            RectTransform slot = button.transform as RectTransform;
            if (slot != null)
            {
                slot.DOKill(true);
                slot.DOPunchAnchorPos(new Vector2(ShakeDistance, 0f), ShakeTime, 14, 0.6f).SetUpdate(true);
            }
            if (readyGlow != null && readyGlow.enabled)
            {
                // 깜빡이는 투명도는 그대로 두고 색만 빨강에서 금색으로 돌려.
                glowFlash?.Kill();
                glowFlash = DOVirtual.Float(0f, 1f, DeniedFlashTime, t =>
                {
                    Color color = Color.Lerp(DeniedGlowColor, glowColor, t);
                    readyGlow.color = new Color(color.r, color.g, color.b, readyGlow.color.a);
                }).SetEase(Ease.InCubic).SetUpdate(true);
            }
            if (cancelToast == null)
                return;
            RectTransform toast = (RectTransform)cancelToast.transform;
            cancelToast.DOKill();
            cancelToast.gameObject.SetActive(true);
            cancelToast.alpha = 0f;
            toast.anchoredPosition = toastPosition + new Vector2(0f, -ToastRise);
            DOTween.Sequence().SetTarget(cancelToast).SetUpdate(true)
                .Append(cancelToast.DOFade(1f, ToastInTime))
                .Join(toast.DOAnchorPos(toastPosition, ToastInTime).SetEase(Ease.OutQuad))
                .AppendInterval(ToastHoldTime)
                .Append(cancelToast.DOFade(0f, ToastOutTime))
                .OnComplete(() => cancelToast.gameObject.SetActive(false));
        }

        private void LateUpdate()
        {
            if (board == null || launcher == null)
                return;
            ItemType item = board.Items.Held;
            ElementType element = launcher.TurnElement;
            // 폭탄은 공이 날아가는 중에만, 얼음은 조준 중에도 쓸 수 있어. 규칙은 보드가 정해.
            bool usable = board.CanUseHeldItem;
            RefreshDamage(item);
            if (item == shownItem && element == shownElement && (usable ? 1 : 0) == shownUsable)
                return;
            bool arrived = shownItem == ItemType.None && item != ItemType.None;
            bool usableChanged = (usable ? 1 : 0) != shownUsable;
            shownItem = item;
            shownElement = element;
            shownUsable = usable ? 1 : 0;
            Refresh(item, element, usable);
            if (usableChanged)
                SetGlow(usable);
            if (arrived)
                PopSlot();
        }

        // 폭탄을 들고 있으면 이번 턴 십자 데미지를 보여 줘. 들고 있는 동안 턴이 지나 세지면 톡 튀어.
        private void RefreshDamage(ItemType item)
        {
            if (damageBadge == null || damageLabel == null)
                return;
            int damage = item == ItemType.Bomb ? board.BombCrossDamage : 0;
            if (damage == shownDamage)
                return;
            bool grew = shownDamage > 0 && damage > shownDamage;
            shownDamage = damage;
            damageBadge.gameObject.SetActive(damage > 0);
            if (damage <= 0)
                return;
            damageLabel.SetText("{0}", damage);
            if (grew)
            {
                damageBadge.DOKill(true);
                damageBadge.DOPunchScale(new Vector3(ArrivePopScale, ArrivePopScale, 0f), ArrivePopTime, 6, 0.5f).SetUpdate(true);
            }
        }

        private void SetGlow(bool on)
        {
            if (readyGlow == null)
                return;
            readyGlow.DOKill();
            glowFlash?.Kill();
            readyGlow.enabled = on;
            if (!on)
                return;
            readyGlow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 1f);
            readyGlow.DOFade(GlowMinAlpha, GlowPulseTime).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }

        private void PopSlot()
        {
            RectTransform slot = button.transform as RectTransform;
            if (slot == null)
                return;
            slot.DOKill(true);
            slot.DOPunchScale(new Vector3(ArrivePopScale, ArrivePopScale, 0f), ArrivePopTime, 6, 0.5f).SetUpdate(true);
        }

        private void OnDisable()
        {
            if (readyGlow != null)
                readyGlow.DOKill();
            glowFlash?.Kill();
            if (button != null)
                button.transform.DOKill(true);
            if (damageBadge != null)
                damageBadge.DOKill(true);
            if (cancelToast != null)
            {
                cancelToast.DOKill();
                cancelToast.gameObject.SetActive(false);
            }
            // 다시 켜지면 처음부터 맞춰 그려.
            shownUsable = -1;
            shownDamage = -1;
        }

        private void Refresh(ItemType item, ElementType element, bool usable)
        {
            bool has = item != ItemType.None;
            // 빈 칸 표시(점선과 "+")는 묶음째 켜고 꺼.
            emptyFrame.gameObject.SetActive(!has);
            itemBody.enabled = has;
            if (itemOverlay != null)
                itemOverlay.enabled = false;
            if (item == ItemType.Bomb)
            {
                // 폭탄은 이번 턴 공 속성으로 터지니까 그 속성의 폭탄을 보여 줘.
                itemBody.sprite = ElementRules.GetBombIcon(element);
                itemBody.color = Color.white;
            }
            else if (item == ItemType.Ice)
            {
                itemBody.sprite = iceIcon;
                itemBody.color = Color.white;
            }
            button.interactable = usable;
            if (label != null)
            {
                label.SetText(usable ? "눌러서 사용" : "아이템");
                // 쓸 수 있을 때는 빛나는 테두리와 같은 금색으로 눈에 띄게 해.
                label.color = usable ? (Color)ReadyLabelColor : Color.white;
            }
        }
    }
}
