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
            [Tooltip("새로 열린 카드에 붙는 NEW 표시야.")]
            public GameObject newTag;
            [Tooltip("아직 안 열린 카드의 아이콘 위 자물쇠야.")]
            public GameObject lockBadge;
        }

        private static readonly Color32 PriceColor = new Color32(0xFF, 0xE4, 0x8A, 0xFF);
        private static readonly Color32 ReasonColor = new Color32(0xFF, 0x9A, 0x8A, 0xFF);
        private static readonly Color32 DescColor = new Color32(0xFF, 0xFF, 0xFF, 0xB4);
        private const float DisabledAlpha = 0.45f;
        // 발사 중에는 상점 칸을 이만큼 흐리게 해. 공 속성 버튼과 같은 값이야.
        private const float ButtonDisabledAlpha = 0.4f;
        // 열 때 목록이 상점 버튼 쪽에서 커지며 나타나고, 카드가 아래부터 차례로 올라와.
        private const float OpenScale = 0.8f;
        private const float OpenTime = 0.2f;
        private const float CardRise = 18f;
        private const float CardTime = 0.22f;
        private const float CardStagger = 0.04f;

        [SerializeField] private BrickBoard board;
        [SerializeField] private BallLauncher launcher;
        [SerializeField] private ElementSelectButton elementSelect;
        [Header("가격 (10턴마다 오르는 값은 1~9턴 0번, 10~19턴 1번, 20~29턴 2번 더해)")]
        [SerializeField, Min(0)] private int bombBasePrice = 60;
        [SerializeField, Min(0)] private int bombPricePerTenTurns = 10;
        [Tooltip("얼음은 이번 판에 쓸 때마다 가격이 2배가 돼(150 → 300 → 600).")]
        [SerializeField, Min(0)] private int iceBasePrice = 150;
        [SerializeField, Min(0)] private int brickChangeBasePrice = 75;
        [SerializeField, Min(0)] private int brickChangePricePerTenTurns = 10;
        [Header("상점 버튼")]
        [Tooltip("버튼과 라벨을 묶은 칸이야. 발사 중에는 칸째 흐리게 해.")]
        [SerializeField] private CanvasGroup shopGroup;
        [SerializeField] private Button shopButton;
        [SerializeField] private TMP_Text shopLabel;
        [Tooltip("새로 열린 아이템이 있으면 상점 버튼에 붙는 NEW 표시야. 상점을 열면 숨어.")]
        [SerializeField] private GameObject shopNewBadge;
        [Header("목록")]
        [SerializeField] private GameObject list;
        [SerializeField] private Button dimOverlay;
        [SerializeField] private Card bombCard;
        [SerializeField] private Card iceCard;
        [SerializeField] private Card brickChangeCard;

        private bool dirty = true;
        private int shownCanPrepare = -1;
        private ElementType shownElement;
        private int shownGold = -1;
        private ItemType shownItem;
        private int shownTurn = -1;
        // 상점 아이템이 열렸는지(마지막으로 확인한 값)와, 열린 뒤 아직 상점에서 못 본 아이템이야. ShopItem 순서야.
        private readonly bool[] knownUnlocked = new bool[3];
        private readonly bool[] isNew = new bool[3];
        private bool unlocksSynced;
        // 아래 카드부터 올라오게 아래→위 순서로 담아.
        private readonly RectTransform[] cardRects = new RectTransform[3];
        private readonly Vector2[] cardPositions = new Vector2[3];

        public bool IsOpen => list != null && list.activeSelf;

        private int BombPrice => bombBasePrice + bombPricePerTenTurns * board.PriceTier;
        private int IcePrice => (int)Math.Min(int.MaxValue, (long)iceBasePrice << Math.Min(board.IceUseCount, 30));
        private int BrickChangePrice => brickChangeBasePrice + brickChangePricePerTenTurns * board.PriceTier;

        // 지금 가격이야. 해금으로 받을 아이템을 칸이 차서 못 받으면 이만큼 골드로 대신 줘.
        public int PriceOf(ShopItem item) =>
            item == ShopItem.Bomb ? BombPrice : item == ShopItem.Ice ? IcePrice : BrickChangePrice;

        private void Awake()
        {
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
            CloseSilently();
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
            GameAudio.Play(SoundCue.ShopOpen);
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
            if (IsOpen)
                GameAudio.Play(SoundCue.ShopClose);
            CloseSilently();
        }

        private void CloseSilently()
        {
            // 한 번 열어서 본 아이템은 NEW를 떼.
            if (IsOpen)
                Array.Clear(isNew, 0, isNew.Length);
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

        private void BuyBomb() => BuyStored(ShopItem.Bomb, ItemType.Bomb, BombPrice);

        private void BuyIce() => BuyStored(ShopItem.Ice, ItemType.Ice, IcePrice);

        private void BuyStored(ShopItem shopItem, ItemType item, int price)
        {
            if (!CanShop() || !IsUnlocked(shopItem) || !board.Items.IsEmpty || !board.Gold.TrySpend(price))
            {
                GameAudio.Play(SoundCue.Denied);
                return;
            }
            board.Items.TryGive(item);
            GameAudio.Play(SoundCue.Purchase);
            dirty = true;
            board.SaveProgress();
        }

        private void BuyBrickChange()
        {
            ElementType element = launcher.TurnElement;
            if (!CanShop() || !IsUnlocked(ShopItem.BrickChange) || element == ElementType.Normal ||
                board.BrickChangeUsedThisTurn || !board.Gold.TrySpend(BrickChangePrice))
            {
                GameAudio.Play(SoundCue.Denied);
                return;
            }
            board.ChangeAllBrickElements(ElementRules.WeakTo(element));
            board.MarkBrickChangeUsed();
            board.SaveProgress();
            // 바로 적용 아이템은 상점을 닫아서 벽돌이 바뀌는 모습이 가려지지 않게 해.
            CloseSilently();
        }

        // 발사 전에만 열려. 아이템 금지 디버프 턴에는 상점도 못 써.
        private bool CanShop() => board != null && launcher != null && launcher.CanPrepare && !board.IsGameOver && !board.ItemsBanned;

        private bool IsUnlocked(ShopItem item) => board.TurnEvents.IsUnlocked(item);

        // 아직 안 열린 아이템은 잠긴 카드로 보여 줘.
        private string LockedReason(ShopItem item) =>
            IsUnlocked(item) ? null : board.TurnEvents.UnlockTurn(item) + "턴에 열려요";

        private void LateUpdate()
        {
            if (board == null || launcher == null)
                return;
            SyncUnlocks();
            bool canShop = CanShop();
            if ((canShop ? 1 : 0) != shownCanPrepare)
            {
                shownCanPrepare = canShop ? 1 : 0;
                if (!canShop)
                    Close();
                // 발사 중에는 공 속성 칸처럼 버튼과 라벨을 칸째 흐리게 해서 누를 수 없는 걸 보여 줘.
                shopButton.interactable = canShop;
                if (shopGroup != null)
                    shopGroup.alpha = canShop ? 1f : ButtonDisabledAlpha;
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
                shopLabel.SetText(IsOpen ? "닫기" : "상점");
            if (IsOpen)
                RefreshCards();
        }

        // 새로 열린 아이템에 NEW를 붙여. 상점 버튼의 NEW는 상점을 열면 숨고, 카드의 NEW는 상점을 한 번 닫으면 사라져.
        private void SyncUnlocks()
        {
            bool anyNew = false;
            for (int i = 0; i < isNew.Length; i++)
            {
                bool open = IsUnlocked((ShopItem)i);
                if (open && !knownUnlocked[i])
                {
                    // 처음 확인할 때(테스트용 시작 턴이라 이미 열려 있던 것)는 NEW를 붙이지 않아.
                    isNew[i] = unlocksSynced;
                    dirty = true;
                }
                knownUnlocked[i] = open;
                anyNew |= isNew[i];
            }
            unlocksSynced = true;
            bool showBadge = anyNew && !IsOpen;
            if (shopNewBadge != null && shopNewBadge.activeSelf != showBadge)
                shopNewBadge.SetActive(showBadge);
        }

        private void RefreshMarks(Card card, ShopItem item)
        {
            if (card.newTag != null)
                card.newTag.SetActive(isNew[(int)item]);
            if (card.lockBadge != null)
                card.lockBadge.SetActive(!IsUnlocked(item));
        }

        private void RefreshCards()
        {
            RefreshMarks(bombCard, ShopItem.Bomb);
            RefreshMarks(iceCard, ShopItem.Ice);
            RefreshMarks(brickChangeCard, ShopItem.BrickChange);
            ElementType element = launcher.TurnElement;
            int gold = board.Gold.Amount;
            bool slotFull = !board.Items.IsEmpty;

            // 속성 폭탄: 아이콘은 이번 턴 공 속성의 폭탄이야. 심지까지 한 장이라 예전 심지 이미지는 숨겨.
            bombCard.iconBody.sprite = ElementRules.GetBombIcon(element);
            bombCard.iconBody.color = Color.white;
            if (bombCard.iconOverlay != null)
                bombCard.iconOverlay.enabled = false;
            // 이번 턴 실제 데미지를 보여 줘. 턴이 지날수록 세져.
            SetCard(bombCard, BombPrice, LockedReason(ShopItem.Bomb) ?? (slotFull ? "아이템 칸이 차 있어요" : gold < BombPrice ? "골드가 부족해요" : null),
                "3x3 · 십자 " + board.BombCrossDamage + " / 대각 " + board.BombBaseDamage);
            SetCard(iceCard, IcePrice, LockedReason(ShopItem.Ice) ?? (slotFull ? "아이템 칸이 차 있어요" : gold < IcePrice ? "골드가 부족해요" : null),
                "이번 턴 벽돌이 안 내려옴");

            // 벽돌 속성 변경: 지금 공에 약한 벽돌 그림을 보여 줘. 일반 공이면 쓸 수 없어.
            ElementType target = ElementRules.WeakTo(element);
            brickChangeCard.iconBody.color = ElementRules.GetBrickTint(target);
            if (brickChangeCard.iconBadge != null)
            {
                brickChangeCard.iconBadge.enabled = target != ElementType.Normal;
                if (target != ElementType.Normal)
                    brickChangeCard.iconBadge.sprite = ElementRules.GetIconSprite(target);
            }
            string reason = LockedReason(ShopItem.BrickChange) ?? (element == ElementType.Normal ? "먼저 공 속성을 골라 주세요"
                : board.BrickChangeUsedThisTurn ? "이번 턴에 이미 썼어요"
                : gold < BrickChangePrice ? "골드가 부족해요" : null);
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
