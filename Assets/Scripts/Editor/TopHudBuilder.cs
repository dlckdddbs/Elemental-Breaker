using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker.Editor
{
    // 지금 열린 씬의 Canvas 아래에 상단 정보판을 한 번 만들고, 예전 공 개수 텍스트를 지워.
    // Ctrl+Z로 되돌릴 수 있고, 씬 저장은 직접 해.
    // 1줄: 턴 | 이번 턴 속성 | 골드    2줄: 공 개수(+얻은 공) | 다음 턴 속성
    public static class TopHudBuilder
    {
        private const string SpriteFolder = "Assets/Art/Casual/Sprites/";
        private const string FontPath = "Assets/Font/Jua/Jua-Regular UI SDF.asset";
        // TMP 재질 프리셋 목록에 나오도록 폰트 에셋 이름(Jua-Regular)으로 시작해.
        private const string OutlinePath = "Assets/Font/Jua/Jua-Regular UI SDF Outline.mat";
        private const string HudName = "TopHUD";
        private const string OldLabelName = "Ball Count Label";
        // 상단 영역 높이야(기준 해상도 480x854 픽셀). 게임판 위쪽 경계선 바로 위까지야.
        public const float HudHeight = 121f;
        // 정보판은 위 8, 양옆 10 여백을 두고 높이 100이야. 1줄 60, 2줄 40.
        private const float PanelTop = 8f;
        private const float PanelSide = 10f;
        private const float PanelHeight = 100f;
        private const float RowOne = 60f;
        private const float PillHeight = 50f;
        private const float AcquiredHeight = 20f;

        private static readonly Color32 Navy = new Color32(0x1E, 0x1B, 0x3D, 0xFF);
        private static readonly Color32 GoldColor = new Color32(0xFF, 0xE4, 0x8A, 0xFF);
        private static readonly Color CaptionColor = new Color(1f, 1f, 1f, 0.67f);
        private static readonly Color DividerColor = new Color32(0xA0, 0xBE, 0xFF, 0x2E);
        // 회색 알약(밝기 225)에 곱해져서 화면에서 #3ED448로 보여.
        private static readonly Color32 AcquiredTint = new Color32(0x47, 0xF0, 0x52, 0xFF);

        [MenuItem("Elemental Breaker/Create Top HUD")]
        private static void CreateFromMenu() => Create();

        public static bool Create()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            BallLauncher launcher = Object.FindFirstObjectByType<BallLauncher>();
            BrickBoard board = Object.FindFirstObjectByType<BrickBoard>();
            if (canvas == null || launcher == null || board == null)
            {
                Notify("씬에서 Canvas, BallLauncher, BrickBoard를 찾지 못했어.");
                return false;
            }
            if (canvas.transform.Find(HudName) != null)
            {
                Notify("이미 TopHUD가 있어. 다시 만들려면 기존 TopHUD를 지운 뒤 실행해.");
                return false;
            }
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                Notify("한글 UI 폰트 에셋을 찾지 못했어: " + FontPath);
                return false;
            }

            EnsureSpriteImported("ui_pill");
            EnsureSpriteImported("ui_panel");
            Material outline = EnsureOutlineMaterial(font);
            Sprite pill = LoadSprite("ui_pill");
            Sprite panelSprite = LoadSprite("ui_panel");
            Sprite circle = LoadSprite("ui_circle");
            Sprite coin = LoadSprite("fx_coin");
            Sprite ball = LoadSprite("ball_normal");
            Sprite normalIcon = LoadSprite("ui_icon_normal");
            if (pill == null || panelSprite == null || circle == null || coin == null || ball == null || normalIcon == null)
            {
                Notify("HUD 이미지 일부를 찾지 못했어. Assets/Art/Casual/Sprites를 확인해.");
                return false;
            }

            RectTransform root = CreateRect(HudName, canvas.transform);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(0f, HudHeight);
            root.anchoredPosition = Vector2.zero;

            // 정보판: 터치는 받지 않아.
            Image panel = CreateImage("Panel", root, panelSprite, Image.Type.Sliced);
            RectTransform p = panel.rectTransform;
            p.anchorMin = Vector2.zero;
            p.anchorMax = Vector2.one;
            p.offsetMin = new Vector2(PanelSide, HudHeight - PanelTop - PanelHeight);
            p.offsetMax = new Vector2(-PanelSide, -PanelTop);

            // 구분선: 1줄 세 칸 사이, 1줄과 2줄 사이, 2줄 두 칸 사이
            Divider(p, new Vector2(1f / 3f, 1f), new Vector2(0f, -12f), new Vector2(1f, RowOne - 20f));
            Divider(p, new Vector2(2f / 3f, 1f), new Vector2(0f, -12f), new Vector2(1f, RowOne - 20f));
            Image line = CreateImage("Divider", p, null, Image.Type.Simple);
            line.color = DividerColor;
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            line.rectTransform.anchoredPosition = new Vector2(0f, -RowOne);
            line.rectTransform.sizeDelta = new Vector2(-28f, 1f);
            Divider(p, new Vector2(0.5f, 1f), new Vector2(0f, -RowOne - 8f), new Vector2(1f, PanelHeight - RowOne - 16f));

            float rowOneY = -RowOne * 0.5f, rowTwoY = -RowOne - (PanelHeight - RowOne) * 0.5f;

            // 1줄 왼쪽: 턴
            CreateText("TurnCaption", p, font, font.material, "턴", 13f, CaptionColor,
                new Vector2(1f / 6f, 1f), Center, new Vector2(0f, -19f), new Vector2(60f, 18f), TextAlignmentOptions.Midline);
            TMP_Text turnLabel = CreateText("TurnNumber", p, font, outline, "1", 28f, Color.white,
                new Vector2(1f / 6f, 1f), Center, new Vector2(0f, -41f), new Vector2(100f, 32f), TextAlignmentOptions.Midline);

            // 1줄 가운데: 이번 턴 속성 [● 이름]
            RectTransform element = Group("Element", p, new Vector2(0.5f, 1f), new Vector2(0f, rowOneY), 34f);
            Image elementCircle = CreateImage("Circle", element, circle, Image.Type.Simple);
            Place(elementCircle.rectTransform, Center, LeftMiddle, new Vector2(-40f, 1f), new Vector2(30f, 32f));
            elementCircle.color = ElementRules.GetBrickTint(ElementType.Normal);
            Image elementIcon = CreateImage("Icon", elementCircle.rectTransform, normalIcon, Image.Type.Simple);
            Place(elementIcon.rectTransform, Center, Center, new Vector2(0f, 1f), new Vector2(18f, 18f));
            TMP_Text elementName = CreateText("Name", element, font, outline, "일반", 22f, Color.white,
                Center, LeftMiddle, Vector2.zero, new Vector2(100f, 30f), TextAlignmentOptions.MidlineLeft);

            // 1줄 오른쪽: 골드 [동전 숫자]
            RectTransform gold = Group("Gold", p, new Vector2(5f / 6f, 1f), new Vector2(0f, rowOneY), 30f);
            Image coinImage = CreateImage("GoldIcon", gold, coin, Image.Type.Simple);
            Place(coinImage.rectTransform, Center, LeftMiddle, new Vector2(-20f, 0f), new Vector2(26f, 26f));
            TMP_Text goldLabel = CreateText("Amount", gold, font, outline, "0", 26f, GoldColor,
                Center, LeftMiddle, Vector2.zero, new Vector2(100f, 30f), TextAlignmentOptions.MidlineLeft);

            // 2줄 왼쪽: 공 개수 [공 공 ×N +M]
            RectTransform balls = Group("Balls", p, new Vector2(0.25f, 1f), new Vector2(0f, rowTwoY), 24f);
            Image ballImage = CreateImage("BallIcon", balls, ball, Image.Type.Simple);
            Place(ballImage.rectTransform, Center, LeftMiddle, new Vector2(-50f, 0f), new Vector2(20f, 20f));
            ballImage.color = ElementRules.GetBallTint(ElementType.Normal);
            TMP_Text ballLabel = CreateText("Count", balls, font, outline, "공 ×1", 17f, Color.white,
                Center, LeftMiddle, Vector2.zero, new Vector2(120f, 24f), TextAlignmentOptions.MidlineLeft);
            Image acquired = CreateImage("Acquired", balls, pill, Image.Type.Sliced);
            Place(acquired.rectTransform, Center, LeftMiddle, new Vector2(40f, 0f), new Vector2(30f, AcquiredHeight));
            acquired.color = AcquiredTint;
            // 작은 알약도 양 끝이 반원이 되게 테두리 비율을 맞춰.
            acquired.pixelsPerUnitMultiplier = PillHeight / AcquiredHeight;
            TMP_Text acquiredLabel = CreateText("Label", acquired.rectTransform, font, outline, "+1", 14f, Color.white,
                Center, Center, new Vector2(0f, 1f), new Vector2(30f, AcquiredHeight), TextAlignmentOptions.Midline);
            acquired.gameObject.SetActive(false);

            // 2줄 오른쪽: 다음 턴 속성 [● 다음: 이름]
            RectTransform next = Group("Next", p, new Vector2(0.75f, 1f), new Vector2(0f, rowTwoY), 24f);
            Image nextCircle = CreateImage("Circle", next, circle, Image.Type.Simple);
            Place(nextCircle.rectTransform, Center, LeftMiddle, new Vector2(-50f, 0f), new Vector2(22f, 23f));
            nextCircle.color = ElementRules.GetBrickTint(ElementType.Normal);
            Image nextIcon = CreateImage("Icon", nextCircle.rectTransform, normalIcon, Image.Type.Simple);
            Place(nextIcon.rectTransform, Center, Center, new Vector2(0f, 0.5f), new Vector2(13f, 13f));
            TMP_Text nextCaption = CreateText("Caption", next, font, font.material, "다음:", 16f, CaptionColor,
                Center, LeftMiddle, new Vector2(-22f, 0f), new Vector2(60f, 24f), TextAlignmentOptions.MidlineLeft);
            TMP_Text nextName = CreateText("Name", next, font, outline, "일반", 16f, Color.white,
                Center, LeftMiddle, new Vector2(20f, 0f), new Vector2(80f, 24f), TextAlignmentOptions.MidlineLeft);

            TopHUD hud = root.gameObject.AddComponent<TopHUD>();
            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("launcher").objectReferenceValue = launcher;
            so.FindProperty("board").objectReferenceValue = board;
            so.FindProperty("turnLabel").objectReferenceValue = turnLabel;
            so.FindProperty("elementGroup").objectReferenceValue = element;
            so.FindProperty("elementCircle").objectReferenceValue = elementCircle;
            so.FindProperty("elementIcon").objectReferenceValue = elementIcon;
            so.FindProperty("elementName").objectReferenceValue = elementName;
            so.FindProperty("nextGroup").objectReferenceValue = next;
            so.FindProperty("nextCaption").objectReferenceValue = nextCaption;
            so.FindProperty("nextCircle").objectReferenceValue = nextCircle;
            so.FindProperty("nextIcon").objectReferenceValue = nextIcon;
            so.FindProperty("nextName").objectReferenceValue = nextName;
            so.FindProperty("goldGroup").objectReferenceValue = gold;
            so.FindProperty("goldIcon").objectReferenceValue = coinImage.rectTransform;
            so.FindProperty("goldLabel").objectReferenceValue = goldLabel;
            so.FindProperty("ballIcon").objectReferenceValue = ballImage.rectTransform;
            so.FindProperty("ballLabel").objectReferenceValue = ballLabel;
            so.FindProperty("acquiredBadge").objectReferenceValue = acquired.rectTransform;
            so.FindProperty("acquiredLabel").objectReferenceValue = acquiredLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            // 다른 UI보다 먼저 그려서 상점 목록 같은 창이 위에 덮여.
            root.SetAsFirstSibling();
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create Top HUD");

            // 새 HUD가 공 개수를 보여 주니까 예전 텍스트는 지워.
            Transform oldLabel = canvas.transform.Find(OldLabelName);
            if (oldLabel != null)
                Undo.DestroyObjectImmediate(oldLabel.gameObject);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = root.gameObject;
            Debug.Log("TopHUD를 만들었어. 확인하고 Ctrl+S로 씬을 저장해. 되돌리려면 Ctrl+Z.");
            return true;
        }

        private static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        // 칸 가운데에 놓이는 묶음이야. 안의 항목은 TopHUD가 글자 길이에 맞춰 이어 붙여.
        private static RectTransform Group(string name, Transform parent, Vector2 anchor, Vector2 position, float height)
        {
            RectTransform group = CreateRect(name, parent);
            Place(group, anchor, Center, position, new Vector2(0f, height));
            return group;
        }

        private static void Divider(Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            Image line = CreateImage("Divider", parent, null, Image.Type.Simple);
            line.color = DividerColor;
            Place(line.rectTransform, anchor, new Vector2(0.5f, 1f), position, size);
        }

        private static void Notify(string message) => EditorUtility.DisplayDialog("Top HUD", message, "확인");

        private static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");

        private static void EnsureSpriteImported(string name)
        {
            // 가져오기 설정 스크립트보다 이미지가 먼저 들어왔으면 9-slice 테두리가 빠져 있어서 한 번 다시 가져와.
            string path = SpriteFolder + name + ".png";
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.spriteBorder == Vector4.zero)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static Material EnsureOutlineMaterial(TMP_FontAsset font)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
            if (material != null)
                return material;
            material = new Material(font.material);
            Shader mobile = Shader.Find("TextMeshPro/Mobile/Distance Field");
            if (mobile != null)
                material.shader = mobile;
            material.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Navy);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            AssetDatabase.CreateAsset(material, OutlinePath);
            return material;
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

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Image.Type type)
        {
            Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? type : Image.Type.Simple;
            image.preserveAspect = sprite != null && type == Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, Material material, string text,
            float size, Color color, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 rectSize, TextAlignmentOptions alignment)
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
            Place(label.rectTransform, anchor, pivot, position, rectSize);
            return label;
        }
    }
}
