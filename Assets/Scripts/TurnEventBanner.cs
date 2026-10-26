using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker
{
    // 턴 이벤트(해금, 골드, 디버프)를 보여 주는 배너야. TurnEvents가 이벤트마다 불러.
    // 상단 정보판 바로 아래에 "N턴 도달!" 배너가 톡 커지며 나타나고, 잠깐 머문 뒤 받은 것이 제자리로 날아가.
    // - 해금(보관형): 아이콘이 아이템 칸으로 날아가서 도착하는 순간 칸에 들어가. 칸이 차 있었으면 동전이 골드로 날아가.
    // - 해금(벽돌 속성 변경): 배너가 사라지면 속성 물결이 퍼지고, 물결이 끝나면 넘어가.
    // - 골드 보상: 해금보다 작은 배너로 보여 주고, 동전이 골드로 날아가.
    // - 디버프: 붉은 경고 배너("N턴 경고!")가 흔들리며 나타나고 화면 가장자리가 붉게 번쩍여.
    //   배너가 사라지면 그 턴 내내 상단 왼쪽에 빨간 표시가 남아. 두 칸 하강 턴에 얼음을 쓰면 표시가 회색으로 흐려져.
    // 빛줄기는 큰 보상 배너가 나타나는 순간에만 아주 흐리게 잠깐 보여. 연출은 모두 실제 시간으로 움직여.
    public sealed class TurnEventBanner : MonoBehaviour
    {
        // 배너 뒤 게임판을 살짝 어둡게 해. 골드 보상은 덜 중요해서 더 옅게 해.
        private const float ScrimAlpha = 0.38f;
        private const float SmallScrimAlpha = 0.2f;
        private const float ScrimTime = 0.2f;
        // 골드 보상은 해금보다 작게 보여 줘.
        private const float SmallScale = 0.78f;
        // 작게 시작해서 톡 커지며 나타나고, 다 나타난 뒤 HoldTime만큼 머물러.
        private const float EnterScale = 0.8f;
        private const float EnterTime = 0.3f;
        private const float FadeInTime = 0.15f;
        private const float HoldTime = 1f;
        private const float ExitTime = 0.25f;
        private const float ExitScale = 0.96f;
        private const float RaysAlpha = 0.06f;
        private const float RaysInTime = 0.15f;
        private const float RaysOutStart = 0.35f;
        private const float RaysOutTime = 0.4f;
        private const float RaysTurn = 20f;
        // 모서리 반짝이는 두 개만 한 번 반짝이고 사라져.
        private const float SparkleDelay = 0.15f;
        private const float SparkleInTime = 0.15f;
        private const float SparkleOutTime = 0.35f;
        // 경고 배너는 나타나면서 좌우로 짧게 흔들리고, 화면 가장자리가 붉게 한 번 번쩍여.
        private const float WarningShake = 8f;
        private const float WarningShakeTime = 0.35f;
        private const float VignetteAlpha = 0.45f;
        private const float VignetteInTime = 0.12f;
        private const float VignetteOutTime = 0.5f;
        // 아이콘이 아이템 칸으로 날아가는 시간과 꼬리 반짝이 간격(경로 비율)이야.
        private const float FlyTime = 0.55f;
        private const float TrailSpacing = 0.04f;
        // 동전은 조금씩 늦게 출발해서 줄지어 골드로 날아가.
        private const float CoinTime = 0.5f;
        private const float CoinStagger = 0.06f;
        private const float CoinSpread = 8f;
        private const int RefundCoins = 4;
        private const int RewardCoins = 7;
        private const float BurstTime = 0.35f;
        // "+100"이 정보판 바로 아래에서 떠오르며 사라져.
        private const float GainRise = 30f;
        private const float GainTime = 0.9f;
        // 속성 물결이 다 지나간 뒤 조금 더 기다렸다가 넘어가.
        private const float WaveTail = 0.2f;
        private const float IconSize = 42f;
        private const float IconGap = 10f;
        // 안내 글자가 없으면(골드 보상) 제목과 내용 줄을 이만큼 내려서 판 가운데에 맞춰.
        private const float NoSubShift = 9f;
        // 상단 왼쪽 디버프 표시야. 톡 커지며 나타나고, 글자 길이에 맞춰 알약 길이가 바뀌어.
        private const float ChipPopTime = 0.25f;
        private const float ChipPadding = 44f;
        // 쉬는 공 안내 글자를 처음 발사 자리 오른쪽에 이만큼 띄워.
        private const float RestingOffset = 18f;
        private static readonly string[] ElementNames = { "일반", "불", "물", "풀" };
        private static readonly Color32 GoldTop = new Color32(0xFF, 0xF6, 0xC2, 0xFF);
        private static readonly Color32 GoldBottom = new Color32(0xFF, 0xC2, 0x3D, 0xFF);
        private static readonly Color32 RedTop = new Color32(0xFF, 0xE6, 0xDC, 0xFF);
        private static readonly Color32 RedBottom = new Color32(0xFF, 0x6A, 0x55, 0xFF);
        private static readonly Color32 RewardSubColor = new Color32(0xE6, 0xEC, 0xFF, 0xFF);
        private static readonly Color32 WarningSubColor = new Color32(0xFF, 0xD9, 0xD2, 0xFF);
        private static readonly Color32 ChipColor = new Color32(0xC4, 0x2A, 0x40, 0xFF);
        private static readonly Color32 ChipOffColor = new Color32(0x5A, 0x61, 0x78, 0xFF);
        private static readonly Color32 ChipOffText = new Color32(0xC9, 0xCE, 0xD9, 0xFF);

        [SerializeField] private BrickBoard board;
        [SerializeField] private BallLauncher launcher;
        [SerializeField] private TopHUD hud;
        [SerializeField] private RectTransform itemSlot;
        [Header("배너")]
        [SerializeField] private Image scrim;
        [SerializeField] private Image rays;
        [SerializeField] private RectTransform banner;
        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Image icon;
        [SerializeField] private Image iconOverlay;
        [SerializeField] private TMP_Text line;
        [SerializeField] private TMP_Text sub;
        [SerializeField] private RectTransform[] sparkles;
        [Header("아이콘 그림 (폭탄은 다음 턴 속성의 폭탄을 코드에서 불러와)")]
        [SerializeField] private Sprite iceIcon;
        [SerializeField] private Sprite coinIcon;
        [SerializeField] private Sprite brickIcon;
        [SerializeField] private Sprite cycleIcon;
        [Header("경고 배너 (디버프)")]
        [Tooltip("배너 바탕이야. 보상은 rewardPanel, 디버프는 warningPanel 그림으로 바꿔 끼워.")]
        [SerializeField] private Image panel;
        [SerializeField] private Sprite rewardPanel;
        [SerializeField] private Sprite warningPanel;
        [Tooltip("경고 배너 안쪽 왼편의 빨간 느낌표야.")]
        [SerializeField] private GameObject alert;
        [Tooltip("화면 가장자리가 붉게 번쩍이는 그림이야.")]
        [SerializeField] private Image vignette;
        [Tooltip("체력 2배, 아이템 금지, 두 칸 하강, 공 절반 순서야.")]
        [SerializeField] private Sprite[] debuffIcons = new Sprite[4];
        [Header("디버프 표시 (상단 왼쪽, 그 턴 내내)")]
        [SerializeField] private RectTransform chip;
        [SerializeField] private Image chipBack;
        [SerializeField] private Image chipDot;
        [SerializeField] private TMP_Text chipMark;
        [SerializeField] private TMP_Text chipLabel;
        [Tooltip("공 절반 턴에 처음 발사 자리에서 쉬는 공 수를 알려 줘.")]
        [SerializeField] private TMP_Text restingLabel;
        [Header("날아가는 것")]
        [SerializeField] private RectTransform flyer;
        [SerializeField] private Image flyerIcon;
        [SerializeField] private RectTransform[] trail;
        [SerializeField] private RectTransform[] coins;
        [SerializeField] private Image burst;
        [SerializeField] private TMP_Text gain;

        private RectTransform root;
        private Canvas canvas;
        private TurnEvent current;
        private Action applyEvent;
        private Action finishEvent;
        private float bannerScale = 1f;
        private Vector2 flyFrom, flyControl, flyTo;
        private RectTransform lineRow;
        private float titleY, lineRowY;
        private Debuff chipDebuff = Debuff.None;
        private bool chipOff;
        private int shownResting = -1;

        private void Awake()
        {
            root = (RectTransform)transform;
            if (board == null || launcher == null || hud == null || itemSlot == null || banner == null)
            {
                Debug.LogError("TurnEventBanner: Board, Launcher, HUD, Item Slot, Banner를 연결해. Apply Layout 메뉴로 다시 만들어.", this);
                enabled = false;
                return;
            }
            canvas = GetComponentInParent<Canvas>().rootCanvas;
            lineRow = (RectTransform)line.rectTransform.parent;
            titleY = title.rectTransform.anchoredPosition.y;
            lineRowY = lineRow.anchoredPosition.y;
            // 보드가 첫 턴 이벤트를 보여 주기 전에 붙어야 해서 Start가 아니라 Awake에서 붙여.
            board.TurnEvents.Presenter = Present;
            HideAll(true);
            if (chip != null)
                chip.gameObject.SetActive(false);
            if (restingLabel != null)
                restingLabel.gameObject.SetActive(false);
        }

        private void Present(TurnEvent turnEvent, Action apply, Action done)
        {
            current = turnEvent;
            applyEvent = apply;
            finishEvent = done;
            // 글자 길이를 재기 전에 켜 둬. 다 채운 뒤 투명한 상태에서 나타나기 시작해.
            banner.gameObject.SetActive(true);
            bannerGroup.alpha = 0f;
            bool warning = turnEvent.Kind == TurnEventKind.Debuff;
            SetStyle(warning);
            Fill(turnEvent);
            PlayEnter(turnEvent.Kind == TurnEventKind.Gold, warning);
        }

        private ElementType NextElement => launcher.ReservedElement ?? launcher.TurnElement;

        // 보상은 남색·금색, 디버프는 붉은 테두리의 경고 배너야.
        private void SetStyle(bool warning)
        {
            Sprite panelSprite = warning ? warningPanel : rewardPanel;
            if (panel != null && panelSprite != null)
                panel.sprite = panelSprite;
            if (alert != null)
                alert.SetActive(warning);
            Color32 top = warning ? RedTop : GoldTop, bottom = warning ? RedBottom : GoldBottom;
            title.colorGradient = new VertexGradient(top, top, bottom, bottom);
            sub.color = warning ? WarningSubColor : RewardSubColor;
        }

        private void Fill(TurnEvent turnEvent)
        {
            icon.color = Color.white;
            iconOverlay.enabled = false;
            string subText = null;
            switch (turnEvent.Kind)
            {
                case TurnEventKind.Gold:
                    title.SetText("{0}턴 도달!", turnEvent.Turn);
                    icon.sprite = coinIcon;
                    line.SetText("+{0} 골드", turnEvent.Gold);
                    break;
                case TurnEventKind.Debuff:
                    title.SetText("{0}턴 경고!", turnEvent.Turn);
                    icon.sprite = debuffIcons[(int)turnEvent.Debuff - 1];
                    line.SetText(DebuffLine(turnEvent.Debuff));
                    subText = DebuffSub(turnEvent.Debuff);
                    break;
                default:
                    title.SetText("{0}턴 도달!", turnEvent.Turn);
                    line.SetText(ItemName(turnEvent.Item) + " 해금!");
                    switch (turnEvent.Item)
                    {
                        case ShopItem.Bomb:
                            icon.sprite = ElementRules.GetBombIcon(NextElement);
                            break;
                        case ShopItem.Ice:
                            icon.sprite = iceIcon;
                            break;
                        default:
                            // 바뀔 속성 색 벽돌에 순환 화살표를 겹쳐.
                            icon.sprite = brickIcon;
                            icon.color = ElementRules.GetBrickTint(turnEvent.BrickElement);
                            iconOverlay.sprite = cycleIcon;
                            iconOverlay.enabled = true;
                            break;
                    }
                    // 안내 글자는 짧게 줄여서 폰에서도 한눈에 읽혀.
                    subText = turnEvent.Item == ShopItem.BrickChange ? "모든 벽돌을 " + ElementNames[(int)turnEvent.BrickElement] + " 속성으로!"
                        : turnEvent.Granted ? "아이템 칸에 지급!" : "칸이 차서 골드로 지급!";
                    break;
            }
            sub.gameObject.SetActive(subText != null);
            if (subText != null)
                sub.SetText(subText);
            float shift = subText == null ? -NoSubShift : 0f;
            title.rectTransform.anchoredPosition = new Vector2(title.rectTransform.anchoredPosition.x, titleY + shift);
            lineRow.anchoredPosition = new Vector2(lineRow.anchoredPosition.x, lineRowY + shift);
            // [아이콘 글자] 묶음이 배너 가운데에 오게 놓아.
            float width = IconSize + IconGap + line.preferredWidth;
            float left = -width * 0.5f;
            icon.rectTransform.anchoredPosition = new Vector2(left + IconSize * 0.5f, 0f);
            line.rectTransform.anchoredPosition = new Vector2(left + IconSize + IconGap, 0f);
        }

        private static string ItemName(ShopItem item) =>
            item == ShopItem.Bomb ? "속성 폭탄" : item == ShopItem.Ice ? "얼음" : "벽돌 속성 변경";

        private static string DebuffLine(Debuff debuff)
        {
            switch (debuff)
            {
                case Debuff.DoubleHealth: return "벽돌 체력 2배!";
                case Debuff.NoItems: return "아이템 사용 금지!";
                case Debuff.DoubleDescent: return "벽돌 두 칸 하강!";
                default: return "공 절반만 발사!";
            }
        }

        private string DebuffSub(Debuff debuff)
        {
            switch (debuff)
            {
                case Debuff.DoubleHealth: return "새로 나오는 줄의 체력 ×2";
                case Debuff.NoItems: return "아이템 · 공 속성 · 상점";
                case Debuff.DoubleDescent: return "얼음으로 막을 수 있어요";
                default: return "이번 턴만 " + launcher.ActiveBallCount + "개 → " + launcher.LaunchCount + "개";
            }
        }

        private static string ChipText(Debuff debuff)
        {
            switch (debuff)
            {
                case Debuff.DoubleHealth: return "체력 2배";
                case Debuff.NoItems: return "아이템 금지";
                case Debuff.DoubleDescent: return "두 칸 하강";
                default: return "공 절반";
            }
        }

        private void PlayEnter(bool small, bool warning)
        {
            bannerScale = small ? SmallScale : 1f;
            scrim.gameObject.SetActive(true);
            SetAlpha(scrim, 0f);
            banner.localScale = Vector3.one * (bannerScale * EnterScale);
            icon.enabled = true;

            Sequence enter = NewSequence();
            enter.Insert(0f, scrim.DOFade(small ? SmallScrimAlpha : ScrimAlpha, ScrimTime));
            enter.Insert(0f, bannerGroup.DOFade(1f, FadeInTime));
            enter.Insert(0f, banner.DOScale(bannerScale, EnterTime).SetEase(Ease.OutBack));
            if (warning)
            {
                enter.Insert(EnterTime * 0.5f, banner.DOPunchAnchorPos(new Vector2(WarningShake, 0f), WarningShakeTime, 12, 0.5f));
                if (vignette != null)
                {
                    vignette.gameObject.SetActive(true);
                    SetAlpha(vignette, 0f);
                    enter.Insert(0f, vignette.DOFade(VignetteAlpha, VignetteInTime));
                    enter.Insert(VignetteInTime, vignette.DOFade(0f, VignetteOutTime));
                }
            }
            else if (!small)
            {
                // 빛줄기는 나타나는 순간에만 아주 흐리게 잠깐 돌다가 사라져.
                RectTransform raysRect = rays.rectTransform;
                rays.gameObject.SetActive(true);
                raysRect.localPosition = Local(banner) + Vector2.down * (banner.rect.height * 0.5f * bannerScale);
                raysRect.localRotation = Quaternion.identity;
                SetAlpha(rays, 0f);
                enter.Insert(0f, rays.DOFade(RaysAlpha, RaysInTime));
                enter.Insert(RaysOutStart, rays.DOFade(0f, RaysOutTime));
                enter.Insert(0f, raysRect.DOLocalRotate(new Vector3(0f, 0f, -RaysTurn), RaysOutStart + RaysOutTime));
            }
            foreach (RectTransform sparkle in sparkles)
            {
                sparkle.localScale = Vector3.zero;
                if (warning)
                    continue;
                enter.Insert(SparkleDelay, sparkle.DOScale(1f, SparkleInTime).SetEase(Ease.OutBack));
                enter.Insert(SparkleDelay + SparkleInTime, sparkle.DOScale(0f, SparkleOutTime).SetEase(Ease.InQuad));
            }
            enter.InsertCallback(EnterTime + HoldTime, PlayExit);
        }

        private void PlayExit()
        {
            if (current.Kind == TurnEventKind.Debuff)
                ExitWarning();
            else if (current.Kind == TurnEventKind.Gold)
                FlyCoins(RewardCoins, current.Gold);
            else if (current.Item == ShopItem.BrickChange)
                ExitForWave();
            else if (current.Granted)
                FlyItem();
            else
                FlyCoins(RefundCoins, current.Gold);
        }

        // 경고 배너가 사라지면 상단 왼쪽 표시가 남아. 체력 2배 턴이면 이때 새 줄에 "×2"가 떠.
        private void ExitWarning()
        {
            Debuff debuff = current.Debuff;
            Sequence exit = NewSequence();
            InsertBannerFade(exit);
            exit.Insert(0f, scrim.DOFade(0f, ExitTime));
            exit.InsertCallback(ExitTime, () =>
            {
                applyEvent?.Invoke();
                ShowChip(debuff);
                if (debuff == Debuff.DoubleHealth)
                    board.RevealBoostedRow();
            });
            exit.InsertCallback(ExitTime + ChipPopTime, Finish);
        }

        private void ShowChip(Debuff debuff)
        {
            if (chip == null)
                return;
            chipDebuff = debuff;
            chipLabel.SetText(ChipText(debuff));
            chip.sizeDelta = new Vector2(ChipPadding + chipLabel.preferredWidth, chip.sizeDelta.y);
            SetChipOff(false);
            chip.gameObject.SetActive(true);
            chip.DOKill();
            chip.localScale = Vector3.one * 0.6f;
            chip.DOScale(1f, ChipPopTime).SetEase(Ease.OutBack).SetUpdate(true);
        }

        // 꺼진 표시(두 칸 하강을 얼음으로 막음)는 회색으로만 흐리게 해.
        private void SetChipOff(bool off)
        {
            chipOff = off;
            chipBack.color = off ? ChipOffColor : ChipColor;
            chipDot.color = off ? (Color)ChipOffText : Color.white;
            chipMark.color = off ? ChipOffColor : ChipColor;
            chipLabel.color = off ? (Color)ChipOffText : Color.white;
        }

        private void LateUpdate()
        {
            UpdateChip();
            UpdateResting();
        }

        // 디버프 표시는 다음 턴이 시작되거나 게임이 끝나면 내려가.
        private void UpdateChip()
        {
            if (chipDebuff == Debuff.None)
                return;
            if (board.TurnEvents.ActiveDebuff != chipDebuff || board.IsGameOver)
            {
                chipDebuff = Debuff.None;
                chip.DOKill();
                chip.gameObject.SetActive(false);
                return;
            }
            bool off = chipDebuff == Debuff.DoubleDescent && board.IsFrozen;
            if (off != chipOff)
                SetChipOff(off);
        }

        // 공 절반 턴에 공이 날아가는 동안, 쏘지 않은 공이 쉬는 자리 옆에 "3개는 쉬어요"를 보여 줘.
        private void UpdateResting()
        {
            if (restingLabel == null)
                return;
            int resting = launcher.RestingBallCount;
            if (resting > 0)
            {
                Camera view = Camera.main;
                if (view == null)
                    resting = 0;
                else
                {
                    Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    Vector2 screen = view.WorldToScreenPoint(launcher.TurnLaunchPosition);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out Vector2 local);
                    restingLabel.rectTransform.localPosition = local + Vector2.right * RestingOffset;
                }
            }
            if (resting == shownResting)
                return;
            shownResting = resting;
            restingLabel.gameObject.SetActive(resting > 0);
            if (resting > 0)
                restingLabel.SetText("{0}개는 쉬어요", resting);
        }

        // 아이콘이 곡선을 그리며 아이템 칸으로 날아가고, 닿는 순간 칸에 들어가.
        private void FlyItem()
        {
            flyFrom = Local(icon.rectTransform);
            flyTo = Local(itemSlot);
            flyControl = new Vector2(Mathf.Min(flyFrom.x, flyTo.x) - Mathf.Abs(flyFrom.x - flyTo.x) * 0.6f - 40f,
                (flyFrom.y + flyTo.y) * 0.5f);
            flyerIcon.sprite = icon.sprite;
            icon.enabled = false;
            flyer.gameObject.SetActive(true);
            UpdateFlight(0f);

            Sequence exit = NewSequence();
            InsertBannerFade(exit);
            exit.Insert(0f, DOVirtual.Float(0f, 1f, FlyTime, UpdateFlight).SetEase(Ease.InOutSine));
            exit.InsertCallback(FlyTime, () =>
            {
                flyer.gameObject.SetActive(false);
                UpdateFlight(1f);
                applyEvent?.Invoke();
                PlayBurst(flyTo);
            });
            exit.Insert(FlyTime, scrim.DOFade(0f, ScrimTime));
            exit.InsertCallback(FlyTime + BurstTime, Finish);
        }

        private void UpdateFlight(float t)
        {
            flyer.localPosition = Bezier(t);
            for (int i = 0; i < trail.Length; i++)
            {
                float behind = t - (i + 1) * TrailSpacing;
                bool on = behind > 0f && t < 1f;
                trail[i].gameObject.SetActive(on);
                if (!on)
                    continue;
                float scale = 1f - (i + 1f) / (trail.Length + 2f);
                trail[i].localPosition = Bezier(behind);
                trail[i].localScale = new Vector3(scale, scale, 1f);
            }
        }

        // 동전이 줄지어 상단 골드로 날아가. 첫 동전이 닿는 순간 골드가 들어와서 숫자가 올라가.
        private void FlyCoins(int count, int amount)
        {
            flyFrom = Local(icon.rectTransform);
            flyTo = Local(hud.GoldIcon);
            flyControl = new Vector2(flyTo.x, flyFrom.y);
            icon.enabled = false;
            count = Mathf.Min(count, coins.Length);

            Sequence exit = NewSequence();
            InsertBannerFade(exit);
            for (int i = 0; i < count; i++)
            {
                RectTransform coin = coins[i];
                Vector2 spread = new Vector2(i % 2 == 0 ? -CoinSpread : CoinSpread, 0f);
                float delay = i * CoinStagger;
                coin.localPosition = Bezier(0f) + spread;
                exit.InsertCallback(delay, () => coin.gameObject.SetActive(true));
                exit.Insert(delay, DOVirtual.Float(0f, 1f, CoinTime, t => coin.localPosition = Bezier(t) + spread * (1f - t))
                    .SetEase(Ease.InQuad));
                exit.InsertCallback(delay + CoinTime, () => coin.gameObject.SetActive(false));
            }
            exit.InsertCallback(CoinTime, () =>
            {
                applyEvent?.Invoke();
                PlayBurst(flyTo);
                ShowGain(amount);
            });
            float last = (count - 1) * CoinStagger + CoinTime;
            exit.Insert(last, scrim.DOFade(0f, ScrimTime));
            exit.InsertCallback(Mathf.Max(last + ScrimTime, CoinTime + GainTime), Finish);
        }

        // 배너가 사라지면 속성 물결이 퍼져. 물결이 다 지나가면 넘어가.
        private void ExitForWave()
        {
            Sequence exit = NewSequence();
            InsertBannerFade(exit);
            exit.Insert(0f, scrim.DOFade(0f, ExitTime));
            exit.InsertCallback(ExitTime, () => applyEvent?.Invoke());
            exit.InsertCallback(ExitTime + ElementWave.Duration + WaveTail, Finish);
        }

        private void InsertBannerFade(Sequence sequence)
        {
            sequence.Insert(0f, bannerGroup.DOFade(0f, ExitTime));
            sequence.Insert(0f, banner.DOScale(bannerScale * ExitScale, ExitTime));
        }

        private void PlayBurst(Vector2 at)
        {
            RectTransform rect = burst.rectTransform;
            rect.DOKill();
            burst.DOKill();
            rect.localPosition = at;
            rect.localScale = Vector3.one * 0.5f;
            SetAlpha(burst, 0.9f);
            burst.gameObject.SetActive(true);
            rect.DOScale(1.3f, BurstTime).SetEase(Ease.OutQuad).SetUpdate(true);
            burst.DOFade(0f, BurstTime).SetUpdate(true).OnComplete(() => burst.gameObject.SetActive(false));
        }

        // 받은 골드를 정보판 바로 아래, 골드 아이콘 밑에서 띄워.
        private void ShowGain(int amount)
        {
            RectTransform rect = gain.rectTransform;
            rect.DOKill();
            DOTween.Kill(gain);
            gain.SetText("+{0}", amount);
            Vector2 start = new Vector2(flyTo.x, Local(banner).y);
            rect.localPosition = start;
            rect.localScale = Vector3.one * 0.6f;
            gain.alpha = 1f;
            gain.gameObject.SetActive(true);
            rect.DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);
            rect.DOLocalMoveY(start.y + GainRise, GainTime).SetEase(Ease.OutQuad).SetUpdate(true);
            DOTween.To(() => gain.alpha, a => gain.alpha = a, 0f, GainTime * 0.5f).SetDelay(GainTime * 0.5f)
                .SetTarget(gain).SetUpdate(true).OnComplete(() => gain.gameObject.SetActive(false));
        }

        private void Finish()
        {
            HideAll(false);
            Action done = finishEvent;
            current = null;
            applyEvent = null;
            finishEvent = null;
            done?.Invoke();
        }

        private Sequence NewSequence() => DOTween.Sequence().SetUpdate(true).SetTarget(this);

        private Vector2 Bezier(float t)
        {
            float u = 1f - t;
            return u * u * flyFrom + 2f * u * t * flyControl + t * t * flyTo;
        }

        // 다른 UI의 위치를 이 오브젝트 기준 좌표로 바꿔. 모두 같은 캔버스 안이라 화면 비율이 달라도 맞아.
        private Vector2 Local(RectTransform target) => root.InverseTransformPoint(target.position);

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        // floating은 골드 숫자와 도착 고리야. 끝날 때는 스스로 사라지게 두고, 처음이나 꺼질 때만 같이 숨겨.
        // 디버프 표시와 쉬는 공 안내는 그 턴 동안 따로 남아서 여기서 숨기지 않아.
        private void HideAll(bool floating)
        {
            scrim.gameObject.SetActive(false);
            rays.gameObject.SetActive(false);
            banner.gameObject.SetActive(false);
            if (vignette != null)
                vignette.gameObject.SetActive(false);
            flyer.gameObject.SetActive(false);
            foreach (RectTransform dot in trail)
                dot.gameObject.SetActive(false);
            foreach (RectTransform coin in coins)
                coin.gameObject.SetActive(false);
            if (!floating)
                return;
            burst.gameObject.SetActive(false);
            gain.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            DOTween.Kill(this);
            DOTween.Kill(gain);
            if (burst != null)
            {
                burst.DOKill();
                burst.rectTransform.DOKill();
            }
            if (gain != null)
                gain.rectTransform.DOKill();
            if (chip != null)
                chip.DOKill();
        }
    }
}
