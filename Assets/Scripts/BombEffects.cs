using UnityEngine;

namespace ElementalBreaker
{
    // 속성 폭탄 연출이야. 폭탄을 들고 있으면 맨 앞 공의 3x3 범위 모서리를 옅게 보여 줘.
    // 누르면 "쾅 → 와르르" 순서로 터져. 히트 스톱 동안은 섬광과 하얀 벽돌로 멈춘 장면(임팩트 프레임)을 보여 주고,
    // 멈춤이 풀리는 순간 속성 그림이 짧게 "팡" 하고, 파편이 사방으로 흩어지고, 화면이 흔들려.
    // 히트 스톱 중에도 움직여야 해서 실제 시간을 써.
    public sealed class BombEffects : MonoBehaviour
    {
        // 폭탄을 들고 있을 때 범위 모서리의 투명도야.
        private const float PreviewAlpha = 0.45f;
        // 맨 앞 공을 따라 칸을 옮길 때 미끄러지는 빠르기야. 클수록 바로 붙어.
        private const float FollowSharpness = 18f;
        // 모서리가 범위 바깥에서 조여드는 시간과 처음 벌어진 거리야.
        private const float LockTime = 0.08f;
        private const float LockInflate = 0.15f;
        // 멈춤이 풀린 뒤 모서리가 남아 있다가 흐려지는 시간이야.
        private const float CornerHoldTime = 0.12f;
        private const float CornerFadeTime = 0.12f;
        // 가운데 섬광의 지름(범위 너비 대비)이야. 멈춤이 풀리면 이만큼 더 커지며 사라져.
        private const float CoreSize = 1f;
        private const float CoreGrow = 0.35f;
        private const float CoreFadeTime = 0.12f;
        // 화면 섬광은 폭발 중심에서 화면 끝까지 번지는 빛이야. 가운데 투명도와, 멈춤이 풀린 뒤 사라지는 시간.
        private const float ScreenFlashAlpha = 0.1f;
        private const float ScreenFlashFadeTime = 0.08f;
        // 화면 섬광 지름(화면 긴 변 대비)이야. 폭발이 화면 가장자리에 있어도 반대쪽까지 빛이 닿아.
        private const float ScreenFlashSpan = 2.4f;
        // 속성 그림은 범위보다 작게 시작해 짧게 커지며 사라져. 크기는 범위를 덮는 크기 대비야.
        private const float PopStartScale = 0.55f;
        private const float PopEndScale = 0.85f;
        private const float PopLife = 0.16f;
        private const float PopFadeStart = 0.06f;
        // 일반 공 폭탄은 속성 그림 대신 빠른 충격파를 써.
        private const float RingTime = 0.18f;
        private const float RingAlpha = 0.8f;
        // 파편 수야. 속성 폭탄은 이 중 SparkleCount개를 하얀 반짝이로 섞어.
        private const int ParticleCount = 16;
        private const int SparkleCount = 4;
        private const float ParticleMinLife = 0.35f;
        private const float ParticleMaxLife = 0.55f;
        // 화면 흔들림: 처음이 가장 세고 점점 약해져. 세기는 월드 거리(벽돌 한 칸이 약 0.66)야.
        private const float ShakeTime = 0.24f;
        private const float ShakeStrength = 0.14f;
        private const int ShakeVibrato = 18;
        // 프레임이 끊겨도 파편이 한 번에 멀리 튀지 않게 한 번에 움직이는 시간을 제한해.
        private const float MaxStep = 0.05f;
        // 이미지 안에서 실제 그림이 차지하는 지름 비율이야.
        private const float BlastContentRatio = 0.94f;
        private const float RingContentRatio = 0.84f;

        private struct ParticleStyle
        {
            public readonly float MinSize, MaxSize, MinSpeed, MaxSpeed, Gravity, Drag, Spin;
            // 반짝이는 커졌다 작아지고, 나머지는 날아가며 조금씩 작아져.
            public readonly bool Twinkle;

            public ParticleStyle(float minSize, float maxSize, float minSpeed, float maxSpeed,
                float gravity, float drag, float spin, bool twinkle)
            {
                MinSize = minSize;
                MaxSize = maxSize;
                MinSpeed = minSpeed;
                MaxSpeed = maxSpeed;
                Gravity = gravity;
                Drag = drag;
                Spin = spin;
                Twinkle = twinkle;
            }
        }

