using UnityEngine;

namespace ElementalBreaker
{
    // 화면에 붙는 UI를 게임판 경계선에 맞춰. 폰마다 화면 비율과 UI 배율이 달라도 띠 끝이 선에 딱 붙어.
    // - Top: 화면 위 ~ 위쪽 경계선까지 채우는 상단 띠 (위에 붙고 높이를 바꿔)
    // - Bottom: 회수선 ~ 화면 아래까지 채우는 하단 띠 (아래에 붙고 높이를 바꿔)
    // - Between: 두 띠 사이 (상점이 열릴 때 게임판을 어둡게 덮는 막)
    // - AboveBottom: 하단 띠 위로 margin만큼 띄움 (상점 목록, 공 속성 선택지)
    // - BelowTop: 상단 띠 아래로 margin만큼 내림 (2배속 표시)
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class PlayAreaBand : MonoBehaviour
    {
        public enum Mode { Top, Bottom, Between, AboveBottom, BelowTop }

        [SerializeField] private Mode mode;
        [Tooltip("AboveBottom, BelowTop: 띠에서 떨어뜨릴 거리. Between: 위아래로 더 줄일 여백.")]
        [SerializeField] private float margin;

        private RectTransform rect;
        private RectTransform canvasRect;
        private BoardPresentation presentation;
        private float shownTop = -1f, shownBottom = -1f;

        // 실행 중에 만드는 UI(2배속 표시)에서 붙일 때 써.
        public void Configure(Mode bandMode, float bandMargin)
        {
            mode = bandMode;
            margin = bandMargin;
            shownTop = shownBottom = -1f;
        }

        private void OnEnable() => shownTop = shownBottom = -1f;
        private void OnValidate() => shownTop = shownBottom = -1f;

        private void LateUpdate()
        {
            if (!TryMeasure(out float top, out float bottom))
                return;
            if (Mathf.Approximately(top, shownTop) && Mathf.Approximately(bottom, shownBottom))
                return;
            shownTop = top;
            shownBottom = bottom;
            switch (mode)
            {
                case Mode.Top:
                    rect.sizeDelta = new Vector2(rect.sizeDelta.x, top);
                    break;
                case Mode.Bottom:
                    rect.sizeDelta = new Vector2(rect.sizeDelta.x, bottom);
                    break;
                case Mode.Between:
                    rect.offsetMin = new Vector2(rect.offsetMin.x, bottom + margin);
                    rect.offsetMax = new Vector2(rect.offsetMax.x, -(top + margin));
                    break;
                case Mode.AboveBottom:
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, bottom + margin);
                    break;
                case Mode.BelowTop:
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -(top + margin));
                    break;
            }
        }

        // 상단 띠 높이(화면 위 ~ 위쪽 경계선)와 하단 띠 높이(회수선 ~ 화면 아래)를 캔버스 단위로 재.
        private bool TryMeasure(out float top, out float bottom)
        {
            top = bottom = 0f;
            if (rect == null)
                rect = (RectTransform)transform;
            if (canvasRect == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas == null)
                    return false;
                canvasRect = (RectTransform)canvas.rootCanvas.transform;
            }
            if (presentation == null)
                presentation = FindFirstObjectByType<BoardPresentation>();
            Camera view = presentation != null ? presentation.View : null;
            if (view == null || view.pixelHeight <= 0 || !presentation.TryGetPlayBounds(out float topY, out float bottomY))
                return false;
            float height = canvasRect.rect.height;
            float topScreen = view.WorldToScreenPoint(new Vector3(0f, topY, 0f)).y / view.pixelHeight;
            float bottomScreen = view.WorldToScreenPoint(new Vector3(0f, bottomY, 0f)).y / view.pixelHeight;
            top = Mathf.Max(0f, height * (1f - topScreen));
            bottom = Mathf.Max(0f, height * bottomScreen);
            return true;
        }
    }
}
