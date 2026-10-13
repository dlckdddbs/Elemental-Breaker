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
    public static class ElementVerification
    {
        private const string Key = "ElementalBreaker.ElementVerification";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        static ElementVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying)
                EditorApplication.update += WaitForStart;
        }

        [MenuItem("Tools/Elemental Breaker/Verify Elements (Play Mode)")]
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
                Debug.Log("ELEMENT VERIFICATION PASSED");
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
            for (int a = 0; a < 4; a++)
            for (int b = 0; b < 4; b++)
            {
                int expected = (a == 1 && b == 3) || (a == 3 && b == 2) || (a == 2 && b == 1) ? 2 : 1;
                Check(ElementRules.Damage((ElementType)a, (ElementType)b) == expected, "Damage table");
                Check(ElementRules.GetBallSprite((ElementType)a) != null, "Ball sprite");
                Check(ElementRules.GetBrickSprite() != null, "Brick sprite");
                Check(ElementRules.GetIconSprite((ElementType)b) != null, "Icon sprite");
            }
            var balls = (List<Ball>)launcher.GetType().GetField("balls", Private).GetValue(launcher);
            var origin = (Transform)launcher.GetType().GetField("launchPoint", Private).GetValue(launcher);
            var template = (Brick)board.GetType().GetField("brickTemplate", Private).GetValue(board);
            Brick target = UnityEngine.Object.Instantiate(template);
            target.Spawn(board, 0, 0, 100, Vector2.one, ElementType.Water);
            int originalCount = launcher.BallCount;
            int completed = launcher.CompletedTurnCount;
            Call(launcher, "BeginTurn", Vector2.up);
            balls[0].PrepareTurn(origin.position, ElementType.Fire);
            balls[0].Launch(Vector2.up);
            Call(balls[0], "HitBrick", target);
            Check(balls[0].IsFlying && balls[0].ReverseHitCount == 1, "Premature retirement");
            Call(balls[0], "HitBrick", target);
            Check(!balls[0].IsFlying && target.Health == 98, "Second reverse hit");
            Check(launcher.IsTurnActive && launcher.CompletedTurnCount == completed, "Pending balls ended turn");
            float firstX = origin.position.x + 0.4f;
            Call(launcher, "LaunchNextBall");
            balls[1].GetComponent<Rigidbody2D>().position = new Vector2(firstX, origin.position.y);
            balls[1].ReturnToLaunch();
            Check(Mathf.Abs(origin.position.x - firstX) < 0.001f, "First surviving landing");
            launcher.AddBall();
            Check(launcher.BallCount == originalCount + 1 && balls.Count == originalCount, "Deferred acquisition");
            ElementType currentElement = launcher.TurnElement;
            launcher.ReserveNextElement(ElementType.Fire);
            launcher.ReserveNextElement(ElementType.Water);
            Check(launcher.ReservedElement == ElementType.Water && launcher.TurnElement == currentElement,
                "Last pickup must reserve without changing current turn");
            while (launcher.PendingBallCount > 0)
                Call(launcher, "LaunchNextBall");
            foreach (Ball ball in balls)
                ball.ReturnToLaunch();
            Call(board, "AdvanceDescent", 1f);
            Check(launcher.TurnElement == ElementType.Water && !launcher.ReservedElement.HasValue,
                "Reservation must apply once on the next turn");
            Check(balls[0].GetComponent<SpriteRenderer>().enabled && balls[0].ReverseHitCount == 0, "Next turn revival");
            Call(launcher, "BeginTurn", Vector2.up);
            Check(balls.Count == originalCount + 1, "Acquired ball missing");
            while (launcher.PendingBallCount > 0)
                Call(launcher, "LaunchNextBall");
            foreach (Ball ball in balls)
            {
                ball.PrepareTurn(origin.position, ElementType.Fire);
                ball.Launch(Vector2.up);
                Call(ball, "HitBrick", target);
                Call(ball, "HitBrick", target);
                Call(ball, "HitBrick", target);
            }
            Check(!launcher.IsTurnActive && launcher.FlyingBallCount == 0, "All retired turn stuck");
            Check(Mathf.Abs(origin.position.x - firstX) < 0.001f, "All retired changed launch position");
            Check(launcher.BallCount == originalCount + 1, "Retirement lost owned balls");
            Call(board, "AdvanceDescent", 1f);
            foreach (Ball ball in balls)
                Check(ball.GetComponent<SpriteRenderer>().enabled && ball.Element == launcher.TurnElement &&
                    ball.ReverseHitCount == 0, "Revival or shared element");
            // 발사 순서보다 실제 도착 순서가 우선인지 확인해.
            Call(launcher, "BeginTurn", Vector2.up);
            while (launcher.PendingBallCount > 0)
                Call(launcher, "LaunchNextBall");
            Ball last = balls[balls.Count - 1];
            last.GetComponent<Rigidbody2D>().position = new Vector2(firstX + 0.3f, origin.position.y);
            last.ReturnToLaunch();
            foreach (Ball ball in balls)
                ball.ReturnToLaunch();
            Check(Mathf.Abs(origin.position.x - firstX - 0.3f) < 0.001f, "Launch order overrode arrival order");
            UnityEngine.Object.Destroy(target.gameObject);
        }

        private static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Private).Invoke(target, args);

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
