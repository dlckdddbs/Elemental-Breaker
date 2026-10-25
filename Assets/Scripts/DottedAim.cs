using UnityEngine;

namespace ElementalBreaker
{
    // 공 반지름으로 경로를 검사하고 미리 만든 점을 돌려 써.
    public sealed class DottedAim : MonoBehaviour
    {
        private readonly SpriteRenderer[] dots = new SpriteRenderer[160];
        private readonly RaycastHit2D[] hits = new RaycastHit2D[128];
        private ContactFilter2D filter;
        private Camera view;
        private float radius;
        private int visibleCount;

        public void Initialize(Camera camera, Ball template, Material material)
        {
            view = camera;
            CircleCollider2D circle = template.GetComponent<CircleCollider2D>();
            radius = circle.radius * Mathf.Max(Mathf.Abs(template.transform.lossyScale.x), Mathf.Abs(template.transform.lossyScale.y));
            filter = new ContactFilter2D();
            int layer = LayerMask.NameToLayer("Ball");
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(layer >= 0 ? layer : template.gameObject.layer));
            filter.useTriggers = false;
            Sprite sprite = Resources.Load<Sprite>("BoardVisuals/aim_dot");
            for (int i = 0; i < dots.Length; i++)
            {
                SpriteRenderer dot = new GameObject("Aim Dot").AddComponent<SpriteRenderer>();
                dot.transform.SetParent(transform, false);
                dot.sprite = sprite;
                dot.sharedMaterial = material;
                dot.sortingOrder = 10;
                dot.color = new Color(0.65f, 0.88f, 1f, 0.72f);
                dot.enabled = false;
                dots[i] = dot;
            }
        }

        public void Show(Vector2 origin, Vector2 direction, float length)
        {
            if (dots[0].sprite == null)
                return;
            float pixel = view.orthographicSize * 2f * view.aspect / 480f;
            float spacing = pixel * 14f;
            float scale = pixel * 10f / dots[0].sprite.bounds.size.x;
            float nextDot = spacing;
            int used = 0;
            // 첫 벽이나 벽돌까지만 발사 방향을 보여 줘.
            int count = Physics2D.CircleCast(origin, radius, direction, filter, hits, length);
            float distance = length;
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null || collider.TryGetComponent<Ball>(out _) || hits[i].distance <= 0.001f)
                    continue;
                distance = Mathf.Min(distance, hits[i].distance);
            }
            while (nextDot <= distance && used < dots.Length)
            {
                SpriteRenderer dot = dots[used++];
                dot.transform.position = origin + direction * nextDot;
                dot.transform.localScale = Vector3.one * scale;
                dot.enabled = true;
                nextDot += spacing;
            }
            for (int i = used; i < visibleCount; i++)
                dots[i].enabled = false;
            visibleCount = used;
        }

        public void Hide()
        {
            for (int i = 0; i < visibleCount; i++)
                dots[i].enabled = false;
            visibleCount = 0;
        }
    }
}