        // 일반, 불, 물, 풀 순서야. 크기는 월드 지름, 속도는 초당 월드 거리, 회전은 초당 각도야.
        private static readonly ParticleStyle[] Styles =
        {
            new ParticleStyle(0.18f, 0.34f, 2.5f, 5f, 0f, 4f, 0f, true),
            // 불씨는 퍼지다가 살짝 떠올라.
            new ParticleStyle(0.26f, 0.44f, 3.5f, 6.5f, -2f, 3.5f, 0f, false),
            // 물방울은 튀었다가 떨어져.
            new ParticleStyle(0.1f, 0.2f, 3.5f, 6.5f, 14f, 1.5f, 0f, false),
            // 잎은 돌며 흩날려.
            new ParticleStyle(0.16f, 0.28f, 3f, 5.5f, 5f, 3f, 720f, false)
        };
        private static readonly ParticleStyle SparkleStyle = new ParticleStyle(0.2f, 0.36f, 2f, 4f, 0f, 5f, 0f, true);

        private struct Particle
        {
            public Vector2 Position, Velocity;
            public float Life, Scale, Gravity, Drag, Angle, Spin;
            public Color Tint;
            public bool Twinkle;
        }

        private BrickBoard board;
        private BallLauncher launcher;
        private readonly SpriteRenderer[] corners = new SpriteRenderer[4];
        private SpriteRenderer pop, ring, core, screenFlash;
        private readonly SpriteRenderer[] particleRenderers = new SpriteRenderer[ParticleCount];
        private readonly Particle[] particles = new Particle[ParticleCount];
        private readonly Sprite[] popSprites = new Sprite[4];
        // 일반(반짝이), 불(불씨), 물(물방울), 풀(잎) 순서야.
        private readonly Sprite[] particleSprites = new Sprite[4];
        private float blastWorldSize = 1f, ringWorldSize = 1f, glowWorldSize = 1f;

        private bool previewShown;
        private Vector2 previewMin, previewMax;
        private float detonateTime = -1f;
        private float impactTime;
        private bool released;
        private Vector2 lockMin, lockMax;
        private Vector3 blastCenter;
        private ElementType blastElement;
        private float popScale = 1f, ringScale = 1f, coreScale = 1f;

        public void Initialize(BrickBoard owner, BallLauncher ballLauncher, Material material, int sortingLayerId, int baseOrder)
        {
            board = owner;
            launcher = ballLauncher;
            Sprite corner = ElementRules.LoadEffectSprite("range_corner");
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = CreateRenderer("Range Corner", material, sortingLayerId, baseOrder + 8);
                corners[i].sprite = corner;
            }
            popSprites[(int)ElementType.Fire] = ElementRules.LoadEffectSprite("bomb_fire");
            popSprites[(int)ElementType.Water] = ElementRules.LoadEffectSprite("bomb_water");
            popSprites[(int)ElementType.Grass] = ElementRules.LoadEffectSprite("bomb_grass");
            particleSprites[(int)ElementType.Normal] = ElementRules.LoadEffectSprite("sparkle");
            particleSprites[(int)ElementType.Fire] = ElementRules.LoadEffectSprite("ember");
            particleSprites[(int)ElementType.Water] = ElementRules.LoadEffectSprite("drop");
            particleSprites[(int)ElementType.Grass] = ElementRules.LoadEffectSprite("leaf");
            Sprite glow = ElementRules.LoadEffectSprite("puff");

            // 그리는 순서(뒤→앞): 속성 그림·충격파(+4) 벽돌 조각(+5) 가운데 섬광(+6) 파편(+7) 모서리(+8) 화면 섬광(+10)
            pop = CreateRenderer("Bomb Pop", material, sortingLayerId, baseOrder + 4);
            ring = CreateRenderer("Bomb Ring", material, sortingLayerId, baseOrder + 4);
            ring.sprite = ElementRules.LoadEffectSprite("ring");
            core = CreateRenderer("Bomb Core Flash", material, sortingLayerId, baseOrder + 6);
            core.sprite = glow;
            screenFlash = CreateRenderer("Bomb Screen Flash", material, sortingLayerId, baseOrder + 10);
            screenFlash.sprite = glow;
            for (int i = 0; i < ParticleCount; i++)
                particleRenderers[i] = CreateRenderer("Bomb Particle", material, sortingLayerId, baseOrder + 7);
            if (popSprites[(int)ElementType.Fire] != null)
                blastWorldSize = popSprites[(int)ElementType.Fire].bounds.size.x;
            if (ring.sprite != null)
                ringWorldSize = ring.sprite.bounds.size.x;
            if (glow != null)
                glowWorldSize = glow.bounds.size.x;
            HideAll();
        }

