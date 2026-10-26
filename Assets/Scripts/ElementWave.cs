using UnityEngine;

namespace ElementalBreaker
{
    // 벽돌 속성 변경 연출이야. 발사 자리에서 바뀔 속성 색의 물결이 위로 퍼지고, 물결이 지나가는 벽돌부터 하얗게 번쩍이며 새 속성으로 바뀌어.
    // 얼음처럼 재질이나 테두리는 건드리지 않고 물결과 앞선을 따라 흩날리는 속성 조각(잎, 불씨, 물방울)만 그려.
    // 상점에서 살 때와 9턴 해금 보상 둘 다 써. 실제 시간으로 움직여.
    public sealed class ElementWave : MonoBehaviour
    {
        // 물결이 가장 먼 벽돌 칸 모서리를 지나기까지 걸리는 시간이야. 해금 배너는 이만큼 기다렸다가 넘어가.
        public const float Duration = 1f;
        private const float StartRadius = 0.2f;
        private const float Overshoot = 0.4f;
        private const float FadeIn = 0.08f;
        // 이 비율부터 물결이 흐려지며 끝나.
        private const float FadeFrom = 0.8f;
        // 앞선 두께, 빛 두께, 뒤로 남는 빛 폭(월드)이야.
        private static readonly Vector4 WaveLine = new Vector4(0.035f, 0.22f, 1.5f, 0f);
        // 벽돌이 바뀌는 순간 하얗게 섞이는 비율이야. 공에 맞을 때(0.45)보다 밝아.
        private const float ChangeFlash = 0.8f;
        private const int PieceCount = 16;
        private const float PieceSize = 0.2f;
        private const string ShaderPath = "Shaders/ElementWave";
        private static readonly int WaveId = Shader.PropertyToID("_Wave");
        private static readonly int LineId = Shader.PropertyToID("_Line");
        private static readonly int MainColorId = Shader.PropertyToID("_MainColor");
        private static readonly int LightColorId = Shader.PropertyToID("_LightColor");

        // 일반, 불, 물, 풀 순서야. 뒤로 남는 빛은 속성색, 앞선은 밝은 속성색이야.
        private static readonly Color32[] MainColors =
        {
            new Color32(0xBF, 0xD0, 0xEE, 0xFF), new Color32(0xFF, 0x6E, 0x30, 0xFF),
            new Color32(0x28, 0xBC, 0xFF, 0xFF), new Color32(0x64, 0xDE, 0x3B, 0xFF)
        };
        private static readonly Color32[] LightColors =
        {
            new Color32(0xF2, 0xF6, 0xFF, 0xFF), new Color32(0xFF, 0xD4, 0x5C, 0xFF),
            new Color32(0x9E, 0xEB, 0xFF, 0xFF), new Color32(0xD8, 0xFF, 0x9A, 0xFF)
        };
        private static readonly string[] PieceSprites = { "sparkle", "ember", "drop", "leaf" };

        private struct Piece
        {
            public float Angle, Offset, Spin, Scale;
        }

        private BrickBoard board;
        private Material material;
        private SpriteRenderer wave;
        private readonly SpriteRenderer[] pieceRenderers = new SpriteRenderer[PieceCount];
        private readonly Piece[] pieces = new Piece[PieceCount];
        private readonly Sprite[] pieceSprites = new Sprite[PieceSprites.Length];
        private Vector2 origin;
        private float endRadius;
        private float startTime = -1f;
        private ElementType target;

        // 물결은 벽돌 뒤(baseOrder - 3), 조각은 벽돌 숫자 위(baseOrder + 4)에 그려.
        public void Initialize(BrickBoard owner, Material spriteMaterial, int sortingLayerId, int baseOrder)
        {
            board = owner;
            Shader shader = Resources.Load<Shader>(ShaderPath);
            if (shader != null)
            {
                material = new Material(shader);
                material.SetVector(LineId, WaveLine);
            }
            else
                Debug.LogWarning("ElementWave: 셰이더를 찾지 못해서 물결 없이 벽돌만 바꿔. " + ShaderPath, this);
            // 흰 사각형 하나에 셰이더로 원을 그려. 한 변이 1월드야.
            Sprite quad = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f, 0,
                SpriteMeshType.FullRect);
            wave = CreateRenderer("Wave", material != null ? material : spriteMaterial, sortingLayerId, baseOrder - 3);
            wave.sprite = quad;
            for (int i = 0; i < PieceSprites.Length; i++)
                pieceSprites[i] = ElementRules.LoadEffectSprite(PieceSprites[i]);
            for (int i = 0; i < PieceCount; i++)
                pieceRenderers[i] = CreateRenderer("Piece", spriteMaterial, sortingLayerId, baseOrder + 4);
            Hide();
        }

