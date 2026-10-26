using DG.Tweening;
using UnityEngine;

namespace ElementalBreaker
{
    // 폭탄을 들고 공이 날아가는 동안, 폭탄이 터질 맨 앞 공에 조준 고리를 붙여 따라다녀.
    // 그 공 주변 3x3에 벽돌이 있으면 주황 고리가 빛나고, 없으면 회색 점선으로 흐려져서 지금 누르면 취소된다는 걸 보여 줘.
    public sealed class BombTargetMarker : MonoBehaviour
    {
        private enum State { Hidden, Idle, Ready }

        private static readonly Color ReadyColor = new Color32(0xFF, 0x8A, 0x3D, 0xFF);
        private static readonly Color IdleColor = new Color32(0xC8, 0xD2, 0xE6, 0x8C);
        private static readonly Color GlowColor = new Color32(0xFF, 0x8C, 0x3C, 0xFF);
        // 빛이 숨 쉬듯 밝아졌다 어두워져. 한 번 오가는 시간(초)과 가장 어두울 때·밝을 때 투명도야.
        private const float PulseTime = 1f;
        private const float GlowMinAlpha = 0.2f;
        private const float GlowMaxAlpha = 0.45f;
        // 주황으로 켜지거나 맨 앞 공이 바뀌면 톡 커졌다 돌아와.
        private const float PopScale = 0.25f;
        private const float PopTime = 0.25f;

        private BrickBoard board;
        private BallLauncher launcher;
        private SpriteRenderer glow;
        private SpriteRenderer ring;
        private Sprite readySprite;
        private Sprite idleSprite;
        private State shown = State.Hidden;
        private Ball shownBall;

        // 빛(baseOrder)과 고리(+1)를 그려.
        public void Initialize(BrickBoard owner, BallLauncher ballLauncher, Material material, int sortingLayerId, int baseOrder)
        {
            board = owner;
            launcher = ballLauncher;
            readySprite = ElementRules.LoadEffectSprite("bomb_target");
            idleSprite = ElementRules.LoadEffectSprite("bomb_target_idle");
            glow = CreateRenderer("Glow", material, sortingLayerId, baseOrder);
            glow.sprite = ElementRules.LoadEffectSprite("bomb_target_glow");
            ring = CreateRenderer("Ring", material, sortingLayerId, baseOrder + 1);
            glow.enabled = ring.enabled = false;
        }

        private void LateUpdate()
        {
            if (board == null || launcher == null)
                return;
            // 폭탄을 지금 쓸 수 있을 때만 보여. 터지면 아이템 칸이 비어서 바로 사라져.
            Ball front = board.Items.Held == ItemType.Bomb && board.CanUseHeldItem ? launcher.FrontBall : null;
            if (front == null)
            {
                SetState(State.Hidden, null);
                return;
            }
            Vector3 position = front.transform.position;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            SetState(board.HasBricksInBombRange(position) ? State.Ready : State.Idle, front);
            if (shown == State.Ready)
            {
                float t = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / PulseTime);
                glow.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, Mathf.Lerp(GlowMinAlpha, GlowMaxAlpha, t));
            }
        }

        private void SetState(State state, Ball front)
        {
            bool newTarget = front != shownBall;
            shownBall = front;
            if (state == shown && !(newTarget && state == State.Ready))
                return;
            bool pop = state == State.Ready;
            shown = state;
            ring.enabled = state != State.Hidden;
            glow.enabled = state == State.Ready;
            transform.DOKill(true);
            transform.localScale = Vector3.one;
            if (state == State.Hidden)
                return;
            ring.sprite = state == State.Ready ? readySprite : idleSprite;
            ring.color = state == State.Ready ? ReadyColor : IdleColor;
            if (pop)
                transform.DOPunchScale(new Vector3(PopScale, PopScale, 0f), PopTime, 6, 0.5f).SetUpdate(true);
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

        private void OnDisable()
        {
            transform.DOKill(true);
        }
    }
}
