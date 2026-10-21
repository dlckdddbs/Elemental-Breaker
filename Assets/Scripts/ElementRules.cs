using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace ElementalBreaker
{
    public enum ElementType { Normal, Fire, Water, Grass }

    // 상성과 속성 이미지를 한곳에서 관리해.
    public static class ElementRules
    {
        public const int AuraFrameCount = 4;

        private static readonly string[] Names = { "normal", "fire", "water", "grass" };
        // 회색 벽돌 바탕(밝기 225)에 곱해져서 화면에서 속성 색으로 보이는 틴트야.
        private static readonly Color32[] BrickTints =
        {
            new Color32(0xCC, 0xDA, 0xE3, 0xFF),
            new Color32(0xFF, 0x6E, 0x30, 0xFF),
            new Color32(0x28, 0xBC, 0xFF, 0xFF),
            new Color32(0x64, 0xDE, 0x3B, 0xFF)
        };
        // 공은 벽돌과 한 톤 다르게 해서 같은 속성 벽돌 옆에서도 묻히지 않게 해.
        private static readonly Color32[] BallTints =
        {
            new Color32(0xCC, 0xDA, 0xE3, 0xFF),
            new Color32(0xFF, 0x54, 0x2C, 0xFF),
            new Color32(0x20, 0x92, 0xFF, 0xFF),
            new Color32(0x3E, 0xD4, 0x48, 0xFF)
        };
        private static Sprite brickSprite;
        private static Sprite[] ballSprites;
        private static Sprite[] iconSprites;
        private static Sprite[] pickupSprites;
        // 상단 HUD용 큰 속성 아이콘이야. 벽돌용 아이콘보다 커서 따로 둬.
        private static Sprite[] uiIconSprites;
        // 속성별 오라 프레임이야. 일반은 오라가 없어서 null이야.
        private static Sprite[][] auraFrames;

        // 원본 이미지는 Resources 밖에 있고, 게임은 Resources의 아틀라스 두 개에서 꺼내 써.
        private const string SpriteAtlasPath = "Atlases/CasualSprites";
        private const string EffectAtlasPath = "Atlases/CasualEffects";
        private static SpriteAtlas spriteAtlas;
        private static SpriteAtlas effectAtlas;
        private static bool atlasesLoaded;
        // GetSprite는 부를 때마다 복제본을 새로 만들어서, 이름별로 한 번만 꺼내 두고 같이 써.
        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
#if UNITY_EDITOR
        private static readonly string[] EditorSourceFolders = { "Assets/Art/Casual/Sprites/", "Assets/Art/Casual/Effects/" };
#endif

        public static Sprite GetBallSprite(ElementType element)
        {
            LoadSprites();
            return ballSprites[(int)element];
        }

        public static Sprite GetBrickSprite()
        {
            LoadSprites();
            return brickSprite;
        }

        public static Sprite GetIconSprite(ElementType element)
        {
            LoadSprites();
            return iconSprites[(int)element];
        }

        public static Sprite GetUiIconSprite(ElementType element)
        {
            LoadSprites();
            return uiIconSprites[(int)element];
        }

        public static Sprite GetPickupSprite(ElementType element)
        {
            LoadSprites();
            return pickupSprites[(int)element];
        }

        public static Sprite[] GetAuraFrames(ElementType element)
        {
            LoadSprites();
            return auraFrames[(int)element];
        }

        // 이펙트용 작은 이미지야. 같은 이미지를 여러 곳에서 불러도 캐시에서 하나를 같이 써.
        public static Sprite LoadEffectSprite(string name) => LoadSprite("fx_" + name);

        public static Color GetBrickTint(ElementType element) => BrickTints[(int)element];

        // fade가 1에 가까울수록 흰색 쪽으로 연해져.
        public static Color GetBallTint(ElementType element, float fade = 0f) =>
            Color.Lerp(BallTints[(int)element], Color.white, fade);

        public static ElementType RandomElement() => (ElementType)Random.Range(0, 4);

        public static bool HasAdvantage(ElementType attacker, ElementType defender) =>
            (attacker == ElementType.Fire && defender == ElementType.Grass) ||
            (attacker == ElementType.Grass && defender == ElementType.Water) ||
            (attacker == ElementType.Water && defender == ElementType.Fire);

        public static int Damage(ElementType attacker, ElementType defender) =>
            HasAdvantage(attacker, defender) ? 2 : 1;

        // attacker가 이기는 속성이야. 일반은 이기는 속성이 없어서 일반을 돌려줘.
        public static ElementType WeakTo(ElementType attacker)
        {
            switch (attacker)
            {
                case ElementType.Fire: return ElementType.Grass;
                case ElementType.Grass: return ElementType.Water;
                case ElementType.Water: return ElementType.Fire;
                default: return ElementType.Normal;
            }
        }

        // 폭탄 아이콘 색이야. 속성 턴은 공 색, 일반 턴은 진한 회색 폭탄이야.
        private static readonly Color32 NormalBombTint = new Color32(0x6B, 0x73, 0x90, 0xFF);
        public static Color GetBombTint(ElementType element) =>
            element == ElementType.Normal ? (Color)NormalBombTint : GetBallTint(element);

        private static void LoadSprites()
        {
            // 벽돌은 회색 바탕 한 장을 틴트로 공유하고, 공은 문양만 다른 회색 이미지에 틴트를 입혀. 전부 한 번만 읽어.
            if (iconSprites != null)
                return;
            brickSprite = LoadSprite("brick_base");
            ballSprites = new Sprite[Names.Length];
            iconSprites = new Sprite[Names.Length];
            pickupSprites = new Sprite[Names.Length];
            uiIconSprites = new Sprite[Names.Length];
            auraFrames = new Sprite[Names.Length][];
            for (int i = 0; i < Names.Length; i++)
            {
                ballSprites[i] = LoadSprite("ball_" + Names[i]);
                iconSprites[i] = LoadSprite("icon_" + Names[i]);
                pickupSprites[i] = LoadSprite("pickup_" + Names[i]);
                uiIconSprites[i] = LoadSprite("ui_icon_" + Names[i]);
                if (i == (int)ElementType.Normal)
                    continue;
                auraFrames[i] = new Sprite[AuraFrameCount];
                for (int frame = 0; frame < AuraFrameCount; frame++)
                    auraFrames[i][frame] = LoadSprite("aura_" + Names[i] + "_" + frame);
            }
        }

        private static Sprite LoadSprite(string name)
        {
            if (spriteCache.TryGetValue(name, out Sprite cached))
                return cached;
            if (!atlasesLoaded)
            {
                spriteAtlas = Resources.Load<SpriteAtlas>(SpriteAtlasPath);
                effectAtlas = Resources.Load<SpriteAtlas>(EffectAtlasPath);
                atlasesLoaded = true;
            }

            Sprite sprite = spriteAtlas != null ? spriteAtlas.GetSprite(name) : null;
            if (sprite == null && effectAtlas != null)
                sprite = effectAtlas.GetSprite(name);
#if UNITY_EDITOR
            // 아틀라스가 아직 만들어지기 전이면 에디터에서는 원본 이미지를 바로 읽어.
            for (int i = 0; sprite == null && i < EditorSourceFolders.Length; i++)
                sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(EditorSourceFolders[i] + name + ".png");
#endif
            if (sprite == null)
                Debug.LogWarning("아틀라스에서 Sprite를 찾을 수 없어: " + name);
            spriteCache[name] = sprite;
            return sprite;
        }
    }
}
