using TMPro;
using UnityEditor;
using UnityEngine;

namespace ElementalBreaker.Editor
{
    // 한글 UI 폰트(동적 폰트)가 원본 글꼴 파일과의 연결을 잃으면 글자를 하나도 못 그려서 전부 깨져 보여.
    // 스크립트를 다시 불러올 때 연결이 비어 있으면 Jua-Regular.ttf를 다시 이어 줘.
    // 글자 그림이 꽉 차도 새 장을 쓰게 Multi Atlas도 켜 둬. 글자는 화면에 나올 때 다시 채워져.
    public static class KoreanFontRepair
    {
        private const string FontAssetPath = "Assets/Font/Jua/Jua-Regular UI SDF.asset";
        private const string SourceFontPath = "Assets/Font/Jua/Jua-Regular.ttf";

        [InitializeOnLoadMethod]
        private static void OnLoad() => EditorApplication.delayCall += Repair;

        [MenuItem("Elemental Breaker/Repair Korean Font")]
        private static void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (fontAsset == null || sourceFont == null)
                return;
            SerializedObject so = new SerializedObject(fontAsset);
            SerializedProperty source = so.FindProperty("m_SourceFontFile");
            SerializedProperty sourceGuid = so.FindProperty("m_SourceFontFileGUID");
            SerializedProperty population = so.FindProperty("m_AtlasPopulationMode");
            SerializedProperty multiAtlas = so.FindProperty("m_IsMultiAtlasTexturesEnabled");
            if (source == null || sourceGuid == null)
                return;
            bool linked = source.objectReferenceValue == sourceFont && !string.IsNullOrEmpty(sourceGuid.stringValue);
            bool dynamic = population == null || population.intValue == (int)AtlasPopulationMode.Dynamic;
            bool multi = multiAtlas == null || multiAtlas.boolValue;
            if (linked && dynamic && multi)
                return;
            source.objectReferenceValue = sourceFont;
            sourceGuid.stringValue = AssetDatabase.AssetPathToGUID(SourceFontPath);
            if (population != null)
                population.intValue = (int)AtlasPopulationMode.Dynamic;
            if (multiAtlas != null)
                multiAtlas.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            fontAsset.ReadFontAssetDefinition();
            TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, fontAsset);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssetIfDirty(fontAsset);
            Debug.Log("한글 UI 폰트를 원본 글꼴(Jua-Regular.ttf)에 다시 이었어. 글자는 화면에 나올 때 다시 채워져.");
        }
    }
}
