using UnityEngine;

namespace ElementalBreaker
{
    // 역상성으로 퇴장한 공의 이펙트를 코드로 움직여. 슬롯을 미리 만들어 두고 Update 하나로 전부 처리해.
    public sealed class BallRetireEffects : MonoBehaviour
    {
        private const int SlotCount = 8;
        private const int ParticleCount = 6;
        private const float Duration = 0.4f;
        // 퇴장 순간 원래 속성 색으로 번쩍이며 살짝 부푸는 구간이야.
        private const float FlashTime = 0.06f;
        // 공과 오라가 사그라드는 데 걸리는 시간이야.
        private const float ShrinkTime = 0.2f;
        private const float ParticleLife = Duration - FlashTime;

        private static readonly Color FireAsh = new Color32(0x9A, 0xA3, 0xAB, 0xFF);
        private static readonly Color GrassBurnt = new Color32(0x5A, 0x46, 0x32, 0xFF);
        private static readonly Color LeafGreen = new Color32(0x5C, 0xC9, 0x3A, 0xFF);
        private static readonly Color LeafBurnt = new Color32(0x8A, 0x5A, 0x2E, 0xFF);

        private enum ParticleKind { None, Steam, Drop, Leaf, Ember }

        private sealed class Slot
        {
            public GameObject Root;
            public SpriteRenderer Ball;
            public SpriteRenderer Aura;
            public readonly SpriteRenderer[] Particles = new SpriteRenderer[ParticleCount];
            public readonly ParticleKind[] Kinds = new ParticleKind[ParticleCount];
            public readonly Vector2[] Velocities = new Vector2[ParticleCount];
            public readonly float[] Spins = new float[ParticleCount];
            public ElementType Element;
            public float StartTime;
            public bool Active;
        }

        private readonly Slot[] slots = new Slot[SlotCount];
        private Sprite steam, drop, ember, leaf;
        private float ballScale = 1f;

        public void Initialize(Material material, int sortingLayerId, int sortingOrder, float scale)
        {
            ballScale = scale;
            steam = ElementRules.LoadEffectSprite("steam");
            drop = ElementRules.LoadEffectSprite("drop");
            ember = ElementRules.LoadEffectSprite("ember");
            leaf = ElementRules.LoadEffectSprite("leaf");
            for (int i = 0; i < SlotCount; i++)
            {
                Slot slot = new Slot { Root = new GameObject("Retire Effect") };
                slot.Root.transform.SetParent(transform, false);
                slot.Aura = CreateRenderer(slot.Root.transform, "Aura", material, sortingLayerId, sortingOrder);
                slot.Ball = CreateRenderer(slot.Root.transform, "Ball", material, sortingLayerId, sortingOrder + 1);
                for (int p = 0; p < ParticleCount; p++)
                    slot.Particles[p] = CreateRenderer(slot.Root.transform, "Particle", material, sortingLayerId, sortingOrder + 2);
                slot.Root.SetActive(false);
                slots[i] = slot;
            }
        }

        private static SpriteRenderer CreateRenderer(Transform parent, string name, Material material, int layerId, int order)
        {
            SpriteRenderer renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = layerId;
            renderer.sortingOrder = order;
            return renderer;
        }

        public void Play(ElementType element, Vector2 position, Vector2 direction)
        {
            Slot slot = TakeSlot();
            slot.Element = element;
            slot.StartTime = Time.time;
            slot.Active = true;
            slot.Root.transform.position = new Vector3(position.x, position.y, transform.position.z);
            slot.Root.SetActive(true);

            // 연해진 공을 원래 속성 색으로 한 번 되돌려서 사라지기 직전 대비를 살려.
            slot.Ball.sprite = ElementRules.GetBallSprite(element);
            slot.Ball.color = ElementRules.GetBallTint(element);
            slot.Ball.transform.localScale = Vector3.one * ballScale;

            Sprite[] frames = ElementRules.GetAuraFrames(element);
            slot.Aura.enabled = frames != null;
            if (frames != null)
            {
                slot.Aura.sprite = frames[0];
                slot.Aura.color = Color.white;
                slot.Aura.transform.localScale = Vector3.one * ballScale;
                slot.Aura.transform.localRotation =
                    Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }

            for (int i = 0; i < ParticleCount; i++)
            {
                slot.Kinds[i] = ParticleKind.None;
                slot.Spins[i] = 0f;
                slot.Particles[i].enabled = false;
                slot.Particles[i].transform.localPosition = Vector3.zero;
                slot.Particles[i].transform.localRotation = Quaternion.identity;
            }
            switch (element)
            {
                case ElementType.Fire:
                    // 꺼진 불에서 수증기 두 덩어리가 피어올라.
                    for (int i = 0; i < 2; i++)
                        SetParticle(slot, i, ParticleKind.Steam, steam,
                            new Vector2((i == 0 ? -0.2f : 0.2f) + Random.Range(-0.1f, 0.1f), Random.Range(0.9f, 1.1f)), 0f);
                    break;
                case ElementType.Water:
                    // 톡 터지면서 물방울이 사방으로 튀고 떨어져.
                    for (int i = 0; i < ParticleCount; i++)
                    {
                        float angle = (i * 60f + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                        SetParticle(slot, i, ParticleKind.Drop, drop,
                            new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(1.6f, 2.2f), 0f);
                    }
                    break;
                case ElementType.Grass:
                    // 타 버린 잎 세 장이 흩어지고 불씨 세 개가 올라가.
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = (30f + i * 60f + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                        float spin = Random.Range(240f, 420f) * (i % 2 == 0 ? 1f : -1f);
                        SetParticle(slot, i, ParticleKind.Leaf, leaf,
                            new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(1f, 1.3f), spin);
                        SetParticle(slot, i + 3, ParticleKind.Ember, ember,
                            new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(1f, 1.3f)), 0f);
                    }
                    break;
            }
        }

