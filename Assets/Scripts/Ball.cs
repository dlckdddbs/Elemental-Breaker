using System;
using UnityEngine;

namespace ElementalBreaker
{
    // 공의 이동, 충돌 데미지, 회수를 맡아.
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class Ball : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 10f;
        [SerializeField, Range(0.01f, 0.5f)] private float minimumVerticalRatio = 0.08f;
        [SerializeField] private float fallbackReturnY = -8.6f;

        private Rigidbody2D body;
        private PhysicsMaterial2D bounceMaterial;
        private Vector2 launchPosition;
        private Vector2 lastDirection = Vector2.up;
        private SpriteRenderer visual;

        public bool IsFlying { get; private set; }
        public bool IsDescending => IsFlying && body.linearVelocity.y < 0f;
        public event Action<Ball> Returned;
        public event Action<Ball> Retired;
        public ElementType Element { get; private set; }
        public int ReverseHitCount { get; private set; }

        public void PrepareTurn(Vector2 position, ElementType element)
        {
            Prepare(position);
            Element = element;
            ReverseHitCount = 0;
            if (visual == null)
                visual = GetComponent<SpriteRenderer>();
            if (visual != null)
            {
                Sprite sprite = ElementRules.GetSprite(element, true);
                if (sprite != null)
                    visual.sprite = sprite;
                visual.enabled = true;
            }
        }

        private void Awake()
        {
            InitializePhysics();
        }

        private void InitializePhysics()
        {
            if (body != null)
                return;
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            // 반사는 물리 엔진에 맡겨. 직접 반사하면 두 번 튕길 수 있어.
            bounceMaterial = new PhysicsMaterial2D("Ball Bounce")
            {
                friction = 0f,
                bounciness = 1f
            };
            GetComponent<CircleCollider2D>().sharedMaterial = bounceMaterial;
            body.simulated = false;
        }

        public void Prepare(Vector2 position)
        {
            // 비활성 프리팹에서 만든 공도 물리를 먼저 준비해.
            InitializePhysics();
            IsFlying = false;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
            launchPosition = position;
            body.position = position;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        public void Launch(Vector2 direction)
        {
            if (IsFlying || direction.sqrMagnitude < 0.001f || direction.y <= 0f)
                return;

            lastDirection = direction.normalized;
            body.simulated = true;
            IsFlying = true;
            body.linearVelocity = lastDirection * speed;
        }

        private void FixedUpdate()
        {
            if (!IsFlying)
                return;

            if (body.position.y <= fallbackReturnY)
            {
                ReturnToLaunch();
                return;
            }

            // 충돌 뒤에도 속도를 유지하고 수평으로만 왕복하는 걸 막아.
            Vector2 direction = body.linearVelocity.sqrMagnitude > 0.001f
                ? body.linearVelocity.normalized : lastDirection;
            if (Mathf.Abs(direction.y) < minimumVerticalRatio)
            {
                float verticalSign = direction.y == 0f ? Mathf.Sign(lastDirection.y) : Mathf.Sign(direction.y);
                direction.y = verticalSign * minimumVerticalRatio;
                direction.x = Mathf.Sign(direction.x) * Mathf.Sqrt(1f - direction.y * direction.y);
            }
            lastDirection = direction;
            body.linearVelocity = direction * speed;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsFlying && collision.collider.TryGetComponent(out Brick brick))
                HitBrick(brick);
        }

        private void HitBrick(Brick brick)
        {
            if (!IsFlying || brick == null || brick.Health <= 0)
                return;
            bool reverse = ElementRules.HasAdvantage(brick.Element, Element);
            brick.TakeDamage(ElementRules.Damage(Element, brick.Element));
            if (!reverse || ++ReverseHitCount < 2)
                return;

            // 퇴장은 바닥 회수와 구분해서 발사 위치를 바꾸지 않아.
            Prepare(launchPosition);
            if (visual != null)
                visual.enabled = false;
            Retired?.Invoke(this);
        }

        public void ReturnToLaunch()
        {
            if (!IsFlying)
                return;

            // 떨어진 X 위치를 다음 발사 위치로 써. 발사 높이는 유지해.
            Vector2 nextLaunchPosition = new Vector2(body.position.x, launchPosition.y);
            Prepare(nextLaunchPosition);
            Returned?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (bounceMaterial != null)
                Destroy(bounceMaterial);
        }
    }
}
