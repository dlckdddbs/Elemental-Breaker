using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalBreaker
{
    // 얼음 아이템(보드 하강 잠금) 연출이야. 오브젝트는 실행 중에 한 번만 만들고 다시 써.
    // 쓰면: 공이 출발하는 자리에서 얇은 서리 파동이 천천히 퍼져. 파동이 닿은 벽돌은 양 위 모서리가 반짝인 뒤
    //       윗변을 따라 "쩌적" 세 번에 끊어 얼어붙고, 다 덮이면 한 번 하얗게 빛나며 광택이 훑고 지나가.
    //       보드 테두리도 파동이 지나간 곳부터 얼며 드러났다가, 다 드러나면 위는 조금, 아래는 많이 옅어진 유지 밝기로 낮아져.
    //       맨 위 빈 칸 가운데에 "하강 방지 1회"가 떠.
    // 턴 동안: 얼어 있는 벽돌이 깨지면 서리도 같이 사라지며 얼음 조각이 튀어.
    // 턴 끝: 서리와 테두리 선만 짧게 빛나고 "하강 방지!"가 톡 튄 뒤, 서리가 작은 가루로 풀리며 사라져. 그다음 다음 턴이 열려.
    // 히트 스톱과 상관없이 실제 시간으로 움직여.
    public sealed class IceEffects : MonoBehaviour
    {
        // ---- 서리 파동 ----
        private const float WaveTime = 1.4f;
        private const float WaveStartRadius = 0.3f;
        // 파동은 공이 출발한 자리에서 가장 먼 테두리 모서리를 이만큼 지나서 끝나. 출발 자리가 한쪽에 있어도 끝까지 닿아.
        private const float WaveOvershoot = 0.3f;
        private const float WaveFadeIn = 0.12f;
        // 마지막 25% 동안 사라져.
        private const float WaveFadeFrom = 0.75f;
        // 월드 두께: 서리선, 부드러운 빛, 안쪽 냉기 폭이야.
        private static readonly Vector4 WaveLine = new Vector4(0.02f, 0.1f, 0.9f, 0f);

        // ---- 벽돌 서리막 ----
        // 파동이 닿은 뒤 다 덮일 때까지의 시간이야. 세 번에 끊어서 번지고, 한 번은 칸의 앞 35% 안에 빠르게 번진 뒤 잠깐 멈춰.
        private const float FreezeGrow = 0.42f;
        private const int FreezeSteps = 3;
        private const float FreezeSnap = 0.35f;
        // 다 덮인 퍼짐 값이야. 셰이더의 가장 늦게 어는 곳(1.0)보다 조금 커야 경계까지 다 보여.
        private const float FilmFull = 1.1f;
        // 다 덮인 뒤 하얗게 빛나고 광택이 훑고 지나가는 시간과 빛 세기야.
        private const float SnapTime = 0.23f;
        private const float SnapGlow = 0.6f;
        // 파동이 닿는 순간 양 위 모서리 반짝임이야.
        private const float SparkleTime = 0.22f;
        private const float SparkleSize = 0.08f;
        private const float SparkleGrow = 0.05f;
        private const int SparkleCount = 32;

        // ---- 보드 테두리 ----
        // 파동보다 아주 조금 늦게 따라 드러나.
        private const float FrameRevealDelay = 0.05f;
        private const float RevealSoftness = 0.05f;
        // 다 드러난 뒤 유지 밝기로 낮아지는 시간과, 유지 밝기에서 빠지는 양(위, 중간, 아래)이야.
        private const float HoldFade = 0.5f;
        private const float HoldTop = 0.25f;
        private const float HoldMiddle = 0.45f;
        private const float HoldBottom = 0.8f;
        // 턴 끝 반짝임: 원래 밝기로 빨리 돌아왔다가 다시 유지 밝기로 내려가고, 바깥 가장자리 띠만 더 빛나.
        private const float FlashUp = 0.06f;
        private const float FlashDown = 0.25f;
        private const float FrameGlowAlpha = 0.7f;
        private const float FrameBand = 0.12f;
        private const float FrameStarSize = 0.22f;
        private const float FrameStarInset = 0.05f;

        // ---- 턴 끝 ----
        private const float ReleaseGlowTime = 0.15f;
        private const float FilmGlowAlpha = 0.7f;
        private const float ScatterStart = 0.45f;
        private const float ScatterTime = 0.5f;
        // 턴 끝 연출 전체 시간이야. 끝나면 다음 턴이 열려.
        private const float ReleaseTime = 1f;

        // ---- 상태 표시 ----
        private const float ChipIn = 1.45f;
        private const float ChipPopTime = 0.2f;
        private const float ChipFadeIn = 0.12f;
        private const float ChipBounceTime = 0.3f;
        private const float ChipBounce = 0.22f;
        private const float ChipGlowAlpha = 0.5f;
        private const float ChipFadeStart = 0.7f;
        private const float ChipFadeTime = 0.2f;
        private const float ChipEnd = 0.95f;
        private const float ChipWidth = 1.75f;
        private const float ChipHeight = 0.36f;
        private const float ChipFlakeSize = 0.22f;
        private const float ChipLabelX = 0.13f;
        private const float ChipFontSize = 2f;
        private const string HoldText = "하강 방지 1회";
        private const string ReleaseText = "하강 방지!";
        private static readonly Color ChipTextColor = new Color32(0xDD, 0xF6, 0xFF, 0xFF);

        // ---- 서리 가루, 얼음 조각 ----
        private const int DustCount = 120;
        private const float DustAlpha = 0.85f;
        private const int ShardCount = 8;
        private const float ShardLife = 0.6f;
        private const float ShardSize = 0.16f;
        private const float ShardGravity = 9f;

        private const string FilmShaderPath = "Shaders/FrostFilm";
        private const string FrameShaderPath = "Shaders/FrostFrame";
        private const string WaveShaderPath = "Shaders/FrostWave";
        private const string AdditiveShaderPath = "Shaders/SpriteAdditive";

        private static readonly int FilmRectId = Shader.PropertyToID("_FilmRect");
        private static readonly int RectId = Shader.PropertyToID("_Rect");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int BandId = Shader.PropertyToID("_Band");
        private static readonly int WaveId = Shader.PropertyToID("_Wave");
        private static readonly int LineId = Shader.PropertyToID("_Line");

        private struct Dust
        {
            public Vector2 Start, Velocity;
            public float Delay, Life, Size;
        }

        private struct Shard
        {
            public Vector2 Origin, Velocity;
            public float Start, Angle, Spin;
        }

        private BrickBoard board;
        private BallLauncher launcher;
        private Material spriteMaterial, additive, filmMaterial, frameMaterial, frameGlowMaterial, waveMaterial;
        private Sprite filmSprite, dustSprite, starSprite, quadSprite;
        private readonly Sprite[] shardSprites = new Sprite[4];
        private SpriteRenderer wave, frame, frameGlow, chip, chipGlow, chipFlake;
        private Transform chipRoot;
        private TextMeshPro chipLabel;
        private readonly SpriteRenderer[] frameStars = new SpriteRenderer[2];
        private readonly SpriteRenderer[] sparkles = new SpriteRenderer[SparkleCount];
        private readonly SpriteRenderer[] dustRenderers = new SpriteRenderer[DustCount];
        private readonly Dust[] dust = new Dust[DustCount];
        private readonly SpriteRenderer[] shardRenderers = new SpriteRenderer[ShardCount];
        private readonly Shard[] shards = new Shard[ShardCount];
        private float dustWorldSize = 1f, starWorldSize = 1f, shardWorldSize = 1f;
        private Rect frameRect;
        private float chipY;

        private Vector2 origin;
        private float waveEndRadius;
        private float freezeStart = -1f;
        private float releaseStart = -1f;
        private Action onReleased;
        private bool bricksSettled, releaseSounded, dustSpawned, badgeAside;
        private int sparkleUsed, dustUsed, nextShard;
        private string chipText;

        public void Initialize(BrickBoard owner, BallLauncher ballLauncher, Material material, int sortingLayerId, int baseOrder)
        {
            board = owner;
            launcher = ballLauncher;
            spriteMaterial = material;
            additive = CreateMaterial(AdditiveShaderPath);
            filmMaterial = CreateMaterial(FilmShaderPath);
            frameMaterial = CreateMaterial(FrameShaderPath);
            frameGlowMaterial = CreateMaterial(FrameShaderPath);
            waveMaterial = CreateMaterial(WaveShaderPath);

            filmSprite = ElementRules.LoadEffectSprite("frost_film");
            dustSprite = ElementRules.LoadEffectSprite("frost_dust");
            starSprite = ElementRules.LoadEffectSprite("freeze_star");
            for (int i = 0; i < shardSprites.Length; i++)
                shardSprites[i] = ElementRules.LoadEffectSprite("freeze_shard_" + i);
            // 서리막이 아틀라스 어디에 있든 셰이더가 이미지 칸 안의 위치를 알 수 있게 UV 사각형을 넘겨.
            if (filmSprite != null && filmMaterial != null)
                filmMaterial.SetVector(FilmRectId, UvRect(filmSprite));
            // 파동은 흰 사각형 하나에 셰이더로 원을 그려. 한 변이 1월드야.
            quadSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f, 0,
                SpriteMeshType.FullRect);
            if (dustSprite != null)
                dustWorldSize = dustSprite.rect.width / dustSprite.pixelsPerUnit;
            if (starSprite != null)
                starWorldSize = starSprite.rect.width / starSprite.pixelsPerUnit;
            if (shardSprites[0] != null)
                shardWorldSize = shardSprites[0].rect.width / shardSprites[0].pixelsPerUnit;

            MeasureFrame();

            // 그리는 순서(뒤→앞): 파동(-3, 벽돌 뒤) 벽돌 서리막(+1, Brick) 서리 가루(+2) 모서리 반짝임(+4)
            //                    얼음 조각·테두리(+6) 테두리 반짝임(+7) 테두리 결정(+8) 상태 표시(+9~+11)
            wave = CreateRenderer("Frost Wave", waveMaterial, sortingLayerId, baseOrder - 3);
            wave.sprite = quadSprite;
            if (waveMaterial != null)
                waveMaterial.SetVector(LineId, WaveLine);

            Sprite frameSprite = ElementRules.LoadEffectSprite("frost_frame");
            frame = CreateFrame("Frost Frame", frameMaterial, frameSprite, sortingLayerId, baseOrder + 6);
            frameGlow = CreateFrame("Frost Frame Glow", frameGlowMaterial, frameSprite, sortingLayerId, baseOrder + 7);
            if (frameGlowMaterial != null)
            {
                frameGlowMaterial.SetFloat(BandId, FrameBand);
                frameGlowMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                frameGlowMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
                frameGlowMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.Zero);
                frameGlowMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            }
            for (int i = 0; i < frameStars.Length; i++)
            {
                frameStars[i] = CreateRenderer("Frost Frame Star", spriteMaterial, sortingLayerId, baseOrder + 8);
                frameStars[i].sprite = starSprite;
            }
            for (int i = 0; i < SparkleCount; i++)
            {
                sparkles[i] = CreateRenderer("Frost Sparkle", spriteMaterial, sortingLayerId, baseOrder + 4);
                sparkles[i].sprite = starSprite;
            }
            for (int i = 0; i < DustCount; i++)
            {
                dustRenderers[i] = CreateRenderer("Frost Dust", spriteMaterial, sortingLayerId, baseOrder + 2);
                dustRenderers[i].sprite = dustSprite;
            }
            for (int i = 0; i < ShardCount; i++)
            {
                shardRenderers[i] = CreateRenderer("Ice Shard", spriteMaterial, sortingLayerId, baseOrder + 6);
                shards[i].Start = -1f;
            }
            CreateChip(sortingLayerId, baseOrder + 9);
            HideAll();
        }

        // 얼음을 쓴 순간이야. from은 공이 출발하는 자리야.
        public void PlayFreeze(Vector2 from)
        {
            origin = from;
            // 테두리 네 모서리 중 가장 먼 곳까지 닿게 파동 끝 반지름을 정해.
            float far = 0f;
            far = Mathf.Max(far, Vector2.Distance(from, new Vector2(frameRect.xMin, frameRect.yMin)));
            far = Mathf.Max(far, Vector2.Distance(from, new Vector2(frameRect.xMax, frameRect.yMin)));
            far = Mathf.Max(far, Vector2.Distance(from, new Vector2(frameRect.xMin, frameRect.yMax)));
            far = Mathf.Max(far, Vector2.Distance(from, new Vector2(frameRect.xMax, frameRect.yMax)));
            waveEndRadius = Mathf.Max(WaveStartRadius + 0.1f, far + WaveOvershoot);
            freezeStart = Time.unscaledTime;
            releaseStart = -1f;
            onReleased = null;
            bricksSettled = releaseSounded = dustSpawned = false;
            chipText = null;
            GameAudio.Play(SoundCue.IceFreeze);
        }

        // 턴이 끝났어. 얼어붙는 연출이 아직이면 다 끝난 뒤에 풀기 시작하고, 다 풀리면 done을 불러.
        public void PlayRelease(Action done)
        {
            if (freezeStart < 0f)
            {
                done?.Invoke();
                return;
            }
            onReleased = done;
            releaseStart = Mathf.Max(Time.unscaledTime, freezeStart + WaveTime + FreezeGrow + SnapTime);
        }

        // 얼어 있던 벽돌이 깨졌어. 서리막은 벽돌과 같이 사라지고, 얼음 조각 두 개가 위로 튀었다 떨어져.
        public void PlayShatter(Vector2 position, Vector2 size)
        {
            for (int i = 0; i < 2; i++)
            {
                int index = nextShard;
                nextShard = (nextShard + 1) % ShardCount;
                shards[index] = new Shard
                {
                    Origin = position + new Vector2(0f, size.y * 0.38f),
                    Velocity = new Vector2((UnityEngine.Random.value - 0.5f) * 3f, 1.5f + UnityEngine.Random.value * 1.5f),
                    Start = Time.unscaledTime,
                    Angle = UnityEngine.Random.value * 360f,
                    Spin = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * 400f
                };
                shardRenderers[index].sprite = shardSprites[UnityEngine.Random.Range(0, shardSprites.Length)];
            }
        }

        private void Update()
        {
            UpdateShards();
            if (freezeStart < 0f)
                return;
            float now = Time.unscaledTime;
            float t = now - freezeStart;
            // 턴 끝 연출이 시작된 뒤 흐른 시간이야. 아직이면 음수야.
            float e = releaseStart >= 0f && now >= releaseStart ? now - releaseStart : -1f;
            if (e >= 0f && !releaseSounded)
            {
                releaseSounded = true;
                GameAudio.Play(SoundCue.IceRelease);
            }
            UpdateWave(t);
            UpdateFrame(t, e);
            UpdateBricks(t, e);
            UpdateChip(t, e);
            UpdateDust(e);
            if (e >= ReleaseTime)
                Finish();
        }

        private void UpdateWave(float t)
        {
            if (t >= WaveTime || waveMaterial == null)
            {
                wave.enabled = false;
                return;
            }
            float k = t / WaveTime;
            float alpha = Mathf.Clamp01(t / WaveFadeIn) * (k < WaveFadeFrom ? 1f : 1f - (k - WaveFadeFrom) / (1f - WaveFadeFrom));
            float radius = WaveRadius(t);
            // 원이 다 들어가는 크기의 사각형만 그려.
            float size = (radius + WaveLine.y) * 2f + 0.1f;
            wave.transform.position = new Vector3(origin.x, origin.y, 0f);
            wave.transform.localScale = new Vector3(size, size, 1f);
            waveMaterial.SetVector(WaveId, new Vector4(origin.x, origin.y, radius, alpha));
            wave.enabled = true;
        }

        private void UpdateFrame(float t, float e)
        {
            if (frameMaterial == null)
                return;
            float alpha = e >= ScatterStart ? 1f - Mathf.Clamp01((e - ScatterStart) / ScatterTime) : 1f;
            bool revealing = t < WaveTime + FrameRevealDelay;
            float revealRadius = revealing ? WaveRadius(Mathf.Max(0f, t - FrameRevealDelay)) : 1000f;
            float hold = Mathf.Clamp01((t - WaveTime - FrameRevealDelay) / HoldFade);
            if (e >= 0f)
                hold *= e < FlashUp ? 1f - e / FlashUp : Mathf.Clamp01((e - FlashUp) / FlashDown);
            Vector4 fade = new Vector4(1f - HoldTop * hold, 1f - HoldMiddle * hold, 1f - HoldBottom * hold, 0f);
            Vector4 reveal = new Vector4(origin.x, origin.y, revealRadius, RevealSoftness);
            frame.enabled = alpha > 0f;
            frame.color = new Color(1f, 1f, 1f, alpha);
            frameMaterial.SetVector(FadeId, fade);
            frameMaterial.SetVector(RevealId, reveal);

            float glow = e >= 0f && e < ReleaseGlowTime ? FrameGlowAlpha * (1f - e / ReleaseGlowTime) : 0f;
            frameGlow.enabled = glow > 0f && frameGlowMaterial != null;
            if (frameGlow.enabled)
            {
                frameGlow.color = new Color(1f, 1f, 1f, alpha * glow);
                frameGlowMaterial.SetVector(FadeId, fade);
                frameGlowMaterial.SetVector(RevealId, reveal);
            }

            // 얼어 나오는 맨 앞(파동과 양옆 테두리가 만나는 곳)에 작은 결정이 반짝여.
            for (int i = 0; i < frameStars.Length; i++)
            {
                SpriteRenderer star = frameStars[i];
                float side = i == 0 ? frameRect.xMin : frameRect.xMax;
                float dx = Mathf.Abs(side - origin.x);
                float y = revealing && revealRadius > dx ? origin.y + Mathf.Sqrt(revealRadius * revealRadius - dx * dx) : float.MaxValue;
                star.enabled = y <= frameRect.yMax;
                if (!star.enabled)
                    continue;
                star.transform.position = new Vector3(side + (i == 0 ? FrameStarInset : -FrameStarInset), y, 0f);
                star.transform.localScale = Vector3.one * (FrameStarSize / starWorldSize);
                star.color = new Color(1f, 1f, 1f, 0.9f);
            }
        }

        private void UpdateBricks(float t, float e)
        {
            // 다 얼어붙은 뒤 턴 끝까지는 서리막이 그대로라 매 프레임 바꾸지 않아.
            bool settled = t > WaveTime + FreezeGrow + SnapTime;
            if (bricksSettled && e < 0f)
                return;
            float fade = e >= ScatterStart ? 1f - Mathf.Clamp01((e - ScatterStart) / ScatterTime) : 1f;
            float releaseGlow = e >= 0f && e < ReleaseGlowTime ? FilmGlowAlpha * (1f - e / ReleaseGlowTime) : 0f;
            // 서리막 셰이더가 없으면 퍼짐 없이 일반 재질로 바로 보여 줘.
            Material filmDraw = filmMaterial != null ? filmMaterial : spriteMaterial;
            sparkleUsed = 0;
            for (int row = 0; row < board.Rows; row++)
                for (int column = 0; column < board.Columns; column++)
                {
                    Brick brick = board.GetBrick(row, column);
                    if (brick == null)
                        continue;
                    Vector2 center = board.GetCellCenter(row, column);
                    float s = t - ReachTime(Vector2.Distance(origin, center));
                    if (s < 0f)
                    {
                        brick.HideFrost();
                        continue;
                    }
                    // "쩌적": 세 번에 끊어서, 한 번은 칸의 앞부분에 빠르게 번지고 잠깐 멈춰.
                    float k = Mathf.Clamp01(s / FreezeGrow);
                    int step = Mathf.Min(FreezeSteps - 1, Mathf.FloorToInt(k * FreezeSteps));
                    float progress = k >= 1f ? FilmFull
                        : FilmFull * (step + EaseOut((k * FreezeSteps - step) / FreezeSnap)) / FreezeSteps;
                    float snap = (s - FreezeGrow) / SnapTime;
                    bool snapping = snap >= 0f && snap < 1f;
                    float glow = Mathf.Max(snapping ? SnapGlow * (1f - snap) : 0f, releaseGlow);
                    brick.SetFrost(filmSprite, filmDraw, progress, glow, snapping ? snap : -1f, fade);
                    if (s < SparkleTime)
                        PlaceSparkles(center, brick.Size, s);
                }
            for (int i = sparkleUsed; i < SparkleCount; i++)
                sparkles[i].enabled = false;
            if (settled && e < 0f)
                bricksSettled = true;
        }

        // 파동이 닿는 순간 벽돌 양 위 모서리에 작은 반짝임을 띄워. 거기서부터 얼기 시작해.
        private void PlaceSparkles(Vector2 center, Vector2 size, float s)
        {
            if (sparkleUsed + 2 > SparkleCount)
                return;
            float q = Mathf.Sin(s / SparkleTime * Mathf.PI);
            float world = SparkleSize + SparkleGrow * q;
            float top = center.y + size.y * 0.5f - world * 0.1f;
            for (int side = 0; side < 2; side++)
            {
                SpriteRenderer sparkle = sparkles[sparkleUsed++];
                float x = side == 0 ? center.x - size.x * 0.5f + world * 0.1f : center.x + size.x * 0.5f - world * 0.1f;
                sparkle.transform.position = new Vector3(x, top, 0f);
                sparkle.transform.localScale = Vector3.one * (world / starWorldSize);
                sparkle.color = new Color(1f, 1f, 1f, 0.9f * q);
                sparkle.enabled = true;
            }
        }

        private void UpdateChip(float t, float e)
        {
            bool visible = t >= ChipIn && e < ChipEnd;
            // 상태 표시가 떠 있는 동안 ×2 배속 표시는 오른쪽으로 비켜.
            if (visible != badgeAside)
            {
                badgeAside = visible;
                launcher.SetSpeedBadgeAside(visible);
            }
            if (chipRoot.gameObject.activeSelf != visible)
                chipRoot.gameObject.SetActive(visible);
            if (!visible)
                return;
            float age = t - ChipIn;
            float scale = 0.6f + 0.4f * EaseOut(age / ChipPopTime);
            float alpha = Mathf.Clamp01(age / ChipFadeIn);
            float glow = 0f;
            string text = HoldText;
            Color textColor = ChipTextColor;
            if (e >= 0f)
            {
                // 턴 끝: "하강 방지!"로 바뀌며 톡 튀고 잠깐 빛난 뒤 사라져.
                text = ReleaseText;
                textColor = Color.white;
                scale = e < ChipBounceTime ? 1f + ChipBounce * Mathf.Sin(e / ChipBounceTime * Mathf.PI) : 1f;
                if (e >= ChipFadeStart)
                    alpha = 1f - Mathf.Clamp01((e - ChipFadeStart) / ChipFadeTime);
                if (e < ChipBounceTime)
                    glow = ChipGlowAlpha * (1f - e / ChipBounceTime);
            }
            chipRoot.localScale = new Vector3(scale, scale, 1f);
            chip.color = new Color(1f, 1f, 1f, alpha);
            chipFlake.color = new Color(1f, 1f, 1f, alpha);
            chipGlow.enabled = glow > 0f;
            chipGlow.color = new Color(1f, 1f, 1f, alpha * glow);
            if (chipText != text)
            {
                chipText = text;
                chipLabel.text = text;
            }
            textColor.a = alpha;
            chipLabel.color = textColor;
        }

        private void UpdateDust(float e)
        {
            if (e < ScatterStart)
                return;
            if (!dustSpawned)
            {
                dustSpawned = true;
                SpawnDust();
            }
            float k = e - ScatterStart;
            for (int i = 0; i < dustUsed; i++)
            {
                Dust d = dust[i];
                SpriteRenderer renderer = dustRenderers[i];
                float age = k - d.Delay;
                if (age < 0f || age > d.Life)
                {
                    renderer.enabled = false;
                    continue;
                }
                renderer.transform.position = d.Start + d.Velocity * age;
                renderer.transform.localScale = Vector3.one * (d.Size / dustWorldSize);
                renderer.color = new Color(1f, 1f, 1f, DustAlpha * (1f - age / d.Life));
                renderer.enabled = true;
            }
        }

        // 서리가 있던 곳(벽돌 양 위 모서리와 윗변 한 곳, 테두리 위쪽과 양옆 위)에서만 몇 알씩 나와.
        private void SpawnDust()
        {
            dustUsed = 0;
            for (int row = 0; row < board.Rows; row++)
                for (int column = 0; column < board.Columns; column++)
                {
                    Brick brick = board.GetBrick(row, column);
                    if (brick == null || !brick.IsFrozen)
                        continue;
                    Vector2 center = board.GetCellCenter(row, column);
                    float left = center.x - brick.Size.x * 0.5f;
                    float top = center.y + brick.Size.y * 0.5f - 0.01f;
                    AddDust(left + 0.05f * brick.Size.x, top);
                    AddDust(left + 0.95f * brick.Size.x, top);
                    AddDust(left + (0.3f + UnityEngine.Random.value * 0.4f) * brick.Size.x, top);
                }
            for (int j = 0; j < 7; j++)
                AddDust(frameRect.xMin + 0.35f + j * (frameRect.width - 0.7f) / 6f, frameRect.yMax - 0.08f);
            for (int j = 0; j < 3; j++)
            {
                AddDust(frameRect.xMin + 0.08f, frameRect.yMax - 0.9f - j * 1.1f);
                AddDust(frameRect.xMax - 0.08f, frameRect.yMax - 0.9f - j * 1.1f);
            }
        }

        private void AddDust(float x, float y)
        {
            if (dustUsed >= DustCount)
                return;
            dust[dustUsed++] = new Dust
            {
                Start = new Vector2(x, y),
                Velocity = new Vector2((UnityEngine.Random.value - 0.4f) * 0.3f, 0.15f + UnityEngine.Random.value * 0.25f),
                Delay = UnityEngine.Random.value * 0.12f,
                Life = 0.4f + UnityEngine.Random.value * 0.2f,
                Size = 0.035f + UnityEngine.Random.value * 0.03f
            };
        }

        private void UpdateShards()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < ShardCount; i++)
            {
                if (shards[i].Start < 0f)
                    continue;
                SpriteRenderer renderer = shardRenderers[i];
                float k = now - shards[i].Start;
                if (k > ShardLife)
                {
                    shards[i].Start = -1f;
                    renderer.enabled = false;
                    continue;
                }
                Shard s = shards[i];
                renderer.transform.position = s.Origin + new Vector2(s.Velocity.x * k, s.Velocity.y * k - 0.5f * ShardGravity * k * k);
                renderer.transform.rotation = Quaternion.Euler(0f, 0f, s.Angle + s.Spin * k);
                renderer.transform.localScale = Vector3.one * (ShardSize / shardWorldSize);
                renderer.color = new Color(1f, 1f, 1f, 1f - k / ShardLife);
                renderer.enabled = true;
            }
        }

        private void Finish()
        {
            freezeStart = -1f;
            releaseStart = -1f;
            for (int row = 0; row < board.Rows; row++)
                for (int column = 0; column < board.Columns; column++)
                {
                    Brick brick = board.GetBrick(row, column);
                    if (brick != null)
                        brick.HideFrost();
                }
            HideAll();
            if (badgeAside)
            {
                badgeAside = false;
                launcher.SetSpeedBadgeAside(false);
            }
            Action done = onReleased;
            onReleased = null;
            done?.Invoke();
        }

        private float WaveRadius(float t) =>
            WaveStartRadius + (waveEndRadius - WaveStartRadius) * (0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(t / WaveTime)));

        // 파동이 distance까지 닿는 시각이야. WaveRadius를 거꾸로 푼 값이야.
        private float ReachTime(float distance)
        {
            float k = Mathf.Clamp01((distance - WaveStartRadius) / (waveEndRadius - WaveStartRadius));
            return Mathf.Acos(1f - 2f * k) / Mathf.PI * WaveTime;
        }

        private static float EaseOut(float k)
        {
            k = Mathf.Clamp01(k);
            return 1f - (1f - k) * (1f - k);
        }

        // 테두리는 양옆 화면 끝(보드 가로 끝), 위쪽 벽 안쪽 면, 회수선에 맞춰.
        private void MeasureFrame()
        {
            Rect boardRect = board.BoardRect;
            float top = boardRect.yMax + board.CellSize.y * 0.95f;
            float bottom = launcher.LaunchPosition.y - 0.2f;
            BoardPresentation presentation = FindFirstObjectByType<BoardPresentation>();
            if (presentation != null && presentation.TryGetPlayBounds(out float wallTop, out float lineBottom))
            {
                top = wallTop;
                bottom = lineBottom;
            }
            frameRect = Rect.MinMaxRect(boardRect.xMin, bottom, boardRect.xMax, top);
            chipY = (top + boardRect.yMax) * 0.5f;
        }

        private SpriteRenderer CreateFrame(string objectName, Material material, Sprite sprite, int layerId, int order)
        {
            SpriteRenderer renderer = CreateRenderer(objectName, material, layerId, order);
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = frameRect.size;
            renderer.transform.position = new Vector3(frameRect.center.x, frameRect.center.y, 0f);
            if (material != null)
                material.SetVector(RectId, new Vector4(frameRect.xMin, frameRect.yMin, frameRect.xMax, frameRect.yMax));
            return renderer;
        }

        private void CreateChip(int layerId, int order)
        {
            chipRoot = new GameObject("Freeze Chip").transform;
            chipRoot.SetParent(transform, false);
            chipRoot.position = new Vector3(board.BoardRect.center.x, chipY, 0f);
            Sprite chipSprite = ElementRules.LoadUiSprite("freeze_chip");
            chip = CreateRenderer("Chip", spriteMaterial, layerId, order);
            chip.transform.SetParent(chipRoot, false);
            chip.sprite = chipSprite;
            chip.drawMode = SpriteDrawMode.Sliced;
            chip.size = new Vector2(ChipWidth, ChipHeight);
            chipGlow = CreateRenderer("Chip Glow", additive != null ? additive : spriteMaterial, layerId, order + 2);
            chipGlow.transform.SetParent(chipRoot, false);
            chipGlow.sprite = chipSprite;
            chipGlow.drawMode = SpriteDrawMode.Sliced;
            chipGlow.size = new Vector2(ChipWidth, ChipHeight);
            chipFlake = CreateRenderer("Chip Flake", spriteMaterial, layerId, order + 1);
            chipFlake.transform.SetParent(chipRoot, false);
            chipFlake.sprite = ElementRules.LoadEffectSprite("freeze_flake_small");
            chipFlake.transform.localPosition = new Vector3(-ChipWidth * 0.5f + 0.1f + ChipFlakeSize * 0.5f, 0f, 0f);
            if (chipFlake.sprite != null)
                chipFlake.transform.localScale = Vector3.one * (ChipFlakeSize / (chipFlake.sprite.rect.width / chipFlake.sprite.pixelsPerUnit));

            chipLabel = new GameObject("Chip Label", typeof(RectTransform)).AddComponent<TextMeshPro>();
            chipLabel.transform.SetParent(chipRoot, false);
            chipLabel.transform.localPosition = new Vector3(ChipLabelX, -0.0125f, 0f);
            chipLabel.rectTransform.sizeDelta = new Vector2(ChipWidth - 0.3f, ChipHeight);
            // 한글이 들어 있는 상단 HUD 폰트를 처음에 한 번만 가져와.
            TopHUD hud = FindFirstObjectByType<TopHUD>();
            TMP_Text sample = hud != null ? hud.GetComponentInChildren<TMP_Text>(true) : null;
            if (sample != null)
                chipLabel.font = sample.font;
            chipLabel.fontSize = ChipFontSize;
            chipLabel.alignment = TextAlignmentOptions.Center;
            chipLabel.text = HoldText;
            if (chipLabel.TryGetComponent(out Renderer labelRenderer))
            {
                labelRenderer.sortingLayerID = layerId;
                labelRenderer.sortingOrder = order + 1;
            }
        }

        private SpriteRenderer CreateRenderer(string objectName, Material material, int layerId, int order)
        {
            SpriteRenderer renderer = new GameObject(objectName).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false);
            renderer.sharedMaterial = material != null ? material : spriteMaterial;
            renderer.sortingLayerID = layerId;
            renderer.sortingOrder = order;
            return renderer;
        }

        private Material CreateMaterial(string shaderPath)
        {
            Shader shader = Resources.Load<Shader>(shaderPath);
            if (shader != null)
                return new Material(shader);
            Debug.LogWarning("IceEffects: 셰이더를 찾지 못해서 일반 재질로 그려. " + shaderPath, this);
            return null;
        }

        // 아틀라스 안에서 이 이미지가 차지하는 UV 사각형(x, y, 너비, 높이)이야.
        private static Vector4 UvRect(Sprite sprite)
        {
            Vector2[] uv = sprite.uv;
            Vector2 min = uv[0], max = uv[0];
            for (int i = 1; i < uv.Length; i++)
            {
                min = Vector2.Min(min, uv[i]);
                max = Vector2.Max(max, uv[i]);
            }
            return new Vector4(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        private void HideAll()
        {
            wave.enabled = false;
            frame.enabled = false;
            frameGlow.enabled = false;
            for (int i = 0; i < frameStars.Length; i++)
                frameStars[i].enabled = false;
            for (int i = 0; i < SparkleCount; i++)
                sparkles[i].enabled = false;
            for (int i = 0; i < DustCount; i++)
                dustRenderers[i].enabled = false;
            dustUsed = 0;
            chipRoot.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            DestroyOwned(additive);
            DestroyOwned(filmMaterial);
            DestroyOwned(frameMaterial);
            DestroyOwned(frameGlowMaterial);
            DestroyOwned(waveMaterial);
            if (quadSprite != null)
                Destroy(quadSprite);
        }

        private static void DestroyOwned(UnityEngine.Object owned)
        {
            if (owned != null)
                Destroy(owned);
        }
    }
}
