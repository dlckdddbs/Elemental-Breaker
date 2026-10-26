using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker.Editor
{
    // GameOverUI가 쓰는 예전 게임 오버 패널을 지우고, 새 결과 카드로 다시 만들어 연결해.
    // 화면 전체를 어둡게 덮고(터치도 막아) 가운데 카드에 [게임 오버 / 도달한 턴 / 공·골드 / 다시 하기]를 놓아.
    // Ctrl+Z로 되돌릴 수 있고, 씬 저장은 직접 해.
    public static class GameOverBuilder
    {
        private const string SpriteFolder = "Assets/Art/Casual/Sprites/";
        private const string FontPath = "Assets/Font/Jua/Jua-Regular UI SDF.asset";
        private const string OutlinePath = "Assets/Font/Jua/Jua-Regular UI SDF Outline.mat";
        private const string PanelName = "GameOverPanel";
        // 카드 크기와 위치야(기준 해상도 480x854). 화면 가운데보다 13 위에 놓여.
        private static readonly Vector2 CardSize = new Vector2(388f, 372f);
        private const float CardRaise = 13f;
        private const float PillHeight = 50f;

        private static readonly Color32 DimColor = new Color32(0x05, 0x09, 0x16, 0xA8);
        private static readonly Color32 GoldColor = new Color32(0xFF, 0xE4, 0x8A, 0xFF);
        private static readonly Color32 DividerColor = new Color32(0xA0, 0xBE, 0xFF, 0x40);
        private static readonly Color32 StatsTint = new Color32(0x06, 0x0E, 0x26, 0x96);
        private static readonly Color32 ButtonTint = new Color32(0x3C, 0x66, 0xE6, 0xFF);
        private static readonly Color CaptionColor = new Color(1f, 1f, 1f, 0.67f);
        private static readonly Vector2 Top = new Vector2(0.5f, 1f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);

        [MenuItem("Elemental Breaker/Create Game Over Card")]
        private static void CreateFromMenu() => Create();

        public static bool Create()
        {
            GameOverUI ui = Object.FindFirstObjectByType<GameOverUI>();
            BallLauncher launcher = Object.FindFirstObjectByType<BallLauncher>();
            Canvas canvas = ui != null ? ui.GetComponentInParent<Canvas>() : null;
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Material outline = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
            Sprite cardSprite = Load("ui_result_card"), slot = Load("ui_slot"), pill = Load("ui_pill");
            Sprite ball = Load("ball_normal"), coin = Load("fx_coin");
            if (ui == null || canvas == null || launcher == null || font == null || outline == null ||
                cardSprite == null || slot == null || pill == null || ball == null || coin == null)
            {
                EditorUtility.DisplayDialog("Game Over", "GameOverUI, Canvas, BallLauncher, 폰트, 카드 이미지 중 하나를 찾지 못했어.", "확인");
                return false;
            }

            // 예전 패널은 지워. GameOverUI 자체는 Canvas에 그대로 있어.
            SerializedObject so = new SerializedObject(ui);
            if (so.FindProperty("gameOverPanel").objectReferenceValue is GameObject oldPanel)
                Undo.DestroyObjectImmediate(oldPanel);

            // 화면 전체를 어둡게 덮는 막. 게임과 버튼 터치를 막아.
            Image dim = CreateImage(PanelName, canvas.transform, null, Image.Type.Simple, true);
            dim.color = DimColor;
            RectTransform dimRect = dim.rectTransform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = dimRect.offsetMax = Vector2.zero;
            dimRect.SetAsLastSibling();

            // 가운데 카드
            Image card = CreateImage("Card", dimRect, cardSprite, Image.Type.Simple, true);
            Place(card.rectTransform, Center, Center, new Vector2(0f, CardRaise), CardSize);
            RectTransform c = card.rectTransform;
            Text("Title", c, font, outline, "게임 오버", 38f, Color.white, new Vector2(0f, -50f), new Vector2(300f, 48f));
            Image divider = CreateImage("Divider", c, null, Image.Type.Simple, false);
            divider.color = DividerColor;
            Place(divider.rectTransform, Top, Center, new Vector2(0f, -88f), new Vector2(CardSize.x - 72f, 1f));
            Text("TurnCaption", c, font, font.material, "도달한 턴", 17f, CaptionColor, new Vector2(0f, -120f), new Vector2(200f, 24f));
            TMP_Text turnLabel = Text("Turn", c, font, outline, "1", 72f, GoldColor, new Vector2(0f, -178f), new Vector2(300f, 84f));

            // 공 개수와 골드: 어두운 알약 안에 반씩
            Image stats = CreateImage("Stats", c, pill, Image.Type.Sliced, false);
            stats.color = StatsTint;
            stats.pixelsPerUnitMultiplier = PillHeight / 46f;
            Place(stats.rectTransform, Top, Center, new Vector2(0f, -251f), new Vector2(CardSize.x - 80f, 46f));
            Image statsDivider = CreateImage("Divider", stats.transform, null, Image.Type.Simple, false);
            statsDivider.color = DividerColor;
            Place(statsDivider.rectTransform, Center, Center, Vector2.zero, new Vector2(1f, 26f));
            float quarter = (CardSize.x - 80f) * 0.25f;
            RectTransform ballGroup = Group("Balls", stats.transform, new Vector2(-quarter, 0f));
            Image ballIcon = CreateImage("Icon", ballGroup, ball, Image.Type.Simple, false);
            ballIcon.color = ElementRules.GetBallTint(ElementType.Normal);
            Place(ballIcon.rectTransform, Center, LeftMiddle, new Vector2(-30f, 0f), new Vector2(22f, 22f));
            TMP_Text ballLabel = PairText("Count", ballGroup, font, outline, "공 1", 20f, Color.white, new Vector2(-2f, 0f));
            RectTransform goldGroup = Group("Gold", stats.transform, new Vector2(quarter, 0f));
            Image goldIcon = CreateImage("Icon", goldGroup, coin, Image.Type.Simple, false);
            Place(goldIcon.rectTransform, Center, LeftMiddle, new Vector2(-22f, 0f), new Vector2(24f, 24f));
            TMP_Text goldLabel = PairText("Amount", goldGroup, font, outline, "0", 22f, GoldColor, new Vector2(8f, 0f));

            // 다시 하기 버튼
            Image buttonImage = CreateImage("RestartButton", c, slot, Image.Type.Sliced, true);
            buttonImage.color = ButtonTint;
            Place(buttonImage.rectTransform, Top, Center, new Vector2(0f, -318f), new Vector2(220f, 58f));
            Button restart = buttonImage.gameObject.AddComponent<Button>();
            Navigation navigation = restart.navigation;
            navigation.mode = Navigation.Mode.None;
            restart.navigation = navigation;
            TMP_Text buttonLabel = Text("Label", buttonImage.transform, font, outline, "다시 하기", 24f, Color.white, Vector2.zero, new Vector2(200f, 40f));
            buttonLabel.rectTransform.anchorMin = buttonLabel.rectTransform.anchorMax = Center;
            buttonLabel.rectTransform.anchoredPosition = new Vector2(0f, 3f);

            so.FindProperty("launcher").objectReferenceValue = launcher;
            so.FindProperty("gameOverPanel").objectReferenceValue = dim.gameObject;
            so.FindProperty("restartButton").objectReferenceValue = restart;
            so.FindProperty("resultLabel").objectReferenceValue = null;
            so.FindProperty("dim").objectReferenceValue = dim;
            so.FindProperty("card").objectReferenceValue = card.rectTransform;
            so.FindProperty("turnLabel").objectReferenceValue = turnLabel;
            so.FindProperty("ballIcon").objectReferenceValue = ballIcon.rectTransform;
            so.FindProperty("ballLabel").objectReferenceValue = ballLabel;
            so.FindProperty("goldIcon").objectReferenceValue = goldIcon.rectTransform;
            so.FindProperty("goldLabel").objectReferenceValue = goldLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 실행하면 GameOverUI가 처음에 숨기지만, 편집 화면에서도 가려지지 않게 꺼 둬.
            dim.gameObject.SetActive(false);
            Undo.RegisterCreatedObjectUndo(dim.gameObject, "Create Game Over Card");
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log("게임 오버 카드를 만들었어. 확인하고 Ctrl+S로 씬을 저장해.");
            return true;
        }

        private static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");

        private static RectTransform Group(string name, Transform parent, Vector2 position)
        {
            RectTransform group = CreateRect(name, parent);
            Place(group, Center, Center, position, new Vector2(0f, 30f));
            return group;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Image.Type type, bool raycast)
        {
            Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? type : Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = raycast;
            return image;
        }

        // 카드 위쪽 가운데 기준으로 놓는 글자야.
        private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, Material material, string text,
            float size, Color color, Vector2 position, Vector2 rectSize)
        {
            TMP_Text label = CreateLabel(name, parent, font, material, text, size, color, TextAlignmentOptions.Midline);
            Place(label.rectTransform, Top, Center, position, rectSize);
            return label;
        }

        // [아이콘 글자] 묶음 안의 글자야. GameOverUI가 글자 길이에 맞춰 가운데로 맞춰.
        private static TMP_Text PairText(string name, Transform parent, TMP_FontAsset font, Material material, string text,
            float size, Color color, Vector2 position)
        {
            TMP_Text label = CreateLabel(name, parent, font, material, text, size, color, TextAlignmentOptions.MidlineLeft);
            Place(label.rectTransform, Center, LeftMiddle, position, new Vector2(120f, 30f));
            return label;
        }

        private static TMP_Text CreateLabel(string name, Transform parent, TMP_FontAsset font, Material material, string text,
            float size, Color color, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI label = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSharedMaterial = material;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }
    }
}
