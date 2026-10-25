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

        private const int RetireHitCount = 2;
        // 퇴장 직전 단계에서 흰색 쪽으로 섞이는 최대 비율이야.
        private const float MaxFade = 0.7f;
        // 트레일 머리 쪽 불투명도야. 꼬리 쪽은 투명해져.
        private const float TrailAlpha = 0.4f;
        // 트레일을 공보다 살짝 밝게 해서 어두운 배경에서 빛 꼬리처럼 보이게 해.
        private const float TrailWhiten = 0.28f;
        private const string AuraName = "Element Aura";
        private const float AuraFps = 12f;
        // 공이 연해질 때 오라도 이 비율만큼 같이 투명해져.
        private const float AuraFadeAlpha = 0.6f;

        private Rigidbody2D body;
        private float speedMultiplier = 1f;

        // 기본 속도는 유지하고 배속만 바꿔.
        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Clamp(multiplier, 1f, 2f);
            if (IsFlying && body != null)
            {
                Vector2 direction = body.linearVelocity.sqrMagnitude > 0.0001f
                    ? body.linearVelocity.normalized : lastDirection;
                body.linearVelocity = direction * (speed * speedMultiplier);
            }
        }
        private PhysicsMaterial2D bounceMaterial;
        private Vector2 launchPosition;
        private Vector2 lastDirection = Vector2.up;
        private SpriteRenderer visual;
        private TrailRenderer trail;
        private SpriteRenderer aura;
        private Sprite[] auraFrames;
        private int auraFrame = -1;
        private int auraFrameOffset;
        private float fade;

        public bool IsFlying { get; private set; }
        public bool HasTrail => trail != null;
        public bool IsDescending => IsFlying && body.linearVelocity.y < 0f;
        public event Action<Ball> Returned;
        public event Action<Ball> Retired;
        public ElementType Element { get; private set; }
        public int ReverseHitCount { get; private set; }
        // 퇴장하면 바로 발사 위치로 옮겨져서, 퇴장 이펙트에 쓸 위치와 방향을 따로 남겨.
        public Vector2 RetirePosition { get; private set; }
        public Vector2 RetireDirection { get; private set; }

        public void PrepareTurn(Vector2 position, ElementType element)
        {
            Prepare(position);
            Element = element;
            ReverseHitCount = 0;
            if (visual == null)
                visual = GetComponent<SpriteRenderer>();
            if (visual != null)
            {
                // 문양만 다른 회색 공 이미지에 속성 색을 틴트로 입혀.
                Sprite sprite = ElementRules.GetBallSprite(element);
                if (sprite != null)
                    visual.sprite = sprite;
                visual.enabled = true;
                EnsureAura();
            }
            auraFrames = ElementRules.GetAuraFrames(element);
            auraFrame = -1;
            fade = 0f;
            ApplyColor();
        }

        public void AttachTrail(TrailRenderer value)
        {
            trail = value;
            trail.transform.SetParent(transform, false);
            trail.transform.localPosition = Vector3.zero;
            trail.gameObject.SetActive(true);
            // 이전 주인의 궤적이 이어 그려지지 않게 비우고 시작해.
            trail.Clear();
            ApplyColor();
            trail.emitting = true;
        }

        public TrailRenderer DetachTrail()
        {
            TrailRenderer detached = trail;
            trail = null;
            return detached;
        }

        private void EnsureAura()
        {
            if (aura != null)
                return;

            // 하이어라키를 건드리지 않게 실행 중에 한 번만 만들어. 복제된 공은 원본의 오라를 다시 써.
            Transform existing = transform.Find(AuraName);
            aura = existing != null
                ? existing.GetComponent<SpriteRenderer>()
                : new GameObject(AuraName).AddComponent<SpriteRenderer>();
            aura.transform.SetParent(transform, false);
            aura.transform.localPosition = Vector3.zero;
            aura.sharedMaterial = visual.sharedMaterial;
            aura.sortingLayerID = visual.sortingLayerID;
            aura.sortingOrder = visual.sortingOrder - 1;
            aura.enabled = false;
        }

        private void ApplyColor()
        {
            Color color = ElementRules.GetBallTint(Element, fade);
            if (visual != null)
                visual.color = color;
            if (aura != null)
                aura.color = new Color(1f, 1f, 1f, 1f - fade * AuraFadeAlpha);
            if (trail != null)
            {
                Color light = Color.Lerp(color, Color.white, TrailWhiten);
                trail.startColor = new Color(light.r, light.g, light.b, TrailAlpha);
                trail.endColor = new Color(light.r, light.g, light.b, 0f);
            }
        }

        private void Awake()
        {
            InitializePhysics();
            // 공마다 프레임을 엇갈려서 다 같이 똑같이 깜빡이지 않게 해.
            auraFrameOffset = UnityEngine.Random.Range(0, ElementRules.AuraFrameCount);
        }

        private void Update()
        {
            if (!IsFlying || aura == null || !aura.enabled)
                return;

            // 오라는 날아가는 반대쪽으로 늘어지게 진행 방향으로 돌려.
            Vector2 velocity = body.linearVelocity;
            if (velocity.sqrMagnitude > 0.0001f)
                aura.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            int frame = ((int)(Time.time * AuraFps) + auraFrameOffset) % ElementRules.AuraFrameCount;
            if (frame != auraFrame)
            {
                auraFrame = frame;
                aura.sprite = auraFrames[frame];
            }
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
            speedMultiplier = 1f;
            // 멈춰 있는 공은 방향이 없어서 오라를 숨겨.
            if (aura != null)
                aura.enabled = false;
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
            body.linearVelocity = lastDirection * (speed * speedMultiplier);
            if (aura != null && auraFrames != null)
            {
                aura.enabled = true;
                auraFrame = -1;
            }
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
            body.linearVelocity = direction * (speed * speedMultiplier);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            KeepLeavingSurface(collision);
            if (IsFlying && collision.collider.TryGetComponent(out Brick brick))
                HitBrick(brick);
        }

        private void OnCollisionStay2D(Collision2D collision) => KeepLeavingSurface(collision);

        // 아주 얕은 각도로 닿으면 물리 엔진이 튕기지 않고 미끄러지게 둬(반발 임계 속도보다 느려서).
        // 그대로 두면 위쪽 벽을 따라 붙은 채로 안 떨어지니까, 표면에서 멀어지지 않고 있으면 직접 반사시켜.
        private void KeepLeavingSurface(Collision2D collision)
        {
            if (!IsFlying || collision.contactCount == 0)
                return;
            ContactPoint2D contact = collision.GetContact(0);
            // 법선이 공 쪽을 향하도록 맞춰. 접점에서 공 중심으로 가는 방향이 기준이야.
            Vector2 normal = contact.normal;
            if (Vector2.Dot(normal, body.position - contact.point) < 0f)
                normal = -normal;
            Vector2 velocity = body.linearVelocity;
            Vector2 direction = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : lastDirection;
            if (Vector2.Dot(direction, normal) >= minimumVerticalRatio)
                return;

            direction = Vector2.Reflect(lastDirection, normal);
            // 반사해도 표면을 거의 따라가면 표면 밖으로 조금 더 꺾어.
            if (Vector2.Dot(direction, normal) < minimumVerticalRatio)
                direction = (direction + normal * (minimumVerticalRatio * 2f)).normalized;
            lastDirection = direction;
            body.linearVelocity = direction * (speed * speedMultiplier);
        }

        private void HitBrick(Brick brick)
        {
            if (!IsFlying || brick == null || brick.Health <= 0)
                return;
            bool reverse = ElementRules.HasAdvantage(brick.Element, Element);
            brick.TakeHit(ElementRules.Damage(Element, brick.Element), Element, body.position);
            if (!reverse)
                return;
            if (++ReverseHitCount < RetireHitCount)
            {
                // 역상성에 맞을 때마다 연해져서 퇴장까지 남은 횟수를 보여 줘.
                fade = MaxFade * ReverseHitCount / (RetireHitCount - 1);
                ApplyColor();
                return;
            }

            // 부딪히기 직전 진행 방향을 남겨서 오라가 꺼지는 모습이 원래 방향을 유지하게 해.
            RetirePosition = body.position;
            RetireDirection = lastDirection;
            // 퇴장은 바닥 회수와 구분해서 발사 위치를 바꾸지 않아.
            Prepare(launchPosition);
            if (visual != null)
                visual.enabled = false;
            GameAudio.Play(SoundCue.Retire);
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
