using System.Text;
using UnityEditor;
using UnityEngine;

namespace ElementalBreaker.Editor
{
    // 웹 빌드 데이터를 줄이는 설정 두 가지야. 코드는 안 바꾸고 화면도 거의 그대로야.
    // 1) 남색 배경(841x1870, 흐릿한 그라데이션)을 최대 1024로 줄여. 게임과 로비 모두 비율에 맞춰 화면을 채워서 크기와 자리는 그대로야.
    // 2) 2D 렌더러의 후처리(Post-processing)를 꺼. 카메라 후처리를 이미 안 써서, 빌드에 들어가던 필름 그레인 텍스처와 후처리 셰이더만 빠져.
    // 문제가 보이면 "되돌리기" 메뉴로 원래대로 돌려.
    public static class BuildSizeSetup
    {
        private const string BackgroundPath = "Assets/Resources/BoardVisuals/navy_background.png";
        private const int SmallBackgroundSize = 1024;
        private const int OriginalBackgroundSize = 2048;
        // 되돌릴 때 다시 연결하는 URP 기본 후처리 데이터야.
        private const string PostProcessDataGuid = "41439944d30ece34e96484bdb6645b55";

        [MenuItem("Elemental Breaker/Build Size/Reduce (용량 줄이기)")]
        private static void Reduce()
        {
            StringBuilder changes = new StringBuilder();
            SetBackgroundSize(SmallBackgroundSize, changes);
            SetPostProcessData(null, changes);
            Debug.Log(changes.Length > 0 ? "빌드 용량 설정을 바꿨어. 빌드 전에 게임과 로비 배경을 한 번 확인해.\n" + changes : "이미 줄인 상태야.");
        }

        [MenuItem("Elemental Breaker/Build Size/Restore (용량 설정 되돌리기)")]
        private static void Restore()
        {
            StringBuilder changes = new StringBuilder();
            SetBackgroundSize(OriginalBackgroundSize, changes);
            Object data = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(PostProcessDataGuid));
            if (data == null)
                Debug.LogWarning("URP 기본 후처리 데이터를 못 찾았어. Renderer2D의 Post-processing을 Inspector에서 켜.");
            else
                SetPostProcessData(data, changes);
            Debug.Log(changes.Length > 0 ? "빌드 용량 설정을 원래대로 돌렸어.\n" + changes : "이미 원래 설정이야.");
        }

        private static void SetBackgroundSize(int size, StringBuilder changes)
        {
            if (!(AssetImporter.GetAtPath(BackgroundPath) is TextureImporter importer))
            {
                Debug.LogWarning("배경 이미지를 못 찾았어: " + BackgroundPath);
                return;
            }
            if (importer.maxTextureSize == size)
                return;
            changes.AppendLine("배경 최대 크기: " + importer.maxTextureSize + " → " + size);
            importer.maxTextureSize = size;
            importer.SaveAndReimport();
        }

        // 프로젝트의 2D 렌더러마다 후처리 데이터를 바꿔. Inspector의 Post-processing 체크와 같아(빈 값이면 꺼짐).
        private static void SetPostProcessData(Object data, StringBuilder changes)
        {
            // 패키지 안의 견본 렌더러는 바꿀 수 없어서 프로젝트 Assets 폴더만 찾아.
            string[] guids = AssetDatabase.FindAssets("t:Renderer2DData", new[] { "Assets" });
            if (guids.Length == 0)
                Debug.LogWarning("2D 렌더러 설정(Renderer2DData)을 못 찾았어.");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object renderer = AssetDatabase.LoadMainAssetAtPath(path);
                if (renderer == null)
                    continue;
                SerializedObject serialized = new SerializedObject(renderer);
                SerializedProperty property = serialized.FindProperty("m_PostProcessData");
                if (property == null || property.objectReferenceValue == data)
                    continue;
                changes.AppendLine(path + " 후처리: " + (data == null ? "끔" : "켬"));
                property.objectReferenceValue = data;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(renderer);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
