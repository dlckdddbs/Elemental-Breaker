using UnityEditor;
using UnityEngine;

namespace ElementalBreaker.Editor
{
    // Art/Casual 스프라이트를 PPU 200, 무압축으로 가져오고 벽돌 바탕에 9-slice 테두리를 걸어.
    // 실제 게임에서는 아틀라스로 묶여서 쓰이고, 아틀라스 압축 설정이 최종 결과를 정해.
    public sealed class CasualSpriteImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Art/Casual/";
        // Unity spriteBorder 순서는 (왼쪽, 아래, 오른쪽, 위)야.
        private static readonly Vector4 BrickBorder = new Vector4(24f, 32f, 24f, 24f);
        // HUD 알약 배지는 양 끝 반원만 고정하고 가운데를 늘려서 이름 길이에 맞춰.
        private static readonly Vector4 PillBorder = new Vector4(50f, 0f, 50f, 0f);
        // 아이템 칸, 상점 버튼, 상점 카드에 쓰는 둥근 사각 판이야. 아래쪽은 테두리 두께만큼 더 잡아.
        private static readonly Vector4 PanelBorder = new Vector4(40f, 46f, 40f, 40f);
        // 오라 이미지(288x160)에서 공 중심이 있는 위치야. 공과 같은 점을 기준으로 회전해.
        private static readonly Vector2 AuraPivot = new Vector2(208f / 288f, 0.5f);
        // 폭탄 범위 모서리(64x64)의 꺾이는 점(9,9)이 범위 귀퉁이에 정확히 오도록 기준점을 거기에 둬.
        private static readonly Vector2 CornerPivot = new Vector2(9f / 64f, 1f - 9f / 64f);

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder))
                return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 200f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            bool isAura = assetPath.Contains("/aura_");
            // 오라와 버스트는 잠깐 보이는 효과라 압축해도 티가 안 나서 메모리를 줄여.
            importer.textureCompression = isAura || assetPath.Contains("/fx_burst_")
                ? TextureImporterCompression.CompressedHQ
                : TextureImporterCompression.Uncompressed;

            bool isBrick = assetPath.EndsWith("/brick_base.png");
            bool isPill = assetPath.EndsWith("/ui_pill.png");
            bool isPanel = assetPath.EndsWith("/ui_slot.png");
            importer.spriteBorder = isBrick ? BrickBorder : isPill ? PillBorder : isPanel ? PanelBorder : Vector4.zero;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            // 9-slice는 Full Rect 메시여야 가운데가 제대로 늘어나.
            settings.spriteMeshType = isBrick || isPill || isPanel ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            bool isCorner = assetPath.EndsWith("/fx_range_corner.png");
            settings.spriteAlignment = isAura || isCorner ? (int)SpriteAlignment.Custom : (int)SpriteAlignment.Center;
            settings.spritePivot = isAura ? AuraPivot : isCorner ? CornerPivot : new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);
        }
    }
}
