using UnityEngine;

namespace ElementalBreaker
{
    // 벽돌이 깨지거나 상성으로 맞을 때의 이펙트를 코드로 움직여. 슬롯을 미리 만들어 두고 Update 하나로 전부 처리해.
    public sealed class BrickEffects : MonoBehaviour
    {
        private const int BreakSlotCount = 12;
        private const int BurstSlotCount = 10;
        private const int ChunkCount = 6;
        private const int AccentCount = 5;
        // 벽돌이 하얗게 번쩍이며 부푸는 시간이야.
        private const float PopTime = 0.05f;
        // 상성으로 깨면 버스트가 먼저 커진 뒤에 조각이 날아가. "쾅 → 와르르" 순서야.
        private const float AdvantageChunkDelay = 0.08f;
        private const float ChunkLife = 0.35f;
        // 버스트는 조각 뒤에 그려지니까 조각이 날아가는 동안 조금 더 남아 있어도 겹쳐 보이지 않아.
        private const float BurstGrowTime = 0.14f;
        private const float BurstHoldTime = 0.14f;
        private const float BurstFadeTime = 0.14f;
        private const float SmallBurstGrowTime = 0.1f;
        private const float SmallBurstHoldTime = 0.07f;
        private const float SmallBurstFadeTime = 0.11f;
        // 일반으로 깰 때 속성 파편은 상성 버스트보다 약하게 보여.
        private const float AccentStrength = 0.75f;
        private const float ChunkGravity = 8.5f;
        private const float CoinSize = 0.22f;

        // 조각 시작 위치와 크기는 벽돌 크기에 대한 비율이야.
        private static readonly Vector2[] ChunkOffsets =
        {
            new Vector2(-0.31f, 0.23f), new Vector2(0f, 0.23f), new Vector2(0.31f, 0.23f),
            new Vector2(-0.31f, -0.23f), new Vector2(0f, -0.23f), new Vector2(0.31f, -0.23f)
        };
        private static readonly Vector2 ChunkSizeRatio = new Vector2(0.29f, 0.31f);
        // 잎 이미지는 회색이라 이 초록을 입혀. 폭탄 파편도 같은 색을 써.
        internal static readonly Color LeafGreen = new Color32(0x6C, 0xD1, 0x3F, 0xFF);

        private sealed class BreakSlot
        {
            public GameObject Root;
            public SpriteRenderer Brick, Burst, Puff;
            // 상성으로 깨서 2골드면 코인도 두 개가 튀어 올라.
            public readonly SpriteRenderer[] Coins = new SpriteRenderer[2];
            public int CoinCount;
            public readonly SpriteRenderer[] Chunks = new SpriteRenderer[ChunkCount];
            public readonly Vector2[] ChunkVelocities = new Vector2[ChunkCount];
            public readonly float[] ChunkSpins = new float[ChunkCount];
            public readonly SpriteRenderer[] Accents = new SpriteRenderer[AccentCount];
            public readonly Vector2[] AccentVelocities = new Vector2[AccentCount];
            public readonly float[] AccentSpins = new float[AccentCount];
            public int AccentUsed;
            public ElementType BrickElement;
            public Color Tint;
            public Vector2 BrickSize;
            public bool Advantage;
            // 폭탄으로 깬 벽돌이야. 큰 폭발이 대표하니까 하얗게 번쩍인 뒤 조각과 코인만 보여 줘.
            public bool Plain;
            public float StartTime;
            public bool Active;
        }

        private sealed class BurstSlot
        {
            public SpriteRenderer Renderer;
            public float StartTime;
            public bool Active;
        }

        private readonly BreakSlot[] breakSlots = new BreakSlot[BreakSlotCount];
        private readonly BurstSlot[] burstSlots = new BurstSlot[BurstSlotCount];
        // 공 속성별 버스트 이미지야. 일반 공은 상성이 없어서 null이야.
        private readonly Sprite[] bursts = new Sprite[4];
        private Sprite brick, puff, coin, ember, drop, leaf, sparkle;
        private Vector2 brickSpriteSize = Vector2.one;
        private float puffWidth = 1f, coinWidth = 1f;

