using UnityEditor;
using UnityEngine;

namespace ElementalBreaker.Editor
{
    // 원본은 보관하고 게임에 들어가는 점 텍스처만 작게 가져와.
    public sealed class BoardVisualImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/BoardVisuals/"))
                return;
            TextureImporter importer = (TextureImporter)assetImporter;
            bool dot = assetPath.EndsWith("aim_dot.png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.maxTextureSize = dot ? 128 : 2048;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = dot;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
        }
    }
}
