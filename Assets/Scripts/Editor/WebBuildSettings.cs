using UnityEditor;
using UnityEngine;

namespace ElementalBreaker.Editor
{
    // 웹 빌드 설정을 수업(GitHub Pages 배포) 기준에 맞춰 한 번만 바꿔.
    // - 웹 템플릿: Assets/WebGLTemplates/ElementalBreaker (탭 이름, 드래그할 때 페이지가 움직이지 않게 막기)
    // - 압축: 끔, Decompression Fallback: 끔. GitHub Pages는 압축 파일에 맞는 헤더를 붙이지 않아서 압축 없이 올려.
    // 템플릿이 아직 Unity 기본(Default)일 때만 바꿔. 나중에 Player Settings에서 직접 고른 값은 건드리지 않아.
    [InitializeOnLoad]
    internal static class WebBuildSettings
    {
        private const string DefaultTemplate = "APPLICATION:Default";
        private const string TemplateFolder = "Assets/WebGLTemplates/ElementalBreaker";
        private const string Template = "PROJECT:ElementalBreaker";

        static WebBuildSettings() => EditorApplication.delayCall += Apply;

        private static void Apply()
        {
            if (PlayerSettings.WebGL.template != DefaultTemplate || !AssetDatabase.IsValidFolder(TemplateFolder))
                return;
            PlayerSettings.WebGL.template = Template;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            AssetDatabase.SaveAssets();
            Debug.Log("웹 빌드 설정을 바꿨어: 템플릿 ElementalBreaker, 압축 끔(Disabled), Decompression Fallback 끔. " +
                      "Player Settings > Web에서 확인할 수 있어.");
        }
    }
}