        public void Initialize(Material material, int sortingLayerId, int baseOrder)
        {
            brick = ElementRules.GetBrickSprite();
            puff = ElementRules.LoadEffectSprite("puff");
            coin = ElementRules.LoadEffectSprite("coin");
            ember = ElementRules.LoadEffectSprite("ember");
            drop = ElementRules.LoadEffectSprite("drop");
            leaf = ElementRules.LoadEffectSprite("leaf");
            sparkle = ElementRules.LoadEffectSprite("sparkle");
            bursts[(int)ElementType.Fire] = ElementRules.LoadEffectSprite("burst_fire");
            bursts[(int)ElementType.Water] = ElementRules.LoadEffectSprite("burst_water");
            bursts[(int)ElementType.Grass] = ElementRules.LoadEffectSprite("burst_grass");
            if (brick != null)
                brickSpriteSize = brick.bounds.size;
            if (puff != null)
                puffWidth = puff.bounds.size.x;
            if (coin != null)
                coinWidth = coin.bounds.size.x;

            // 그리는 순서(뒤→앞): 벽돌(+0) 아이콘(+1) 숫자(+2) 번쩍이는 벽돌(+3) 버스트·먼지(+4) 조각(+5) 파편(+6) 코인(+7)
            for (int i = 0; i < BreakSlotCount; i++)
            {
                BreakSlot slot = new BreakSlot { Root = new GameObject("Brick Break") };
                Transform root = slot.Root.transform;
                root.SetParent(transform, false);
                slot.Brick = CreateRenderer(root, material, sortingLayerId, baseOrder + 3);
                slot.Brick.drawMode = SpriteDrawMode.Sliced;
                slot.Burst = CreateRenderer(root, material, sortingLayerId, baseOrder + 4);
                slot.Puff = CreateRenderer(root, material, sortingLayerId, baseOrder + 4);
                for (int c = 0; c < ChunkCount; c++)
                    slot.Chunks[c] = CreateRenderer(root, material, sortingLayerId, baseOrder + 5);
                for (int a = 0; a < AccentCount; a++)
                    slot.Accents[a] = CreateRenderer(root, material, sortingLayerId, baseOrder + 6);
                for (int c = 0; c < slot.Coins.Length; c++)
                    slot.Coins[c] = CreateRenderer(root, material, sortingLayerId, baseOrder + 7);
                slot.Root.SetActive(false);
                breakSlots[i] = slot;
            }
            for (int i = 0; i < BurstSlotCount; i++)
            {
                BurstSlot slot = new BurstSlot { Renderer = CreateRenderer(transform, material, sortingLayerId, baseOrder + 6) };
                slot.Renderer.gameObject.SetActive(false);
                burstSlots[i] = slot;
            }
        }

        private static SpriteRenderer CreateRenderer(Transform parent, Material material, int layerId, int order)
        {
            SpriteRenderer renderer = new GameObject("Effect").AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = layerId;
            renderer.sortingOrder = order;
            return renderer;
        }

        // 유리한 상성으로 맞혔지만 안 깨졌을 때, 맞은 자리에 아주 작은 버스트를 띄워.
        public void PlaySmallBurst(ElementType attacker, Vector2 point)
        {
            Sprite sprite = bursts[(int)attacker];
            if (sprite == null)
                return;
            BurstSlot slot = burstSlots[0];
            for (int i = 0; i < BurstSlotCount; i++)
            {
                if (!burstSlots[i].Active)
                {
                    slot = burstSlots[i];
                    break;
                }
                if (burstSlots[i].StartTime < slot.StartTime)
                    slot = burstSlots[i];
            }
            slot.Active = true;
            slot.StartTime = Time.time;
            slot.Renderer.sprite = sprite;
            slot.Renderer.color = Color.white;
            slot.Renderer.transform.position = new Vector3(point.x, point.y, transform.position.z);
            slot.Renderer.transform.localScale = Vector3.zero;
            slot.Renderer.gameObject.SetActive(true);
        }

