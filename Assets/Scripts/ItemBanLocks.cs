using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker
{
    // 아이템 금지 디버프 턴이야. 아래 띠 세 버튼(아이템 칸, 공 속성, 상점) 위에 자물쇠를 덮어.
    // 자물쇠를 누르면 그 버튼이 흔들리고 "이번 턴은 사용할 수 없어요" 말풍선이 그 버튼을 가리키며 잠깐 떠.
    public sealed class ItemBanLocks : MonoBehaviour
    {
        private const float ShakeDistance = 6f;
        private const float ShakeTime = 0.3f;
        // 말풍선은 살짝 올라오며 나타나서 잠깐 머물다 사라져. 화면 양 끝에서 이만큼 안쪽에 머물러.
        private const float ToastRise = 8f;
        private const float ToastInTime = 0.15f;
        private const float ToastHoldTime = 1.3f;
        private const float ToastOutTime = 0.3f;
        private const float ToastMargin = 16f;

        [SerializeField] private BrickBoard board;
        [Tooltip("아이템 칸, 공 속성, 상점 버튼 위 자물쇠야. 각 버튼의 자식이고, 누를 수 있어.")]
        [SerializeField] private Button[] locks = new Button[3];
        [SerializeField] private CanvasGroup toast;
        [Tooltip("말풍선 꼬리야. 누른 버튼 가운데를 가리키게 옮겨.")]
        [SerializeField] private RectTransform toastTail;

        private RectTransform bar;
        private bool shown;
        private float toastY;

        private void Awake()
        {
            bar = (RectTransform)transform;
            for (int i = 0; i < locks.Length; i++)
            {
                if (locks[i] == null)
                    continue;
                Button target = locks[i];
                target.onClick.AddListener(() => Deny(target));
                target.gameObject.SetActive(false);
            }
            if (toast != null)
            {
                toastY = ((RectTransform)toast.transform).anchoredPosition.y;
                toast.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            bool banned = board != null && board.ItemsBanned && !board.IsGameOver;
            if (banned == shown)
                return;
            shown = banned;
            foreach (Button lockButton in locks)
                if (lockButton != null)
                    lockButton.gameObject.SetActive(banned);
            if (!banned && toast != null)
            {
                toast.DOKill();
                toast.gameObject.SetActive(false);
            }
        }

        private void Deny(Button lockButton)
        {
            GameAudio.Play(SoundCue.Denied);
            RectTransform button = lockButton.transform.parent as RectTransform;
            if (button == null)
                return;
            button.DOKill(true);
            button.DOPunchAnchorPos(new Vector2(ShakeDistance, 0f), ShakeTime, 14, 0.6f).SetUpdate(true);
            ShowToast(button);
        }

        private void ShowToast(RectTransform button)
        {
            if (toast == null)
                return;
            // 말풍선은 아래 띠 왼쪽 아래 기준이야. 누른 버튼 가운데 아래에 꼬리가 오게 하고, 화면 밖으로 나가지 않게 당겨.
            RectTransform rect = (RectTransform)toast.transform;
            float buttonX = bar.InverseTransformPoint(button.position).x + bar.rect.width * bar.pivot.x;
            float width = rect.rect.width;
            float left = Mathf.Clamp(buttonX - width * 0.5f, ToastMargin, bar.rect.width - ToastMargin - width);
            if (toastTail != null)
                toastTail.anchoredPosition = new Vector2(buttonX - left, toastTail.anchoredPosition.y);
            Vector2 position = new Vector2(left, toastY);

            toast.DOKill();
            toast.gameObject.SetActive(true);
            toast.alpha = 0f;
            rect.anchoredPosition = position + new Vector2(0f, -ToastRise);
            DOTween.Sequence().SetTarget(toast).SetUpdate(true)
                .Append(toast.DOFade(1f, ToastInTime))
                .Join(rect.DOAnchorPos(position, ToastInTime).SetEase(Ease.OutQuad))
                .AppendInterval(ToastHoldTime)
                .Append(toast.DOFade(0f, ToastOutTime))
                .OnComplete(() => toast.gameObject.SetActive(false));
        }

        private void OnDisable()
        {
            if (toast != null)
                toast.DOKill();
        }
    }
}
