using TMPro;
using UnityEngine;

namespace ElementalBreaker
{
    // 속성 폭탄 연출이야. 범위 표시 없이 폭발 자체로 범위를 보여 줘.
    // 누르면 "쾅 → 와르르" 순서로 터져. 히트 스톱 동안은 가운데 작은 별만 보이는 멈춘 장면이고,
    // 멈춤이 풀리는 순간 폭발 그림(8프레임)이 범위까지 빠르게 퍼지면서 끊어진 원형 충격파, 빛줄기, 불꽃, 속성 파편이 튀고 화면이 살짝 흔들려.
    // 폭발 그림은 가운데가 빈 고리 모양으로 조금 더 남아서 뒤따르는 공이 보여.
    // 범위 안 벽돌의 반응(하얘짐, 밀림)과 깨진 벽돌 조각, 코인은 Brick과 BrickEffects가 맡아. 히트 스톱 중에도 움직여야 해서 실제 시간을 써.
    public sealed class BombEffects : MonoBehaviour
    {
        // 폭발 그림 한 칸이 화면에서 차지하는 크기(3x3 범위 가로세로 평균 대비)야. 가장 큰 프레임이 그 평균의 약 1.2배가 돼.
        private const float BlastSize = 1.32f;
        // 멈춘 장면의 작은 별 크기 배율이야.
        private const float ImpactStarScale = 1.5f;
        // 멈춤이 풀린 뒤 1~7번 프레임이 각각 보이는 시간(초)이야.
        // 가장 밝은 1~2번은 뒤따르는 공을 가리지 않게 짧게 두고, 가운데는 비고 외곽이 두꺼운 3~5번을 길게 붙잡아
        // 폭발의 무게감을 남겨. 6~7번에서 흩어지며 사라져.
        private static readonly float[] FrameTimes = { 0.035f, 0.05f, 0.13f, 0.13f, 0.1f, 0.06f, 0.06f };
        // 재생하는 동안 고리 에너지가 바깥으로 계속 퍼지는 정도야.
        private const float BlastGrow = 0.15f;
        // 폭발 그림 아래 속성색 빛이야. 흰빛으로 덮지 않게 약하게 하고, 고리 단계에 들어가면 빨리 빠져서 가운데가 비어 보여.
        private const float GlowTime = 0.15f;
        private const float GlowLightAlpha = 0.55f;
        private const float GlowMainAlpha = 0.45f;
        // 원형 충격파: 끊어진 고리가 범위 크기(가로세로 평균)의 0.3배 반지름에서 0.72배까지 퍼지며 범위 모서리쯤에서 사라져.
        // 폭발 본체가 범위를 충분히 보여 주니까, 충격파는 옅게 지나가는 정도로만 둬.
        private const float RingTime = 0.26f;
        private const float RingStartRadius = 0.3f;
        private const float RingEndRadius = 0.72f;
        private const float RingAlpha = 0.7f;
        // 고리 이미지(128px)에서 고리 지름이 차지하는 비율이야.
        private const float RingContentRatio = 0.94f;
        // 빛줄기: 범위 테두리 안에서 끝나게 길이를 제한해.
        private const int RayCount = 6;
        private const float RayGrowTime = 0.08f;
        private const float RayTime = 0.18f;
        private const float RayAlpha = 0.75f;
        private const int SparkCount = 10;
        private const int PartCount = 5;
        // 화면 섬광은 누르는 순간 아주 옅게 켜지고 빠르게 꺼져.
        private const float ScreenFlashAlpha = 0.06f;
        private const float ScreenFlashTime = 0.08f;
        // 화면 섬광 지름(화면 긴 변 대비)이야. 폭발이 가장자리에 있어도 반대쪽까지 빛이 닿아.
        private const float ScreenFlashSpan = 2.4f;
        // 화면 흔들림: 작고 빠르게 떨려서 폭발이 한쪽으로 흘러가 보이지 않아. 세기는 월드 거리(벽돌 한 칸이 약 0.66)야.
        private const float ShakeTime = 0.18f;
        private const float ShakeStrength = 0.07f;
        private const int ShakeVibrato = 30;
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
        // 원형 충격파 색이야. 흰색, 주황, 청록, 연두.
        private static readonly Color32[] RingColors =
        {
            new Color32(0xE6, 0xEE, 0xFF, 0xFF), new Color32(0xFF, 0x8A, 0x2A, 0xFF),
            new Color32(0x4F, 0xE0, 0xFF, 0xFF), new Color32(0x9C, 0xFF, 0x3A, 0xFF)
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
            new[] { new PartStyle("bomb_star", 4, 0.28f, float.NaN, true), new PartStyle("bomb_star_blue", 1, 0.2f, float.NaN, true) },
            new[] { new PartStyle("bomb_flame", 3, 0.27f, -135f), new PartStyle("bomb_ember", 2, 0.2f, -135f) },
            new[] { new PartStyle("bomb_drop", 2, 0.17f, -90f), new PartStyle("bomb_splash", 2, 0.22f, 45f), new PartStyle("bomb_splash_small", 1, 0.13f, 90f) },
            new[] { new PartStyle("bomb_leaf_small", 3, 0.22f, float.NaN), new PartStyle("bomb_leaf", 1, 0.26f, float.NaN), new PartStyle("bomb_shard_grass", 1, 0.2f, 45f) }
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
        private BombTotalPopup totalPopup;
        private Material additive;
        private bool ownsAdditive;
        private SpriteRenderer glowMain, glowLight, blast, ring, screenFlash;
        private readonly SpriteRenderer[] rayRenderers = new SpriteRenderer[RayCount];
        private readonly SpriteRenderer[] sparkRenderers = new SpriteRenderer[SparkCount];
        private readonly SpriteRenderer[] partRenderers = new SpriteRenderer[PartCount];
        private readonly LightRay[] rays = new LightRay[RayCount];
        private readonly Spark[] sparks = new Spark[SparkCount];
        private readonly Part[] parts = new Part[PartCount];
        private readonly Sprite[][] partSprites = new Sprite[4][];
        private Sprite[] frames;
        private float frameTotal, rayLength = 1f, rayWidth = 1f, glowWorldSize = 1f, ringWorldSize = 1f;

        private float detonateTime = -1f;
        private float impactTime;
        private bool released;
        private int partUsed;
        private float ringAngle;
        private Vector2 blastCenter;
        private ElementType blastElement;
        private float nominalSize = 1f, blastScale = 1f;
        private Color lightColor, mainColor, ringColor;

        // labelStyle은 벽돌 숫자야. 데미지 합계 숫자가 같은 글꼴을 써.
        public void Initialize(BrickBoard owner, Material material, TMP_Text labelStyle, int sortingLayerId, int baseOrder)
        {
            board = owner;
            // 빛은 더하기 합성으로 그려서 어두운 배경에서 빛나 보여. 셰이더가 없으면 일반 재질로 대신해.
            Shader shader = Resources.Load<Shader>(AdditiveShaderPath);
            ownsAdditive = shader != null;
            additive = ownsAdditive ? new Material(shader) : material;
            if (!ownsAdditive)
                Debug.LogWarning("BombEffects: 더하기 셰이더를 찾지 못해서 일반 재질로 그려. " + AdditiveShaderPath, this);

            // 그리는 순서(뒤→앞): 폭발 아래 빛(+3) 폭발 그림(+4) 벽돌 조각(+5, BrickEffects)·충격파·빛줄기·불꽃(+5)
            //                    속성 파편(+6) 화면 섬광(+10)
            Sprite glow = ElementRules.LoadEffectSprite("puff");
            Sprite ray = ElementRules.LoadEffectSprite("ray");
            glowMain = CreateRenderer("Bomb Glow", additive, sortingLayerId, baseOrder + 3);
            glowMain.sprite = glow;
            glowLight = CreateRenderer("Bomb Glow Core", additive, sortingLayerId, baseOrder + 3);
            glowLight.sprite = glow;
            blast = CreateRenderer("Bomb Blast", material, sortingLayerId, baseOrder + 4);
            ring = CreateRenderer("Bomb Shockwave", additive, sortingLayerId, baseOrder + 5);
            ring.sprite = ElementRules.LoadEffectSprite("bomb_ring");
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
            screenFlash = CreateRenderer("Bomb Screen Flash", material, sortingLayerId, baseOrder + 10);
            screenFlash.sprite = glow;
            // 데미지 합계 숫자(+11~+12)는 폭발 연출 전부보다 위에 그려.
            totalPopup = new GameObject("Bomb Total").AddComponent<BombTotalPopup>();
            totalPopup.transform.SetParent(transform, false);
            totalPopup.Initialize(owner, material, labelStyle, sortingLayerId, baseOrder + 11);

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
            if (ring.sprite != null)
                ringWorldSize = SpriteWidth(ring.sprite);
            if (ray != null)
            {
                rayLength = SpriteWidth(ray);
                rayWidth = ray.rect.height / ray.pixelsPerUnit;
            }
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

        // 아틀라스에서 잘려 들어와도 원래 이미지 한 칸 크기로 재야 프레임마다 크기가 같아.
        private static float SpriteWidth(Sprite sprite) => sprite.rect.width / sprite.pixelsPerUnit;

        private void HideAll()
        {
            glowMain.enabled = false;
            glowLight.enabled = false;
            blast.enabled = false;
            ring.enabled = false;
            screenFlash.enabled = false;
            for (int i = 0; i < RayCount; i++)
                rayRenderers[i].enabled = false;
            for (int i = 0; i < SparkCount; i++)
                sparkRenderers[i].enabled = false;
            for (int i = 0; i < PartCount; i++)
                partRenderers[i].enabled = false;
        }

        // center는 누른 순간 맨 앞 공의 자리야. 폭발은 그 자리에서 터지고 끝까지 움직이지 않아.
        // impactDuration은 히트 스톱 시간이고, 그동안 멈춘 장면을 보여 준 뒤 터져.
        // totalDamage는 벽돌에서 실제로 깎인 체력의 합이고, 터진 뒤 폭발 위에 숫자로 떠.
        public void PlayDetonate(Vector2 center, ElementType element, float impactDuration, int totalDamage)
        {
            HideAll();
            totalPopup.Play(center, element, totalDamage, Mathf.Max(0f, impactDuration));
            detonateTime = Time.unscaledTime;
            impactTime = Mathf.Max(0f, impactDuration);
            released = false;
            blastCenter = center;
            blastElement = element;
            lightColor = LightColors[(int)element];
            mainColor = MainColors[(int)element];
            ringColor = RingColors[(int)element];
            ringAngle = Random.Range(0f, 360f);
            frames = ElementRules.GetBlastFrames(element);

            // 폭발 크기는 3x3 범위의 가로세로 평균이 기준이야. 칸이 가로로 넓어서 너비만 쓰면 위아래로 한 줄 가까이 넘쳐 보여.
            // 보드 가장자리에서 범위가 잘려도 폭발 크기는 같아.
            Vector2 rangeSize = board.CellSize * (board.BombRadius * 2 + 1);
            nominalSize = (rangeSize.x + rangeSize.y) * 0.5f;
            blastScale = frames[0] != null ? nominalSize * BlastSize / SpriteWidth(frames[0]) : 1f;
            Vector3 position = WorldPoint(blastCenter);
            blast.transform.position = position;
            ring.transform.position = position;
            glowMain.transform.position = position;
            glowLight.transform.position = position;

            float flashScale = ScreenSpan() * ScreenFlashSpan / glowWorldSize;
            screenFlash.transform.position = position;
            screenFlash.transform.localScale = new Vector3(flashScale, flashScale, 1f);

            SetupRays(rangeSize);
            SetupSparks();
            SetupParts();
            // 이번 프레임의 Update가 이미 지나갔어도 멈춘 장면이 한 프레임 늦지 않게 바로 그려.
            UpdateDetonate(0f);
        }

        private Vector3 WorldPoint(Vector2 point) => new Vector3(point.x, point.y, transform.position.z);

        private void SetupRays(Vector2 rangeSize)
        {
            float start = nominalSize * 0.08f;
            for (int i = 0; i < RayCount; i++)
            {
                float angle = (i / (float)RayCount) * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
                // 폭발 자리에서 3x3 크기 테두리까지의 거리야. 빛줄기는 그 안에서 끝나.
                float edge = Mathf.Min(rangeSize.x * 0.5f / Mathf.Max(0.0001f, Mathf.Abs(Mathf.Cos(angle))),
                    rangeSize.y * 0.5f / Mathf.Max(0.0001f, Mathf.Abs(Mathf.Sin(angle))));
                rays[i] = new LightRay
                {
                    Angle = angle,
                    Length = Mathf.Max(0.05f, edge * Random.Range(0.75f, 0.87f) - start),
                    Width = Random.Range(0.08f, 0.12f)
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
                return;
            if (!UpdateDetonate(Time.unscaledTime - detonateTime))
            {
                detonateTime = -1f;
                HideAll();
            }
        }

        // t는 누른 뒤 지난 실제 시간이야. 아직 보여 줄 게 남아 있으면 true야.
        private bool UpdateDetonate(float t)
        {
            if (!released && t >= impactTime)
                Release();
            // 하나라도 남아 있으면 계속 그려. 모두 돌려야 해서 ||가 아니라 |를 써.
            bool alive = UpdateScreenFlash(t) | UpdateBlast(t);
            if (!released)
                return true;
            float r = t - impactTime;
            return alive | UpdateGlow(r) | UpdateRing(r) | UpdateRays(r) | UpdateSparks(r) | UpdateParts(r);
        }

        // 멈춤이 풀리는 순간이야. 충격파, 빛줄기, 불꽃, 속성 파편, 화면 흔들림이 한꺼번에 시작돼.
        private void Release()
        {
            released = true;
            GameAudio.Bomb(blastElement);
            for (int i = 0; i < RayCount; i++)
                rayRenderers[i].enabled = true;
            for (int i = 0; i < SparkCount; i++)
                sparkRenderers[i].enabled = true;
            for (int i = 0; i < partUsed; i++)
                partRenderers[i].enabled = true;
            glowMain.enabled = true;
            glowLight.enabled = true;
            ring.enabled = ring.sprite != null;
            ScreenShake.Play(ShakeTime, ShakeStrength, ShakeVibrato);
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
                // 고리 에너지가 남아 있는 동안 바깥으로 계속 퍼지고, 마지막 두 프레임에서 흐려져.
                scale = blastScale * (1f + BlastGrow * EffectMath.EaseOut(r / frameTotal));
                if (index == frames.Length - 2)
                    alpha = 1f - 0.25f * k;
                else if (index == frames.Length - 1)
                    alpha = 0.75f * (1f - k);
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
            // 폭발 그림 아래에서 속성색 빛이 번졌다가 사라져. 흰빛으로 덮지 않아서 남은 벽돌과 공이 보여.
            float alpha = 1f - r / GlowTime;
            float radius = nominalSize * (0.45f + 0.35f * EffectMath.EaseOut(r / 0.1f));
            float mainScale = radius * 2f / glowWorldSize, lightScale = radius * 1.1f / glowWorldSize;
            glowMain.transform.localScale = new Vector3(mainScale, mainScale, 1f);
            glowMain.color = WithAlpha(mainColor, GlowMainAlpha * alpha);
            glowLight.transform.localScale = new Vector3(lightScale, lightScale, 1f);
            glowLight.color = WithAlpha(lightColor, GlowLightAlpha * alpha);
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
            // 고정된 테두리가 아니라 바깥으로 퍼지며 사라지는 폭발의 일부야. 살짝 돌면서 퍼져.
            float radius = nominalSize * Mathf.Lerp(RingStartRadius, RingEndRadius, EffectMath.EaseOut(k));
            float scale = radius * 2f / (ringWorldSize * RingContentRatio);
            ring.transform.localScale = new Vector3(scale, scale, 1f);
            ring.transform.rotation = Quaternion.Euler(0f, 0f, ringAngle + r * 86f);
            // 퍼지면서 빠르게 옅어져서 바깥에 테두리처럼 남지 않아.
            ring.color = WithAlpha(ringColor, RingAlpha * (1f - k) * (1f - k));
            return true;
        }

        private bool UpdateRays(float r)
        {
            float alpha = RayAlpha * (r < 0.04f ? 1f : 1f - (r - 0.04f) / (RayTime - 0.04f));
            if (alpha <= 0f)
            {
                if (rayRenderers[0].enabled)
                    for (int i = 0; i < RayCount; i++)
                        rayRenderers[i].enabled = false;
                return false;
            }
            float start = nominalSize * 0.08f;
            float grow = EffectMath.EaseOut(r / RayGrowTime);
            float thin = 1f - 0.6f * Mathf.Min(1f, r / RayTime);
            Color color = WithAlpha(lightColor, alpha);
            for (int i = 0; i < RayCount; i++)
            {
                LightRay lightRay = rays[i];
                float length = Mathf.Max(0.001f, lightRay.Length * grow);
                Vector2 direction = new Vector2(Mathf.Cos(lightRay.Angle), Mathf.Sin(lightRay.Angle));
                // 빛줄기 이미지는 왼쪽(굵은 쪽)이 가운데를 향해.
                Transform target = rayRenderers[i].transform;
                target.position = WorldPoint(blastCenter + direction * (start + length * 0.5f));
                target.rotation = Quaternion.Euler(0f, 0f, lightRay.Angle * Mathf.Rad2Deg);
                target.localScale = new Vector3(length / rayLength, lightRay.Width * thin / rayWidth, 1f);
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

        private void OnDisable()
        {
            if (blast == null)
                return;
            detonateTime = -1f;
            HideAll();
        }

        private void OnDestroy()
        {
            if (ownsAdditive && additive != null)
                Destroy(additive);
        }
    }
}