        private SpriteRenderer CreateRenderer(string objectName, Material material, int layerId, int order)
        {
            SpriteRenderer renderer = new GameObject(objectName).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false);
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = layerId;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void HideAll()
        {
            for (int i = 0; i < corners.Length; i++)
                corners[i].enabled = false;
            pop.enabled = false;
            ring.enabled = false;
            core.enabled = false;
            screenFlash.enabled = false;
            for (int i = 0; i < ParticleCount; i++)
                particleRenderers[i].enabled = false;
        }

        // impactDuration은 히트 스톱 시간이야. 그동안 멈춘 장면을 보여 주고, 끝나는 순간 터져.
        public void PlayDetonate(Vector2 min, Vector2 max, Vector3 center, ElementType element, float impactDuration)
        {
            HideAll();
            detonateTime = Time.unscaledTime;
            impactTime = Mathf.Max(0f, impactDuration);
            released = false;
            previewShown = false;
            lockMin = min;
            lockMax = max;
            blastCenter = new Vector3(center.x, center.y, transform.position.z);
            blastElement = element;

            // 크기는 범위 너비에 맞춰. 그래서 범위 칸 수를 바꿔도 폭발이 범위를 덮어.
            float width = max.x - min.x;
            popScale = width * 1.05f / (blastWorldSize * BlastContentRatio);
            ringScale = width * 1.1f / (ringWorldSize * RingContentRatio);
            coreScale = width * CoreSize / glowWorldSize;

            // 임팩트 프레임: 누르는 순간 가운데 섬광이 바로 가장 밝고, 화면 전체에 빛이 번져.
            core.enabled = true;
            core.transform.position = blastCenter;
            core.transform.localScale = new Vector3(coreScale, coreScale, 1f);
            core.color = Color.white;
            float flashScale = ScreenSpan() * ScreenFlashSpan / glowWorldSize;
            screenFlash.enabled = true;
            screenFlash.transform.position = blastCenter;
            screenFlash.transform.localScale = new Vector3(flashScale, flashScale, 1f);
            screenFlash.color = new Color(1f, 1f, 1f, ScreenFlashAlpha);
            // 이번 프레임의 Update가 이미 지나갔어도 모서리가 한 프레임 꺼지지 않게 바로 그려.
            UpdateCorners(0f);
        }

        private static float ScreenSpan()
        {
            Camera main = Camera.main;
            if (main == null || !main.orthographic)
                return 12f;
            float height = main.orthographicSize * 2f;
            return Mathf.Max(height, height * main.aspect);
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            if (detonateTime < 0f)
            {
                UpdatePreview(dt);
                return;
            }
            float t = Time.unscaledTime - detonateTime;
            if (!released && t >= impactTime)
                Release();
            bool alive = UpdateCorners(t);
            if (!released)
                return;
            float r = t - impactTime;
            // 하나라도 남아 있으면 계속 그려. 모두 돌려야 해서 ||가 아니라 |를 써.
            alive |= UpdateFlashes(r) | UpdatePop(r) | UpdateRing(r) | UpdateParticles(r, dt);
            if (!alive)
            {
                detonateTime = -1f;
                HideAll();
            }
        }

        private void UpdatePreview(float dt)
        {
            Ball front = board.Items.Held == ItemType.Bomb ? launcher.FrontBall : null;
            if (front == null)
            {
                if (previewShown)
                {
                    previewShown = false;
                    for (int i = 0; i < corners.Length; i++)
                        corners[i].enabled = false;
                }
                return;
            }
            board.WorldToCell(front.transform.position, out int row, out int column);
            board.GetRangeBounds(row, column, board.BombRadius, out Vector2 targetMin, out Vector2 targetMax);
            if (!previewShown)
            {
                previewShown = true;
                previewMin = targetMin;
                previewMax = targetMax;
            }
            else
            {
                float k = 1f - Mathf.Exp(-FollowSharpness * dt);
                previewMin = Vector2.Lerp(previewMin, targetMin, k);
                previewMax = Vector2.Lerp(previewMax, targetMax, k);
            }
            PlaceCorners(previewMin, previewMax, PreviewAlpha, 1f);
        }

