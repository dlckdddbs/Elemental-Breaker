using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElementalBreaker
{
    // 게임 중 일시 정지 창이야. 상단 띠의 톱니 버튼으로 열고, [계속하기 / 메인으로] 두 버튼만 있어.
    // 열려 있는 동안은 게임 시간이 멈춰. 판은 행동할 때마다 이미 저장돼 있어서 메인으로 가도 이어하기로 돌아올 수 있어.
    // 공이 날아가는 중에 나가면 그 샷 결과는 사라져서 그때만 안내 문구를 바꿔 보여 줘.
    public sealed class PausePanel : MonoBehaviour
    {
        // 카드가 살짝 작게 시작해서 톡 커지며 나타나. 로비 확인 창, 게임 오버 카드와 같아.
        private const float OpenScale = 0.85f;
        private const float OpenTime = 0.25f;
        private static readonly Color SavedColor = new Color32(0xD6, 0xE0, 0xFF, 0x99);
        private static readonly Color WarningColor = new Color32(0xFF, 0xB4, 0xA8, 0xFF);

        [SerializeField] private BrickBoard board;
        [SerializeField] private BallLauncher launcher;
        [Tooltip("메인으로를 누르면 여는 로비 씬 이름이야. Build Profiles의 Scene List에 들어 있어야 해.")]
        [SerializeField] private string lobbySceneName = "Lobby";
        [SerializeField] private Button gearButton;
        [SerializeField] private GameObject panel;
        [Tooltip("창 바깥(어두운 막)이야. 누르면 계속하기와 같아.")]
        [SerializeField] private Button dim;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text message;
        [SerializeField] private LobbyButton resumeButton;
        [SerializeField] private LobbyButton mainButton;

        private bool leaving;

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (gearButton != null)
                gearButton.onClick.AddListener(Open);
            if (dim != null)
                dim.onClick.AddListener(Close);
            if (resumeButton != null && resumeButton.Button != null)
                resumeButton.Button.onClick.AddListener(Close);
            if (mainButton != null && mainButton.Button != null)
                mainButton.Button.onClick.AddListener(GoToMain);
            if (resumeButton != null)
                resumeButton.SetStyle(LobbyButtonStyle.Strong);
            if (mainButton != null)
                mainButton.SetStyle(LobbyButtonStyle.Calm);
            if (panel != null)
                panel.SetActive(false);
        }

        private void LateUpdate()
        {
            // 게임이 끝나면 다시 하기 창이 뜨니까 톱니 버튼을 숨기고, 열려 있던 창도 닫아.
            bool playing = board == null || !board.IsGameOver;
            if (gearButton != null && gearButton.gameObject.activeSelf != playing)
                gearButton.gameObject.SetActive(playing);
            if (!playing && IsOpen && !leaving)
                Close();
        }

        private void Open()
        {
            if (leaving || IsOpen || panel == null || (board != null && board.IsGameOver))
                return;
            GamePause.Pause();
            GameAudio.Play(SoundCue.Click);
            bool flying = launcher != null && launcher.IsTurnActive;
            if (message != null)
            {
                message.SetText(flying ? "지금 나가면 이번 샷 결과는 사라져요." : "진행 상황은 자동으로 저장돼요.");
                message.color = flying ? WarningColor : SavedColor;
            }
            panel.SetActive(true);
            if (card != null)
            {
                card.DOKill();
                card.localScale = new Vector3(OpenScale, OpenScale, 1f);
                card.DOScale(1f, OpenTime).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        private void Close()
        {
            if (leaving || !IsOpen)
                return;
            GameAudio.Play(SoundCue.Click);
            if (card != null)
                card.DOKill();
            panel.SetActive(false);
            GamePause.Resume();
        }

        // 마지막으로 저장한 순간부터 이어할 수 있어서 따로 저장하지 않고 바로 로비로 가.
        private void GoToMain()
        {
            if (leaving)
                return;
            if (!Application.CanStreamedLevelBeLoaded(lobbySceneName))
            {
                Debug.LogError($"'{lobbySceneName}' 씬을 Build Profiles의 Scene List에 추가해.", this);
                return;
            }
            leaving = true;
            if (resumeButton != null && resumeButton.Button != null)
                resumeButton.Button.interactable = false;
            if (mainButton != null && mainButton.Button != null)
                mainButton.Button.interactable = false;
            GameAudio.Play(SoundCue.Click);
            GamePause.Clear();
            SceneManager.LoadSceneAsync(lobbySceneName, LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            // 멈춘 채로 씬을 떠나도 다음 씬이 멈춰 있지 않게 해.
            if (GamePause.IsPaused)
                GamePause.Clear();
            if (card != null)
                card.DOKill();
            if (gearButton != null)
                gearButton.onClick.RemoveListener(Open);
            if (dim != null)
                dim.onClick.RemoveListener(Close);
            if (resumeButton != null && resumeButton.Button != null)
                resumeButton.Button.onClick.RemoveListener(Close);
            if (mainButton != null && mainButton.Button != null)
                mainButton.Button.onClick.RemoveListener(GoToMain);
        }
    }
}
