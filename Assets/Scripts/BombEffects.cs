using UnityEngine;

namespace ElementalBreaker
{
    // 속성 폭탄 연출이야. 폭탄을 들고 있으면 맨 앞 공의 3x3 범위 모서리를 옅게 보여 줘.
    // 누르면 "쾅 → 와르르" 순서로 터져. 히트 스톱 동안은 멈춘 장면(가운데 작은 별, 범위 번쩍임, 테두리 빛, 모서리)을 보여 주고,
    // 멈춤이 풀리는 순간 폭발 그림(8프레임)이 재생되면서 빛줄기, 불꽃, 속성 파편이 튀고 화면이 흔들려.
    // 깨진 벽돌 조각과 코인은 BrickEffects가 맡아. 히트 스톱 중에도 움직여야 해서 실제 시간을 써.
    public sealed class BombEffects : MonoBehaviour
    {
        // 폭탄을 들고 있을 때 범위 모서리의 투명도야.
        private const float PreviewAlpha = 0.45f;
        // 맨 앞 공을 따라 칸을 옮길 때 미끄러지는 빠르기야. 클수록 바로 붙어.
        private const float FollowSharpness = 18f;
        // 모서리가 범위 바깥에서 조여드는 시간과 처음 벌어진 거리야. 멈춤이 풀린 뒤 잠깐 머물다 흐려져.
        private const float LockTime = 0.08f;
        private const float LockInflate = 0.15f;
        private const float CornerHoldTime = 0.12f;
        private const float CornerFadeTime = 0.12f;
        // 누르는 순간 3x3 전체가 속성색으로 번쩍이는 시간과 밝기야. 짧아야 벽돌 숫자가 금방 다시 보여.
        private const float RangeFlashTime = 0.06f;
        private const float RangeFlashAlpha = 0.6f;
        private const float RangeInnerAlpha = 0.4f;
        // 번쩍임이 끝나도 범위가 보이게 옅은 색과 테두리 빛이 남아. 멈춤이 풀린 뒤 EdgeHoldTime 동안 유지하고 EdgeFadeTime 동안 흐려져.
        private const float RangeTintAlpha = 0.12f;
        private const float RangeEdgeAlpha = 0.9f;
        private const float EdgeHoldTime = 0.12f;
        private const float EdgeFadeTime = 0.18f;
        // 범위 빛/테두리 이미지(96px)에서 둥근 사각형이 가장자리로부터 들어온 거리(픽셀)야.
        private const float RangeSpriteInset = 24f;
        // 폭발 그림 한 칸이 화면에서 차지하는 크기(3x3 범위 너비 대비)야. 가장 큰 프레임이 범위의 약 1.2배가 돼.
        private const float BlastSize = 1.32f;
        // 멈춘 장면의 작은 별 크기 배율이야.
        private const float ImpactStarScale = 1.5f;
        // 멈춤이 풀린 뒤 1~7번 프레임이 각각 보이는 시간(초)이야. 다 더하면 약 0.36초야.
        private static readonly float[] FrameTimes = { 0.035f, 0.05f, 0.05f, 0.05f, 0.055f, 0.06f, 0.06f };
        // 재생하는 동안 폭발 그림이 조금씩 커지는 비율이야.
        private const float BlastGrow = 0.06f;
        // 폭발 그림 아래 빛이 남는 시간이야.
        private const float GlowTime = 0.22f;
        // 빛줄기: 범위 테두리 안에서 끝나게 길이를 제한해.
        private const int RayCount = 8;
        private const float RayGrowTime = 0.08f;
        private const float RayTime = 0.18f;
        private const int SparkCount = 10;
        private const int PartCount = 8;
        // 화면 섬광은 누르는 순간 켜지고 빠르게 꺼져.
        private const float ScreenFlashAlpha = 0.1f;
        private const float ScreenFlashTime = 0.08f;
        // 화면 섬광 지름(화면 긴 변 대비)이야. 폭발이 가장자리에 있어도 반대쪽까지 빛이 닿아.
        private const float ScreenFlashSpan = 2.4f;
        // 화면 흔들림: 처음이 가장 세고 점점 약해져. 세기는 월드 거리(벽돌 한 칸이 약 0.66)야.
        private const float ShakeTime = 0.24f;
        private const float ShakeStrength = 0.14f;
        private const int ShakeVibrato = 18;
        // 프레임이 끊겨도 미리보기 모서리가 한 번에 멀리 튀지 않게 해.
        private const float MaxStep = 0.05f;
        private const string AdditiveShaderPath = "Shaders/SpriteAdditive";

