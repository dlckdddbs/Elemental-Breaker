using DG.Tweening;
using DG.Tweening.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker
{
    // 상단 HUD야. 턴, 이번 턴 속성과 다음 턴 예약, 골드, 공 개수를 보여 주고 값이 바뀔 때만 다시 그려.
    public sealed class TopHUD : MonoBehaviour
    {
        private static readonly string[] ElementNames = { "일반", "불", "물", "풀" };
        // 배지 안 여백과 아이콘 크기야. 생성 메뉴의 배치와 같은 값이야(기준 해상도 480x854 픽셀).
        private const float BadgePadding = 18f;
        private const float IconSize = 34f;
        private const float IconGap = 8f;
        private const float NextGap = 6f;
        private const float AcquiredGap = 6f;
        // 값이 바뀔 때 톡 튀는 시간과 크기야. 튕김 횟수가 적을수록 한 번 "톡" 하고 끝나.
        private const float PopTime = 0.25f;
        private const float PopScale = 0.22f;
        private const int PopVibrato = 6;
        private const float PopElasticity = 0.5f;
        // 골드가 늘면 숫자가 이 시간 동안 촤라락 올라가.
        private const float GoldCountTime = 0.35f;

        [SerializeField] private BallLauncher launcher;
        [SerializeField] private BrickBoard board;
        [Header("턴")]
        [SerializeField] private TMP_Text turnLabel;
        [Header("속성 [현재 → 다음]")]
        [SerializeField] private RectTransform elementGroup;
        [SerializeField] private Image elementBadge;
        [SerializeField] private Image elementIcon;
        [SerializeField] private TMP_Text elementName;
        [SerializeField] private RectTransform nextGroup;
        [SerializeField] private Image nextCircle;
        [SerializeField] private Image nextIcon;
        [Header("골드와 공")]
        [SerializeField] private TMP_Text goldLabel;
        [SerializeField] private TMP_Text ballLabel;
        [SerializeField] private RectTransform acquiredBadge;
        [SerializeField] private TMP_Text acquiredLabel;

        private int shownTurn = -1;
        private int shownGold = -1;
        private int shownBalls = -1;
        private int shownAcquired = -1;
        private bool elementShown;
        private ElementType shownElement;
        private ElementType? shownNext;
        // 화면에 보이는 골드야. 실제 골드를 향해 올라가.
        private int goldDisplay;
        // 숫자 트윈에 넘길 함수야. 한 번만 만들어서 골드가 바뀔 때마다 새로 할당하지 않아.
        private DOGetter<int> goldGetter;
        private DOSetter<int> goldSetter;

        private void Awake()
        {
            goldGetter = () => goldDisplay;
            goldSetter = SetGoldDisplay;
        }

        private void LateUpdate()
        {
            if (launcher == null || board == null)
                return;
            RefreshTurn();
            RefreshElement();
            RefreshGold();
            RefreshBalls();
        }

        private void OnDisable()
        {
            if (goldLabel != null)
            {
                DOTween.Kill(goldLabel, true);
                goldLabel.rectTransform.DOKill(true);
            }
            if (elementGroup != null)
                elementGroup.DOKill(true);
            if (acquiredBadge != null)
                acquiredBadge.DOKill(true);
        }

        private void RefreshTurn()
        {
            int turn = board.TurnNumber;
            if (turn == shownTurn)
                return;
            shownTurn = turn;
            if (turnLabel != null)
                turnLabel.SetText("{0}", turn);
        }

        private void RefreshElement()
        {
            ElementType element = launcher.TurnElement;
            ElementType? next = launcher.ReservedElement;
            if (elementShown && element == shownElement && next == shownNext)
                return;

            // 속성 아이템을 먹어 예약이 바뀌었거나 턴이 넘어가 속성이 바뀌면 배지가 톡 튀어.
            bool popNow = elementShown && (element != shownElement || (next.HasValue && next != shownNext));
            elementShown = true;
            shownElement = element;
            shownNext = next;

            elementBadge.color = ElementRules.GetBrickTint(element);
            elementIcon.sprite = ElementRules.GetUiIconSprite(element);
            elementName.SetText(ElementNames[(int)element]);
            bool hasNext = next.HasValue;
            nextGroup.gameObject.SetActive(hasNext);
            if (hasNext)
            {
                nextCircle.color = ElementRules.GetBrickTint(next.Value);
                nextIcon.sprite = ElementRules.GetUiIconSprite(next.Value);
            }
            LayoutElement(hasNext);
            if (popNow)
                Pop(elementGroup);
        }

        private void LayoutElement(bool hasNext)
        {
            // 배지는 현재 속성(아이콘+이름)만 감싸고, [→ 다음 속성]은 배지 밖에 붙여. 묶음 전체를 가운데에 둬.
            float badgeWidth = BadgePadding + IconSize + IconGap + elementName.preferredWidth + BadgePadding;
            float groupWidth = hasNext ? badgeWidth + NextGap + nextGroup.sizeDelta.x : badgeWidth;
            float left = -groupWidth * 0.5f;
            RectTransform badge = elementBadge.rectTransform;
            badge.sizeDelta = new Vector2(badgeWidth, badge.sizeDelta.y);
            badge.anchoredPosition = new Vector2(left, badge.anchoredPosition.y);
            if (hasNext)
                nextGroup.anchoredPosition = new Vector2(left + badgeWidth + NextGap, nextGroup.anchoredPosition.y);
        }

        private void RefreshGold()
        {
            int gold = board.Gold.Amount;
            if (gold == shownGold)
                return;
            bool gained = shownGold >= 0 && gold > shownGold;
            shownGold = gold;
            if (goldLabel == null)
                return;
            DOTween.Kill(goldLabel);
            if (!gained)
            {
                // 처음 보여 줄 때와 골드를 쓸 때는 바로 바꿔.
                SetGoldDisplay(gold);
                return;
            }
            // 연달아 벌면 보이는 숫자에서 이어서 올라가.
            DOTween.To(goldGetter, goldSetter, gold, GoldCountTime).SetEase(Ease.OutQuad)
                .SetTarget(goldLabel).SetUpdate(true);
            Pop(goldLabel.rectTransform);
        }

        private void SetGoldDisplay(int value)
        {
            goldDisplay = value;
            goldLabel.SetText("{0}", value);
        }

        private void RefreshBalls()
        {
            int balls = launcher.ActiveBallCount;
            int acquired = launcher.AcquiredBallCount;
            if (balls == shownBalls && acquired == shownAcquired)
                return;
            bool popAcquired = shownAcquired >= 0 && acquired > shownAcquired;
            shownBalls = balls;
            shownAcquired = acquired;
            if (ballLabel != null)
                ballLabel.SetText("x{0}", balls);
            if (acquiredBadge == null)
                return;
            // 이번 턴에 얻은 공은 다음 턴에 합류하니까 "+N"으로 따로 보여 줘.
            acquiredBadge.gameObject.SetActive(acquired > 0);
            if (acquired <= 0)
                return;
            acquiredLabel.SetText("+{0}", acquired);
            float x = ballLabel.rectTransform.anchoredPosition.x + ballLabel.preferredWidth + AcquiredGap;
            acquiredBadge.anchoredPosition = new Vector2(x, acquiredBadge.anchoredPosition.y);
            if (popAcquired)
                Pop(acquiredBadge);
        }

        private static void Pop(RectTransform target)
        {
            // 이미 튀는 중이면 끝까지 두어서, 벽돌을 연달아 깨도 "톡, 톡" 박자가 살아 있게 해.
            if (target == null || DOTween.IsTweening(target))
                return;
            target.localScale = Vector3.one;
            // 히트 스톱으로 게임이 느려져도 HUD는 같은 빠르기로 움직이게 실제 시간을 써.
            target.DOPunchScale(new Vector3(PopScale, PopScale, 0f), PopTime, PopVibrato, PopElasticity).SetUpdate(true);
        }
    }
}
