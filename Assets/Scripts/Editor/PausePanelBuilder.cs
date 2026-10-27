using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker.Editor
{
    // 게임 씬에 일시 정지 창을 만들어. 상단 띠 아래 줄 오른쪽 끝의 톱니 버튼과 [계속하기 / 메인으로] 창이야.
    // 버튼과 카드는 로비와 같은 걸 써. 다시 누르면 예전 것을 지우고 새로 만들어. Ctrl+Z로 되돌릴 수 있고, 씬 저장은 직접 해.
    // 다른 UI보다 늘 위에 그려지게 따로 정렬 순서를 가진 캔버스(PauseUI) 안에 둬서, 상단 띠를 다시 만들어도 영향이 없어.
    public static class PausePanelBuilder
    {
        private const string RootName = "PauseUI";
        // 다시 하기 창, 배너, 2배속 표시(10)보다 위야.
        private const int SortingOrder = 50;
        // 톱니 버튼: 화면 오른쪽에서 28, 상단 띠 아래 끝에서 24.5 위(띠 아래 줄 가운데). 띠 높이는 폰마다 달라서 PlayAreaBand가 맞춰.
        private const float GearRight = 28f;
        private const float GearAboveBand = 24.5f;
        private const float GearSize = 30f;
        private const float GearIconSize = 18f;
        // 버튼 바탕·테두리 그림의 모서리(40px, 화면 20)를 버튼 반지름(15)에 맞춰 원으로 만들어.
        private const float GearSliceMultiplier = 20f / 15f;
        private static readonly Color GearFill = new Color32(0x12, 0x1C, 0x3A, 0xCC);
        private static readonly Color GearBorder = new Color32(0xA0, 0xBE, 0xFF, 0x61);
        private static readonly Color GearIconColor = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Vector2 CardSize = new Vector2(300f, 262f);
        private static readonly Vector2 ButtonSize = new Vector2(236f, 56f);

        [MenuItem("Elemental Breaker/Create Pause Panel (설정)")]
        private static void CreateFromMenu() => Create();

        public static bool Create()
        {
            LobbyBuilder.EnsureImported(LobbyBuilder.SpriteFolder + "ui_button_fill.png");
            LobbyBuilder.EnsureImported(LobbyBuilder.SpriteFolder + "ui_button_line.png");
            LobbyBuilder.EnsureImported(LobbyBuilder.SpriteFolder + "ui_result_card.png");
            BrickBoard board = Object.FindFirstObjectByType<BrickBoard>();
            BallLauncher launcher = Object.FindFirstObjectByType<BallLauncher>();
            TopHUD hud = Object.FindFirstObjectByType<TopHUD>();
            Canvas canvas = hud != null ? hud.GetComponentInParent<Canvas>() : null;
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LobbyBuilder.FontPath);
            Sprite buttonFill = LobbyBuilder.Load("ui_button_fill"), buttonLine = LobbyBuilder.Load("ui_button_line");
            Sprite resultCard = LobbyBuilder.Load("ui_result_card"), gear = LobbyBuilder.Load("ui_gear");
            if (board == null || launcher == null || canvas == null || font == null ||
                buttonFill == null || buttonLine == null || resultCard == null || gear == null)
            {
                EditorUtility.DisplayDialog("Pause", "게임 씬(BrickBoard, BallLauncher, 상단 띠)을 열고 눌러. 폰트나 버튼·톱니 그림이 없어도 못 만들어.", "확인");
                return false;
            }
            Material shadow = LobbyBuilder.ShadowMaterial(font);
            canvas = canvas.rootCanvas;

            PausePanel old = Object.FindFirstObjectByType<PausePanel>(FindObjectsInactive.Include);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            RectTransform root = LobbyBuilder.CreateRect(RootName, canvas.transform);
            LobbyBuilder.Stretch(root);
            Canvas layer = root.gameObject.AddComponent<Canvas>();
            layer.overrideSorting = true;
            layer.sortingOrder = SortingOrder;
            root.gameObject.AddComponent<GraphicRaycaster>();

            // 톱니 버튼: 로비의 차분한 버튼과 같은 남색 원 + 얇은 테두리 + 흰 톱니
            Image gearFill = LobbyBuilder.CreateImage("GearButton", root, buttonFill, Image.Type.Sliced, true);
            gearFill.color = GearFill;
            gearFill.pixelsPerUnitMultiplier = GearSliceMultiplier;
            RectTransform gearRect = gearFill.rectTransform;
            LobbyBuilder.Place(gearRect, Vector2.one, LobbyBuilder.Center,
                new Vector2(-GearRight, -(TopHudBuilder.HudHeight - GearAboveBand)), new Vector2(GearSize, GearSize));
            gearFill.gameObject.AddComponent<PlayAreaBand>().Configure(PlayAreaBand.Mode.BelowTop, -GearAboveBand);
            Image gearBorder = LobbyBuilder.CreateImage("Border", gearRect, buttonLine, Image.Type.Sliced, false);
            gearBorder.color = GearBorder;
            gearBorder.pixelsPerUnitMultiplier = GearSliceMultiplier;
            LobbyBuilder.Stretch(gearBorder.rectTransform);
            Image gearIcon = LobbyBuilder.CreateImage("Icon", gearRect, gear, Image.Type.Simple, false);
            gearIcon.color = GearIconColor;
            LobbyBuilder.Place(gearIcon.rectTransform, LobbyBuilder.Center, LobbyBuilder.Center, Vector2.zero, new Vector2(GearIconSize, GearIconSize));
            Button gearButton = gearFill.gameObject.AddComponent<Button>();
            ColorBlock colors = gearButton.colors;
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.fadeDuration = 0.08f;
            gearButton.colors = colors;
            Navigation navigation = gearButton.navigation;
            navigation.mode = Navigation.Mode.None;
            gearButton.navigation = navigation;

            // 창: 화면을 어둡게 덮고(바깥을 누르면 계속하기) 가운데 카드에 [일시 정지 · 안내 한 줄 · 계속하기 · 메인으로]
            // 카드를 눌러도 막의 계속하기가 불리지 않게, 막과 카드는 나란히 둬.
            RectTransform panel = LobbyBuilder.CreateRect("Panel", root);
            LobbyBuilder.Stretch(panel);
            Image dim = LobbyBuilder.CreateImage("Dim", panel, null, Image.Type.Simple, true);
            dim.color = LobbyBuilder.DimColor;
            LobbyBuilder.Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            Navigation dimNavigation = dimButton.navigation;
            dimNavigation.mode = Navigation.Mode.None;
            dimButton.navigation = dimNavigation;
            Image card = LobbyBuilder.CreateImage("Card", panel, resultCard, Image.Type.Sliced, true);
            card.color = LobbyBuilder.CardTint;
            LobbyBuilder.Place(card.rectTransform, LobbyBuilder.Center, LobbyBuilder.Center, Vector2.zero, CardSize);
            // 카드 위쪽 가운데에서 잰 높이야(카드 높이 262).
            float top = CardSize.y * 0.5f;
            LobbyBuilder.Label("Title", card.rectTransform, font, shadow, "일시 정지", 26f, Color.white,
                new Vector2(0f, top - 46f), new Vector2(260f, 38f));
            TMP_Text message = LobbyBuilder.Label("Message", card.rectTransform, font, font.material, "진행 상황은 자동으로 저장돼요.",
                15f, LobbyBuilder.MutedColor, new Vector2(0f, top - 81f), new Vector2(280f, 24f));
            LobbyButton resume = LobbyBuilder.CreateButton("ResumeButton", card.rectTransform, new Vector2(0f, top - 138f), ButtonSize,
                "계속하기", 23f, 0f, buttonFill, buttonLine, font, shadow);
            resume.SetStyle(LobbyButtonStyle.Strong);
            LobbyButton main = LobbyBuilder.CreateButton("MainButton", card.rectTransform, new Vector2(0f, top - 206f), ButtonSize,
                "메인으로", 23f, 0f, buttonFill, buttonLine, font, shadow);
            main.SetStyle(LobbyButtonStyle.Calm);
            panel.gameObject.SetActive(false);

            PausePanel pause = root.gameObject.AddComponent<PausePanel>();
            SerializedObject so = new SerializedObject(pause);
            so.FindProperty("board").objectReferenceValue = board;
            so.FindProperty("launcher").objectReferenceValue = launcher;
            so.FindProperty("gearButton").objectReferenceValue = gearButton;
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("dim").objectReferenceValue = dimButton;
            so.FindProperty("card").objectReferenceValue = card.rectTransform;
            so.FindProperty("message").objectReferenceValue = message;
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("mainButton").objectReferenceValue = main;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create Pause Panel");
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log("일시 정지 창을 만들었어. 확인하고 Ctrl+S로 씬을 저장해.");
            return true;
        }
    }
}
