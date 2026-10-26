using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ElementalBreaker.Editor
{
    // 별도 플레이 세션에서 턴과 보드의 경계 조건을 확인해.
    [InitializeOnLoad]
    public static class BoardVerification
    {
        private const string Key = "ElementalBreaker.BoardVerification";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        static BoardVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying)
                EditorApplication.update += WaitForStart;
        }

        [MenuItem("Tools/Elemental Breaker/Verify Board (Play Mode)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이를 종료한 뒤 검증해.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            {
                EditorApplication.update -= WaitForStart;
                EditorApplication.update += WaitForStart;
            }
        }

        private static void WaitForStart()
        {
            BrickBoard board = UnityEngine.Object.FindFirstObjectByType<BrickBoard>();
            BallLauncher launcher = UnityEngine.Object.FindFirstObjectByType<BallLauncher>();
            if (board == null || launcher == null || launcher.BallCount == 0 || launcher.IsInputBlocked)
                return;
            EditorApplication.update -= WaitForStart;
            SessionState.SetBool(Key, false);
            try
            {
                Verify(board, launcher);
                Debug.Log("BOARD VERIFICATION PASSED: pending balls, one descent per turn, cleared cell, pooling, sizing, first return, row 9 game over, launch lock.");
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
                else
                    EditorApplication.ExitPlaymode();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                else
                    EditorApplication.ExitPlaymode();
            }
        }

        private static void Verify(BrickBoard board, BallLauncher launcher)
        {
            Set(board, "earlySpawnProbability", 1f);
            Set(board, "lateSpawnProbability", 1f);
            for (int column = 0; column < 7; column++)
            {
                Brick existing = board.GetBrick(0, column);
                if (existing != null)
                    existing.TakeDamage(int.MaxValue);
            }
            Call(board, "SpawnTopRow", true);
            var balls = (List<Ball>)launcher.GetType().GetField("balls", Private).GetValue(launcher);
            Check(balls.Count > 1, "검증용 공이 두 개 이상 필요해.");
            int occupied = 0;
            Brick tracked = null;
            for (int column = 0; column < 7; column++)
            {
                Brick brick = board.GetBrick(0, column);
                if (brick == null)
                    continue;
                occupied++;
                tracked = brick;
            }
            Check(occupied == 6, "한 줄 최대 6개 생성 실패");
            int trackedColumn = tracked.Column;
            Vector3 originalPosition = tracked.transform.position;
            Vector3 spriteSize = tracked.GetComponent<SpriteRenderer>().bounds.size;
            Check(Mathf.Abs(spriteSize.x - (4.6f / 7f - 0.08f)) < 0.001f &&
                Mathf.Abs(spriteSize.y - 0.62f) < 0.001f, "Sprite 크기 자동 계산 실패");
            Check((tracked.GetComponent<BoxCollider2D>().size - (Vector2)spriteSize).sqrMagnitude < 0.0001f,
                "Collider 크기 자동 계산 실패");

            int endEvents = 0;
            launcher.TurnEnded += () => endEvents++;
            Call(launcher, "BeginTurn", Vector2.up);
            balls[0].GetComponent<Rigidbody2D>().position = new Vector2(0.75f, -3.25f);
            balls[0].ReturnToLaunch();
            Check(launcher.IsTurnActive && launcher.PendingBallCount == balls.Count - 1 &&
                launcher.FlyingBallCount == 0 && endEvents == 0 && board.DescentCount == 0,
                "발사 대기 중 조기 턴 종료");
            while (launcher.PendingBallCount > 0)
            {
                Call(launcher, "LaunchNextBall");
                int index = balls.Count - launcher.PendingBallCount - 1;
                balls[index].ReturnToLaunch();
            }
            Check(endEvents == 1 && board.DescentCount == 1 && board.IsProcessing && launcher.IsInputBlocked,
                "하강 시작 또는 발사 잠금 실패");
            Call(launcher, "BeginTurn", Vector2.up);
            Check(!launcher.IsTurnActive, "하강 중 발사 허용");
            Call(board, "OnTurnEnded");
            Check(board.DescentCount == 1 && board.GetBrick(1, trackedColumn) == tracked && tracked.Row == 1,
                "중복 하강 또는 배열 이동 실패");
            Call(board, "AdvanceDescent", 1f);
            Check(Mathf.Abs(tracked.transform.position.y - originalPosition.y + 0.7f) < 0.001f,
                "정확히 한 칸 하강 실패");
            Check(!launcher.IsInputBlocked, "처리 완료 후 잠금 해제 실패");
            for (int column = 0; column < 7; column++)
            {
                Brick nextRowBrick = board.GetBrick(0, column);
                if (nextRowBrick != null)
                    Check(nextRowBrick.Health == 4, "턴별 체력 증가 실패");
            }
            Call(board, "OnTurnEnded");
            Check(board.DescentCount == 1, "처리 완료 후 중복 하강");
            foreach (Ball ball in balls)
                Check(Mathf.Abs(ball.transform.position.x - 0.75f) < 0.001f, "첫 회수 위치 집결 실패");
            tracked.TakeDamage(int.MaxValue);
            Check(board.GetBrick(1, trackedColumn) == null && !tracked.gameObject.activeSelf,
                "파괴된 배열 칸 또는 풀 반환 실패");
            tracked.TakeDamage(1);
            Check(board.GetBrick(1, trackedColumn) == null, "중복 파괴 처리 실패");
            int gameOvers = 0;
            board.GameOver += () => gameOvers++;
            for (int turn = 0; turn < 9; turn++)
            {
                Call(launcher, "BeginTurn", Vector2.up);
                Check(Mathf.Abs(balls[0].transform.position.x - 0.75f) < 0.001f,
                    "다음 턴 발사 위치 실패");
                while (launcher.PendingBallCount > 0)
                    Call(launcher, "LaunchNextBall");
                foreach (Ball ball in balls)
                    ball.ReturnToLaunch();
                int before = board.DescentCount;
                Call(board, "OnTurnEnded");
                Check(board.DescentCount == before, "턴마다 한 번 하강 실패");
                Call(board, "AdvanceDescent", 1f);
                if (board.IsGameOver)
                    break;
            }
            Check(board.IsGameOver && gameOvers == 1 && board.DescentCount == 9 && launcher.IsInputBlocked,
                "9행 도달 게임 오버 실패");
            for (int column = 0; column < 7; column++)
                Check(board.GetBrick(0, column) == null, "게임 오버 후 새 줄 생성");
            Check(tracked.gameObject.activeSelf && tracked.Health > 0, "풀 벽돌 재사용 실패");
            Call(launcher, "BeginTurn", Vector2.up);
            Call(board, "OnTurnEnded");
            Check(!launcher.IsTurnActive && board.DescentCount == 9 && gameOvers == 1,
                "게임 오버 후 진행 재개");
        }

        private static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Private).Invoke(target, args);

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Private).SetValue(target, value);

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