        private static void SetParticle(Slot slot, int index, ParticleKind kind, Sprite sprite, Vector2 velocity, float spin)
        {
            slot.Kinds[index] = kind;
            slot.Particles[index].sprite = sprite;
            slot.Velocities[index] = velocity;
            slot.Spins[index] = spin;
        }

        private Slot TakeSlot()
        {
            // 빈 슬롯이 없으면 가장 오래된 이펙트를 다시 써.
            Slot oldest = slots[0];
            for (int i = 0; i < SlotCount; i++)
            {
                if (!slots[i].Active)
                    return slots[i];
                if (slots[i].StartTime < oldest.StartTime)
                    oldest = slots[i];
            }
            return oldest;
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = 0; i < SlotCount; i++)
            {
                Slot slot = slots[i];
                if (slot == null || !slot.Active)
                    continue;
                float elapsed = now - slot.StartTime;
                if (elapsed >= Duration)
                {
                    slot.Active = false;
                    slot.Root.SetActive(false);
                    continue;
                }
                UpdateBall(slot, elapsed);
                UpdateAura(slot, elapsed);
                UpdateParticles(slot, elapsed);
            }
        }

        private void UpdateBall(Slot slot, float elapsed)
        {
            Color color = ElementRules.GetBallTint(slot.Element);
            float scale;
            if (elapsed < FlashTime)
            {
                scale = Mathf.Lerp(1f, 1.15f, elapsed / FlashTime);
            }
            else
            {
                float k = Mathf.Clamp01((elapsed - FlashTime) / ShrinkTime);
                scale = Mathf.Lerp(1.15f, 0f, k);
                // 불은 잿빛으로 식고 풀은 검게 그을려. 물은 색을 유지한 채 터져.
                if (slot.Element == ElementType.Fire)
                    color = Color.Lerp(color, FireAsh, k * 2f);
                else if (slot.Element == ElementType.Grass)
                    color = Color.Lerp(color, GrassBurnt, k * 2f);
                color.a = 1f - k;
            }
            slot.Ball.transform.localScale = Vector3.one * (ballScale * scale);
            slot.Ball.color = color;
        }

        private void UpdateAura(Slot slot, float elapsed)
        {
            if (!slot.Aura.enabled)
                return;
            // 오라가 공 쪽으로 오그라들며 꺼져. 이미 익숙한 오라가 변해서 작아도 잘 읽혀.
            float k = Mathf.Clamp01(elapsed / ShrinkTime);
            Color color = Color.white;
            if (slot.Element == ElementType.Fire)
                color = Color.Lerp(Color.white, FireAsh, k);
            else if (slot.Element == ElementType.Grass)
                color = Color.Lerp(Color.white, LeafBurnt, k);
            color.a = 1f - k;
            slot.Aura.color = color;
            slot.Aura.transform.localScale = Vector3.one * (ballScale * (1f - k));
        }

        private void UpdateParticles(Slot slot, float elapsed)
        {
            float life = elapsed - FlashTime;
            if (life < 0f)
                return;
            float t = life / ParticleLife;
            for (int i = 0; i < ParticleCount; i++)
            {
                ParticleKind kind = slot.Kinds[i];
                if (kind == ParticleKind.None)
                    continue;
                SpriteRenderer particle = slot.Particles[i];
                particle.enabled = true;
                Vector2 velocity = slot.Velocities[i];
                Vector2 position;
                float size;
                float alpha;
                Color color = Color.white;
                switch (kind)
                {
                    case ParticleKind.Steam:
                        position = velocity * life;
                        size = Mathf.Lerp(0.5f, 1f, t);
                        alpha = t < 0.2f ? t / 0.2f * 0.9f : 0.9f * (1f - (t - 0.2f) / 0.8f);
                        break;
                    case ParticleKind.Drop:
                        position = velocity * life + 0.5f * life * life * new Vector2(0f, -6f);
                        size = Mathf.Lerp(0.42f, 0.2f, t);
                        alpha = 1f - t * t;
                        break;
                    case ParticleKind.Leaf:
                        position = velocity * life + 0.5f * life * life * new Vector2(0f, -3f);
                        size = 0.6f;
                        alpha = 1f - t * t;
                        color = Color.Lerp(LeafGreen, LeafBurnt, t * 2f);
                        break;
                    default:
                        position = velocity * life;
                        size = Mathf.Lerp(0.5f, 0.15f, t);
                        alpha = 1f - t;
                        break;
                }
                particle.transform.localPosition = position;
                particle.transform.localScale = Vector3.one * size;
                if (slot.Spins[i] != 0f)
                    particle.transform.localRotation = Quaternion.Euler(0f, 0f, slot.Spins[i] * life);
                color.a = alpha;
                particle.color = color;
            }
        }
    }
}
