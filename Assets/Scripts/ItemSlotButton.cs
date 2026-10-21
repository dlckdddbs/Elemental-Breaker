using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker
{
    // 왼쪽 아래 아이템 칸이야. 들고 있는 아이템을 보여 주고, 공이 날아가는 동안 누르면 써.
    public sealed class ItemSlotButton : MonoBehaviour
    {
        // 쓸 수 있을 때 테두리 빛이 숨 쉬듯 깜빡여. 밝음 → 어두움까지 걸리는 시간과 가장 어두울 때 투명도야.
        private const float GlowPulseTime = 0.5f;
        private const float GlowMinAlpha = 0.1f;
        // 아이템이 칸에 들어올 때 톡 튀는 크기와 시간이야.
        private const float ArrivePopScale = 0.18f;
        private const float ArrivePopTime = 0.28f;

        [SerializeField] private BrickBoard board;
        [SerializeField] private BallLauncher launcher;
        [SerializeField] private Button button;
        [SerializeField] private Image emptyFrame;
        [SerializeField] private Image readyGlow;
        [SerializeField] private Image itemBody;
        [SerializeField] private Image itemOverlay;
        [SerializeField] private TMP_Text label;
        [Header("아이템 이미지")]
        [SerializeField] private Sprite bombBody;
        [SerializeField] private Sprite bombFuse;
        [SerializeField] private Sprite iceIcon;

        private ItemType shownItem = (ItemType)(-1);
        private ElementType shownElement;
        private int shownUsable = -1;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            if (board != null)
                board.UseHeldItem();
        }

        private void LateUpdate()
        {
            if (board == null || launcher == null)
                return;
            ItemType item = board.Items.Held;
            ElementType element = launcher.TurnElement;
            bool usable = item != ItemType.None && launcher.IsTurnActive && launcher.FlyingBallCount > 0 && !board.IsGameOver;
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

        private void SetGlow(bool on)
        {
            if (readyGlow == null)
                return;
            readyGlow.DOKill();
            readyGlow.enabled = on;
            if (!on)
                return;
            Color color = readyGlow.color;
            readyGlow.color = new Color(color.r, color.g, color.b, 1f);
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
            if (button != null)
                button.transform.DOKill(true);
            // 다시 켜지면 처음부터 맞춰 그려.
            shownUsable = -1;
        }

        private void Refresh(ItemType item, ElementType element, bool usable)
        {
            bool has = item != ItemType.None;
            emptyFrame.enabled = !has;
            itemBody.enabled = has;
            itemOverlay.enabled = item == ItemType.Bomb;
            if (item == ItemType.Bomb)
            {
                // 폭탄은 이번 턴 공 속성으로 터지니까 그 색을 입혀.
                itemBody.sprite = bombBody;
                itemBody.color = ElementRules.GetBombTint(element);
                itemOverlay.sprite = bombFuse;
            }
            else if (item == ItemType.Ice)
            {
                itemBody.sprite = iceIcon;
                itemBody.color = Color.white;
            }
            button.interactable = usable;
            if (label != null)
                label.SetText(usable ? "눌러서 사용" : "아이템");
        }
    }
}
