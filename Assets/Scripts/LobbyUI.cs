using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElementalBreaker
{
    // 첫 화면(로비)이야. 이어하기와 새 게임 두 버튼이 있어. 웹에서는 탭을 닫을 수 없어서 나가기는 없어.
    // 저장된 판이 있으면 이어하기를, 없으면 새 게임을 파란 버튼으로 강조해.
    // 저장된 판이 있는데 새 게임을 누르면 기록이 지워진다고 한 번 확인해.
    public sealed class LobbyUI : MonoBehaviour
    {
        // 확인 창 카드가 살짝 작게 시작해서 톡 커지며 나타나. 게임 오버 카드와 같아.
        private const float OpenScale = 0.85f;
        private const float OpenTime = 0.25f;

        [Tooltip("이어하기와 새 게임이 여는 게임 씬 이름이야. Build Profiles의 Scene List에 들어 있어야 해.")]
        [SerializeField] private string gameSceneName = "SampleScene";
        [SerializeField] private LobbyButton continueButton;
        [SerializeField] private LobbyButton newGameButton;
        [Tooltip("이어하기 버튼 오른쪽 글자야. 저장된 턴이나 '기록 없음'을 보여 줘.")]
        [SerializeField] private TMP_Text continueInfo;
        [Header("새 게임 확인 창 (저장된 판이 있을 때)")]
        [SerializeField] private GameObject confirmPanel;
        [Tooltip("확인 창 바깥(어두운 막)이야. 누르면 취소야.")]
        [SerializeField] private Button confirmDim;
        [SerializeField] private RectTransform confirmCard;
        [SerializeField] private TMP_Text confirmMessage;
        [SerializeField] private LobbyButton confirmCancel;
        [SerializeField] private LobbyButton confirmAccept;

        private SaveData saved;
        private bool loading;

        private void Awake()
        {
            // 일시 정지나 히트 스톱 중에 넘어와도 로비와 다음 게임이 보통 속도로 돌게 해.
            GamePause.Clear();
            AddListener(continueButton, ContinueGame);
            AddListener(newGameButton, RequestNewGame);
            AddListener(confirmCancel, CloseConfirm);
            AddListener(confirmAccept, StartNewGame);
            if (confirmDim != null)
                confirmDim.onClick.AddListener(CloseConfirm);
            if (confirmPanel != null)
                confirmPanel.SetActive(false);
        }

        private void Start()
        {
            bool hasSave = GameSave.TryLoad(out saved);
            if (continueButton != null)
                continueButton.SetStyle(hasSave ? LobbyButtonStyle.Strong : LobbyButtonStyle.Disabled);
            if (newGameButton != null)
                newGameButton.SetStyle(hasSave ? LobbyButtonStyle.Calm : LobbyButtonStyle.Strong);
            if (continueInfo != null)
            {
                if (hasSave)
                    continueInfo.SetText("{0}턴", saved.turn);
                else
                    continueInfo.SetText("기록 없음");
            }
            if (confirmCancel != null)
                confirmCancel.SetStyle(LobbyButtonStyle.Calm);
            if (confirmAccept != null)
                confirmAccept.SetStyle(LobbyButtonStyle.Danger);
        }

        private void ContinueGame()
        {
            if (loading || saved == null)
                return;
            GameSave.Pending = saved;
            if (!LoadGame())
                GameSave.Pending = null;
        }

        private void RequestNewGame()
        {
            if (loading)
                return;
            if (saved == null || confirmPanel == null)
            {
                StartNewGame();
                return;
            }
            GameAudio.Play(SoundCue.Click);
            if (confirmMessage != null)
                confirmMessage.SetText("저장된 {0}턴 기록이 지워져요.\n지운 기록은 되돌릴 수 없어요.", saved.turn);
            confirmPanel.SetActive(true);
            if (confirmCard != null)
            {
                confirmCard.DOKill();
                confirmCard.localScale = new Vector3(OpenScale, OpenScale, 1f);
                confirmCard.DOScale(1f, OpenTime).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        private void CloseConfirm()
        {
            if (loading || confirmPanel == null || !confirmPanel.activeSelf)
                return;
            GameAudio.Play(SoundCue.Click);
            if (confirmCard != null)
                confirmCard.DOKill();
            confirmPanel.SetActive(false);
        }

        // 새 게임은 저장된 판을 지우고 시작해. 첫 턴이 시작되면 새 판으로 다시 저장돼.
        private void StartNewGame()
        {
            if (loading)
                return;
            GameSave.Pending = null;
            if (!CanLoadGame())
                return;
            GameSave.Delete();
            saved = null;
            LoadGame();
        }

        private bool CanLoadGame()
        {
            if (Application.CanStreamedLevelBeLoaded(gameSceneName))
                return true;
            Debug.LogError($"'{gameSceneName}' 씬을 Build Profiles의 Scene List에 추가해.", this);
            return false;
        }

        private bool LoadGame()
        {
            if (!CanLoadGame())
                return false;
            loading = true;
            SetInteractable(continueButton, false);
            SetInteractable(newGameButton, false);
            SetInteractable(confirmCancel, false);
            SetInteractable(confirmAccept, false);
            GameAudio.Play(SoundCue.Click);
            SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Single);
            return true;
        }

        private static void AddListener(LobbyButton target, UnityEngine.Events.UnityAction action)
        {
            if (target != null && target.Button != null)
                target.Button.onClick.AddListener(action);
        }

        private static void SetInteractable(LobbyButton target, bool interactable)
        {
            if (target != null && target.Button != null)
                target.Button.interactable = interactable;
        }

        private void OnDestroy()
        {
            if (confirmCard != null)
                confirmCard.DOKill();
            if (continueButton != null && continueButton.Button != null)
                continueButton.Button.onClick.RemoveListener(ContinueGame);
            if (newGameButton != null && newGameButton.Button != null)
                newGameButton.Button.onClick.RemoveListener(RequestNewGame);
            if (confirmCancel != null && confirmCancel.Button != null)
                confirmCancel.Button.onClick.RemoveListener(CloseConfirm);
            if (confirmAccept != null && confirmAccept.Button != null)
                confirmAccept.Button.onClick.RemoveListener(StartNewGame);
            if (confirmDim != null)
                confirmDim.onClick.RemoveListener(CloseConfirm);
        }
    }
}
