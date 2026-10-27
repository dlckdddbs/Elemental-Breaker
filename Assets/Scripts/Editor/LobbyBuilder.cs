using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElementalBreaker.Editor
{
    // 로비 씬(Assets/Scenes/Lobby.unity)을 만들고 빌드 목록 맨 앞에 넣어서 첫 화면이 되게 해.
    // 지금 열린 씬은 건드리지 않고 로비 씬만 따로 열어 만든 뒤 저장해. 다시 누르면 로비를 처음부터 새로 만들어.
    // 배치는 기준 해상도 480x854에서 위로부터 잰 높이로 정하고, 화면 비율이 달라도 같은 비율 위치에 놓여.
    public static class LobbyBuilder
    {
        private const string ScenePath = "Assets/Scenes/Lobby.unity";
        private const string GameSceneName = "SampleScene";
        internal const string SpriteFolder = "Assets/Art/Casual/Sprites/";
        private const string BoardFolder = "Assets/Resources/BoardVisuals/";
        internal const string FontPath = "Assets/Font/Jua/Jua-Regular UI SDF.asset";
        private const string OutlinePath = "Assets/Font/Jua/Jua-Regular UI SDF Outline.mat";
        private const string ShadowPath = "Assets/Font/Jua/Jua-Regular UI SDF Shadow.mat";
        private const string DefaultActionsPath = "Packages/com.unity.inputsystem/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions";
        private const float RefWidth = 480f;
        private const float RefHeight = 854f;
        // 조준점 그림(aim_dot)은 가운데 1/3쯤만 보여서, 보이는 지름의 이 배만큼 크게 놓아.
        private const float DotScale = 3.1f;
        // 게임판 벽돌과 같은 크기와 모서리야.
        private static readonly Vector2 BrickSize = new Vector2(77f, 56f);
        private const float BrickSliceMultiplier = 1.17f;
        private const float BrickAlpha = 0.4f;
        private const float ButtonWidth = 272f;
        // 아래 그림자는 예전 버튼 두께의 절반이야. 위쪽 밝은 선은 짧은 선처럼 보여서 넣지 않아.
        private const float ShadowDrop = 3f;
        // 이어하기 오른쪽 저장 정보 크기야. 360~390px 폭 휴대폰에서 약 15px로 보여.
        private const float InfoSize = 20f;

        private static readonly Color CameraColor = new Color(0.094f, 0.149f, 0.239f, 1f);
        private static readonly Color VignetteTint = new Color32(0x04, 0x0A, 0x1A, 0x80);
        private static readonly Color DustColor = new Color32(0xC8, 0xDC, 0xFF, 0xFF);
        private static readonly Color SubtitleColor = new Color32(0xBE, 0xD0, 0xFF, 0xB3);
        internal static readonly Color MutedColor = new Color32(0xD6, 0xE0, 0xFF, 0x99);
        // 파란 바탕 위에서 글자 대비가 4.5:1 이상 나오는 밝기야.
        private static readonly Color InfoColor = new Color32(0xDC, 0xE6, 0xFF, 0xE0);
        private static readonly Color DividerColor = new Color32(0xBE, 0xD2, 0xFF, 0x59);
        private static readonly Color AimColor = new Color32(0xDC, 0xE8, 0xFF, 0xFF);
        private static readonly Color ShadowColor = new Color32(0x06, 0x0E, 0x26, 0x80);
        // 확인 창은 게임 오버 카드와 같은 카드 그림, 같은 어두운 막을 써.
        internal static readonly Color DimColor = new Color32(0x05, 0x09, 0x16, 0x9E);
        internal static readonly Color CardTint = new Color32(0xC8, 0xCC, 0xDB, 0xFF);
        private static readonly Vector2 ConfirmSize = new Vector2(340f, 214f);
        private static readonly Vector2 ConfirmButtonSize = new Vector2(136f, 50f);
        internal static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

        [MenuItem("Elemental Breaker/Create Lobby Scene (로비)")]
        private static void CreateFromMenu() => Create();

        public static bool Create()
        {
            EnsureImported(SpriteFolder + "ui_button_fill.png");
            EnsureImported(SpriteFolder + "ui_button_line.png");
            EnsureImported(SpriteFolder + "ui_result_card.png");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Material outline = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
            Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(BoardFolder + "navy_background.png");
            Sprite dot = AssetDatabase.LoadAssetAtPath<Sprite>(BoardFolder + "aim_dot.png");
            Sprite vignette = Load("fx_edge_vignette"), brick = Load("brick_base"), ball = Load("ball_normal");
            Sprite buttonFill = Load("ui_button_fill"), buttonLine = Load("ui_button_line"), resultCard = Load("ui_result_card");
            if (font == null || outline == null || background == null || dot == null || vignette == null || brick == null || ball == null ||
                buttonFill == null || buttonLine == null || resultCard == null)
            {
                EditorUtility.DisplayDialog("Lobby", "폰트나 로비에 쓰는 이미지(배경, 벽돌, 공, 버튼) 중 하나를 찾지 못했어.", "확인");
                return false;
            }
            if (!File.Exists(ScenePath) && Enumerable.Range(0, SceneManager.sceneCount).Any(i => string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)))
            {
                EditorUtility.DisplayDialog("Lobby", "저장하지 않은 새 씬이 열려 있어. 먼저 저장하고 다시 눌러.", "확인");
                return false;
            }
            Material shadow = ShadowMaterial(font);

            Scene previous = SceneManager.GetActiveScene();
            Scene lobby = SceneManager.GetSceneByPath(ScenePath);
            bool wasOpen = lobby.IsValid() && lobby.isLoaded;
            if (!wasOpen)
                lobby = File.Exists(ScenePath)
                    ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            foreach (GameObject root in lobby.GetRootGameObjects())
                Object.DestroyImmediate(root);

            // 새로 만드는 오브젝트가 로비 씬에 들어가게 잠깐 로비를 활성 씬으로 둬.
            SceneManager.SetActiveScene(lobby);
            try
            {
                Build(font, outline, shadow, background, dot, vignette, brick, ball, buttonFill, buttonLine, resultCard);
            }
            finally
            {
                if (previous.IsValid() && previous != lobby)
                    SceneManager.SetActiveScene(previous);
            }
            EditorSceneManager.SaveScene(lobby, ScenePath);
            if (!wasOpen)
                EditorSceneManager.CloseScene(lobby, true);

            PutFirstInBuildList();
            Debug.Log("로비 씬을 만들고 빌드 목록 맨 앞에 넣었어. Assets/Scenes/Lobby.unity를 열고 실행해서 확인해.");
            return true;
        }

        private static void Build(TMP_FontAsset font, Material outline, Material shadow, Sprite background, Sprite dot, Sprite vignette,
            Sprite brick, Sprite ball, Sprite buttonFill, Sprite buttonLine, Sprite resultCard)
        {
            // 화면 지우기와 소리 듣기만 해. 그림은 전부 UI야.
            GameObject cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CameraColor;
            cameraObject.AddComponent<AudioListener>();

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            AssignDefaultActions(eventSystem.AddComponent<InputSystemUIInputModule>());

            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            RectTransform root = (RectTransform)canvasObject.transform;

            // 배경: 게임과 같은 남색 그림을 비율 그대로 화면에 꽉 채우고, 가장자리만 살짝 어둡게
            Image backdrop = CreateImage("Background", root, background, Image.Type.Simple, false);
            Place(backdrop.rectTransform, Center, Center, Vector2.zero, new Vector2(RefWidth, RefHeight));
            AspectRatioFitter fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = background.rect.width / background.rect.height;
            Image edge = CreateImage("Vignette", root, vignette, Image.Type.Simple, false);
            edge.color = VignetteTint;
            Stretch(edge.rectTransform);

            // 떠 있는 작은 빛 점. 정해진 난수라 다시 만들어도 같은 자리야.
            RectTransform dust = CreateRect("Dust", root);
            Stretch(dust);
            System.Random random = new System.Random(7);
            for (int i = 0; i < 26; i++)
            {
                Vector2 anchor = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
                float diameter = 1.2f + 3f * (float)random.NextDouble();
                Image speck = CreateImage("Speck", dust, dot, Image.Type.Simple, false);
                speck.color = WithAlpha(DustColor, 0.05f + 0.16f * (float)random.NextDouble());
                Place(speck.rectTransform, anchor, Center, Vector2.zero, Vector2.one * diameter * DotScale);
            }

            // 제목: 작은 "엘리멘탈" + 흰 "브레이커" + 속성 색 점 네 개(일반, 불, 물, 풀)
            RectTransform title = Group("Title", root, 232f);
            TMP_Text subtitle = Label("Subtitle", title, font, font.material, "엘리멘탈", 22f, SubtitleColor, new Vector2(0f, 56f), new Vector2(300f, 32f));
            subtitle.characterSpacing = 45f;
            Label("Name", title, font, outline, "브레이커", 66f, Color.white, Vector2.zero, new Vector2(380f, 84f));
            for (int i = 0; i < 4; i++)
            {
                Image elementDot = CreateImage("ElementDot", title, dot, Image.Type.Simple, false);
                elementDot.color = WithAlpha(ElementRules.GetBrickTint((ElementType)i), 0.85f);
                Place(elementDot.rectTransform, Center, Center, new Vector2((i - 1.5f) * 20f, -58f), Vector2.one * 8f * DotScale);
            }

            // 가운데: 흐린 벽돌 한 줄과, 벽돌 사이 빈칸을 겨누는 공 하나
            RectTransform scene = Group("Scene", root, 453f);
            RectTransform bricks = CreateRect("Bricks", scene);
            Place(bricks, Center, Center, Vector2.zero, Vector2.zero);
            bricks.gameObject.AddComponent<CanvasGroup>().alpha = BrickAlpha;
            ElementType[] kinds = { ElementType.Fire, ElementType.Water, ElementType.Grass, ElementType.Normal };
            float[] columns = { -120f, -40f, 120f, 200f };
            for (int i = 0; i < kinds.Length; i++)
            {
                Image block = CreateImage("Brick", bricks, brick, Image.Type.Sliced, false);
                block.color = ElementRules.GetBrickTint(kinds[i]);
                block.pixelsPerUnitMultiplier = BrickSliceMultiplier;
                Place(block.rectTransform, Center, Center, new Vector2(columns[i], 75f), BrickSize);
                Sprite icon = kinds[i] == ElementType.Normal ? null : Load("icon_" + kinds[i].ToString().ToLowerInvariant());
                if (icon == null)
                    continue;
                Image mark = CreateImage("Icon", block.rectTransform, icon, Image.Type.Simple, false);
                Place(mark.rectTransform, TopLeft, TopLeft, new Vector2(3.4f, -3.4f), new Vector2(20.5f, 20.5f));
            }
            Vector2 ballPosition = new Vector2(0f, -75f), aimTarget = new Vector2(46f, 69f);
            int steps = Mathf.FloorToInt(Vector2.Distance(ballPosition, aimTarget) / 17f);
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                Image aim = CreateImage("AimDot", scene, dot, Image.Type.Simple, false);
                aim.color = WithAlpha(AimColor, 0.55f * (1f - t) + 0.08f);
                Place(aim.rectTransform, Center, Center, Vector2.Lerp(ballPosition, aimTarget, t), Vector2.one * (6.4f - 2.8f * t) * DotScale);
            }
            Image ballImage = CreateImage("Ball", scene, ball, Image.Type.Simple, false);
            ballImage.color = ElementRules.GetBallTint(ElementType.Normal);
            Place(ballImage.rectTransform, Center, Center, ballPosition, new Vector2(26f, 26f));

            // 버튼: 이어하기, 새 게임은 모양이 같아. 웹에서는 탭을 닫을 수 없어서 나가기는 두지 않아.
            RectTransform buttons = Group("Buttons", root, 712f);
            LobbyButton continueButton = CreateButton("ContinueButton", buttons, new Vector2(0f, 84f), new Vector2(ButtonWidth, 56f),
                "이어하기", 24f, -34f, buttonFill, buttonLine, font, shadow);
            Image divider = CreateImage("Divider", continueButton.transform, null, Image.Type.Simple, false);
            divider.color = DividerColor;
            Place(divider.rectTransform, Center, Center, new Vector2(28f, 0f), new Vector2(1f, 22f));
            TMP_Text info = Label("Info", continueButton.transform, font, font.material, "기록 없음", InfoSize, InfoColor, new Vector2(70f, 1f), new Vector2(90f, 28f));
            LobbyButton newGameButton = CreateButton("NewGameButton", buttons, Vector2.zero, new Vector2(ButtonWidth, 66f),
                "새 게임", 24f, 0f, buttonFill, buttonLine, font, shadow);
            // 편집 화면에서는 저장이 없을 때 모양으로 둬. 실행하면 LobbyUI가 저장을 보고 다시 정해.
            continueButton.SetStyle(LobbyButtonStyle.Disabled);
            newGameButton.SetStyle(LobbyButtonStyle.Strong);

            // 새 게임 확인 창: 화면을 어둡게 덮고(바깥을 누르면 취소) 가운데 카드에 [제목 · 설명 두 줄 · 취소 / 새 게임]
            // 카드를 눌러도 막의 취소가 불리지 않게, 막과 카드는 부모-자식이 아니라 나란히 둬.
            RectTransform confirm = CreateRect("NewGameConfirm", root);
            Stretch(confirm);
            Image dim = CreateImage("Dim", confirm, null, Image.Type.Simple, true);
            dim.color = DimColor;
            Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            Navigation dimNavigation = dimButton.navigation;
            dimNavigation.mode = Navigation.Mode.None;
            dimButton.navigation = dimNavigation;
            Image card = CreateImage("Card", confirm, resultCard, Image.Type.Sliced, true);
            card.color = CardTint;
            Place(card.rectTransform, Center, Center, Vector2.zero, ConfirmSize);
            Label("Title", card.rectTransform, font, shadow, "새 게임을 시작할까요?", 25f, Color.white, new Vector2(0f, 57f), new Vector2(300f, 36f));
            TMP_Text message = Label("Message", card.rectTransform, font, font.material,
                "저장된 12턴 기록이 지워져요.\n지운 기록은 되돌릴 수 없어요.", 16f, MutedColor, new Vector2(0f, 3f), new Vector2(300f, 56f));
            message.lineSpacing = 25f;
            LobbyButton cancel = CreateButton("CancelButton", card.rectTransform, new Vector2(-76f, -61f), ConfirmButtonSize,
                "취소", 21f, 0f, buttonFill, buttonLine, font, shadow);
            cancel.SetStyle(LobbyButtonStyle.Calm);
            LobbyButton accept = CreateButton("AcceptButton", card.rectTransform, new Vector2(76f, -61f), ConfirmButtonSize,
                "새 게임", 21f, 0f, buttonFill, buttonLine, font, shadow);
            accept.SetStyle(LobbyButtonStyle.Danger);
            confirm.gameObject.SetActive(false);

            LobbyUI ui = canvasObject.AddComponent<LobbyUI>();
            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("gameSceneName").stringValue = GameSceneName;
            so.FindProperty("continueButton").objectReferenceValue = continueButton;
            so.FindProperty("newGameButton").objectReferenceValue = newGameButton;
            so.FindProperty("continueInfo").objectReferenceValue = info;
            so.FindProperty("confirmPanel").objectReferenceValue = confirm.gameObject;
            so.FindProperty("confirmDim").objectReferenceValue = dimButton;
            so.FindProperty("confirmCard").objectReferenceValue = card.rectTransform;
            so.FindProperty("confirmMessage").objectReferenceValue = message;
            so.FindProperty("confirmCancel").objectReferenceValue = cancel;
            so.FindProperty("confirmAccept").objectReferenceValue = accept;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // [그림자 · 바탕 · 얇은 테두리 · 글자] 순서로 겹친 버튼이야. 색은 LobbyButton이 정해.
        internal static LobbyButton CreateButton(string name, Transform parent, Vector2 position, Vector2 size, string text,
            float labelSize, float labelX, Sprite fillSprite, Sprite lineSprite, TMP_FontAsset font, Material shadow)
        {
            RectTransform rect = CreateRect(name, parent);
            Place(rect, Center, Center, position, size);
            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();

            Image drop = CreateImage("Shadow", rect, fillSprite, Image.Type.Sliced, false);
            drop.color = ShadowColor;
            Stretch(drop.rectTransform);
            drop.rectTransform.offsetMin = new Vector2(0f, -ShadowDrop);
            drop.rectTransform.offsetMax = new Vector2(0f, -ShadowDrop);
            Image fill = CreateImage("Fill", rect, fillSprite, Image.Type.Sliced, true);
            Stretch(fill.rectTransform);
            Image border = CreateImage("Border", rect, lineSprite, Image.Type.Sliced, false);
            Stretch(border.rectTransform);
            Label("Label", rect, font, shadow, text, labelSize, Color.white, new Vector2(labelX, 1f), new Vector2(160f, 36f));

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            // 누를 수 없는 모양은 LobbyButton이 칸 전체를 흐리게 해서 보여 줘.
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            LobbyButton lobbyButton = rect.gameObject.AddComponent<LobbyButton>();
            SerializedObject so = new SerializedObject(lobbyButton);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("border").objectReferenceValue = border;
            so.ApplyModifiedPropertiesWithoutUndo();
            return lobbyButton;
        }

        // 흰 글자 아래에 옅은 그림자만 까는 글꼴 재질이야. 없을 때 한 번만 만들어.
        internal static Material ShadowMaterial(TMP_FontAsset font)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(ShadowPath);
            if (existing != null)
                return existing;
            ShaderUtilities.GetShaderPropertyIDs();
            Material material = new Material(font.material) { name = Path.GetFileNameWithoutExtension(ShadowPath) };
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.35f));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
            AssetDatabase.CreateAsset(material, ShadowPath);
            return material;
        }

        // 게임 씬의 EventSystem과 같은 기본 UI 입력(Input System)을 연결해.
        private static void AssignDefaultActions(InputSystemUIInputModule module)
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DefaultActionsPath);
            if (asset == null)
                return;
            InputActionReference[] references = AssetDatabase.LoadAllAssetsAtPath(DefaultActionsPath).OfType<InputActionReference>().ToArray();
            InputActionReference Find(string action) => references.FirstOrDefault(r =>
                r.action != null && r.action.actionMap != null && r.action.actionMap.name == "UI" && r.action.name == action);
            module.actionsAsset = asset;
            module.point = Find("Point");
            module.leftClick = Find("Click");
            module.rightClick = Find("RightClick");
            module.middleClick = Find("MiddleClick");
            module.scrollWheel = Find("ScrollWheel");
            module.move = Find("Navigate");
            module.submit = Find("Submit");
            module.cancel = Find("Cancel");
            module.trackedDevicePosition = Find("TrackedDevicePosition");
            module.trackedDeviceOrientation = Find("TrackedDeviceOrientation");
        }

        private static void PutFirstInBuildList()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            if (!scenes.Any(s => s.enabled && Path.GetFileNameWithoutExtension(s.path) == GameSceneName))
                Debug.LogWarning($"빌드 목록에 {GameSceneName} 씬이 없어. 새 게임이 동작하게 추가해.");
        }

        internal static void EnsureImported(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.spriteBorder == Vector4.zero)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        internal static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");

        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        // 기준 화면에서 위로부터 y만큼 내려온 높이에 놓는 묶음이야. 자식은 묶음 가운데를 기준으로 놓아.
        private static RectTransform Group(string name, Transform parent, float yFromTop)
        {
            RectTransform rect = CreateRect(name, parent);
            Place(rect, new Vector2(0.5f, 1f - yFromTop / RefHeight), Center, Vector2.zero, Vector2.zero);
            return rect;
        }

        internal static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        internal static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Center;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        internal static Image CreateImage(string name, Transform parent, Sprite sprite, Image.Type type, bool raycast)
        {
            Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? type : Image.Type.Simple;
            image.raycastTarget = raycast;
            return image;
        }

        internal static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, Material material, string text,
            float size, Color color, Vector2 position, Vector2 rectSize)
        {
            TextMeshProUGUI label = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSharedMaterial = material;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Midline;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            Place(label.rectTransform, Center, Center, position, rectSize);
            return label;
        }
    }
}
