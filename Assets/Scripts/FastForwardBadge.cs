using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker
{
    // 배속 중에만 상단 정보 아래에 표시해. 터치는 받지 않아.
    public sealed class FastForwardBadge : MonoBehaviour
    {
        // 상단 띠(화면 위 ~ 위쪽 경계선) 바로 아래, 게임판 위쪽 빈 칸에 떠. 처음 자리이고 실행 중에는 경계선을 따라가.
        private const float BadgeTop = -127f;
        private const float BelowTopBand = 9f;
        // 얼음 상태 표시가 맨 위 빈 칸 가운데에 떠 있으면 ×2를 이만큼 오른쪽으로 비켜.
        private const float AsideOffset = 124f;

        private Canvas canvas;
        private RectTransform badgeRect;
        private RectTransform safeRoot;
        private Rect lastSafeArea;
        private Vector2Int lastScreen;

        public static FastForwardBadge Create()
        {
            GameObject root = new GameObject("Fast Forward UI (Runtime)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            FastForwardBadge badge = root.AddComponent<FastForwardBadge>();
            badge.canvas = root.GetComponent<Canvas>();
            badge.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            badge.canvas.sortingOrder = 10;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(480f, 854f);
            scaler.matchWidthOrHeight = 0.5f;

            badge.safeRoot = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
            badge.safeRoot.SetParent(root.transform, false);
            Image background = new GameObject("Speed Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            background.transform.SetParent(badge.safeRoot, false);
            RectTransform rect = background.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, BadgeTop);
            badge.badgeRect = rect;
            background.gameObject.AddComponent<PlayAreaBand>().Configure(PlayAreaBand.Mode.BelowTop, BelowTopBand);
            rect.sizeDelta = new Vector2(68f, 32f);
            background.color = new Color32(30, 27, 61, 210);
            background.raycastTarget = false;

            TextMeshProUGUI label = new GameObject("Multiplier", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(rect, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            // 기존 HUD 폰트를 처음에 한 번만 가져와.
            TopHUD hud = Object.FindFirstObjectByType<TopHUD>();
            TMP_Text sample = hud != null ? hud.GetComponentInChildren<TMP_Text>(true) : null;
            if (sample != null)
                label.font = sample.font;
            label.text = "×2";
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color32(255, 228, 138, 255);
            label.raycastTarget = false;
            badge.UpdateSafeArea();
            badge.SetVisible(false);
            return badge;
        }

        public void SetVisible(bool visible) => canvas.enabled = visible;

        // 얼음 상태 표시와 겹치지 않게 오른쪽으로 비키거나 가운데로 돌아와.
        public void SetAside(bool aside) => badgeRect.anchoredPosition = new Vector2(aside ? AsideOffset : 0f, badgeRect.anchoredPosition.y);

        private void LateUpdate()
        {
            if (lastSafeArea != Screen.safeArea || lastScreen.x != Screen.width || lastScreen.y != Screen.height)
                UpdateSafeArea();
        }

        private void UpdateSafeArea()
        {
            lastSafeArea = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            Vector2 size = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            safeRoot.anchorMin = lastSafeArea.min / size;
            safeRoot.anchorMax = lastSafeArea.max / size;
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        }
    }
}
