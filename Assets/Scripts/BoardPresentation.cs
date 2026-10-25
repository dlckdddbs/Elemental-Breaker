using UnityEngine;

namespace ElementalBreaker
{
    // 씬에 저장된 배경과 회수선을 화면 크기에 맞춰. 오브젝트는 만들지 않아.
    [ExecuteAlways]
    public sealed class BoardPresentation : MonoBehaviour
    {
        [SerializeField] private Camera view;
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

        private void OnEnable() => RefreshLayout();
        private void LateUpdate() => RefreshLayout();

        private void RefreshLayout()
        {
            if (view == null || !view.orthographic)
                return;
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
    }
}
