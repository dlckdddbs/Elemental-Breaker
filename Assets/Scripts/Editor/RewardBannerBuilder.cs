using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker.Editor
{
    // 턴 보상 배너(TurnEventBanner)를 지금 열린 씬의 Canvas 아래에 한 번 만들어 연결해.
    // 배너 바탕은 ui_reward_banner.png가 있으면 그 그림을 쓰고, 없으면 금색 테두리 + 남색 판 임시 바탕을 만들어.
    // Ctrl+Z로 되돌릴 수 있고, 씬 저장은 직접 해.
    public static class RewardBannerBuilder
    {
        private const string SpriteFolder = "Assets/Art/Casual/Sprites/";
        private const string EffectFolder = "Assets/Art/Casual/Effects/";
        private const string FontPath = "Assets/Font/Jua/Jua-Regular UI SDF.asset";
        private const string OutlinePath = "Assets/Font/Jua/Jua-Regular UI SDF Outline.mat";
        public static readonly string[] CreatedNames = { "TurnBanner" };
        // 배너 크기(기준 해상도 480x854)와 상단 띠에서 떨어진 거리야. 바탕 그림은 높이 408px(3배)이고 가운데만 늘어나서 가로 비율은 달라도 돼.
        private static readonly Vector2 BannerSize = new Vector2(400f, 136f);
        private const float BannerGap = 14f;
        private const float EdgeWidth = 4f;
        private const int TrailCount = 8;
        private const int CoinCount = 8;

        private static readonly Color ScrimColor = new Color32(0x05, 0x09, 0x16, 0x00);
        // 회색 판(밝기 225)에 곱해져서 금색 테두리와 남색 판으로 보여.
        private static readonly Color32 EdgeTint = new Color32(0xFF, 0xDC, 0x6E, 0xFF);
        private static readonly Color32 FillTint = new Color32(0x36, 0x4A, 0xA6, 0xFF);
        private static readonly Color32 GoldTop = new Color32(0xFF, 0xF6, 0xC2, 0xFF);
        private static readonly Color32 GoldBottom = new Color32(0xFF, 0xC2, 0x3D, 0xFF);
        private static readonly Color32 SubColor = new Color32(0xE6, 0xEC, 0xFF, 0xFF);
        private static readonly Color32 SparkleTint = new Color32(0xFF, 0xE9, 0xA0, 0xFF);
        private static readonly Color32 TrailTint = new Color32(0xFF, 0xE4, 0x8A, 0xFF);
        private static readonly Color32 RaysTint = new Color32(0xFF, 0xD6, 0x6B, 0x00);
        private static readonly Color32 GlowTint = new Color32(0xFF, 0xDC, 0x78, 0x8C);
        private static readonly Color32 BurstTint = new Color32(0xFF, 0xD8, 0x6B, 0xFF);
        // 경고 배너의 느낌표, 화면 가장자리 붉은 빛(처음엔 투명), 상단 왼쪽 디버프 표시야.
        private static readonly Color32 AlertTint = new Color32(0xFF, 0x4D, 0x5E, 0xFF);
        private static readonly Color32 VignetteTint = new Color32(0xFF, 0x28, 0x3C, 0x00);
        private static readonly Color32 ChipTint = new Color32(0xC4, 0x2A, 0x40, 0xFF);
        private static readonly Color RestingColor = new Color(1f, 1f, 1f, 0.75f);
        // 디버프 표시는 상단 띠 아래, 띠와 벽돌 첫 줄 사이 가운데(얼음 표시와 같은 높이) 왼쪽에 놓여.
        private static readonly Vector2 ChipSize = new Vector2(140f, 28f);
        private const float ChipGap = 29f;
        private const float ChipLeft = 16f;
        private static readonly string[] DebuffIconNames = { "ui_debuff_health", "ui_debuff_items", "ui_debuff_descent", "ui_debuff_half" };
        private static readonly Vector2 Top = new Vector2(0.5f, 1f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);

        private static TMP_FontAsset font;
        private static Material outline;

        [MenuItem("Elemental Breaker/Create Turn Banner")]
        private static void CreateFromMenu() => Create();

        public static bool Create()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            BrickBoard board = Object.FindFirstObjectByType<BrickBoard>();
            BallLauncher launcher = Object.FindFirstObjectByType<BallLauncher>();
            TopHUD hud = Object.FindFirstObjectByType<TopHUD>();
            RectTransform itemSlot = canvas != null ? canvas.transform.Find("BottomBar/ItemSlot") as RectTransform : null;
            if (canvas == null || board == null || launcher == null || hud == null || itemSlot == null)
            {
                Notify("Canvas, BrickBoard, BallLauncher, TopHUD, 아이템 칸(BottomBar/ItemSlot) 중 하나를 찾지 못했어. 상단·하단 띠를 먼저 만들어.");
                return false;
            }
            if (canvas.transform.Find(CreatedNames[0]) != null)
            {
                Notify("이미 TurnBanner가 있어. 다시 만들려면 지운 뒤 실행해.");
                return false;
            }
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            outline = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
            Sprite slot = Load("ui_slot"), sparkle = Load("fx_sparkle"), rays = Load("fx_reward_rays"), coin = Load("fx_coin");
            Sprite ice = Load("ui_ice"), brick = Load("brick_base"), cycle = Load("ui_cycle_arrow"), puff = Load("fx_puff");
            Sprite ring = AssetDatabase.LoadAssetAtPath<Sprite>(EffectFolder + "fx_ring.png");
            Sprite circle = Load("ui_circle"), pill = Load("ui_pill"), vignetteSprite = Load("fx_edge_vignette");
            Sprite[] debuffIcons = new Sprite[DebuffIconNames.Length];
            bool hasDebuffIcons = true;
            for (int i = 0; i < debuffIcons.Length; i++)
            {
                debuffIcons[i] = Load(DebuffIconNames[i]);
                hasDebuffIcons &= debuffIcons[i] != null;
            }
            if (font == null || outline == null || slot == null || sparkle == null || rays == null || coin == null ||
                ice == null || brick == null || cycle == null || puff == null || ring == null ||
                circle == null || pill == null || vignetteSprite == null || !hasDebuffIcons)
            {
                Notify("한글 UI 폰트나 배너 이미지 일부를 찾지 못했어. Assets/Art/Casual을 확인해.");
                return false;
            }

            RectTransform root = CreateRect(CreatedNames[0], canvas.transform);
            Stretch(root, 0f);
            root.SetAsLastSibling();

            // 배너 뒤 게임판만 살짝 어둡게 덮어. 터치는 막지 않아(그동안은 조작이 막혀 있어).
            Image scrim = CreateImage("Scrim", root, null, Image.Type.Simple);
            scrim.color = ScrimColor;
            Stretch(scrim.rectTransform, 0f);
            scrim.gameObject.AddComponent<PlayAreaBand>().Configure(PlayAreaBand.Mode.Between, 0f);

            // 경고 배너가 나타날 때 게임판 가장자리가 붉게 번쩍여.
            Image vignette = CreateImage("Vignette", root, vignetteSprite, Image.Type.Simple);
            vignette.preserveAspect = false;
            vignette.color = VignetteTint;
            Stretch(vignette.rectTransform, 0f);
            vignette.gameObject.AddComponent<PlayAreaBand>().Configure(PlayAreaBand.Mode.Between, 0f);

            Image raysImage = CreateImage("Rays", root, rays, Image.Type.Simple);
            raysImage.color = RaysTint;
            Place(raysImage.rectTransform, Center, Center, Vector2.zero, new Vector2(560f, 560f));

            // 배너: 상단 띠 바로 아래. 폰마다 상단 띠 높이가 달라도 PlayAreaBand가 띠 끝에 맞춰.
            RectTransform banner = CreateRect("Banner", root);
            Place(banner, Top, Top, new Vector2(0f, -(TopHudBuilder.HudHeight + BannerGap)), BannerSize);
            banner.gameObject.AddComponent<PlayAreaBand>().Configure(PlayAreaBand.Mode.BelowTop, BannerGap);
            CanvasGroup group = banner.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            EnsureImported(SpriteFolder + "ui_reward_banner.png");
            EnsureImported(SpriteFolder + "ui_debuff_banner.png");
            Sprite custom = Load("ui_reward_banner"), warning = Load("ui_debuff_banner");
            Image panel = null;
            if (custom != null)
            {
                // 둥근 모서리와 금색 테두리는 그대로 두고 가운데만 늘려. 그림 높이가 배너 높이에 딱 맞게 배율을 정해.
                // 경고 배너 바탕은 같은 크기의 그림이라 실행 중에 그림만 바꿔 끼워.
                panel = CreateImage("Panel", banner, custom, Image.Type.Sliced);
                panel.pixelsPerUnitMultiplier = custom.rect.height / (custom.pixelsPerUnit / 100f) / BannerSize.y;
                Stretch(panel.rectTransform, 0f);
            }
            else
            {
                Image edge = CreateImage("Edge", banner, slot, Image.Type.Sliced);
                edge.color = EdgeTint;
                Stretch(edge.rectTransform, 0f);
                Image fill = CreateImage("Fill", banner, slot, Image.Type.Sliced);
                fill.color = FillTint;
                Stretch(fill.rectTransform, EdgeWidth);
            }
            TMP_Text title = Label("Title", banner, outline, "3턴 도달!", 36f, Color.white, TextAlignmentOptions.Midline);
            Place(title.rectTransform, Top, Center, new Vector2(0f, -38f), new Vector2(360f, 44f));
            Gold(title);

            // [아이콘 글자] 한 줄. TurnEventBanner가 글자 길이에 맞춰 가운데로 맞춰.
            RectTransform lineRow = CreateRect("Line", banner);
            Place(lineRow, Top, Center, new Vector2(0f, -84f), new Vector2(0f, 44f));
            Image icon = CreateImage("Icon", lineRow, coin, Image.Type.Simple);
            Place(icon.rectTransform, Center, Center, Vector2.zero, new Vector2(42f, 42f));
            Image overlay = CreateImage("Overlay", icon.transform, cycle, Image.Type.Simple);
            Place(overlay.rectTransform, Center, Center, new Vector2(13f, -10f), new Vector2(26f, 26f));
            overlay.enabled = false;
            TMP_Text line = Label("Text", lineRow, outline, "속성 폭탄 해금!", 22f, Color.white, TextAlignmentOptions.MidlineLeft);
            Place(line.rectTransform, Center, LeftMiddle, Vector2.zero, new Vector2(300f, 30f));
            TMP_Text sub = Label("Sub", banner, font.material, "아이템 칸에 지급!", 17f, SubColor, TextAlignmentOptions.Midline);
            Place(sub.rectTransform, Top, Center, new Vector2(0f, -116f), new Vector2(360f, 24f));

            // 경고 배너의 빨간 느낌표: 패널 안쪽 왼편 가운데
            Image alert = CreateImage("Alert", banner, circle, Image.Type.Simple);
            alert.color = AlertTint;
            Place(alert.rectTransform, Top, Center, new Vector2(-BannerSize.x * 0.5f + 34f, -BannerSize.y * 0.5f), new Vector2(34f, 36f));
            TMP_Text alertMark = Label("Mark", alert.transform, outline, "!", 24f, Color.white, TextAlignmentOptions.Midline);
            Place(alertMark.rectTransform, Center, Center, new Vector2(0f, 1f), new Vector2(34f, 34f));
            alert.gameObject.SetActive(false);

            // 모서리 반짝이 두 개: 왼쪽 위, 오른쪽 아래
            RectTransform[] sparkles = new RectTransform[2];
            Vector2[] sparklePositions = { new Vector2(-BannerSize.x * 0.5f + 14f, -12f), new Vector2(BannerSize.x * 0.5f - 14f, -BannerSize.y + 12f) };
            for (int i = 0; i < sparkles.Length; i++)
            {
                Image image = CreateImage("Sparkle", banner, sparkle, Image.Type.Simple);
                image.color = SparkleTint;
                Place(image.rectTransform, Top, Center, sparklePositions[i], new Vector2(24f, 24f));
                image.rectTransform.localScale = Vector3.zero;
                sparkles[i] = image.rectTransform;
            }

            // 디버프 표시: 상단 띠 아래 왼쪽의 빨간 알약 [● ! 아이템 금지]. 그 턴 내내 남아.
            RectTransform chip = CreateRect("DebuffChip", root);
            Place(chip, new Vector2(0f, 1f), LeftMiddle, new Vector2(ChipLeft, -(TopHudBuilder.HudHeight + ChipGap)), ChipSize);
            chip.gameObject.AddComponent<PlayAreaBand>().Configure(PlayAreaBand.Mode.BelowTop, ChipGap);
            Image chipBack = CreateImage("Back", chip, pill, Image.Type.Sliced);
            chipBack.color = ChipTint;
            chipBack.pixelsPerUnitMultiplier = 50f / ChipSize.y;
            Stretch(chipBack.rectTransform, 0f);
            Image chipDot = CreateImage("Dot", chip, circle, Image.Type.Simple);
            Place(chipDot.rectTransform, LeftMiddle, Center, new Vector2(15f, 0f), new Vector2(18f, 19f));
            TMP_Text chipMark = Label("Mark", chipDot.transform, font.material, "!", 14f, ChipTint, TextAlignmentOptions.Midline);
            Place(chipMark.rectTransform, Center, Center, new Vector2(0f, 0.5f), new Vector2(18f, 18f));
            TMP_Text chipLabel = Label("Label", chip, outline, "아이템 금지", 15f, Color.white, TextAlignmentOptions.MidlineLeft);
            Place(chipLabel.rectTransform, LeftMiddle, LeftMiddle, new Vector2(30f, 0f), new Vector2(200f, 24f));

            // 공 절반 턴에 쉬는 공 옆에 뜨는 안내야. 실행 중에 발사 자리 옆으로 옮겨.
            TMP_Text resting = Label("Resting", root, font.material, "3개는 쉬어요", 13f, RestingColor, TextAlignmentOptions.MidlineLeft);
            Place(resting.rectTransform, Center, LeftMiddle, Vector2.zero, new Vector2(140f, 20f));

            // 날아가는 것들: 꼬리 반짝이는 아이콘 뒤에 그려.
            RectTransform[] trail = new RectTransform[TrailCount];
            for (int i = 0; i < TrailCount; i++)
            {
                Image dot = CreateImage("Trail", root, sparkle, Image.Type.Simple);
                dot.color = TrailTint;
                Place(dot.rectTransform, Center, Center, Vector2.zero, new Vector2(16f, 16f));
                trail[i] = dot.rectTransform;
            }
            RectTransform flyer = CreateRect("Flyer", root);
            Place(flyer, Center, Center, Vector2.zero, new Vector2(48f, 48f));
            Image glow = CreateImage("Glow", flyer, puff, Image.Type.Simple);
            glow.color = GlowTint;
            Place(glow.rectTransform, Center, Center, Vector2.zero, new Vector2(90f, 90f));
            Image flyerIcon = CreateImage("Icon", flyer, coin, Image.Type.Simple);
            Place(flyerIcon.rectTransform, Center, Center, Vector2.zero, new Vector2(48f, 48f));
            RectTransform[] coins = new RectTransform[CoinCount];
            for (int i = 0; i < CoinCount; i++)
            {
                Image image = CreateImage("Coin", root, coin, Image.Type.Simple);
                Place(image.rectTransform, Center, Center, Vector2.zero, new Vector2(26f, 26f));
                coins[i] = image.rectTransform;
            }
            Image burst = CreateImage("Burst", root, ring, Image.Type.Simple);
            burst.color = BurstTint;
            Place(burst.rectTransform, Center, Center, Vector2.zero, new Vector2(110f, 110f));
            TMP_Text gain = Label("Gain", root, outline, "+100", 26f, Color.white, TextAlignmentOptions.Midline);
            Place(gain.rectTransform, Center, Center, Vector2.zero, new Vector2(160f, 34f));
            Gold(gain);

            TurnEventBanner script = root.gameObject.AddComponent<TurnEventBanner>();
            SerializedObject so = new SerializedObject(script);
            Set(so, "board", board); Set(so, "launcher", launcher); Set(so, "hud", hud); Set(so, "itemSlot", itemSlot);
            Set(so, "scrim", scrim); Set(so, "rays", raysImage); Set(so, "banner", banner); Set(so, "bannerGroup", group);
            Set(so, "title", title); Set(so, "icon", icon); Set(so, "iconOverlay", overlay); Set(so, "line", line); Set(so, "sub", sub);
            Set(so, "iceIcon", ice); Set(so, "coinIcon", coin); Set(so, "brickIcon", brick); Set(so, "cycleIcon", cycle);
            Set(so, "flyer", flyer); Set(so, "flyerIcon", flyerIcon); Set(so, "burst", burst); Set(so, "gain", gain);
            SetArray(so, "sparkles", sparkles);
            SetArray(so, "trail", trail);
            SetArray(so, "coins", coins);
            Set(so, "panel", panel); Set(so, "rewardPanel", custom); Set(so, "warningPanel", warning);
            Set(so, "alert", alert.gameObject); Set(so, "vignette", vignette);
            Set(so, "chip", chip); Set(so, "chipBack", chipBack); Set(so, "chipDot", chipDot); Set(so, "chipMark", chipMark);
            Set(so, "chipLabel", chipLabel); Set(so, "restingLabel", resting);
            SerializedProperty icons = so.FindProperty("debuffIcons");
            icons.arraySize = debuffIcons.Length;
            for (int i = 0; i < debuffIcons.Length; i++)
                icons.GetArrayElementAtIndex(i).objectReferenceValue = debuffIcons[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // 편집 화면에서 게임판을 가리지 않게 모두 꺼 둬. 실행하면 TurnEventBanner가 필요할 때만 켜.
            for (int i = 0; i < root.childCount; i++)
                root.GetChild(i).gameObject.SetActive(false);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create Turn Banner");
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log("턴 보상 배너를 만들었어. " + (custom != null ? "배너 바탕은 ui_reward_banner 그림이야." : "배너 바탕 그림이 없어서 임시 바탕을 넣었어.") +
                " 확인하고 Ctrl+S로 씬을 저장해.");
            return true;
        }

        private static void Notify(string message) => EditorUtility.DisplayDialog("Turn Banner", message, "확인");

        private static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");

        // 가져오기 설정 스크립트보다 그림이 먼저 들어왔으면 9-slice 테두리가 빠져 있어서 한 번 다시 가져와.
        private static void EnsureImported(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.spriteBorder == Vector4.zero)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
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

        // 부모를 꽉 채우고 inset만큼 안쪽으로 줄여.
        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Center;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        // 배너 UI는 터치를 받지 않아.
        private static Image CreateImage(string name, Transform parent, Sprite sprite, Image.Type type)
        {
            Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? type : Image.Type.Simple;
            image.preserveAspect = sprite != null && type == Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Label(string name, Transform parent, Material material, string text, float size, Color color,
            TextAlignmentOptions alignment)
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

        // 위는 밝고 아래는 진한 금색 글자야.
        private static void Gold(TMP_Text label)
        {
            label.enableVertexGradient = true;
            label.colorGradient = new VertexGradient(GoldTop, GoldTop, GoldBottom, GoldBottom);
        }

        private static void Set(SerializedObject so, string path, Object value)
        {
            SerializedProperty property = so.FindProperty(path);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void SetArray(SerializedObject so, string path, RectTransform[] values)
        {
            SerializedProperty property = so.FindProperty(path);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