        public void Play(Vector2 from, ElementType element)
        {
            // 앞 물결이 아직 지나가는 중이면 그 속성으로 마저 바꾸고 새로 시작해.
            if (startTime >= 0f)
                ChangeBricks(float.MaxValue);
            origin = from;
            target = element;
            Rect rect = board.BoardRect;
            float far = 0f;
            far = Mathf.Max(far, Vector2.Distance(from, new Vector2(rect.xMin, rect.yMin)));
            far = Mathf.Max(far, Vector2.Distance(from, new Vector2(rect.xMax, rect.yMin)));
            far = Mathf.Max(far, Vector2.Distance(from, new Vector2(rect.xMin, rect.yMax)));
            far = Mathf.Max(far, Vector2.Distance(from, new Vector2(rect.xMax, rect.yMax)));
            endRadius = far + Overshoot;
            if (material != null)
            {
                material.SetColor(MainColorId, MainColors[(int)element]);
                material.SetColor(LightColorId, LightColors[(int)element]);
            }
            // 조각은 물결이 올라가는 위쪽 반원에 고르게 흩어 놓고, 자리와 크기를 조금씩 흔들어.
            Sprite sprite = pieceSprites[(int)element];
            float width = sprite != null ? sprite.rect.width / sprite.pixelsPerUnit : 1f;
            for (int i = 0; i < PieceCount; i++)
            {
                pieces[i] = new Piece
                {
                    Angle = Mathf.Lerp(12f, 168f, (i + Random.Range(0.1f, 0.9f)) / PieceCount) * Mathf.Deg2Rad,
                    Offset = Random.Range(-0.15f, 0.05f),
                    Spin = Random.Range(-240f, 240f),
                    Scale = PieceSize * Random.Range(0.8f, 1.15f) / width
                };
                pieceRenderers[i].sprite = sprite;
            }
            startTime = Time.unscaledTime;
            Update();
        }

        private void Update()
        {
            if (startTime < 0f)
                return;
            float t = Time.unscaledTime - startTime;
            float k = t / Duration;
            if (k >= 1f)
            {
                ChangeBricks(float.MaxValue);
                startTime = -1f;
                Hide();
                return;
            }
            // 처음엔 빠르게 퍼지다가 위쪽에서 살짝 느려져.
            float radius = Mathf.Lerp(StartRadius, endRadius, Mathf.Lerp(k, EffectMath.EaseOut(k), 0.5f));
            float alpha = Mathf.Clamp01(t / FadeIn) * (k < FadeFrom ? 1f : 1f - (k - FadeFrom) / (1f - FadeFrom));
            ChangeBricks(radius);

            float size = (radius + WaveLine.y) * 2f + 0.1f;
            wave.transform.position = new Vector3(origin.x, origin.y, 0f);
            wave.transform.localScale = new Vector3(size, size, 1f);
            if (material != null)
            {
                material.SetVector(WaveId, new Vector4(origin.x, origin.y, radius, alpha));
                wave.enabled = true;
            }
            for (int i = 0; i < PieceCount; i++)
            {
                Piece piece = pieces[i];
                float r = radius + piece.Offset;
                SpriteRenderer renderer = pieceRenderers[i];
                renderer.enabled = renderer.sprite != null;
                renderer.transform.position = new Vector3(origin.x + Mathf.Cos(piece.Angle) * r, origin.y + Mathf.Sin(piece.Angle) * r, 0f);
                renderer.transform.rotation = Quaternion.Euler(0f, 0f, piece.Spin * t);
                renderer.transform.localScale = new Vector3(piece.Scale, piece.Scale, 1f);
                renderer.color = new Color(1f, 1f, 1f, 0.9f * alpha);
            }
        }

        // 물결 반지름 안에 들어온 벽돌을 새 속성으로 바꿔. 이미 바뀐 벽돌은 그대로야.
        private void ChangeBricks(float radius)
        {
            for (int row = 0; row < board.Rows; row++)
                for (int column = 0; column < board.Columns; column++)
                {
                    Brick brick = board.GetBrick(row, column);
                    if (brick != null && brick.Element != target &&
                        Vector2.Distance(brick.transform.position, origin) <= radius)
                        brick.SetElement(target, ChangeFlash);
                }
        }

        private void Hide()
        {
            wave.enabled = false;
            for (int i = 0; i < PieceCount; i++)
                pieceRenderers[i].enabled = false;
        }

        private SpriteRenderer CreateRenderer(string objectName, Material rendererMaterial, int layerId, int order)
        {
            SpriteRenderer renderer = new GameObject(objectName).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false);
            renderer.sharedMaterial = rendererMaterial;
            renderer.sortingLayerID = layerId;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }
    }
}
