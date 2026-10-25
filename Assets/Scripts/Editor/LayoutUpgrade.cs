using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ElementalBreaker.Editor
{
    // 화면 배치 2판을 지금 열린 씬에 한 번에 적용해. Ctrl+Z 한 번으로 전부 되돌릴 수 있고, 씬 저장은 직접 해.
    // 1) 게임판을 통째로 0.33 아래로 내려서 상단 정보판 자리를 넓혀. 게임판 크기와 공이 날아가는 거리는 그대로야.
    //    옮기는 것: 벽 세 개와 회수 영역(Collider2D가 있는 최상위 오브젝트), 발사 위치(LaunchPoint), 발사기(Launcher), 벽돌 판 가운데(BrickBoard)
    //    위쪽 경계선과 회수선은 BoardPresentation이 벽과 회수 영역을 따라 자동으로 맞춰.
    // 2) 상단 정보판과 아래 띠·상점을 지우고 새 배치로 다시 만들어.
    public static class LayoutUpgrade
    {
        // 화면에서 28px(기준 해상도 480x854)만큼이야.
        private const float BoardShift = 0.33f;
        // 처음 배치의 벽돌 판 가운데 높이야. 이 값일 때만 내려서 두 번 눌러도 두 번 내려가지 않아.
        private const float OriginalBoardY = 0.15f;

        [MenuItem("Elemental Breaker/Apply Layout v2 (게임판 내리기 + 상단·하단 판)")]
        private static void Apply()
        {
            BrickBoard board = Object.FindFirstObjectByType<BrickBoard>();
            BallLauncher launcher = Object.FindFirstObjectByType<BallLauncher>();
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (board == null || launcher == null || canvas == null)
            {
                EditorUtility.DisplayDialog("Layout v2", "씬에서 BrickBoard, BallLauncher, Canvas를 찾지 못했어.", "확인");
                return;
            }
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Layout v2", "플레이를 멈춘 뒤 실행해.", "확인");
                return;
            }
            Undo.SetCurrentGroupName("Apply Layout v2");
            int undoGroup = Undo.GetCurrentGroup();
            string moved = ShiftBoard(board, launcher);
            RebuildUi(canvas);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
            Debug.Log("배치 2판을 적용했어. " + moved + " 상단 정보판과 아래 띠를 새로 만들었어. 확인하고 Ctrl+S로 씬을 저장해.");
        }

        private static string ShiftBoard(BrickBoard board, BallLauncher launcher)
        {
            SerializedObject boardObject = new SerializedObject(board);
            SerializedProperty center = boardObject.FindProperty("boardCenter");
            if (!Mathf.Approximately(center.vector2Value.y, OriginalBoardY))
                return "게임판은 이미 내려가 있어서 그대로 뒀어.";
            center.vector2Value += Vector2.down * BoardShift;
            boardObject.ApplyModifiedProperties();

            List<Transform> targets = new List<Transform>();
            foreach (GameObject root in board.gameObject.scene.GetRootGameObjects())
                if (root.GetComponent<Collider2D>() != null)
                    targets.Add(root.transform);
            if (new SerializedObject(launcher).FindProperty("launchPoint").objectReferenceValue is Transform launchPoint)
                targets.Add(launchPoint);
            targets.Add(launcher.transform);

            StringBuilder names = new StringBuilder("BrickBoard");
            HashSet<Transform> movedSet = new HashSet<Transform>();
            foreach (Transform target in targets)
            {
                // 같은 오브젝트는 한 번만, 목록에 있는 오브젝트의 자식이면 부모와 같이 움직이니 건너뛰어.
                if (!movedSet.Add(target) || HasAncestorIn(target, targets))
                    continue;
                Undo.RecordObject(target, "Apply Layout v2");
                target.position += Vector3.down * BoardShift;
                names.Append(", ").Append(target.name);
            }
            return "게임판을 " + BoardShift + " 내렸어(" + names + ").";
        }

        private static bool HasAncestorIn(Transform target, List<Transform> list)
        {
            for (Transform parent = target.parent; parent != null; parent = parent.parent)
                if (list.Contains(parent))
                    return true;
            return false;
        }

        private static void RebuildUi(Canvas canvas)
        {
            List<string> names = new List<string> { "TopHUD" };
            names.AddRange(BottomBarBuilder.CreatedNames);
            foreach (string name in names)
            {
                Transform old = canvas.transform.Find(name);
                if (old != null)
                    Undo.DestroyObjectImmediate(old.gameObject);
            }
            if (TopHudBuilder.Create())
                BottomBarBuilder.Create();
        }
    }
}