        // 폭발 빛 색이야. 일반, 불, 물, 풀 순서. light는 안쪽 밝은 색, main은 바깥 속성색이야.
        private static readonly Color32[] LightColors =
        {
            new Color32(0xFF, 0xFF, 0xFF, 0xFF), new Color32(0xFF, 0xD4, 0x5C, 0xFF),
            new Color32(0x9E, 0xEB, 0xFF, 0xFF), new Color32(0xD8, 0xFF, 0x7A, 0xFF)
        };
        private static readonly Color32[] MainColors =
        {
            new Color32(0xBF, 0xD0, 0xEE, 0xFF), new Color32(0xFF, 0x4D, 0x1A, 0xFF),
            new Color32(0x1F, 0x8B, 0xFF, 0xFF), new Color32(0x3C, 0xCB, 0x2E, 0xFF)
        };

        private struct PartStyle
        {
            public readonly string Sprite;
            public readonly int Count;
            // 이미지 한 변의 월드 크기야.
            public readonly float Size;
            // 이미지에서 머리가 향한 방향(도)이야. 날아가는 방향으로 머리를 돌려. NaN이면 날아가며 빙글 돌아.
            public readonly float Head;
            // 반짝이는 커졌다 작아져.
            public readonly bool Twinkle;

            public PartStyle(string sprite, int count, float size, float head, bool twinkle = false)
            {
                Sprite = sprite;
                Count = count;
                Size = size;
                Head = head;
                Twinkle = twinkle;
            }
        }

        // 속성 파편이야. 일반, 불, 물, 풀 순서이고 개수 합은 PartCount야.
        private static readonly PartStyle[][] PartStyles =
        {
            new[] { new PartStyle("bomb_star", 6, 0.28f, float.NaN, true), new PartStyle("bomb_star_blue", 2, 0.2f, float.NaN, true) },
            new[] { new PartStyle("bomb_flame", 4, 0.27f, -135f), new PartStyle("bomb_ember", 4, 0.2f, -135f) },
            new[] { new PartStyle("bomb_drop", 3, 0.17f, -90f), new PartStyle("bomb_splash", 3, 0.22f, 45f), new PartStyle("bomb_splash_small", 2, 0.13f, 90f) },
            new[] { new PartStyle("bomb_leaf_small", 4, 0.22f, float.NaN), new PartStyle("bomb_leaf", 2, 0.26f, float.NaN), new PartStyle("bomb_shard_grass", 2, 0.2f, 45f) }
        };
        // 속성 파편의 중력(x)과 공기 저항(y)이야. 불씨는 살짝 떠오르고 물방울은 떨어져.
        private static readonly Vector2[] PartPhysics =
        {
            new Vector2(0f, 3.5f), new Vector2(-1f, 2.8f), new Vector2(10f, 1.6f), new Vector2(4f, 2.6f)
        };
        private const float SparkGravity = 4f;
        private const float SparkDrag = 8f;

        private struct Part
        {
            public Vector2 Start, Velocity;
            public float Life, Scale, Angle, Spin, Head;
            public bool Twinkle;
        }

        private struct LightRay
        {
            public float Angle, Length, Width;
        }

        private struct Spark
        {
            public Vector2 Direction;
            public float Speed, Life, Width;
        }

