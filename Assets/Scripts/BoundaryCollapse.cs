using System.Collections.Generic;
using UnityEngine;

namespace ElementalBreaker
{
    // 게임 오버 연출이야. 벽돌이 기준선(회수선)에 닿으면 회수선과 위쪽 경계선이 조각나서 무너져.
    // 1) 금: 선이 제자리에서 조각으로 갈라지며 굵어지고 밝게 번쩍이며 떨려.
    // 2) 무너짐: 조각들이 조금씩 다른 때에 튀어 올랐다가 빙글 돌며 떨어지고 흐려져.
    // 다 무너진 뒤(Duration) GameOverUI가 다시 하기 창을 띄워. 실제 시간으로 움직여.
    public sealed class BoundaryCollapse : MonoBehaviour
    {
        private const float CrackTime = 0.18f;
        private const float FallTime = 0.75f;
        // 다시 하기 창은 선이 다 무너진 뒤에 떠.
        public const float Duration = CrackTime + FallTime;
        private const int PiecesPerLine = 8;
        // 조각 사이 틈(조각 폭 대비)과, 금 갈 때 굵어지는 배율, 떨리는 거리(월드)야.
        private const float PieceFill = 0.9f;
        private const float CrackThicken = 2.5f;
        private const float CrackJitter = 0.02f;
        private const float CrackFlash = 0.6f;
        // 조각마다 출발이 이만큼까지 늦어서 한꺼번에 떨어지지 않아.
        private const float MaxDelay = 0.12f;
        private const float Gravity = 14f;

        private struct Piece
        {
            public SpriteRenderer Renderer;
            public Vector2 Start, Velocity;
            public Vector3 Scale;
            public Color Color;
            public float Spin, Delay, Phase;
        }

        private BoardPresentation presentation;
        private readonly List<Piece> pieces = new List<Piece>(PiecesPerLine * 2);
        private float startTime = -1f;

        public void Initialize(BoardPresentation owner) => presentation = owner;

        public void Play()
        {
            if (startTime >= 0f || presentation == null)
                return;
            Break(presentation.ReturnLine);
            Break(presentation.TopBoundaryLine);
            startTime = Time.unscaledTime;
            Update();
        }

        // 선을 같은 그림의 조각들로 바꾸고 원래 선은 숨겨.
        private void Break(SpriteRenderer line)
        {
            if (line == null || !line.enabled || line.sprite == null)
                return;
            Bounds bounds = line.bounds;
            Vector2 spriteSize = line.sprite.bounds.size;
            float width = bounds.size.x / PiecesPerLine;
            for (int i = 0; i < PiecesPerLine; i++)
            {
                SpriteRenderer renderer = new GameObject("Piece").AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(transform, false);
                renderer.sprite = line.sprite;
                renderer.sharedMaterial = line.sharedMaterial;
                renderer.sortingLayerID = line.sortingLayerID;
                renderer.sortingOrder = line.sortingOrder;
                pieces.Add(new Piece
                {
                    Renderer = renderer,
                    Start = new Vector2(bounds.min.x + (i + 0.5f) * width, bounds.center.y),
                    Velocity = new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(0.4f, 1.6f)),
                    Scale = new Vector3(width * PieceFill / spriteSize.x, bounds.size.y / spriteSize.y, 1f),
                    Color = line.color,
                    Spin = Random.Range(-260f, 260f),
                    Delay = Random.Range(0f, MaxDelay),
                    Phase = Random.Range(0f, Mathf.PI * 2f)
                });
            }
            line.enabled = false;
        }

        private void Update()
        {
            if (startTime < 0f)
                return;
            float t = Time.unscaledTime - startTime;
            bool cracking = t < CrackTime;
            float crack = EffectMath.EaseOut(t / CrackTime);
            for (int i = 0; i < pieces.Count; i++)
            {
                Piece piece = pieces[i];
                Transform pieceTransform = piece.Renderer.transform;
                Vector3 scale = piece.Scale;
                scale.y *= Mathf.Lerp(1f, CrackThicken, crack);
                pieceTransform.localScale = scale;
                if (cracking)
                {
                    // 제자리에서 갈라지며 떨리고 밝게 번쩍여.
                    Vector2 jitter = new Vector2(Mathf.Sin(t * 80f + piece.Phase), Mathf.Cos(t * 70f + piece.Phase)) * (CrackJitter * crack);
                    pieceTransform.position = piece.Start + jitter;
                    pieceTransform.rotation = Quaternion.identity;
                    piece.Renderer.color = Color.Lerp(piece.Color, Color.white, CrackFlash * crack);
                    continue;
                }
                float fall = Mathf.Max(0f, t - CrackTime - piece.Delay);
                pieceTransform.position = piece.Start + piece.Velocity * fall + Vector2.down * (0.5f * Gravity * fall * fall);
                pieceTransform.rotation = Quaternion.Euler(0f, 0f, piece.Spin * fall);
                Color color = Color.Lerp(piece.Color, Color.white, CrackFlash * (1f - Mathf.Clamp01(fall / 0.15f)));
                color.a *= 1f - Mathf.Clamp01(fall / (FallTime - piece.Delay));
                piece.Renderer.color = color;
            }
            if (t < Duration)
                return;
            // 다 무너지면 조각을 숨겨. 다시 하기는 씬을 새로 불러와서 선이 원래대로 돌아와.
            foreach (Piece piece in pieces)
                piece.Renderer.enabled = false;
            startTime = -1f;
        }
    }
}
