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
                GameAudio.Play(board.UseHeldItem() ? SoundCue.Click : SoundCue.Denied);
        }

        private void LateUpdate()
        {
            if (board == null || launcher == null)
                return;
            ItemType item = board.Items.Held;
            ElementType element = launcher.TurnElement;
            // 폭탄은 공이 날아가는 중에만, 얼음은 조준 중에도 쓸 수 있어. 규칙은 보드가 정해.
            bool usable = board.CanUseHeldItem;
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