        public void PlayBreak(Vector3 position, Vector2 brickSize, ElementType brickElement, bool advantage, ElementType attacker, bool plain = false)
        {
            BreakSlot slot = TakeBreakSlot();
            slot.Active = true;
            slot.StartTime = Time.time;
            slot.BrickElement = brickElement;
            slot.Tint = ElementRules.GetBrickTint(brickElement);
            slot.BrickSize = brickSize;
            Sprite burst = bursts[(int)attacker];
            // 상성으로 깼고 공 속성 버스트가 있을 때만 큰 버스트를 써. 이때는 먼지와 벽돌 속성 파편을 빼서 겹치지 않게 해.
            slot.Plain = plain;
            slot.Advantage = advantage && burst != null && !plain;
            slot.Root.transform.position = new Vector3(position.x, position.y, transform.position.z);
            slot.Root.SetActive(true);

            slot.Brick.sprite = brick;
            slot.Brick.size = brickSize;
            // 폭탄으로 깨면 처음부터 완전히 하얘. 히트 스톱 동안 그대로 멈춰 있어서 임팩트 프레임이 돼.
            slot.Brick.color = plain ? Color.white : slot.Tint;
            slot.Brick.enabled = true;

            slot.Burst.enabled = slot.Advantage;
            if (slot.Advantage)
            {
                slot.Burst.sprite = burst;
                slot.Burst.color = Color.white;
                slot.Burst.transform.localScale = Vector3.zero;
                slot.Burst.transform.localRotation = Quaternion.identity;
            }

            slot.Puff.sprite = puff;
            slot.Puff.enabled = false;
            slot.CoinCount = advantage ? 2 : 1;
            for (int c = 0; c < slot.Coins.Length; c++)
            {
                slot.Coins[c].sprite = coin;
                slot.Coins[c].enabled = false;
            }

            for (int i = 0; i < ChunkCount; i++)
            {
                Vector2 offset = Vector2.Scale(ChunkOffsets[i], brickSize);
                slot.ChunkVelocities[i] = new Vector2(offset.x * 5.5f + Random.Range(-0.4f, 0.4f),
                    1.3f + Random.Range(0f, 1.2f) + offset.y * 2f);
                slot.ChunkSpins[i] = Random.Range(-520f, 520f);
                SpriteRenderer chunk = slot.Chunks[i];
                chunk.sprite = brick;
                chunk.color = slot.Tint;
                chunk.enabled = false;
            }

            slot.AccentUsed = 0;
            for (int i = 0; i < AccentCount; i++)
                slot.Accents[i].enabled = false;
            if (!slot.Advantage && !slot.Plain)
                SetupAccents(slot);
        }

        private void SetupAccents(BreakSlot slot)
        {
            Sprite sprite;
            float minSpeed, maxSpeed;
            switch (slot.BrickElement)
            {
                case ElementType.Fire: sprite = ember; minSpeed = 2.6f; maxSpeed = 3.2f; break;
                case ElementType.Water: sprite = drop; minSpeed = 2.1f; maxSpeed = 2.6f; break;
                case ElementType.Grass: sprite = leaf; minSpeed = 1.8f; maxSpeed = 2.3f; break;
                default: sprite = sparkle; minSpeed = 1.5f; maxSpeed = 1.9f; break;
            }
            if (sprite == null)
                return;
            slot.AccentUsed = slot.BrickElement == ElementType.Grass ? 4 : AccentCount;
            for (int i = 0; i < slot.AccentUsed; i++)
            {
                // 위쪽 부채꼴로 퍼져.
                float angle = (90f + (i - (slot.AccentUsed - 1) * 0.5f) * 34f + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
                slot.AccentVelocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(minSpeed, maxSpeed);
                slot.AccentSpins[i] = slot.BrickElement == ElementType.Grass ? (i % 2 == 0 ? 600f : -600f) : 0f;
                SpriteRenderer accent = slot.Accents[i];
                accent.sprite = sprite;
                accent.transform.localRotation = Quaternion.identity;
            }
        }

        private BreakSlot TakeBreakSlot()
        {
            // 빈 슬롯이 없으면 가장 오래된 이펙트를 다시 써.
            BreakSlot oldest = breakSlots[0];
            for (int i = 0; i < BreakSlotCount; i++)
            {
                if (!breakSlots[i].Active)
                    return breakSlots[i];
                if (breakSlots[i].StartTime < oldest.StartTime)
                    oldest = breakSlots[i];
            }
            return oldest;
        }

        private static float EaseOut(float k)
        {
            k = Mathf.Clamp01(k);
            return 1f - (1f - k) * (1f - k);
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = 0; i < BreakSlotCount; i++)
            {
                BreakSlot slot = breakSlots[i];
                if (slot != null && slot.Active)
                    UpdateBreak(slot, now - slot.StartTime);
            }
            for (int i = 0; i < BurstSlotCount; i++)
            {
                BurstSlot slot = burstSlots[i];
                if (slot != null && slot.Active)
                    UpdateSmallBurst(slot, now - slot.StartTime);
            }
        }