        private BrickBoard board;
        private BallLauncher launcher;
        private Material additive;
        private bool ownsAdditive;
        private readonly SpriteRenderer[] corners = new SpriteRenderer[4];
        private SpriteRenderer rangeFill, rangeInner, rangeEdge, glowMain, glowLight, blast, screenFlash;
        private readonly SpriteRenderer[] rayRenderers = new SpriteRenderer[RayCount];
        private readonly SpriteRenderer[] sparkRenderers = new SpriteRenderer[SparkCount];
        private readonly SpriteRenderer[] partRenderers = new SpriteRenderer[PartCount];
        private readonly LightRay[] rays = new LightRay[RayCount];
        private readonly Spark[] sparks = new Spark[SparkCount];
        private readonly Part[] parts = new Part[PartCount];
        private readonly Sprite[][] partSprites = new Sprite[4][];
        private Sprite[] frames;
        private float frameTotal, rayLength = 1f, rayWidth = 1f, glowWorldSize = 1f, rangeInset;

        private bool previewShown;
        private Vector2 previewMin, previewMax;
        private float detonateTime = -1f;
        private float impactTime;
        private bool released;
        private int partUsed;
        private Vector2 lockMin, lockMax, blastCenter;
        private ElementType blastElement;
        private float nominalWidth = 1f, blastScale = 1f;
        private Color lightColor, mainColor;

        public void Initialize(BrickBoard owner, BallLauncher ballLauncher, Material material, int sortingLayerId, int baseOrder)
        {
            board = owner;
            launcher = ballLauncher;
            // 빛은 더하기 합성으로 그려서 어두운 배경에서 빛나 보여. 셰이더가 없으면 일반 재질로 대신해.
            Shader shader = Resources.Load<Shader>(AdditiveShaderPath);
            ownsAdditive = shader != null;
            additive = ownsAdditive ? new Material(shader) : material;
            if (!ownsAdditive)
                Debug.LogWarning("BombEffects: 더하기 셰이더를 찾지 못해서 일반 재질로 그려. " + AdditiveShaderPath, this);

            // 그리는 순서(뒤→앞): 범위 빛·폭발 아래 빛(+3) 폭발 그림(+4) 벽돌 조각(+5, BrickEffects)·빛줄기·불꽃(+5)
            //                    속성 파편(+6) 모서리(+8) 화면 섬광(+10)
            Sprite glow = ElementRules.LoadEffectSprite("puff");
            Sprite rangeGlow = ElementRules.LoadEffectSprite("range_glow");
            Sprite edge = ElementRules.LoadEffectSprite("range_edge");
            Sprite ray = ElementRules.LoadEffectSprite("ray");
            Sprite corner = ElementRules.LoadEffectSprite("range_corner");
            rangeFill = CreateSliced("Bomb Range Fill", rangeGlow, sortingLayerId, baseOrder + 3);
            rangeInner = CreateSliced("Bomb Range Inner", rangeGlow, sortingLayerId, baseOrder + 3);
            rangeEdge = CreateSliced("Bomb Range Edge", edge, sortingLayerId, baseOrder + 3);
            glowMain = CreateRenderer("Bomb Glow", additive, sortingLayerId, baseOrder + 3);
            glowMain.sprite = glow;
            glowLight = CreateRenderer("Bomb Glow Core", additive, sortingLayerId, baseOrder + 3);
            glowLight.sprite = glow;
            blast = CreateRenderer("Bomb Blast", material, sortingLayerId, baseOrder + 4);
            for (int i = 0; i < RayCount; i++)
            {
                rayRenderers[i] = CreateRenderer("Bomb Ray", additive, sortingLayerId, baseOrder + 5);
                rayRenderers[i].sprite = ray;
            }
            for (int i = 0; i < SparkCount; i++)
            {
                sparkRenderers[i] = CreateRenderer("Bomb Spark", additive, sortingLayerId, baseOrder + 5);
                sparkRenderers[i].sprite = ray;
            }
            for (int i = 0; i < PartCount; i++)
                partRenderers[i] = CreateRenderer("Bomb Particle", material, sortingLayerId, baseOrder + 6);
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = CreateRenderer("Range Corner", material, sortingLayerId, baseOrder + 8);
                corners[i].sprite = corner;
            }
            screenFlash = CreateRenderer("Bomb Screen Flash", material, sortingLayerId, baseOrder + 10);
            screenFlash.sprite = glow;

