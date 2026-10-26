using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElementalBreaker
{
    // 게임 오버 화면을 띄우고 현재 게임 씬을 다시 시작해. 경계선이 무너지는 연출이 끝난 뒤에 떠.
    // 게임 화면을 어둡게 덮고 가운데 카드에 [게임 오버 / 도달한 턴 / 공 개수와 골드 / 다시 하기]를 보여 줘.
    public sealed class GameOverUI : MonoBehaviour
    {
        // 카드가 살짝 작게 시작해서 톡 커지며 나타나고, 뒤 화면은 그동안 어두워져.
        private const float OpenScale = 0.85f;
        private const float OpenTime = 0.25f;
        private const float DimTime = 0.2f;
        private const float IconGap = 6f;

        [SerializeField] private BrickBoard board;
        [SerializeField] private BallLauncher launcher;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Button restartButton;
        [Tooltip("예전 패널의 결과 글자야. 새 카드에서는 비워 둬.")]
        [SerializeField] private TMP_Text resultLabel;
        [Header("결과 카드")]
        [SerializeField] private Image dim;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text turnLabel;
        [SerializeField] private RectTransform ballIcon;
        [SerializeField] private TMP_Text ballLabel;
        [SerializeField] private RectTransform goldIcon;
        [SerializeField] private TMP_Text goldLabel;
        private float dimAlpha = -1f;

        private bool restarting;
        private bool configured;
        // 경계선이 무너지는 동안 창을 띄우려고 기다리는 중이야.
        private bool waiting;

        private void Awake()
        {
            if (board == null || gameOverPanel == null || restartButton == null)
            {
                Debug.LogError("GameOverUI: Board, Game Over Panel, Restart Button을 연결해.", this);
                enabled = false;
                return;
            }
            // 패널을 숨겨도 이 컴포넌트는 이벤트를 계속 받아야 해.
            if (transform.IsChildOf(gameOverPanel.transform))
            {
                Debug.LogError("GameOverUI는 Game Over Panel 밖의 Canvas나 별도 관리 오브젝트에 붙여.", this);
                enabled = false;
                return;
            }
            configured = true;
            gameOverPanel.SetActive(false);
        }

        private void OnEnable()
        {
            if (!configured)
                return;
            board.GameOver += ShowGameOver;
            restartButton.onClick.AddListener(RestartGame);
            if (board.IsGameOver)
                ShowGameOver();
        }

        // 벽돌이 기준선에 닿으면 위아래 경계선이 무너지는 동안(BoundaryCollapse.Duration) 기다렸다가 창을 띄워.
        private void ShowGameOver()
        {
            if (restarting || waiting)
                return;
            waiting = true;
            DOVirtual.DelayedCall(BoundaryCollapse.Duration, OpenPanel, true).SetTarget(this);
        }

        private void OpenPanel()
        {
            waiting = false;
            if (restarting)
                return;
            GameAudio.Play(SoundCue.GameOver);
            if (resultLabel != null)
                resultLabel.SetText("게임 오버\n도달 턴: {0}", board.TurnNumber);
            if (turnLabel != null)
                turnLabel.SetText("{0}", board.TurnNumber);
            if (ballLabel != null && launcher != null)
            {
                ballLabel.SetText("공 {0}", launcher.ActiveBallCount);
                LayoutPair(ballIcon, ballLabel);
            }
            if (goldLabel != null)
            {
                goldLabel.SetText("{0}", board.Gold.Amount);
                LayoutPair(goldIcon, goldLabel);
            }
            restartButton.interactable = true;
            gameOverPanel.SetActive(true);
            PlayOpen();
        }

        // [아이콘 글자]를 이어 붙이고 묶음을 칸 가운데에 둬. 둘 다 왼쪽 가운데 기준이야.
        private static void LayoutPair(RectTransform icon, TMP_Text label)
        {
            if (icon == null)
                return;
            float width = label.preferredWidth;
            float x = -(icon.sizeDelta.x + IconGap + width) * 0.5f;
            icon.anchoredPosition = new Vector2(x, icon.anchoredPosition.y);
            label.rectTransform.anchoredPosition = new Vector2(x + icon.sizeDelta.x + IconGap, label.rectTransform.anchoredPosition.y);
        }

        // 히트 스톱 같은 시간 변화와 상관없이 실제 시간으로 움직여.
        private void PlayOpen()
        {
            if (dim != null)
            {
                if (dimAlpha < 0f)
                    dimAlpha = dim.color.a;
                dim.DOKill();
                Color color = dim.color;
                dim.color = new Color(color.r, color.g, color.b, 0f);
                dim.DOFade(dimAlpha, DimTime).SetUpdate(true);
            }
            if (card != null)
            {
                card.DOKill();
                card.localScale = new Vector3(OpenScale, OpenScale, 1f);
                card.DOScale(1f, OpenTime).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        public void RestartGame()
        {
            if (!configured || restarting || !board.IsGameOver)
                return;

            Scene scene = board.gameObject.scene;
            if (scene.buildIndex < 0 || !Application.CanStreamedLevelBeLoaded(scene.buildIndex))
            {
                Debug.LogError("재시작할 게임 씬을 Build Profiles의 Scene List에 추가해.", this);
                return;
            }

            GameAudio.Play(SoundCue.Click);
            restarting = true;
            restartButton.interactable = false;
            try
            {
                // 씬을 다시 읽어서 공, 보드, 턴과 발사 위치를 초기화해.
                AsyncOperation operation = SceneManager.LoadSceneAsync(scene.buildIndex, LoadSceneMode.Single);
                if (operation == null)
                    throw new InvalidOperationException("게임 씬을 다시 불러올 수 없어.");
            }
            catch (Exception exception)
            {
                restarting = false;
                restartButton.interactable = true;
                Debug.LogException(exception, this);
            }
        }

        private void OnDisable()
        {
            DOTween.Kill(this);
            waiting = false;
            if (dim != null)
                dim.DOKill();
            if (card != null)
                card.DOKill();
            if (board != null)
                board.GameOver -= ShowGameOver;
            if (restartButton != null)
                restartButton.onClick.RemoveListener(RestartGame);
        }
    }
}