        private void UpdateSmallBurst(BurstSlot slot, float elapsed)
        {
            if (elapsed >= SmallBurstHoldTime + SmallBurstFadeTime)
            {
                slot.Active = false;
                slot.Renderer.gameObject.SetActive(false);
                return;
            }
            float scale = 0.2f + 0.18f * EaseOut(elapsed / SmallBurstGrowTime);
            float alpha = elapsed < SmallBurstHoldTime ? 1f : 1f - (elapsed - SmallBurstHoldTime) / SmallBurstFadeTime;
            slot.Renderer.transform.localScale = new Vector3(scale, scale, 1f);
            slot.Renderer.color = new Color(1f, 1f, 1f, alpha);
        }

        private void UpdateBreak(BreakSlot slot, float elapsed)
        {
            float chunkStart = slot.Advantage ? AdvantageChunkDelay : PopTime;
            if (elapsed >= chunkStart + ChunkLife)
            {
                slot.Active = false;
                slot.Root.SetActive(false);
                return;
            }

            // 하얗게 번쩍이며 살짝 부푼 뒤 사라져.
            if (elapsed < PopTime)
            {
                float k = elapsed / PopTime;
                slot.Brick.size = slot.BrickSize * (1f + 0.08f * k);
                slot.Brick.color = slot.Plain ? Color.white : Color.Lerp(slot.Tint, Color.white, 0.65f * k);
            }
            else if (slot.Brick.enabled)
            {
                slot.Brick.enabled = false;
            }

            // 큰 버스트는 조각 뒤에서 먼저 터지고 빠르게 사라져.
            if (slot.Burst.enabled)
            {
                float alpha = elapsed < BurstHoldTime ? 1f : 1f - (elapsed - BurstHoldTime) / BurstFadeTime;
                if (alpha <= 0f)
                {
                    slot.Burst.enabled = false;
                }
                else
                {
                    float scale = 0.35f + 0.55f * EaseOut(elapsed / BurstGrowTime);
                    slot.Burst.transform.localScale = new Vector3(scale, scale, 1f);
                    slot.Burst.transform.localRotation =
                        Quaternion.Euler(0f, 0f, 12f * Mathf.Clamp01(elapsed / (BurstHoldTime + BurstFadeTime)));
                    slot.Burst.color = new Color(1f, 1f, 1f, alpha);
                }
            }

            float life = elapsed - chunkStart;
            if (life < 0f)
                return;
            float t = life / ChunkLife;

            // 일반으로 깰 때만 가운데에 은은한 먼지가 퍼져.
            if (!slot.Advantage && !slot.Plain)
            {
                if (t < 0.5f)
                {
                    float diameter = 0.5f + 0.9f * (t / 0.5f);
                    float scale = diameter / puffWidth;
                    slot.Puff.enabled = true;
                    slot.Puff.transform.localScale = new Vector3(scale, scale, 1f);
                    slot.Puff.color = new Color(1f, 1f, 1f, 0.59f * (1f - t / 0.5f));
                }
                else if (slot.Puff.enabled)
                {
                    slot.Puff.enabled = false;
                }
            }

            UpdateChunks(slot, life, t);
            UpdateAccents(slot, life, t);
            UpdateCoin(slot, t);
        }

