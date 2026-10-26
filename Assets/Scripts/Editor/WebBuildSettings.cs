using UnityEditor;
using UnityEngine;

namespace ElementalBreaker.Editor
{
    // 웹 빌드 설정을 수업(GitHub Pages 배포) 기준에 맞춰.
    // - 웹 템플릿: Assets/WebGLTemplates/ElementalBreaker (탭 이름, 드래그할 때 페이지가 움직이지 않게 막기)
    //   템플릿이 아직 Unity 기본(Default)일 때만 한 번 바꿔. 나중에 Player Settings에서 직접 고른 값은 건드리지 않아.
    // - 압축: 메뉴 "Web Build: Gzip 압축 켜기"로 Gzip + Decompression Fallback을 켜.
    //   GitHub Pages는 압축 파일에 맞는 헤더를 붙이지 않지만, Fallback을 켜면 브라우저에서 직접 풀어서 그대로 돌아가.
    //   게임 내용은 그대로이고 폰에서 처음 받는 크기만 크게 줄어.
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
            AssetDatabase.SaveAssets();
            Debug.Log("웹 빌드 템플릿을 ElementalBreaker로 바꿨어. Player Settings > Web에서 확인할 수 있어.");
        }

        [MenuItem("Elemental Breaker/Web Build: Gzip 압축 켜기")]
        private static void EnableCompression()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            AssetDatabase.SaveAssets();
            Debug.Log("웹 빌드 압축을 켰어: Gzip + Decompression Fallback. 빌드하기 전에 docs/Build 안의 예전 파일을 지우고 빌드해.");
        }
    }
}
