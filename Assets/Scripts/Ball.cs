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

        public bool IsFlying { get; private set; }
        public bool IsDescending => IsFlying && body.linearVelocity.y < 0f;
        public event Action Returned;

        private void Awake()
        {
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
                brick.TakeDamage(1);
        }

        public void ReturnToLaunch()
        {
            if (!IsFlying)
                return;

            // 공을 없애지 않고 처음 발사 위치로 돌려놓아.
            Prepare(launchPosition);
            Returned?.Invoke();
        }

        private void OnDestroy()
        {
            if (bounceMaterial != null)
                Destroy(bounceMaterial);
        }
    }
}