        private void UpdateChunks(BreakSlot slot, float life, float t)
        {
            float shrink = 1f - 0.5f * t;
            Vector2 size = Vector2.Scale(slot.BrickSize, ChunkSizeRatio) * shrink;
            Vector3 scale = new Vector3(size.x / brickSpriteSize.x, size.y / brickSpriteSize.y, 1f);
            Color color = slot.Tint;
            color.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            for (int i = 0; i < ChunkCount; i++)
            {
                SpriteRenderer chunk = slot.Chunks[i];
                chunk.enabled = true;
                Vector2 position = Vector2.Scale(ChunkOffsets[i], slot.BrickSize) + slot.ChunkVelocities[i] * life
                    + 0.5f * life * life * new Vector2(0f, -ChunkGravity);
                chunk.transform.localPosition = position;
                chunk.transform.localScale = scale;
                chunk.transform.localRotation = Quaternion.Euler(0f, 0f, slot.ChunkSpins[i] * life);
                chunk.color = color;
            }
        }

        private void UpdateAccents(BreakSlot slot, float life, float t)
        {
            if (slot.AccentUsed == 0)
                return;
            float alpha = AccentStrength * (t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f);
            for (int i = 0; i < slot.AccentUsed; i++)
            {
                SpriteRenderer accent = slot.Accents[i];
                accent.enabled = true;
                Vector2 velocity = slot.AccentVelocities[i];
                float gravity, scale;
                Color color = Color.white;
                switch (slot.BrickElement)
                {
                    case ElementType.Fire:
                        gravity = 5f;
                        scale = 1.46f * (1f - 0.5f * t);
                        break;
                    case ElementType.Water:
                        gravity = 14f;
                        scale = 0.59f * (1f - 0.5f * t);
                        break;
                    case ElementType.Grass:
                        gravity = 7f;
                        scale = 0.94f;
                        color = LeafGreen;
                        break;
                    default:
                        gravity = 0f;
                        float bump = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
                        scale = (bump * 22f + 3f) / 22f * AccentStrength;
                        break;
                }
                accent.transform.localPosition = velocity * life + 0.5f * life * life * new Vector2(0f, -gravity);
                accent.transform.localScale = new Vector3(scale, scale, 1f);
                if (slot.AccentSpins[i] != 0f)
                    accent.transform.localRotation = Quaternion.Euler(0f, 0f, slot.AccentSpins[i] * life);
                color.a = alpha;
                accent.color = color;
            }
        }

        private void UpdateCoin(BreakSlot slot, float t)
        {
            // 조각이 퍼진 뒤에 코인이 톡 튀어 올라. 두 개면 양옆으로 살짝 벌어지고 두 번째가 조금 늦게 나와.
            for (int i = 0; i < slot.CoinCount; i++)
            {
                float start = 0.12f + i * 0.15f;
                if (t < start)
                    continue;
                float ct = (t - start) / (1f - start);
                float pop = ct < 0.2f ? 0.6f + 0.4f * ct / 0.2f : 1f;
                float scale = CoinSize / coinWidth * pop;
                float x = slot.CoinCount == 2 ? (i == 0 ? -0.12f : 0.12f) : 0f;
                SpriteRenderer coinRenderer = slot.Coins[i];
                coinRenderer.enabled = true;
                coinRenderer.transform.localPosition = new Vector3(x, 0.2f + 0.45f * EaseOut(ct), 0f);
                coinRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                coinRenderer.color = new Color(1f, 1f, 1f, ct < 0.7f ? 1f : 1f - (ct - 0.7f) / 0.3f);
            }
        }
    }
}
