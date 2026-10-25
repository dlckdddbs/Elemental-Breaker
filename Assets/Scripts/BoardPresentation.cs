using DG.Tweening;
using UnityEngine;

namespace ElementalBreaker
{
    // 씬에 저장된 배경과 회수선을 화면 크기에 맞춰. 오브젝트는 만들지 않아.
    // 카메라는 480x854(9:16)에서 짠 게임판이 어떤 폰에서든 가로로 꽉 차게 맞춰.
    // 더 길쭉한 폰에서 남는 세로 길이는 위쪽(상단 UI와 게임판 사이)으로 보내서, 게임판과 하단 버튼 사이는 480x854와 같아.
    [ExecuteAlways]
    public sealed class BoardPresentation : MonoBehaviour
    {
        [SerializeField] private Camera view;
        [Header("화면 비율 맞추기 (480x854 기준)")]
        [Tooltip("480x854에서 보이는 월드 크기야. 가로는 보드 폭, 세로는 Orthographic Size의 두 배.")]
        [SerializeField] private Vector2 designView = new Vector2(5.62f, 10f);
        [Tooltip("480x854에서 카메라가 보는 가운데 월드 위치야.")]
        [SerializeField] private Vector2 designCenter = Vector2.zero;
        [Tooltip("더 길쭉한 폰에서 남는 세로 길이 중 위로 보낼 비율. 1이면 전부 위(게임판이 아래에 붙음), 0.5면 위아래 반씩.")]
        [SerializeField, Range(0f, 1f)] private float extraSpaceTop = 1f;
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private BoxCollider2D returnZone;
        [SerializeField] private SpriteRenderer returnLine;
        [SerializeField, Min(0.5f)] private float returnLinePixels = 1.5f;
        [SerializeField] private BoxCollider2D topWall;
        [SerializeField] private SpriteRenderer topBoundaryLine;
        [SerializeField, Min(0.5f)] private float topLinePixels = 1f;

        // 공이 다니는 영역의 위아래 끝이야. 위는 위쪽 벽 안쪽 면, 아래는 회수선이야. 얼음 테두리를 여기에 맞춰.
        public bool TryGetPlayBounds(out float top, out float bottom)
        {
            top = topWall != null ? topWall.bounds.min.y : 0f;
            bottom = returnZone != null ? returnZone.bounds.max.y : 0f;
            return topWall != null && returnZone != null;
        }

        private float fittedAspect = -1f;

        private void OnEnable()
        {
            fittedAspect = -1f;
            RefreshLayout();
        }

        private void LateUpdate() => RefreshLayout();

        // 인스펙터에서 값을 바꾸면 다음 프레임에 카메라를 다시 맞춰.
        private void OnValidate() => fittedAspect = -1f;

        private void RefreshLayout()
        {
            if (view == null || !view.orthographic)
                return;
            FitCamera();
            float height = view.orthographicSize * 2f;
            float width = height * view.aspect;
            if (background != null && background.sprite != null)
            {
                Vector2 size = background.sprite.bounds.size;
                float scale = Mathf.Max(width / size.x, height / size.y);
                background.transform.localScale = Vector3.one * scale;
                background.transform.position = new Vector3(view.transform.position.x, view.transform.position.y, 1f);
            }
            if (returnZone != null && returnLine != null && returnLine.sprite != null)
            {
                Bounds bounds = returnZone.bounds;
                Vector2 size = returnLine.sprite.bounds.size;
                returnLine.transform.position = new Vector3(bounds.center.x, bounds.max.y, 0f);
                returnLine.transform.localScale = new Vector3(bounds.size.x / size.x,
                    width / 480f * returnLinePixels / size.y, 1f);
            }
            if (topWall != null && topBoundaryLine != null && topBoundaryLine.sprite != null)
            {
                // 공이 닿는 위쪽 벽의 안쪽 면에 선을 맞춰.
                Bounds bounds = topWall.bounds;
                Vector2 size = topBoundaryLine.sprite.bounds.size;
                Transform line = topBoundaryLine.transform;
                Vector3 parentScale = line.parent != null ? line.parent.lossyScale : Vector3.one;
                line.position = new Vector3(bounds.center.x, bounds.min.y, 0f);
                line.localScale = new Vector3(bounds.size.x / size.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                    width / 480f * topLinePixels / size.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1f);
            }
        }

        // 화면 비율이 바뀔 때만 카메라를 맞춰. 매 프레임 맞추면 화면 흔들림을 덮어써.
        private void FitCamera()
        {
            float aspect = view.aspect;
            if (Mathf.Approximately(aspect, fittedAspect))
                return;
            fittedAspect = aspect;
            float designSize = designView.y * 0.5f;
            // 480x854보다 길쭉하면 보드 폭이 꽉 차게 세로를 늘려. 넓적하면(태블릿) 세로는 그대로 두고 양옆에 배경이 더 보여.
            float size = aspect < designView.x / designView.y ? designView.x / aspect * 0.5f : designSize;
            view.orthographicSize = size;
            // 늘어난 세로 길이(size - designSize)를 위아래로 나눠. 아래로 갈 몫만큼 카메라 가운데를 올려.
            float centerY = designCenter.y + (size - designSize) * (2f * extraSpaceTop - 1f);
            Transform cam = view.transform;
            // 흔들리던 중이면 멈추고 새 자리에서 시작해. 다음 흔들림은 이 자리를 기준으로 해.
            if (Application.isPlaying)
                cam.DOKill();
            cam.position = new Vector3(designCenter.x, centerY, cam.position.z);
        }
    }
}