        // 멈춤이 풀리는 순간이야. 속성 그림(일반이면 충격파), 파편, 화면 흔들림이 한꺼번에 시작돼.
        private void Release()
        {
            released = true;
            Sprite sprite = popSprites[(int)blastElement];
            pop.enabled = sprite != null;
            if (pop.enabled)
            {
                pop.sprite = sprite;
                pop.transform.position = blastCenter;
                pop.color = Color.white;
            }
            ring.enabled = sprite == null && ring.sprite != null;
            if (ring.enabled)
                ring.transform.position = blastCenter;
            SpawnParticles();
            ScreenShake.Play(ShakeTime, ShakeStrength, ShakeVibrato);
        }

        private bool UpdateCorners(float t)
        {
            // 누르는 순간 범위보다 살짝 바깥에서 "착" 조여들고, 멈춤이 풀린 뒤 잠깐 머물다 흐려져.
            float fadeStart = impactTime + CornerHoldTime;
            if (t >= fadeStart + CornerFadeTime)
            {
                if (corners[0].enabled)
                    for (int i = 0; i < corners.Length; i++)
                        corners[i].enabled = false;
                return false;
            }
            float lockK = Mathf.Clamp01(t / LockTime);
            float inflate = LockInflate * (1f - EaseOut(lockK));
            Vector2 grow = new Vector2(inflate, inflate);
            float alpha = t < fadeStart ? 1f : 1f - (t - fadeStart) / CornerFadeTime;
            PlaceCorners(lockMin - grow, lockMax + grow, alpha, 1f + 0.15f * (1f - lockK));
            return true;
        }

        private bool UpdateFlashes(float r)
        {
            // 가운데 섬광은 조금 커지며 빠르게 사라지고, 화면 섬광은 그보다 먼저 꺼져.
            bool alive = false;
            if (core.enabled)
            {
                float k = r / CoreFadeTime;
                if (k >= 1f)
                {
                    core.enabled = false;
                }
                else
                {
                    float scale = coreScale * (1f + CoreGrow * EaseOut(k));
                    core.transform.localScale = new Vector3(scale, scale, 1f);
                    core.color = new Color(1f, 1f, 1f, (1f - k) * (1f - k));
                    alive = true;
                }
            }
            if (screenFlash.enabled)
            {
                float k = r / ScreenFlashFadeTime;
                if (k >= 1f)
                {
                    screenFlash.enabled = false;
                }
                else
                {
                    screenFlash.color = new Color(1f, 1f, 1f, ScreenFlashAlpha * (1f - k) * (1f - k));
                    alive = true;
                }
            }
            return alive;
        }

        private bool UpdatePop(float r)
        {
            if (!pop.enabled)
                return false;
            if (r >= PopLife)
            {
                pop.enabled = false;
                return false;
            }
            float k = r / PopLife;
            float scale = popScale * Mathf.Lerp(PopStartScale, PopEndScale, EaseOut(k));
            pop.transform.localScale = new Vector3(scale, scale, 1f);
            pop.color = new Color(1f, 1f, 1f, r < PopFadeStart ? 1f : 1f - (r - PopFadeStart) / (PopLife - PopFadeStart));
            return true;
        }

        private bool UpdateRing(float r)
        {
            if (!ring.enabled)
                return false;
            float k = r / RingTime;
            if (k >= 1f)
            {
                ring.enabled = false;
                return false;
            }
            float scale = ringScale * (0.25f + 0.85f * EaseOut(k));
            ring.transform.localScale = new Vector3(scale, scale, 1f);
            ring.color = new Color(1f, 1f, 1f, RingAlpha * (1f - k));
            return true;
        }