            for (int e = 0; e < PartStyles.Length; e++)
            {
                partSprites[e] = new Sprite[PartStyles[e].Length];
                for (int s = 0; s < PartStyles[e].Length; s++)
                    partSprites[e][s] = ElementRules.LoadEffectSprite(PartStyles[e][s].Sprite);
            }
            for (int i = 0; i < FrameTimes.Length; i++)
                frameTotal += FrameTimes[i];
            if (glow != null)
                glowWorldSize = SpriteWidth(glow);
            if (ray != null)
            {
                rayLength = SpriteWidth(ray);
                rayWidth = ray.rect.height / ray.pixelsPerUnit;
            }
            if (edge != null)
                rangeInset = RangeSpriteInset / edge.pixelsPerUnit;
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

        private SpriteRenderer CreateSliced(string objectName, Sprite sprite, int layerId, int order)
        {
            SpriteRenderer renderer = CreateRenderer(objectName, additive, layerId, order);
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            return renderer;
        }

        // 아틀라스에서 잘려 들어와도 원래 이미지 한 칸 크기로 재야 프레임마다 크기가 같아.
        private static float SpriteWidth(Sprite sprite) => sprite.rect.width / sprite.pixelsPerUnit;

        private void HideAll()
        {
            for (int i = 0; i < corners.Length; i++)
                corners[i].enabled = false;
            rangeFill.enabled = false;
            rangeInner.enabled = false;
            rangeEdge.enabled = false;
            glowMain.enabled = false;
            glowLight.enabled = false;
            blast.enabled = false;
            screenFlash.enabled = false;
            for (int i = 0; i < RayCount; i++)
                rayRenderers[i].enabled = false;
            for (int i = 0; i < SparkCount; i++)
                sparkRenderers[i].enabled = false;
            for (int i = 0; i < PartCount; i++)
                partRenderers[i].enabled = false;
        }

        // min, max는 범위의 월드 경계야. impactDuration은 히트 스톱 시간이고, 그동안 멈춘 장면을 보여 준 뒤 터져.
        public void PlayDetonate(Vector2 min, Vector2 max, ElementType element, float impactDuration)
        {
            HideAll();
            detonateTime = Time.unscaledTime;
            impactTime = Mathf.Max(0f, impactDuration);
            released = false;
            previewShown = false;
            lockMin = min;
            lockMax = max;
            blastCenter = (min + max) * 0.5f;
            blastElement = element;
            lightColor = LightColors[(int)element];
            mainColor = MainColors[(int)element];
            frames = ElementRules.GetBlastFrames(element);

            // 폭발 크기는 3x3 범위 너비가 기준이야. 보드 가장자리에서 범위가 잘려도 폭발 크기는 같아.
            nominalWidth = board.CellSize.x * (board.BombRadius * 2 + 1);
            blastScale = frames[0] != null ? nominalWidth * BlastSize / SpriteWidth(frames[0]) : 1f;
            Vector3 center = WorldPoint(blastCenter);
            Vector2 size = max - min;
            // 테두리 빛은 범위 경계 바로 안쪽에 오고, 채움 빛은 경계 밖으로 조금 번져.
            rangeEdge.size = size + Vector2.one * (rangeInset * 2f - 0.04f);
            rangeFill.size = size + Vector2.one * (rangeInset * 2f + 0.12f);
            rangeInner.size = size + Vector2.one * (rangeInset * 2f - 0.26f);
            rangeFill.transform.position = center;
            rangeInner.transform.position = center;
            rangeEdge.transform.position = center;
            blast.transform.position = center;
            glowMain.transform.position = center;
            glowLight.transform.position = center;

            float flashScale = ScreenSpan() * ScreenFlashSpan / glowWorldSize;
            screenFlash.transform.position = center;
            screenFlash.transform.localScale = new Vector3(flashScale, flashScale, 1f);

            SetupRays(size);
            SetupSparks();
            SetupParts();
            // 이번 프레임의 Update가 이미 지나갔어도 멈춘 장면이 한 프레임 늦지 않게 바로 그려.
            UpdateDetonate(0f);
        }

        private Vector3 WorldPoint(Vector2 point) => new Vector3(point.x, point.y, transform.position.z);

