using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker.Editor
{
    // 지금 열린 씬의 Canvas 아래에 상단 띠를 한 번 만들고, 예전 공 개수 텍스트를 지워.
    // Ctrl+Z로 되돌릴 수 있고, 씬 저장은 직접 해.
    // 띠는 화면 끝에서 끝까지, 화면 위부터 위쪽 경계선까지 채워. 테두리는 따로 없고 경계선이 띠의 아래 끝이야.
    // 정보 두 줄은 경계선 바로 위에 붙어. 1줄: 턴 | 이번 턴 속성 | 골드    2줄: 공 개수(+얻은 공) | 다음 턴 속성
    public static class TopHudBuilder
    {
        private const string SpriteFolder = "Assets/Art/Casual/Sprites/";
        private const string FontPath = "Assets/Font/Jua/Jua-Regular UI SDF.asset";
        // TMP 재질 프리셋 목록에 나오도록 폰트 에셋 이름(Jua-Regular)으로 시작해.
        private const string OutlinePath = "Assets/Font/Jua/Jua-Regular UI SDF Outline.mat";
        private const string HudName = "TopHUD";
        private const string OldLabelName = "Ball Count Label";
        // 상단 띠 높이야(기준 해상도 480x854 픽셀). 실행 중에는 PlayAreaBand가 위쪽 경계선에 맞춰 바꿔.
        public const float HudHeight = 118f;
        // 띠 배경 이미지(ui_top_band, 1440x543)는 480x854 화면에서 이 높이에 꼭 맞고, 구분선이 그려져 있어.
        // 정보는 이미지 구분선 자리에 맞춰. 경계선(띠 아래 끝)에서 위로 2줄 가운데 24.5, 가로 구분선 47,
        // 1줄은 세로 구분선 가운데(72.5)보다 조금 위(75)에 둬서 글자 위 빈 곳이 덜 남게 해.
        // 아이콘과 글자의 처음 자리는 기본 글자(일반, 0, 공 ×1) 기준이야. 실행하면 TopHUD가 글자 길이에 맞춰 다시 맞춰.
        private const string BandSprite = "ui_top_band";
        private const float RowOneY = 75f;
        private const float RowTwoY = 24.5f;
        // 1줄 세 칸 가운데는 이미지 세로 구분선(폭의 34.4%, 65.6%) 사이야. 2줄 두 칸은 25%, 75%.
        private const float LeftCell = 0.172f;
        private const float RightCell = 0.828f;
        private const float PillHeight = 50f;
        private const float AcquiredHeight = 20f;

        private static readonly Color32 Navy = new Color32(0x1E, 0x1B, 0x3D, 0xFF);
        private static readonly Color32 GoldColor = new Color32(0xFF, 0xE4, 0x8A, 0xFF);
        private static readonly Color CaptionColor = new Color(1f, 1f, 1f, 0.67f);
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
            EnsureSpriteImported(BandSprite);
            Material outline = EnsureOutlineMaterial(font);
            Sprite pill = LoadSprite("ui_pill");
            Sprite bandSprite = LoadSprite(BandSprite);
            Sprite circle = LoadSprite("ui_circle");
            Sprite coin = LoadSprite("fx_coin");
            Sprite ball = LoadSprite("ball_normal");
            Sprite normalIcon = LoadSprite("ui_icon_normal");
            if (pill == null || bandSprite == null || circle == null || coin == null || ball == null || normalIcon == null)
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
            // 띠 높이는 실행 중에 위쪽 경계선 위치로 맞춰. 긴 폰에서는 띠가 화면 위까지 길어지고 정보는 경계선 위에 남아.
            root.gameObject.AddComponent<PlayAreaBand>();

            // 띠 배경: 구분선이 그려진 이미지. 구분선이 있는 아래쪽은 고정하고 단순한 위쪽만 늘려서,
            // 긴 폰에서 띠가 위로 길어져도 구분선은 경계선 위 같은 자리에 있어. 480x854에서 이미지 전체가 띠 높이에 꼭 맞게 배율을 정해.
            Image band = CreateImage("Band", root, bandSprite, Image.Type.Sliced);
            band.pixelsPerUnitMultiplier = bandSprite.rect.height / (bandSprite.pixelsPerUnit / canvas.referencePixelsPerUnit) / HudHeight;
            band.rectTransform.anchorMin = Vector2.zero;
            band.rectTransform.anchorMax = Vector2.one;
            band.rectTransform.offsetMin = band.rectTransform.offsetMax = Vector2.zero;

            // 1줄 왼쪽: 턴
            CreateText("TurnCaption", root, font, font.material, "턴", 14f, CaptionColor,
                new Vector2(LeftCell, 0f), Center, new Vector2(0f, RowOneY + 12f), new Vector2(60f, 18f), TextAlignmentOptions.Midline);
            TMP_Text turnLabel = CreateText("TurnNumber", root, font, outline, "1", 30f, Color.white,
                new Vector2(LeftCell, 0f), Center, new Vector2(0f, RowOneY - 9f), new Vector2(100f, 32f), TextAlignmentOptions.Midline);

            // 1줄 가운데: 이번 턴 속성 [● 이름]
            RectTransform element = Group("Element", root, new Vector2(0.5f, 0f), new Vector2(0f, RowOneY), 34f);
            Image elementCircle = CreateImage("Circle", element, circle, Image.Type.Simple);
            Place(elementCircle.rectTransform, Center, LeftMiddle, new Vector2(-41f, 1f), new Vector2(30f, 32f));
            elementCircle.color = ElementRules.GetBrickTint(ElementType.Normal);
            Image elementIcon = CreateImage("Icon", elementCircle.rectTransform, normalIcon, Image.Type.Simple);
            Place(elementIcon.rectTransform, Center, Center, new Vector2(0f, 1f), new Vector2(18f, 18f));
            TMP_Text elementName = CreateText("Name", element, font, outline, "일반", 24f, Color.white,
                Center, LeftMiddle, new Vector2(-3f, 0f), new Vector2(100f, 30f), TextAlignmentOptions.MidlineLeft);

            // 1줄 오른쪽: 골드 [동전 숫자]
            RectTransform gold = Group("Gold", root, new Vector2(RightCell, 0f), new Vector2(0f, RowOneY), 30f);
            Image coinImage = CreateImage("GoldIcon", gold, coin, Image.Type.Simple);
            Place(coinImage.rectTransform, Center, LeftMiddle, new Vector2(-24.5f, 0f), new Vector2(28f, 28f));
            TMP_Text goldLabel = CreateText("Amount", gold, font, outline, "0", 28f, GoldColor,
                Center, LeftMiddle, new Vector2(9.5f, 0f), new Vector2(100f, 30f), TextAlignmentOptions.MidlineLeft);

            // 2줄 왼쪽: 공 개수 [공 공 ×N +M]
            RectTransform balls = Group("Balls", root, new Vector2(0.25f, 0f), new Vector2(0f, RowTwoY), 24f);
            Image ballImage = CreateImage("BallIcon", balls, ball, Image.Type.Simple);
            Place(ballImage.rectTransform, Center, LeftMiddle, new Vector2(-34.5f, 0f), new Vector2(20f, 20f));
            ballImage.color = ElementRules.GetBallTint(ElementType.Normal);
            TMP_Text ballLabel = CreateText("Count", balls, font, outline, "공 ×1", 18f, Color.white,
                Center, LeftMiddle, new Vector2(-8.5f, 0f), new Vector2(120f, 24f), TextAlignmentOptions.MidlineLeft);
            Image acquired = CreateImage("Acquired", balls, pill, Image.Type.Sliced);
            Place(acquired.rectTransform, Center, LeftMiddle, new Vector2(40.5f, 0f), new Vector2(30f, AcquiredHeight));
            acquired.color = AcquiredTint;
            // 작은 알약도 양 끝이 반원이 되게 테두리 비율을 맞춰.
            acquired.pixelsPerUnitMultiplier = PillHeight / AcquiredHeight;
            TMP_Text acquiredLabel = CreateText("Label", acquired.rectTransform, font, outline, "+1", 14f, Color.white,
                Center, Center, new Vector2(0f, 1f), new Vector2(30f, AcquiredHeight), TextAlignmentOptions.Midline);
            acquired.gameObject.SetActive(false);

            // 2줄 오른쪽: 다음 턴 속성 [● 다음: 이름]
            RectTransform next = Group("Next", root, new Vector2(0.75f, 0f), new Vector2(0f, RowTwoY), 24f);
            Image nextCircle = CreateImage("Circle", next, circle, Image.Type.Simple);
            Place(nextCircle.rectTransform, Center, LeftMiddle, new Vector2(-52f, 0f), new Vector2(22f, 23f));
            nextCircle.color = ElementRules.GetBrickTint(ElementType.Normal);
            Image nextIcon = CreateImage("Icon", nextCircle.rectTransform, normalIcon, Image.Type.Simple);
            Place(nextIcon.rectTransform, Center, Center, new Vector2(0f, 0.5f), new Vector2(13f, 13f));
            TMP_Text nextCaption = CreateText("Caption", next, font, font.material, "다음:", 17f, CaptionColor,
                Center, LeftMiddle, new Vector2(-24f, 0f), new Vector2(60f, 24f), TextAlignmentOptions.MidlineLeft);
            TMP_Text nextName = CreateText("Name", next, font, outline, "일반", 18f, Color.white,
                Center, LeftMiddle, new Vector2(19f, 0f), new Vector2(80f, 24f), TextAlignmentOptions.MidlineLeft);

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
            Debug.Log("상단 띠(TopHUD)를 만들었어. 확인하고 Ctrl+S로 씬을 저장해. 되돌리려면 Ctrl+Z.");
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
