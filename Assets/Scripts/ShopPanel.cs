using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker
{
    // 오른쪽 아래 상점이야. 발사 전에만 열리고, 버튼 위로 목록이 펼쳐져.
    // 위 구역은 아이템 칸에 보관하는 것(속성 폭탄, 얼음), 아래 구역은 바로 적용하는 것(벽돌 속성 변경)이야.
    public sealed class ShopPanel : MonoBehaviour
    {
        [Serializable]
        public sealed class Card
        {
            public CanvasGroup group;
            public Button buyButton;
            public TMP_Text description;
            public TMP_Text priceLabel;
            public Image iconBody;
            public Image iconOverlay;
            public Image iconBadge;
        }

        private static readonly Color32 PriceColor = new Color32(0xFF, 0xE4, 0x8A, 0xFF);
        private static readonly Color32 ReasonColor = new Color32(0xFF, 0x9A, 0x8A, 0xFF);
        private static readonly Color32 DescColor = new Color32(0xFF, 0xFF, 0xFF, 0xB4);
        private const float DisabledAlpha = 0.45f;
        // 열 때 목록이 상점 버튼 쪽에서 커지며 나타나고, 카드가 아래부터 차례로 올라와.
        private const float OpenScale = 0.8f;
        private const float OpenTime = 0.2f;
        private const float CardRise = 18f;
        private const float CardTime = 0.22f;
        private const float CardStagger = 0.04f;

        [SerializeField] private BrickBoard board;
        [SerializeField] private BallLauncher launcher;
        [SerializeField] private ElementSelectButton elementSelect;
        [Header("가격 (테스트용 1골드)")]
        [SerializeField, Min(0)] private int bombPrice = 1;
        [SerializeField, Min(0)] private int icePrice = 1;
        [SerializeField, Min(0)] private int brickChangePrice = 1;
        [Tooltip("같은 판에서 같은 아이템을 살 때마다 오르는 가격. 0이면 안 올라.")]
        [SerializeField, Min(0)] private int priceStep = 0;
        [Header("상점 버튼")]
        [SerializeField] private Button shopButton;
        [SerializeField] private Image shopFace;
        [SerializeField] private TMP_Text shopLabel;
        [Header("목록")]
        [SerializeField] private GameObject list;
        [SerializeField] private Button dimOverlay;
        [SerializeField] private Card bombCard;
        [SerializeField] private Card iceCard;
        [SerializeField] private Card brickChangeCard;

        private int bombBought, iceBought, brickChangeBought;
        private int brickChangeUsedTurn = -1;
        private bool dirty = true;
        private int shownCanPrepare = -1;
        private ElementType shownElement;
        private int shownGold = -1;
        private ItemType shownItem;
        private int shownTurn = -1;
        private Color shopFaceColor;
        // 아래 카드부터 올라오게 아래→위 순서로 담아.
        private readonly RectTransform[] cardRects = new RectTransform[3];
        private readonly Vector2[] cardPositions = new Vector2[3];

        public bool IsOpen => list != null && list.activeSelf;

        private int BombPrice => bombPrice + bombBought * priceStep;
        private int IcePrice => icePrice + iceBought * priceStep;
        private int BrickChangePrice => brickChangePrice + brickChangeBought * priceStep;

        private void Awake()
        {
            if (shopFace != null)
                shopFaceColor = shopFace.color;
            CacheCard(0, brickChangeCard);
            CacheCard(1, iceCard);
            CacheCard(2, bombCard);
            if (shopButton != null)
                shopButton.onClick.AddListener(Toggle);
            if (dimOverlay != null)
                dimOverlay.onClick.AddListener(Close);
            if (bombCard.buyButton != null)
                bombCard.buyButton.onClick.AddListener(BuyBomb);
            if (iceCard.buyButton != null)
                iceCard.buyButton.onClick.AddListener(BuyIce);
            if (brickChangeCard.buyButton != null)
                brickChangeCard.buyButton.onClick.AddListener(BuyBrickChange);
            Close();
        }

        private void Toggle()
        {
            if (IsOpen)
            {
                Close();
                return;
            }
            if (launcher == null || !launcher.CanPrepare || board.IsGameOver)
                return;
            if (elementSelect != null)
                elementSelect.Close();
            list.SetActive(true);
            if (dimOverlay != null)
                dimOverlay.gameObject.SetActive(true);
            dirty = true;
            PlayOpen();
        }

        private void CacheCard(int index, Card card)
        {
            if (card.group == null)
                return;
            cardRects[index] = card.group.transform as RectTransform;
            if (cardRects[index] != null)
                cardPositions[index] = cardRects[index].anchoredPosition;
        }

        private void PlayOpen()
        {
            // 연출은 실제 시간으로 돌아. 목록은 버튼 쪽(오른쪽 아래)을 기준으로 커져.
            Transform listTransform = list.transform;
            listTransform.DOKill(true);
            listTransform.localScale = new Vector3(OpenScale, OpenScale, 1f);
            listTransform.DOScale(1f, OpenTime).SetEase(Ease.OutBack).SetUpdate(true);
            for (int i = 0; i < cardRects.Length; i++)
            {
                RectTransform card = cardRects[i];
                if (card == null)
                    continue;
                card.DOKill(true);
                card.anchoredPosition = cardPositions[i] + new Vector2(0f, -CardRise);
                card.DOAnchorPos(cardPositions[i], CardTime).SetEase(Ease.OutBack)
                    .SetDelay(i * CardStagger).SetUpdate(true);
            }
        }

        public void Close()
        {
            // 닫기는 바로 해. 남은 연출은 끝 상태로 맞춰서 다음에 열 때 자리가 어긋나지 않게 해.
            if (list != null)
                list.transform.DOKill(true);
            for (int i = 0; i < cardRects.Length; i++)
                if (cardRects[i] != null)
                    cardRects[i].DOKill(true);
            if (list != null)
                list.SetActive(false);
            if (dimOverlay != null)
                dimOverlay.gameObject.SetActive(false);
            dirty = true;
        }

        private void BuyBomb() => BuyStored(ItemType.Bomb, BombPrice, ref bombBought);

        private void BuyIce()
        {
            // 얼음 기능은 아직이라 카드만 보여 줘.
        }

        private void BuyStored(ItemType item, int price, ref int bought)
        {
            if (!CanShop() || !board.Items.IsEmpty || !board.Gold.TrySpend(price))
                return;
            board.Items.TryGive(item);
            bought++;
            dirty = true;
        }

        private void BuyBrickChange()
        {
            ElementType element = launcher.TurnElement;
            if (!CanShop() || element == ElementType.Normal || brickChangeUsedTurn == board.TurnNumber ||
                !board.Gold.TrySpend(BrickChangePrice))
                return;
            board.ChangeAllBrickElements(ElementRules.WeakTo(element));
            brickChangeUsedTurn = board.TurnNumber;
            brickChangeBought++;
            // 바로 적용 아이템은 상점을 닫아서 벽돌이 바뀌는 모습이 가려지지 않게 해.
            Close();
        }

        private bool CanShop() => board != null && launcher != null && launcher.CanPrepare && !board.IsGameOver;

        private void LateUpdate()
        {
            if (board == null || launcher == null)
                return;
            bool canShop = CanShop();
            if ((canShop ? 1 : 0) != shownCanPrepare)
            {
                shownCanPrepare = canShop ? 1 : 0;
                if (!canShop)
                    Close();
                shopButton.interactable = canShop;
                shopFace.color = canShop ? shopFaceColor : new Color(shopFaceColor.r, shopFaceColor.g, shopFaceColor.b, 0.4f);
                dirty = true;
            }
            if (shownElement != launcher.TurnElement || shownGold != board.Gold.Amount ||
                shownItem != board.Items.Held || shownTurn != board.TurnNumber)
                dirty = true;
            if (!dirty)
                return;
            dirty = false;
            shownElement = launcher.TurnElement;
            shownGold = board.Gold.Amount;
            shownItem = board.Items.Held;
            shownTurn = board.TurnNumber;
            if (shopLabel != null)
                shopLabel.SetText(!canShop ? "발사 중" : IsOpen ? "닫기" : "상점");
            if (IsOpen)
                RefreshCards();
        }

        private void RefreshCards()
        {
            ElementType element = launcher.TurnElement;
            int gold = board.Gold.Amount;
            bool slotFull = !board.Items.IsEmpty;

            // 속성 폭탄: 아이콘은 이번 턴 공 속성 색이야.
            bombCard.iconBody.color = ElementRules.GetBombTint(element);
            SetCard(bombCard, BombPrice, slotFull ? "아이템 칸이 차 있어요" : gold < BombPrice ? "골드가 부족해요" : null,
                "맨 앞 공 주변 3x3 폭발");
            SetCard(iceCard, IcePrice, "곧 추가돼요", "이번 턴 벽돌이 안 내려옴");

            // 벽돌 속성 변경: 지금 공에 약한 벽돌 그림을 보여 줘. 일반 공이면 쓸 수 없어.
            ElementType target = ElementRules.WeakTo(element);
            brickChangeCard.iconBody.color = ElementRules.GetBrickTint(target);
            if (brickChangeCard.iconBadge != null)
            {
                brickChangeCard.iconBadge.enabled = target != ElementType.Normal;
                if (target != ElementType.Normal)
                    brickChangeCard.iconBadge.sprite = ElementRules.GetIconSprite(target);
            }
            string reason = element == ElementType.Normal ? "먼저 공 속성을 골라 주세요"
                : brickChangeUsedTurn == board.TurnNumber ? "이번 턴에 이미 썼어요"
                : gold < BrickChangePrice ? "골드가 부족해요" : null;
            SetCard(brickChangeCard, BrickChangePrice, reason, "모든 벽돌을 " + ElementName(target) + " 속성으로");
        }

        private static void SetCard(Card card, int price, string disabledReason, string description)
        {
            bool enabled = disabledReason == null;
            card.group.alpha = enabled ? 1f : DisabledAlpha;
            card.buyButton.interactable = enabled;
            card.description.SetText(enabled ? description : disabledReason);
            card.description.color = enabled ? (Color)DescColor : ReasonColor;
            card.priceLabel.SetText("{0}", price);
            card.priceLabel.color = PriceColor;
        }

        private static string ElementName(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return "불";
                case ElementType.Water: return "물";
                case ElementType.Grass: return "풀";
                default: return "일반";
            }
        }
    }
}
