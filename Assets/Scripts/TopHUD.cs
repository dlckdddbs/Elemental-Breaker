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
        private const float PopTime = 0.18f;
        private const float PopScale = 0.25f;

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
        private float goldPop = -1f;
        private float elementPop = -1f;
        private float acquiredPop = -1f;

        private void LateUpdate()
        {
            if (launcher == null || board == null)
                return;
            RefreshTurn();
            RefreshElement();
            RefreshGold();
            RefreshBalls();
            float dt = Time.unscaledDeltaTime;
            Pop(ref goldPop, goldLabel != null ? goldLabel.rectTransform : null, dt);
            Pop(ref elementPop, elementGroup, dt);
            Pop(ref acquiredPop, acquiredBadge, dt);
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
                elementPop = 0f;
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
            if (shownGold >= 0 && gold > shownGold)
                goldPop = 0f;
            shownGold = gold;
            if (goldLabel != null)
                goldLabel.SetText("{0}", gold);
        }

        private void RefreshBalls()
        {
            int balls = launcher.ActiveBallCount;
            int acquired = launcher.AcquiredBallCount;
            if (balls == shownBalls && acquired == shownAcquired)
                return;
            if (shownAcquired >= 0 && acquired > shownAcquired)
                acquiredPop = 0f;
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
        }

        private static void Pop(ref float time, RectTransform target, float dt)
        {
            if (time < 0f || target == null)
                return;
            time += dt;
            float k = time / PopTime;
            if (k >= 1f)
            {
                time = -1f;
                target.localScale = Vector3.one;
                return;
            }
            float scale = 1f + PopScale * Mathf.Sin(k * Mathf.PI);
            target.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
