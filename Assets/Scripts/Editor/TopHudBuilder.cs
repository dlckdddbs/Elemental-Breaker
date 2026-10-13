using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker.Editor
{
    // 지금 열린 씬의 Canvas 아래에 상단 HUD를 한 번 만들고, 예전 공 개수 텍스트를 지워.
    // Ctrl+Z로 되돌릴 수 있고, 씬 저장은 직접 해.
    public static class TopHudBuilder
    {
        private const string SpriteFolder = "Assets/Art/Casual/Sprites/";
        private const string FontPath = "Assets/Font/Jua/Jua-Regular UI SDF.asset";
        // TMP 재질 프리셋 목록에 나오도록 폰트 에셋 이름(Jua-Regular)으로 시작해.
        private const string OutlinePath = "Assets/Font/Jua/Jua-Regular UI SDF Outline.mat";
        private const string HudName = "TopHUD";
        private const string OldLabelName = "Ball Count Label";
        // 상단 검은 선 위 띠의 높이야(기준 해상도 480x854 픽셀).
        private const float HudHeight = 76f;
        private const float PillHeight = 50f;
        private const float AcquiredHeight = 22f;

        private static readonly Color32 Navy = new Color32(0x1E, 0x1B, 0x3D, 0xFF);
        private static readonly Color32 GoldColor = new Color32(0xFF, 0xE4, 0x8A, 0xFF);
        private static readonly Color CaptionColor = new Color(1f, 1f, 1f, 0.67f);
        // 회색 알약(밝기 225)에 곱해져서 화면에서 #3ED448로 보여.
        private static readonly Color32 AcquiredTint = new Color32(0x47, 0xF0, 0x52, 0xFF);

        [MenuItem("Elemental Breaker/Create Top HUD")]
        private static void Create()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            BallLauncher launcher = Object.FindFirstObjectByType<BallLauncher>();
            BrickBoard board = Object.FindFirstObjectByType<BrickBoard>();
            if (canvas == null || launcher == null || board == null)
            {
                Notify("씬에서 Canvas, BallLauncher, BrickBoard를 찾지 못했어.");
                return;
            }
            if (canvas.transform.Find(HudName) != null)
            {
                Notify("이미 TopHUD가 있어. 다시 만들려면 기존 TopHUD를 지운 뒤 실행해.");
                return;
            }
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                Notify("한글 UI 폰트 에셋을 찾지 못했어: " + FontPath);
                return;
            }

            EnsureSpriteImported("ui_pill");
            Material outline = EnsureOutlineMaterial(font);
            Sprite pill = LoadSprite("ui_pill");
            Sprite circle = LoadSprite("ui_circle");
            Sprite arrow = LoadSprite("ui_arrow");
            Sprite coin = LoadSprite("fx_coin");
            Sprite ball = LoadSprite("ball_normal");
            Sprite normalIcon = LoadSprite("ui_icon_normal");
            if (pill == null || circle == null || arrow == null || coin == null || ball == null || normalIcon == null)
            {
                Notify("HUD 이미지 일부를 찾지 못했어. Assets/Art/Casual/Sprites를 확인해.");
                return;
            }

            RectTransform root = CreateRect(HudName, canvas.transform);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(0f, HudHeight);
            root.anchoredPosition = Vector2.zero;

            // 왼쪽: 턴
            CreateText("TurnCaption", root, font, font.material, "턴", 16f, CaptionColor,
                TopLeft, new Vector2(0f, 0.5f), new Vector2(16f, -17f), new Vector2(60f, 22f), TextAlignmentOptions.MidlineLeft);
            TMP_Text turnLabel = CreateText("TurnNumber", root, font, outline, "1", 34f, Color.white,
                TopLeft, new Vector2(0f, 0.5f), new Vector2(14f, -46f), new Vector2(100f, 40f), TextAlignmentOptions.MidlineLeft);

            // 가운데: [현재 속성 배지] → (다음 속성)
            RectTransform element = CreateRect("Element", root);
            Place(element, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-30f, -37f), new Vector2(0f, PillHeight));
            Image badge = CreateImage("Badge", element, pill, Image.Type.Sliced);
            Place(badge.rectTransform, Center, new Vector2(0f, 0.5f), new Vector2(-65f, 0f), new Vector2(130f, PillHeight));
            badge.color = ElementRules.GetBrickTint(ElementType.Normal);
            Image badgeIcon = CreateImage("Icon", badge.rectTransform, normalIcon, Image.Type.Simple);
            Place(badgeIcon.rectTransform, LeftMiddle, new Vector2(0f, 0.5f), new Vector2(18f, 2.5f), new Vector2(34f, 34f));
            TMP_Text badgeName = CreateText("Name", badge.rectTransform, font, outline, "일반", 26f, Color.white,
                LeftMiddle, new Vector2(0f, 0.5f), new Vector2(60f, 2.5f), new Vector2(80f, 40f), TextAlignmentOptions.MidlineLeft);

            RectTransform next = CreateRect("Next", element);
            Place(next, Center, new Vector2(0f, 0.5f), new Vector2(71f, 0f), new Vector2(74f, PillHeight));
            Image arrowImage = CreateImage("Arrow", next, arrow, Image.Type.Simple);
            Place(arrowImage.rectTransform, LeftMiddle, new Vector2(0f, 0.5f), new Vector2(0f, 2.5f), new Vector2(26f, 22f));
            Image nextCircle = CreateImage("Circle", next, circle, Image.Type.Simple);
            Place(nextCircle.rectTransform, LeftMiddle, new Vector2(0f, 0.5f), new Vector2(32f, 0f), new Vector2(42f, 45f));
            Image nextIcon = CreateImage("Icon", nextCircle.rectTransform, normalIcon, Image.Type.Simple);
            Place(nextIcon.rectTransform, Center, new Vector2(0.5f, 0.5f), new Vector2(0f, 1.5f), new Vector2(30f, 30f));
            next.gameObject.SetActive(false);

            // 오른쪽 위: 골드
            Image coinImage = CreateImage("GoldIcon", root, coin, Image.Type.Simple);
            Place(coinImage.rectTransform, TopRight, new Vector2(0.5f, 0.5f), new Vector2(-126f, -24f), new Vector2(24f, 24f));
            TMP_Text goldLabel = CreateText("Gold", root, font, outline, "0", 24f, GoldColor,
                TopRight, new Vector2(0f, 0.5f), new Vector2(-106f, -24f), new Vector2(100f, 30f), TextAlignmentOptions.MidlineLeft);

            // 오른쪽 아래: 공 개수와 이번 턴에 얻은 공
            Image ballImage = CreateImage("BallIcon", root, ball, Image.Type.Simple);
            Place(ballImage.rectTransform, TopRight, new Vector2(0.5f, 0.5f), new Vector2(-126f, -60f), new Vector2(36f, 36f));
            ballImage.color = ElementRules.GetBallTint(ElementType.Normal);
            TMP_Text ballLabel = CreateText("Balls", root, font, outline, "x1", 24f, Color.white,
                TopRight, new Vector2(0f, 0.5f), new Vector2(-106f, -60f), new Vector2(80f, 30f), TextAlignmentOptions.MidlineLeft);
            Image acquired = CreateImage("Acquired", root, pill, Image.Type.Sliced);
            Place(acquired.rectTransform, TopRight, new Vector2(0f, 0.5f), new Vector2(-70f, -60f), new Vector2(32f, AcquiredHeight));
            acquired.color = AcquiredTint;
            // 작은 알약도 양 끝이 반원이 되게 테두리 비율을 맞춰.
            acquired.pixelsPerUnitMultiplier = PillHeight / AcquiredHeight;
            TMP_Text acquiredLabel = CreateText("Label", acquired.rectTransform, font, outline, "+1", 15f, Color.white,
                Center, new Vector2(0.5f, 0.5f), new Vector2(0f, 1.5f), new Vector2(32f, AcquiredHeight), TextAlignmentOptions.Midline);
            acquired.gameObject.SetActive(false);

            TopHUD hud = root.gameObject.AddComponent<TopHUD>();
            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("launcher").objectReferenceValue = launcher;
            so.FindProperty("board").objectReferenceValue = board;
            so.FindProperty("turnLabel").objectReferenceValue = turnLabel;
            so.FindProperty("elementGroup").objectReferenceValue = element;
            so.FindProperty("elementBadge").objectReferenceValue = badge;
            so.FindProperty("elementIcon").objectReferenceValue = badgeIcon;
            so.FindProperty("elementName").objectReferenceValue = badgeName;
            so.FindProperty("nextGroup").objectReferenceValue = next;
            so.FindProperty("nextCircle").objectReferenceValue = nextCircle;
            so.FindProperty("nextIcon").objectReferenceValue = nextIcon;
            so.FindProperty("goldLabel").objectReferenceValue = goldLabel;
            so.FindProperty("ballLabel").objectReferenceValue = ballLabel;
            so.FindProperty("acquiredBadge").objectReferenceValue = acquired.rectTransform;
            so.FindProperty("acquiredLabel").objectReferenceValue = acquiredLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create Top HUD");

            // 새 HUD가 공 개수를 보여 주니까 예전 텍스트는 지워.
            Transform oldLabel = canvas.transform.Find(OldLabelName);
            if (oldLabel != null)
                Undo.DestroyObjectImmediate(oldLabel.gameObject);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = root.gameObject;
            Debug.Log("TopHUD를 만들었어. 확인하고 Ctrl+S로 씬을 저장해. 되돌리려면 Ctrl+Z.");
        }

        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

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
            image.type = type;
            image.preserveAspect = type == Image.Type.Simple;
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
