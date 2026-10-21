using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker
{
    // 공 바로 아래 가운데 버튼이야. 발사 전에 눌러서 이번 턴 공 속성을 골라. 고를 때마다 골드를 써.
    public sealed class ElementSelectButton : MonoBehaviour
    {
        // 선택지가 버튼 쪽(아래 가운데)에서 커지며 나타나.
        private const float OpenScale = 0.8f;
        private const float OpenTime = 0.18f;

        [SerializeField] private BrickBoard board;
        [SerializeField] private BallLauncher launcher;
        [SerializeField] private ShopPanel shop;
        [Tooltip("속성을 한 번 바꿀 때 드는 골드")]
        [SerializeField, Min(0)] private int price = 1;
        [Header("버튼")]
        [SerializeField] private GameObject buttonRoot;
        [SerializeField] private Button button;
        [SerializeField] private Image face;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text priceLabel;
        [Header("펼쳐지는 선택지 (일반, 불, 물, 풀 순서)")]
        [SerializeField] private GameObject popover;
        [SerializeField] private Button closeBlocker;
        [SerializeField] private Button[] options = new Button[4];
        [SerializeField] private Image[] selectedRings = new Image[4];

        private int shownElement = -1;
        private int shownCanPrepare = -1;
        private int shownGold = -1;

        public bool IsOpen => popover != null && popover.activeSelf;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(Toggle);
            if (closeBlocker != null)
                closeBlocker.onClick.AddListener(Close);
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == null)
                    continue;
                ElementType element = (ElementType)i;
                options[i].onClick.AddListener(() => Choose(element));
            }
            Close();
        }

        private void Toggle()
        {
            if (IsOpen)
            {
                Close();
                return;
            }
            if (launcher == null || !launcher.CanPrepare)
                return;
            if (shop != null)
                shop.Close();
            popover.SetActive(true);
            if (closeBlocker != null)
                closeBlocker.gameObject.SetActive(true);
            shownElement = -1;
            Transform popoverTransform = popover.transform;
            popoverTransform.DOKill(true);
            popoverTransform.localScale = new Vector3(OpenScale, OpenScale, 1f);
            popoverTransform.DOScale(1f, OpenTime).SetEase(Ease.OutBack).SetUpdate(true);
        }

        public void Close()
        {
            if (popover != null)
            {
                popover.transform.DOKill(true);
                popover.SetActive(false);
            }
            if (closeBlocker != null)
                closeBlocker.gameObject.SetActive(false);
        }

        private void Choose(ElementType element)
        {
            if (launcher == null || board == null || !launcher.CanPrepare)
            {
                Close();
                return;
            }
            // 같은 속성을 다시 고르면 골드를 쓰지 않고 닫아.
            if (element != launcher.TurnElement && board.Gold.TrySpend(price))
                launcher.SetTurnElement(element);
            Close();
        }

        private void LateUpdate()
        {
            if (launcher == null || board == null)
                return;
            bool canPrepare = launcher.CanPrepare && !board.IsGameOver;
            if ((canPrepare ? 1 : 0) != shownCanPrepare)
            {
                shownCanPrepare = canPrepare ? 1 : 0;
                // 발사 중에는 숨겨서 아래 띠를 단순하게 해.
                if (buttonRoot != null)
                    buttonRoot.SetActive(canPrepare);
                if (!canPrepare)
                    Close();
            }
            int element = (int)launcher.TurnElement;
            int gold = board.Gold.Amount;
            if (element == shownElement && gold == shownGold)
                return;
            if (element != shownElement)
            {
                face.color = ElementRules.GetBrickTint(launcher.TurnElement);
                icon.sprite = ElementRules.GetUiIconSprite(launcher.TurnElement);
                for (int i = 0; i < selectedRings.Length; i++)
                    if (selectedRings[i] != null)
                        selectedRings[i].enabled = i == element;
            }
            shownElement = element;
            {
                shownGold = gold;
                if (priceLabel != null)
                {
                    priceLabel.SetText("{0}", price);
                    priceLabel.color = gold >= price ? new Color32(0xFF, 0xE4, 0x8A, 0xFF) : new Color32(0xFF, 0x8A, 0x7A, 0xFF);
                }
                for (int i = 0; i < options.Length; i++)
                    if (options[i] != null)
                        options[i].interactable = gold >= price || i == element;
            }
        }
    }
}
