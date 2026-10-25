using DG.Tweening;
using DG.Tweening.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker
{
    // 상단 정보판이야. 두 줄로 보여 주고 값이 바뀔 때만 다시 그려.
    // 1줄: 턴 | 이번 턴 속성 | 골드    2줄: 공 개수(+이번 턴에 얻은 공) | 다음 턴 속성
    // 묶음마다 아이콘과 글자를 이어 붙이고, 글자 길이가 달라져도 묶음 전체가 칸 가운데에 오게 맞춰.
    public sealed class TopHUD : MonoBehaviour
    {
        private static readonly string[] ElementNames = { "일반", "불", "물", "풀" };
        // 다음 속성 이름 색이야. 일반, 불, 물, 풀 순서로 어두운 판 위에서 잘 읽히는 밝은 속성색이야.
        private static readonly Color32[] ElementTextColors =
        {
            new Color32(0xE3, 0xEC, 0xF3, 0xFF), new Color32(0xFF, 0x9B, 0x6B, 0xFF),
            new Color32(0x7F, 0xD6, 0xFF, 0xFF), new Color32(0x8F, 0xEA, 0x6E, 0xFF)
        };
        // 묶음 안 간격이야(기준 해상도 480x854 픽셀).
        private const float IconGap = 8f;
        private const float SmallGap = 6f;
        private const float CaptionGap = 4f;
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
        [Header("이번 턴 속성")]
        [SerializeField] private RectTransform elementGroup;
        [SerializeField] private Image elementCircle;
        [SerializeField] private Image elementIcon;
        [SerializeField] private TMP_Text elementName;
        [Header("다음 턴 속성 (예약이 없으면 이번 턴과 같은 속성)")]
        [SerializeField] private RectTransform nextGroup;
        [SerializeField] private TMP_Text nextCaption;
        [SerializeField] private Image nextCircle;
        [SerializeField] private Image nextIcon;
        [SerializeField] private TMP_Text nextName;
        [Header("골드")]
        [SerializeField] private RectTransform goldGroup;
        [SerializeField] private RectTransform goldIcon;
        [SerializeField] private TMP_Text goldLabel;
        [Header("공")]
        [SerializeField] private RectTransform ballIcon;
        [SerializeField] private TMP_Text ballLabel;
        [SerializeField] private RectTransform acquiredBadge;
        [SerializeField] private TMP_Text acquiredLabel;

        private int shownTurn = -1;
        private int shownGold = -1;
        private int shownBalls = -1;
        private int shownAcquired = -1;
        private bool elementShown;
        private ElementType shownElement;
        private ElementType shownNext;
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
            if (nextGroup != null)
                nextGroup.DOKill(true);
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
            // 속성 아이템을 안 먹었으면 다음 턴도 이번 턴과 같은 속성이야.
            ElementType next = launcher.ReservedElement ?? element;
            if (elementShown && element == shownElement && next == shownNext)
                return;
            bool popElement = elementShown && element != shownElement;
            bool popNext = elementShown && next != shownNext;
            elementShown = true;
            shownElement = element;
            shownNext = next;

            elementCircle.color = ElementRules.GetBrickTint(element);
            elementIcon.sprite = ElementRules.GetUiIconSprite(element);
            elementName.SetText(ElementNames[(int)element]);
            LayoutRow(elementGroup, elementCircle.rectTransform, IconGap, elementName.rectTransform, elementName.preferredWidth);

            nextCircle.color = ElementRules.GetBrickTint(next);
            nextIcon.sprite = ElementRules.GetUiIconSprite(next);
            nextName.SetText(ElementNames[(int)next]);
            nextName.color = ElementTextColors[(int)next];
            LayoutNext();
            if (popElement)
                Pop(elementGroup);
            if (popNext)
                Pop(nextGroup);
        }

        // [● 다음: 풀] 순서로 이어 붙여.
        private void LayoutNext()
        {
            RectTransform circle = nextCircle.rectTransform, caption = nextCaption.rectTransform, name = nextName.rectTransform;
            float captionWidth = nextCaption.preferredWidth, nameWidth = nextName.preferredWidth;
            float x = -(circle.sizeDelta.x + SmallGap + captionWidth + CaptionGap + nameWidth) * 0.5f;
            circle.anchoredPosition = new Vector2(x, circle.anchoredPosition.y);
            x += circle.sizeDelta.x + SmallGap;
            caption.anchoredPosition = new Vector2(x, caption.anchoredPosition.y);
            x += captionWidth + CaptionGap;
            name.anchoredPosition = new Vector2(x, name.anchoredPosition.y);
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
            // 자릿수가 바뀌어도 [동전 숫자] 묶음이 칸 가운데에 오게 해.
            LayoutRow(goldGroup, goldIcon, SmallGap, goldLabel.rectTransform, goldLabel.preferredWidth);
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
            ballLabel.SetText("공 ×{0}", balls);
            // 이번 턴에 얻은 공은 다음 턴에 합류하니까 "+N"으로 따로 보여 줘.
            bool hasAcquired = acquired > 0;
            acquiredBadge.gameObject.SetActive(hasAcquired);
            if (hasAcquired)
                acquiredLabel.SetText("+{0}", acquired);
            float labelWidth = ballLabel.preferredWidth;
            float total = ballIcon.sizeDelta.x + SmallGap + labelWidth + (hasAcquired ? SmallGap + acquiredBadge.sizeDelta.x : 0f);
            float x = -total * 0.5f;
            ballIcon.anchoredPosition = new Vector2(x, ballIcon.anchoredPosition.y);
            x += ballIcon.sizeDelta.x + SmallGap;
            ballLabel.rectTransform.anchoredPosition = new Vector2(x, ballLabel.rectTransform.anchoredPosition.y);
            x += labelWidth + SmallGap;
            acquiredBadge.anchoredPosition = new Vector2(x, acquiredBadge.anchoredPosition.y);
            if (popAcquired)
                Pop(acquiredBadge);
        }

        // [아이콘 글자] 두 항목을 gap을 두고 이어 붙이고, 묶음 전체를 가운데에 둬. 항목은 왼쪽 가운데 기준이야.
        private static void LayoutRow(RectTransform group, RectTransform icon, float gap, RectTransform label, float labelWidth)
        {
            if (group == null || icon == null || label == null)
                return;
            float x = -(icon.sizeDelta.x + gap + labelWidth) * 0.5f;
            icon.anchoredPosition = new Vector2(x, icon.anchoredPosition.y);
            label.anchoredPosition = new Vector2(x + icon.sizeDelta.x + gap, label.anchoredPosition.y);
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
