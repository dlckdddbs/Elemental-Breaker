using UnityEngine;

namespace ElementalBreaker
{
    public enum ElementType { Normal, Fire, Water, Grass }

    // 상성과 속성 이미지를 한곳에서 관리해.
    public static class ElementRules
    {
        private static readonly string[] Names = { "normal", "fire", "water", "grass" };
        private static Sprite[] ballSprites;
        private static Sprite[] brickSprites;

        public static ElementType RandomElement() => (ElementType)Random.Range(0, 4);

        public static bool HasAdvantage(ElementType attacker, ElementType defender) =>
            (attacker == ElementType.Fire && defender == ElementType.Grass) ||
            (attacker == ElementType.Grass && defender == ElementType.Water) ||
            (attacker == ElementType.Water && defender == ElementType.Fire);

        public static int Damage(ElementType attacker, ElementType defender) =>
            HasAdvantage(attacker, defender) ? 2 : 1;

        public static Sprite GetSprite(ElementType element, bool isBall)
        {
            if (ballSprites == null)
            {
                ballSprites = new Sprite[4];
                brickSprites = new Sprite[4];
                for (int i = 0; i < 4; i++)
                {
                    ballSprites[i] = LoadSprite("png/ball_" + Names[i]);
                    brickSprites[i] = LoadSprite("png/brick_" + Names[i]);
                }
            }
            return isBall ? ballSprites[(int)element] : brickSprites[(int)element];
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                // Multiple로 가져온 이미지도 첫 스프라이트를 사용해.
                Sprite[] sprites = Resources.LoadAll<Sprite>(path);
                if (sprites.Length > 0)
                    sprite = sprites[0];
            }
            if (sprite == null)
                Debug.LogWarning("속성 Sprite를 찾을 수 없어: Resources/" + path);
            return sprite;
        }
    }
}
