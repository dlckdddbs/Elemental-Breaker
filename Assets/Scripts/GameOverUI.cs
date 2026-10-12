using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElementalBreaker
{
    // 게임 오버 화면을 띄우고 현재 게임 씬을 다시 시작해.
    public sealed class GameOverUI : MonoBehaviour
    {
        [SerializeField] private BrickBoard board;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Button restartButton;
        [SerializeField] private TMP_Text resultLabel;

        private bool restarting;
        private bool configured;

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

        private void ShowGameOver()
        {
            if (restarting)
                return;
            if (resultLabel != null)
                resultLabel.SetText("게임 오버\n도달 턴: {0}", board.TurnNumber);
            restartButton.interactable = true;
            gameOverPanel.SetActive(true);
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
            if (board != null)
                board.GameOver -= ShowGameOver;
            if (restartButton != null)
                restartButton.onClick.RemoveListener(RestartGame);
        }
    }
}