        private void SetupRays(Vector2 rangeSize)
        {
            float start = nominalWidth * 0.08f;
            for (int i = 0; i < RayCount; i++)
            {
                float angle = (i / (float)RayCount) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                // 가운데에서 범위 테두리까지의 거리야. 빛줄기는 그 안에서 끝나.
                float edge = Mathf.Min(rangeSize.x * 0.5f / Mathf.Max(0.0001f, Mathf.Abs(Mathf.Cos(angle))),
                    rangeSize.y * 0.5f / Mathf.Max(0.0001f, Mathf.Abs(Mathf.Sin(angle))));
                rays[i] = new LightRay
                {
                    Angle = angle,
                    Length = Mathf.Max(0.05f, edge * Random.Range(0.8f, 0.92f) - start),
                    Width = Random.Range(0.09f, 0.14f)
                };
            }
        }

        private void SetupSparks()
        {
            for (int i = 0; i < SparkCount; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                sparks[i] = new Spark
                {
                    Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                    Speed = Random.Range(7f, 11f),
                    Life = Random.Range(0.18f, 0.28f),
                    Width = Random.Range(0.03f, 0.045f)
                };
            }
        }

        private void SetupParts()
        {
            PartStyle[] styles = PartStyles[(int)blastElement];
            Sprite[] sprites = partSprites[(int)blastElement];
            int total = 0;
            for (int s = 0; s < styles.Length; s++)
                total += styles[s].Count;
            partUsed = 0;
            for (int s = 0; s < styles.Length; s++)
            {
                if (sprites[s] == null)
                    continue;
                float width = SpriteWidth(sprites[s]);
                for (int c = 0; c < styles[s].Count && partUsed < PartCount; c++)
                {
                    // 둘레를 고르게 나눈 방향에 조금씩 흔들림을 줘서 한쪽으로 몰리지 않게 해.
                    float angle = partUsed / (float)total * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    parts[partUsed] = new Part
                    {
                        Start = direction * 0.2f,
                        Velocity = direction * Random.Range(4f, 6.5f) + new Vector2(0f, 1f),
                        Life = Random.Range(0.45f, 0.6f),
                        Scale = styles[s].Size * Random.Range(0.85f, 1.15f) / width,
                        Angle = Random.Range(0f, 360f),
                        Spin = Random.Range(-344f, 344f),
                        Head = styles[s].Head,
                        Twinkle = styles[s].Twinkle
                    };
                    partRenderers[partUsed].sprite = sprites[s];
                    partUsed++;
                }
            }
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
            if (detonateTime < 0f)
            {
                UpdatePreview(Mathf.Min(Time.unscaledDeltaTime, MaxStep));
                return;
            }
            if (!UpdateDetonate(Time.unscaledTime - detonateTime))
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

        // t는 누른 뒤 지난 실제 시간이야. 아직 보여 줄 게 남아 있으면 true야.
        private bool UpdateDetonate(float t)
        {
            if (!released && t >= impactTime)
                Release();
            // 하나라도 남아 있으면 계속 그려. 모두 돌려야 해서 ||가 아니라 |를 써.
            bool alive = UpdateCorners(t) | UpdateRange(t) | UpdateScreenFlash(t) | UpdateBlast(t);
            if (!released)
                return true;
            float r = t - impactTime;
            return alive | UpdateGlow(r) | UpdateRays(r) | UpdateSparks(r) | UpdateParts(r);
        }

        // 멈춤이 풀리는 순간이야. 빛줄기, 불꽃, 속성 파편, 화면 흔들림이 한꺼번에 시작돼.
        private void Release()
        {
            released = true;
            for (int i = 0; i < RayCount; i++)
                rayRenderers[i].enabled = true;
            for (int i = 0; i < SparkCount; i++)
                sparkRenderers[i].enabled = true;
            for (int i = 0; i < partUsed; i++)
                partRenderers[i].enabled = true;
            glowMain.enabled = true;
            glowLight.enabled = true;
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
            float inflate = LockInflate * (1f - EffectMath.EaseOut(lockK));
            Vector2 grow = new Vector2(inflate, inflate);
            float alpha = t < fadeStart ? 1f : 1f - (t - fadeStart) / CornerFadeTime;
            PlaceCorners(lockMin - grow, lockMax + grow, alpha, 1f + 0.15f * (1f - lockK));
            return true;
        }

        private bool UpdateRange(float t)
        {
            float flash = Mathf.Clamp01(1f - t / RangeFlashTime);
            float edge = t < impactTime + EdgeHoldTime ? 1f : 1f - (t - impactTime - EdgeHoldTime) / EdgeFadeTime;
            if (flash <= 0f && edge <= 0f)
            {
                rangeFill.enabled = false;
                rangeInner.enabled = false;
                rangeEdge.enabled = false;
                return false;
            }
            // 처음엔 범위 전체가 번쩍이고, 그 뒤엔 옅은 색과 테두리만 남아.
            rangeFill.enabled = true;
            rangeFill.color = WithAlpha(mainColor, Mathf.Max(RangeFlashAlpha * flash, RangeTintAlpha * edge));
            rangeInner.enabled = flash > 0f;
            rangeInner.color = WithAlpha(lightColor, RangeInnerAlpha * flash);
            rangeEdge.enabled = edge > 0f;
            rangeEdge.color = WithAlpha(lightColor, RangeEdgeAlpha * edge);
            return true;
        }

        private bool UpdateScreenFlash(float t)
        {
            float k = t / ScreenFlashTime;
            screenFlash.enabled = k < 1f;
            if (k >= 1f)
                return false;
            screenFlash.color = new Color(1f, 1f, 1f, ScreenFlashAlpha * (1f - k) * (1f - k));
            return true;
        }

        private bool UpdateBlast(float t)
        {
            Sprite sprite;
            float scale, alpha = 1f;
            if (t < impactTime)
            {
                // 멈춘 장면: 가운데 작은 별이 톡 커져.
                sprite = frames[0];
                scale = blastScale * ImpactStarScale * (0.7f + 0.3f * EffectMath.EaseOut(t / 0.04f));
            }
            else
            {
                float r = t - impactTime, start = 0f;
                int index = 0;
                float k = 0f;
                for (int i = 0; i < FrameTimes.Length; i++)
                {
                    if (r < start + FrameTimes[i])
                    {
                        index = i + 1;
                        k = (r - start) / FrameTimes[i];
                        break;
                    }
                    start += FrameTimes[i];
                }
                if (index == 0 || index >= frames.Length)
                {
                    blast.enabled = false;
                    return false;
                }
                sprite = frames[index];
                scale = blastScale * (1f + BlastGrow * Mathf.Clamp01(r / frameTotal));
                // 마지막 프레임은 흐려지며 사라져.
                if (index == frames.Length - 1)
                    alpha = 1f - k;
            }
            blast.enabled = sprite != null;
            if (sprite == null)
                return true;
            blast.sprite = sprite;
            blast.transform.localScale = new Vector3(scale, scale, 1f);
            blast.color = new Color(1f, 1f, 1f, alpha);
            return true;
        }

        private bool UpdateGlow(float r)
        {
            if (r >= GlowTime)
            {
                glowMain.enabled = false;
                glowLight.enabled = false;
                return false;
            }
            // 폭발 그림 아래에서 속성색 빛이 번졌다가 사라져. 겹친 가운데가 가장 밝아.
            float alpha = 1f - r / GlowTime;
            float radius = nominalWidth * (0.45f + 0.35f * EffectMath.EaseOut(r / 0.1f));
            float mainScale = radius * 2f / glowWorldSize, lightScale = radius * 1.1f / glowWorldSize;
            glowMain.transform.localScale = new Vector3(mainScale, mainScale, 1f);
            glowMain.color = WithAlpha(mainColor, 0.7f * alpha);
            glowLight.transform.localScale = new Vector3(lightScale, lightScale, 1f);
            glowLight.color = WithAlpha(lightColor, alpha);
            return true;
        }

        private bool UpdateRays(float r)
        {
            float alpha = r < 0.04f ? 1f : 1f - (r - 0.04f) / (RayTime - 0.04f);
            if (alpha <= 0f)
            {
                if (rayRenderers[0].enabled)
                    for (int i = 0; i < RayCount; i++)
                        rayRenderers[i].enabled = false;
                return false;
            }
            float start = nominalWidth * 0.08f;
            float grow = EffectMath.EaseOut(r / RayGrowTime);
            float thin = 1f - 0.6f * Mathf.Min(1f, r / RayTime);
            Color color = WithAlpha(lightColor, alpha);
            for (int i = 0; i < RayCount; i++)
            {
                LightRay ray = rays[i];
                float length = Mathf.Max(0.001f, ray.Length * grow);
                Vector2 direction = new Vector2(Mathf.Cos(ray.Angle), Mathf.Sin(ray.Angle));
                // 빛줄기 이미지는 왼쪽(굵은 쪽)이 가운데를 향해.
                Transform target = rayRenderers[i].transform;
                target.position = WorldPoint(blastCenter + direction * (start + length * 0.5f));
                target.rotation = Quaternion.Euler(0f, 0f, ray.Angle * Mathf.Rad2Deg);
                target.localScale = new Vector3(length / rayLength, ray.Width * thin / rayWidth, 1f);
                rayRenderers[i].color = color;
            }
            return true;
        }

        private bool UpdateSparks(float r)
        {
            bool alive = false;
            for (int i = 0; i < SparkCount; i++)
            {
                SpriteRenderer renderer = sparkRenderers[i];
                if (!renderer.enabled)
                    continue;
                Spark spark = sparks[i];
                float k = r / spark.Life;
                if (k >= 1f)
                {
                    renderer.enabled = false;
                    continue;
                }
                alive = true;
                Vector2 velocity0 = spark.Direction * spark.Speed;
                Vector2 head = blastCenter + spark.Direction * 0.15f + EffectMath.Drift(velocity0, SparkGravity, SparkDrag, r);
                Vector2 velocity = EffectMath.VelocityAt(velocity0, SparkGravity, SparkDrag, r);
                float speed = velocity.magnitude;
                // 빠를수록 꼬리가 길어. 밝은 쪽(이미지 왼쪽)이 앞으로 가게 뒤집어.
                float length = Mathf.Max(0.02f, speed * 0.04f);
                Vector2 direction = speed > 0.0001f ? velocity / speed : spark.Direction;
                Transform target = renderer.transform;
                target.position = WorldPoint(head - direction * (length * 0.5f));
                target.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f);
                target.localScale = new Vector3(length / rayLength, spark.Width * (1f - 0.5f * k) / rayWidth, 1f);
                renderer.color = WithAlpha(lightColor, 1f - k);
            }
            return alive;
        }

