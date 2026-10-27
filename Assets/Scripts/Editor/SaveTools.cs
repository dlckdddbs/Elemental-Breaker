using UnityEditor;
using UnityEngine;

namespace ElementalBreaker.Editor
{
    // 이어하기 테스트용 메뉴야. 에디터에서도 웹 빌드와 같은 PlayerPrefs에 저장해.
    public static class SaveTools
    {
        [MenuItem("Elemental Breaker/Save/Delete Save (저장 지우기)")]
        private static void DeleteSave()
        {
            GameSave.Delete();
            Debug.Log("저장을 지웠어. 로비의 이어하기가 '기록 없음'이 돼.");
        }

        [MenuItem("Elemental Breaker/Save/Show Save (저장 보기)")]
        private static void ShowSave()
        {
            if (GameSave.TryLoad(out SaveData data))
                Debug.Log("저장된 판 (" + (SavePhase)data.phase + ", " + data.turn + "턴):\n" + JsonUtility.ToJson(data, true));
            else
                Debug.Log("저장된 판이 없어.");
        }

        // 실행 중에 눌러. 지금 판을 저장 형식으로 바꿨다가 다시 읽어도 같은지, 이 게임판에 다시 놓을 수 있는지 확인해.
        [MenuItem("Elemental Breaker/Save/Check Save (저장 검사)")]
        private static void CheckSave()
        {
            BrickBoard board = Object.FindFirstObjectByType<BrickBoard>();
            if (!Application.isPlaying || board == null)
            {
                EditorUtility.DisplayDialog("Save", "게임 씬을 실행한 상태에서 눌러.", "확인");
                return;
            }
            if (board.VerifySave(out string json))
                Debug.Log("저장 검사 통과:\n" + json);
            else
                Debug.LogWarning("저장 검사 실패: 다시 읽은 판이 다르거나 값이 이상해.\n" + json);
        }
    }
}
