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
        // 상점 목록 뒤의 둥근 판이야. 둥근 모서리(반지름 32)와 테두리만 고정하고 가운데를 늘려.
        private static readonly Vector4 FrameBorder = new Vector4(34f, 34f, 34f, 34f);
        // 상단 띠 배경(1440x543)이야. 구분선이 있는 아래 443px은 고정하고, 단순한 위쪽 100px만 늘려서 긴 폰에서 띠가 위로 길어져도 구분선 자리가 그대로야.
        private static readonly Vector4 TopBandBorder = new Vector4(0f, 443f, 0f, 0f);
        // 얼음 상태 표시판은 양 끝 둥근 부분만 고정하고 가운데를 늘려.
        private static readonly Vector4 ChipBorder = new Vector4(30f, 0f, 30f, 0f);
        // 보드 서리 테두리는 모서리 100px을 고정하고 가장자리 무늬(56px)를 이어 붙여(Tiled).
        private static readonly Vector4 FrostFrameBorder = new Vector4(100f, 100f, 100f, 100f);
        // 턴 보상 배너 바탕(1306x408)이야. 둥근 모서리(반지름 약 103px)와 금색 테두리는 고정하고 가운데만 늘려서 배너 비율이 달라도 모서리가 안 찌그러져.
        private static readonly Vector4 RewardBannerBorder = new Vector4(112f, 112f, 112f, 112f);
        // 오라 이미지(288x160)에서 공 중심이 있는 위치야. 공과 같은 점을 기준으로 회전해.
        private static readonly Vector2 AuraPivot = new Vector2(208f / 288f, 0.5f);

        // 규칙을 바꾸면 이 숫자를 올려. 이미 들어온 이미지도 새 규칙으로 다시 가져와.
        public override uint GetVersion() => 5;

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
            // 오라, 버스트, 폭탄 폭발은 잠깐 보이는 효과라 압축해도 티가 안 나서 메모리를 줄여.
            importer.textureCompression = isAura || assetPath.Contains("/fx_burst_") || assetPath.Contains("/fx_blast_")
                ? TextureImporterCompression.CompressedHQ
                : TextureImporterCompression.Uncompressed;

            bool isBrick = assetPath.EndsWith("/brick_base.png");
            bool isPill = assetPath.EndsWith("/ui_pill.png");
            bool isPanel = assetPath.EndsWith("/ui_slot.png");
            bool isFrame = assetPath.EndsWith("/ui_panel.png");
            bool isTopBand = assetPath.EndsWith("/ui_top_band.png");
            bool isChip = assetPath.EndsWith("/ui_freeze_chip.png");
            bool isFrostFrame = assetPath.EndsWith("/fx_frost_frame.png");
            // 경고 배너 바탕은 보상 배너 그림의 색만 바꾼 거라 크기와 테두리가 같아.
            bool isRewardBanner = assetPath.EndsWith("/ui_reward_banner.png") || assetPath.EndsWith("/ui_debuff_banner.png");
            // 벽돌 서리막은 셰이더가 이미지 칸 안의 위치로 퍼지는 순서를 계산해서, 잘리지 않은 사각형 메시가 필요해.
            bool isFrostFilm = assetPath.EndsWith("/fx_frost_film.png");
            importer.spriteBorder = isBrick ? BrickBorder : isPill ? PillBorder : isPanel ? PanelBorder : isFrame ? FrameBorder
                : isTopBand ? TopBandBorder : isChip ? ChipBorder : isFrostFrame ? FrostFrameBorder
                : isRewardBanner ? RewardBannerBorder : Vector4.zero;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            // 9-slice는 Full Rect 메시여야 가운데가 제대로 늘어나.
            settings.spriteMeshType = isBrick || isPill || isPanel || isFrame || isTopBand || isChip || isFrostFrame || isFrostFilm ||
                isRewardBanner ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            settings.spriteAlignment = isAura ? (int)SpriteAlignment.Custom : (int)SpriteAlignment.Center;
            settings.spritePivot = isAura ? AuraPivot : new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);
        }
    }
}