        private bool UpdateParts(float r)
        {
            bool alive = false;
            Vector2 physics = PartPhysics[(int)blastElement];
            for (int i = 0; i < partUsed; i++)
            {
                SpriteRenderer renderer = partRenderers[i];
                if (!renderer.enabled)
                    continue;
                Part part = parts[i];
                float k = r / part.Life;
                if (k >= 1f)
                {
                    renderer.enabled = false;
                    continue;
                }
                alive = true;
                Vector2 position = blastCenter + part.Start + EffectMath.Drift(part.Velocity, physics.x, physics.y, r);
                float scale = part.Twinkle
                    ? part.Scale * (k < 0.25f ? 0.5f + 0.5f * k / 0.25f : 1f - 0.6f * (k - 0.25f) / 0.75f)
                    : part.Scale * (1f - 0.35f * k);
                float angle;
                if (float.IsNaN(part.Head))
                {
                    angle = part.Angle + part.Spin * r;
                }
                else
                {
                    // 머리가 날아가는 쪽을 보게 돌려.
                    Vector2 velocity = EffectMath.VelocityAt(part.Velocity, physics.x, physics.y, r);
                    angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - part.Head;
                }
                Transform target = renderer.transform;
                target.position = WorldPoint(position);
                target.rotation = Quaternion.Euler(0f, 0f, angle);
                target.localScale = new Vector3(scale, scale, 1f);
                renderer.color = new Color(1f, 1f, 1f, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
            }
            return alive;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
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

        private void OnDisable()
        {
            if (blast == null)
                return;
            detonateTime = -1f;
            previewShown = false;
            HideAll();
        }

        private void OnDestroy()
        {
            if (ownsAdditive && additive != null)
                Destroy(additive);
        }
    }
}
