using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace ElementalBreaker.Editor
{
    // Art/Casual 이미지를 아틀라스 두 개로 묶어. 없을 때만 한 번 만들고, 이미 있으면 건드리지 않아.
    // 원본 이미지는 Resources 밖에 두고 아틀라스만 Resources에 둬서, 빌드에 텍스처가 두 번 들어가지 않게 해.
    [InitializeOnLoad]
    public static class CasualAtlasBuilder
    {
        private const string AtlasFolder = "Assets/Resources/Atlases";
        // 벽돌, 공, 아이콘처럼 모서리가 또렷해야 하는 이미지는 무압축으로 묶어.
        private const string SpriteAtlasPath = AtlasFolder + "/CasualSprites.spriteatlasv2";
        private const string SpriteSource = "Assets/Art/Casual/Sprites";
        // 오라와 버스트는 잠깐 보이는 부드러운 효과라 압축해서 메모리를 줄여.
        private const string EffectAtlasPath = AtlasFolder + "/CasualEffects.spriteatlasv2";
        private const string EffectSource = "Assets/Art/Casual/Effects";

        static CasualAtlasBuilder()
        {
            EditorApplication.delayCall += EnsureAtlases;
        }

        [MenuItem("Elemental Breaker/Rebuild Casual Atlases")]
        private static void Rebuild()
        {
            AssetDatabase.DeleteAsset(SpriteAtlasPath);
            AssetDatabase.DeleteAsset(EffectAtlasPath);
            EnsureAtlases();
        }

        private static void EnsureAtlases()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            Ensure(SpriteAtlasPath, SpriteSource, false);
            Ensure(EffectAtlasPath, EffectSource, true);
        }

        private static void Ensure(string path, string source, bool compressed)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || !AssetDatabase.IsValidFolder(source))
                return;
            if (!AssetDatabase.IsValidFolder(AtlasFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "Atlases");

            SpriteAtlasAsset atlas = new SpriteAtlasAsset();
            atlas.Add(new Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(source) });
            SpriteAtlasAsset.Save(atlas, path);
            AssetDatabase.ImportAsset(path);

            SpriteAtlasImporter importer = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
            if (importer == null)
            {
                Debug.LogError("아틀라스를 만들지 못했어: " + path);
                return;
            }
            // 9-slice 벽돌 때문에 회전과 타이트 패킹은 꺼.
            importer.packingSettings = new SpriteAtlasPackingSettings
            {
                blockOffset = 1,
                padding = 4,
                enableRotation = false,
                enableTightPacking = false,
                enableAlphaDilation = true
            };
            importer.textureSettings = new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = false,
                sRGB = true,
                filterMode = FilterMode.Bilinear
            };
            TextureImporterPlatformSettings platform = importer.GetPlatformSettings("DefaultTexturePlatform");
            platform.maxTextureSize = 2048;
            platform.textureCompression = compressed
                ? TextureImporterCompression.CompressedHQ
                : TextureImporterCompression.Uncompressed;
            importer.SetPlatformSettings(platform);
            importer.includeInBuild = true;
            importer.SaveAndReimport();
            Debug.Log("아틀라스를 만들었어: " + path);
        }
    }
}
