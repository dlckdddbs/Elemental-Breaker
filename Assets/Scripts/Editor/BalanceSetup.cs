using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ElementalBreaker.Editor
{
    // 밸런스 숫자는 게임 씬에 저장돼서 코드 기본값만 바꾸면 게임에 반영되지 않아.
    // 열린 게임 씬의 게임판(BrickBoard)과 상점(ShopPanel)에 정해 둔 값을 넣어. Ctrl+Z로 되돌릴 수 있고, 씬 저장은 직접 해.
    public static class BalanceSetup
    {
        [MenuItem("Elemental Breaker/Apply Balance (밸런스 적용)")]
        private static void Apply()
        {
            BrickBoard board = Object.FindFirstObjectByType<BrickBoard>(FindObjectsInactive.Include);
            ShopPanel shop = Object.FindFirstObjectByType<ShopPanel>(FindObjectsInactive.Include);
            ElementSelectButton elementSelect = Object.FindFirstObjectByType<ElementSelectButton>(FindObjectsInactive.Include);
            if (board == null || shop == null || elementSelect == null)
            {
                EditorUtility.DisplayDialog("Balance", "게임 씬(BrickBoard, ShopPanel, ElementSelectButton)을 열고 눌러.", "확인");
                return;
            }
            StringBuilder changes = new StringBuilder();

            SerializedObject boardData = new SerializedObject(board);
            SetInt(boardData, "startingGold", 200, "시작 골드", changes);
            SetFloat(boardData, "earlySpawnProbability", 0.6f, "1~20턴 벽돌 확률", changes);
            SetFloat(boardData, "lateSpawnProbability", 0.7f, "21~29턴 벽돌 확률", changes);
            SetFloat(boardData, "finalSpawnProbability", 0.8f, "30턴부터 벽돌 확률", changes);
            SetInt(boardData, "lateSpawnTurn", 20, "확률이 70%로 바뀌는 턴(이 턴 다음부터, 20이면 21턴부터)", changes);
            SetInt(boardData, "finalSpawnTurn", 29, "확률이 80%로 바뀌는 턴(이 턴 다음부터, 29면 30턴부터)", changes);
            SetInt(boardData, "breakGoldBonusInterval", 40, "벽돌 골드 +1 간격(턴)", changes);
            SetFloat(boardData, "eventSettings.goldPerTurn", 5f, "10턴 보상 (턴 × 이 값)", changes);
            SetFloat(boardData, "eventSettings.healthBoostMultiplier", 2f, "체력 디버프 배율", changes);
            boardData.ApplyModifiedProperties();

            SerializedObject shopData = new SerializedObject(shop);
            SetInt(shopData, "iceBasePrice", 150, "얼음 시작 가격", changes);
            SetInt(shopData, "brickChangeBasePrice", 75, "벽돌 속성 변경 기본 가격", changes);
            shopData.ApplyModifiedProperties();

            SerializedObject elementData = new SerializedObject(elementSelect);
            SetInt(elementData, "basePrice", 20, "공 속성 변경 기본 가격", changes);
            SetInt(elementData, "basePricePerTenTurns", 5, "공 속성 변경 10턴마다 오르는 값", changes);
            elementData.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
            Debug.Log(changes.Length > 0
                ? "밸런스를 적용했어. Ctrl+S로 씬을 저장해.\n" + changes
                : "이미 같은 밸런스 값이야. 바뀐 게 없어.");
        }

        private static void SetInt(SerializedObject data, string path, int value, string label, StringBuilder changes)
        {
            SerializedProperty property = data.FindProperty(path);
            if (property == null)
            {
                Debug.LogWarning("밸런스 항목을 못 찾았어: " + path);
                return;
            }
            if (property.intValue == value)
                return;
            changes.AppendLine(label + ": " + property.intValue + " → " + value);
            property.intValue = value;
        }

        private static void SetFloat(SerializedObject data, string path, float value, string label, StringBuilder changes)
        {
            SerializedProperty property = data.FindProperty(path);
            if (property == null)
            {
                Debug.LogWarning("밸런스 항목을 못 찾았어: " + path);
                return;
            }
            if (Mathf.Approximately(property.floatValue, value))
                return;
            changes.AppendLine(label + ": " + property.floatValue + " → " + value);
            property.floatValue = value;
        }
    }
}