        private void SpawnParticles()
        {
            int element = (int)blastElement;
            int sparkles = blastElement == ElementType.Normal ? 0 : SparkleCount;
            int mainCount = ParticleCount - sparkles;
            float z = transform.position.z;
            for (int i = 0; i < ParticleCount; i++)
            {
                bool sparkle = i >= mainCount;
                Sprite sprite = sparkle ? particleSprites[(int)ElementType.Normal] : particleSprites[element];
                SpriteRenderer renderer = particleRenderers[i];
                renderer.enabled = sprite != null;
                if (sprite == null)
                    continue;
                ParticleStyle style = sparkle ? SparkleStyle : Styles[element];
                // 둘레를 고르게 나눈 방향에 조금씩 흔들림을 줘서 한쪽으로 몰리지 않게 해.
                int index = sparkle ? i - mainCount : i;
                int count = sparkle ? sparkles : mainCount;
                float angle = ((index + Random.value * 0.6f) / count * 360f + (sparkle ? 45f : 0f)) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                ref Particle particle = ref particles[i];
                particle.Position = (Vector2)blastCenter + direction * Random.Range(0.05f, 0.2f);
                particle.Velocity = direction * Random.Range(style.MinSpeed, style.MaxSpeed);
                particle.Life = Random.Range(ParticleMinLife, ParticleMaxLife);
                particle.Scale = Random.Range(style.MinSize, style.MaxSize) / sprite.bounds.size.x;
                particle.Gravity = style.Gravity;
                particle.Drag = style.Drag;
                particle.Angle = Random.Range(0f, 360f);
                particle.Spin = style.Spin * Random.Range(0.5f, 1f) * (Random.value < 0.5f ? -1f : 1f);
                particle.Twinkle = style.Twinkle;
                // 잎 이미지는 회색이라 초록을 입혀. 나머지는 이미지 색 그대로야.
                particle.Tint = !sparkle && blastElement == ElementType.Grass ? BrickEffects.LeafGreen : Color.white;

                renderer.sprite = sprite;
                renderer.transform.SetPositionAndRotation(new Vector3(particle.Position.x, particle.Position.y, z),
                    Quaternion.Euler(0f, 0f, particle.Angle));
                renderer.transform.localScale = Vector3.zero;
            }
        }

        private bool UpdateParticles(float r, float dt)
        {
            bool alive = false;
            float z = transform.position.z;
            for (int i = 0; i < ParticleCount; i++)
            {
                SpriteRenderer renderer = particleRenderers[i];
                if (!renderer.enabled)
                    continue;
                ref Particle particle = ref particles[i];
                float k = r / particle.Life;
                if (k >= 1f)
                {
                    renderer.enabled = false;
                    continue;
                }
                alive = true;
                // 공기 저항 때문에 처음엔 빠르게 퍼지고 금방 느려져.
                particle.Velocity.y -= particle.Gravity * dt;
                particle.Velocity *= 1f / (1f + particle.Drag * dt);
                particle.Position += particle.Velocity * dt;
                particle.Angle += particle.Spin * dt;
                float scale = particle.Twinkle
                    ? particle.Scale * (k < 0.25f ? 0.4f + 0.6f * k / 0.25f : 1f - 0.7f * (k - 0.25f) / 0.75f)
                    : particle.Scale * (1f - 0.4f * k);
                Transform target = renderer.transform;
                target.SetPositionAndRotation(new Vector3(particle.Position.x, particle.Position.y, z),
                    Quaternion.Euler(0f, 0f, particle.Angle));
                target.localScale = new Vector3(scale, scale, 1f);
                Color color = particle.Tint;
                color.a = k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f;
                renderer.color = color;
            }
            return alive;
        }

        private void PlaceCorners(Vector2 min, Vector2 max, float alpha, float scale)
        {
            // 모서리 이미지는 왼쪽 위 방향(⌜)이라, 나머지 세 귀퉁이는 돌려서 써.
            float z = transform.position.z;
            SetCorner(corners[0], new Vector3(min.x, max.y, z), 0f, alpha, scale);
            SetCorner(corners[1], new Vector3(max.x, max.y, z), -90f, alpha, scale);
            SetCorner(corners[2], new Vector3(max.x, min.y, z), 180f, alpha, scale);
            SetCorner(corners[3], new Vector3(min.x, min.y, z), 90f, alpha, scale);
        }

        private static void SetCorner(SpriteRenderer corner, Vector3 position, float angle, float alpha, float scale)
        {
            corner.enabled = alpha > 0f;
            corner.transform.position = position;
            corner.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            corner.transform.localScale = new Vector3(scale, scale, 1f);
            corner.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        private static float EaseOut(float k)
        {
            k = Mathf.Clamp01(k);
            return 1f - (1f - k) * (1f - k);
        }

        private void OnDisable()
        {
            if (pop == null)
                return;
            detonateTime = -1f;
            previewShown = false;
            HideAll();
        }
    }
}
