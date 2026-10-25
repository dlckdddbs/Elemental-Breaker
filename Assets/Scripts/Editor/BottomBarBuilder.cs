using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker.Editor
{
    // 지금 열린 씬의 Canvas 아래에 아래 띠([아이템 칸] [공 속성] [상점])와 상점 목록, 공 속성 선택지를 한 번 만들어.
    // Ctrl+Z로 되돌릴 수 있고, 씬 저장은 직접 해.
    public static class BottomBarBuilder
    {
        private const string SpriteFolder = "Assets/Art/Casual/Sprites/";
        private const string EffectFolder = "Assets/Art/Casual/Effects/";
        private const string FontPath = "Assets/Font/Jua/Jua-Regular UI SDF.asset";
        private const string OutlinePath = "Assets/Font/Jua/Jua-Regular UI SDF Outline.mat";
        // 아래 띠 높이와 상단 정보판 영역 높이야(기준 해상도 480x854 픽셀).
        public const float BarHeight = 124f;
        private const float HudHeight = TopHudBuilder.HudHeight;
        // 아래 띠의 판은 양옆 10, 아래 8, 위 4 여백을 둬. 판 안에서 버튼 가운데는 바닥에서 70, 라벨은 17.
        private const float ButtonSize = 62f;
        private const float ButtonY = 70f;
        private const float LabelY = 17f;
        private const float LabelSize = 15f;

        // 회색 판(밝기 225)에 곱해져서 화면에서 원하는 색으로 보이는 값이야.
        private static readonly Color32 CardTint = new Color32(0x3B, 0x49, 0x8B, 0xFF);
        private static readonly Color32 PopoverTint = new Color32(0x35, 0x42, 0x7F, 0xFF);
        private static readonly Color32 IconBackTint = new Color32(0x22, 0x2A, 0x54, 0xFF);
        private static readonly Color32 PriceTint = new Color32(0x17, 0x20, 0x3A, 0xFF);
        private static readonly Color32 GlowColor = new Color32(0xFF, 0xE4, 0x8A, 0xFF);
        private static readonly Color32 DimColor = new Color32(0x0B, 0x0E, 0x1F, 0x8C);
        // 아래 띠 버튼 세 개의 파란 판이야. 회색 판(밝기 225)에 곱해져.
        private static readonly Color32 ButtonTint = new Color32(0x3C, 0x66, 0xE6, 0xFF);
        private static readonly Color32 DividerColor = new Color32(0xA0, 0xBE, 0xFF, 0x2E);
        private static readonly Color Muted = new Color(1f, 1f, 1f, 0.67f);

        private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        private static readonly Vector2 BottomRight = new Vector2(1f, 0f);
        private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private static TMP_FontAsset font;
        private static Material outline;

        // 지금 Canvas 아래에 이 이름의 UI가 있으면 아래 띠와 상점이 이미 있는 거야.
        public static readonly string[] CreatedNames = { "BottomBar", "ShopOverlay", "ShopList", "ElementBlocker", "ElementPopover" };

        [MenuItem("Elemental Breaker/Create Bottom Bar & Shop")]
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
            if (canvas.transform.Find("BottomBar") != null)
            {
                Notify("이미 BottomBar가 있어. 다시 만들려면 BottomBar, ShopOverlay, ShopList, ElementBlocker, ElementPopover를 지운 뒤 실행해.");
                return false;
            }
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            outline = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
            if (font == null || outline == null)
            {
                Notify("한글 UI 폰트나 외곽선 재질을 찾지 못했어. 먼저 Create Top HUD를 실행했는지 확인해.");
                return false;
            }
            EnsureImported(SpriteFolder + "ui_slot.png");
            EnsureImported(SpriteFolder + "ui_panel.png");
            Sprite slot = Load("ui_slot"), dash = Load("ui_slot_dash"), circle = Load("ui_circle"), pill = Load("ui_pill");
            Sprite panelSprite = Load("ui_panel");
            // 폭탄은 실행 중에 이번 턴 속성의 아이콘으로 바뀌어. 처음에는 일반 폭탄을 넣어 둬.
            Sprite bombIcon = Load("ui_bomb_normal"), ice = Load("ui_ice");
            Sprite bag = Load("ui_bag"), chevron = Load("ui_chevron"), cycle = Load("ui_cycle_arrow"), brick = Load("brick_base");
            Sprite coin = Load("fx_coin");
            Sprite ring = AssetDatabase.LoadAssetAtPath<Sprite>(EffectFolder + "fx_ring.png");
            Sprite[] icons = { Load("ui_icon_normal"), Load("ui_icon_fire"), Load("ui_icon_water"), Load("ui_icon_grass") };
            if (slot == null || dash == null || circle == null || pill == null || panelSprite == null || bombIcon == null || ice == null ||
                bag == null || chevron == null || cycle == null || brick == null || coin == null || ring == null || icons[0] == null)
            {
                Notify("UI 이미지 일부를 찾지 못했어. Assets/Art/Casual을 확인해.");
                return false;
            }

            Transform hud = canvas.transform.Find("TopHUD");
            int sibling = hud != null ? hud.GetSiblingIndex() + 1 : 0;

            // 상점이 열렸을 때 게임 화면을 어둡게 덮어. 누르면 닫혀.
            Image dim = CreateImage("ShopOverlay", canvas.transform, null, Image.Type.Simple, true);
            RectTransform dimRect = dim.rectTransform;
            dimRect.anchorMin = Vector2.zero; dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = new Vector2(0f, BarHeight); dimRect.offsetMax = new Vector2(0f, -HudHeight);
            dim.color = DimColor;
            Button dimButton = AddButton(dim.gameObject, false);
            dim.transform.SetSiblingIndex(sibling++);

            // 상점 목록: 버튼 위로 펼쳐져. 위 구역은 보관형, 아래 구역은 바로 적용.
            RectTransform list = CreateRect("ShopList", canvas.transform);
            Place(list, BottomRight, new Vector2(1f, 0f), new Vector2(-14f, BarHeight + 8f), new Vector2(290f, 300f));
            list.SetSiblingIndex(sibling++);
            // 제목과 카드 전체를 판 위에 올려서 뒤의 벽돌과 겹쳐도 잘 읽혀. 판을 눌러도 상점이 닫히지 않아.
            Image listBack = CreateImage("Back", list, panelSprite, Image.Type.Sliced, true);
            listBack.rectTransform.anchorMin = Vector2.zero;
            listBack.rectTransform.anchorMax = Vector2.one;
            listBack.rectTransform.offsetMin = new Vector2(-8f, -8f);
            listBack.rectTransform.offsetMax = new Vector2(8f, 12f);
            Text("Title", list, "상점", 20f, Color.white, true, BottomLeft, new Vector2(0f, 0.5f), new Vector2(4f, 289f), new Vector2(120f, 24f), TextAlignmentOptions.MidlineLeft);
            Text("Hint", list, "발사 전에만 살 수 있어요", 12.5f, Muted, false, BottomLeft, new Vector2(1f, 0.5f), new Vector2(288f, 289f), new Vector2(170f, 20f), TextAlignmentOptions.MidlineRight);
            Text("StoredSection", list, "아이템 칸에 보관", 13f, Muted, false, BottomLeft, new Vector2(0f, 0.5f), new Vector2(6f, 260f), new Vector2(200f, 18f), TextAlignmentOptions.MidlineLeft);
            ShopPanel.Card bombCard = CreateCard("BombCard", list, 176f, "속성 폭탄", slot, circle, pill, coin);
            bombCard.iconBody = CreateImage("Body", bombCard.group.transform, bombIcon, Image.Type.Simple, false);
            Place(bombCard.iconBody.rectTransform, BottomLeft, Center, new Vector2(36f, 31f), new Vector2(46f, 46f));
            ShopPanel.Card iceCard = CreateCard("IceCard", list, 102f, "얼음", slot, circle, pill, coin);
            iceCard.iconBody = CreateImage("Body", iceCard.group.transform, ice, Image.Type.Simple, false);
            Place(iceCard.iconBody.rectTransform, BottomLeft, Center, new Vector2(36f, 31f), new Vector2(40f, 40f));
            Text("InstantSection", list, "바로 적용", 13f, Muted, false, BottomLeft, new Vector2(0f, 0.5f), new Vector2(6f, 84f), new Vector2(200f, 18f), TextAlignmentOptions.MidlineLeft);
            ShopPanel.Card brickCard = CreateCard("BrickChangeCard", list, 0f, "벽돌 속성 변경", slot, circle, pill, coin);
            brickCard.iconBody = CreateImage("Brick", brickCard.group.transform, brick, Image.Type.Sliced, false);
            brickCard.iconBody.pixelsPerUnitMultiplier = 3f;
            Place(brickCard.iconBody.rectTransform, BottomLeft, Center, new Vector2(36f, 30f), new Vector2(34f, 25f));
            brickCard.iconBadge = CreateImage("Badge", brickCard.group.transform, icons[1], Image.Type.Simple, false);
            Place(brickCard.iconBadge.rectTransform, BottomLeft, Center, new Vector2(28f, 35f), new Vector2(12f, 12f));
            brickCard.iconOverlay = CreateImage("Cycle", brickCard.group.transform, cycle, Image.Type.Simple, false);
            Place(brickCard.iconOverlay.rectTransform, BottomLeft, Center, new Vector2(36f, 31f), new Vector2(46f, 46f));

            // 아래 띠. 컨테이너 자체는 입력을 막지 않아서 빈 곳에서는 조준이 그대로 돼.
            RectTransform bar = CreateRect("BottomBar", canvas.transform);
            bar.anchorMin = BottomLeft; bar.anchorMax = BottomRight; bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, BarHeight); bar.anchoredPosition = Vector2.zero;
            bar.SetSiblingIndex(sibling++);

            // 판: 버튼 세 개를 감싸. 터치는 받지 않아서 판 위 빈 곳에서도 조준과 2배속이 그대로 돼.
            Image barPanel = CreateImage("Panel", bar, panelSprite, Image.Type.Sliced, false);
            RectTransform panel = barPanel.rectTransform;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = new Vector2(10f, 8f);
            panel.offsetMax = new Vector2(-10f, -4f);
            Divider(panel, 1f / 3f);
            Divider(panel, 2f / 3f);
            // 칸 가운데: 판 폭의 1/6, 1/2, 5/6. 라벨 세 개는 같은 줄이야.
            Vector2 leftColumn = new Vector2(1f / 6f, 0f), rightColumn = new Vector2(5f / 6f, 0f);
            Vector2 buttonSize = new Vector2(ButtonSize, ButtonSize), labelRect = new Vector2(120f, 20f);

            // 왼쪽: 아이템 칸. 비어 있으면 점선과 "+", 쓸 수 있으면 금색 빛
            Image slotImage = CreateImage("ItemSlot", panel, slot, Image.Type.Sliced, true);
            slotImage.color = ButtonTint;
            Place(slotImage.rectTransform, leftColumn, Center, new Vector2(0f, ButtonY), buttonSize);
            Button slotButton = AddButton(slotImage.gameObject, false);
            Image glow = CreateImage("ReadyGlow", slotImage.transform, ring, Image.Type.Simple, false);
            glow.color = GlowColor;
            Place(glow.rectTransform, Center, Center, new Vector2(0f, 2f), new Vector2(78f, 78f));
            Image empty = CreateImage("Empty", slotImage.transform, dash, Image.Type.Simple, false);
            Place(empty.rectTransform, Center, Center, new Vector2(0f, 2.5f), new Vector2(46f, 45f));
            Text("Plus", empty.transform, "+", 24f, new Color(1f, 1f, 1f, 0.5f), false, Center, Center, new Vector2(0f, 1f), new Vector2(30f, 30f), TextAlignmentOptions.Midline);
            Image itemBody = CreateImage("ItemBody", slotImage.transform, bombIcon, Image.Type.Simple, false);
            Place(itemBody.rectTransform, Center, Center, new Vector2(0f, 2.5f), new Vector2(45f, 45f));
            TMP_Text slotLabel = Text("ItemLabel", panel, "아이템", LabelSize, Color.white, false, leftColumn, Center, new Vector2(0f, LabelY), labelRect, TextAlignmentOptions.Midline);

            // 가운데: 공 속성. 버튼과 라벨을 한 칸으로 묶어서 발사 중에는 칸째 흐리게 해.
            RectTransform elementCell = CreateRect("ElementSelect", panel);
            elementCell.anchorMin = new Vector2(1f / 3f, 0f);
            elementCell.anchorMax = new Vector2(2f / 3f, 1f);
            elementCell.offsetMin = elementCell.offsetMax = Vector2.zero;
            CanvasGroup elementGroup = elementCell.gameObject.AddComponent<CanvasGroup>();
            Image faceButtonImage = CreateImage("Button", elementCell, slot, Image.Type.Sliced, true);
            faceButtonImage.color = ButtonTint;
            Place(faceButtonImage.rectTransform, BottomCenter, Center, new Vector2(0f, ButtonY), buttonSize);
            Button faceButton = AddButton(faceButtonImage.gameObject, false);
            Image face = CreateImage("Circle", faceButtonImage.transform, circle, Image.Type.Simple, false);
            face.color = ElementRules.GetBrickTint(ElementType.Normal);
            Place(face.rectTransform, Center, Center, new Vector2(0f, 4f), new Vector2(40f, 43f));
            Image faceIcon = CreateImage("Icon", face.transform, icons[0], Image.Type.Simple, false);
            Place(faceIcon.rectTransform, Center, Center, new Vector2(0f, 1f), new Vector2(23f, 23f));
            Image chev = CreateImage("Chevron", faceButtonImage.transform, chevron, Image.Type.Simple, false);
            Place(chev.rectTransform, Center, Center, new Vector2(21f, 21f), new Vector2(16f, 16f));
            // 가격은 버튼 아래 테두리에 겹쳐서, 라벨 줄이 다른 버튼과 같아.
            TMP_Text facePrice = PricePill(faceButtonImage.transform, new Vector2(0f, -ButtonSize * 0.5f + 1f), new Vector2(42f, 19f), pill, coin, 12.5f);
            Text("Label", elementCell, "공 속성", LabelSize, Color.white, false, BottomCenter, Center, new Vector2(0f, LabelY), labelRect, TextAlignmentOptions.Midline);

            // 오른쪽: 상점 버튼 (다른 버튼과 같은 파란 판, 가방 아이콘만 노란색)
            Image shopImage = CreateImage("ShopButton", panel, slot, Image.Type.Sliced, true);
            shopImage.color = ButtonTint;
            Place(shopImage.rectTransform, rightColumn, Center, new Vector2(0f, ButtonY), buttonSize);
            Button shopButton = AddButton(shopImage.gameObject, false);
            Image bagIcon = CreateImage("Bag", shopImage.transform, bag, Image.Type.Simple, false);
            Place(bagIcon.rectTransform, Center, Center, new Vector2(0f, 3f), new Vector2(40f, 40f));
            TMP_Text shopLabel = Text("ShopLabel", panel, "상점", LabelSize, Color.white, false, rightColumn, Center, new Vector2(0f, LabelY), labelRect, TextAlignmentOptions.Midline);

            // 공 속성 선택지: 바깥을 누르면 닫히는 투명 막 + 버튼 위로 펼쳐지는 창
            Image blocker = CreateImage("ElementBlocker", canvas.transform, null, Image.Type.Simple, true);
            RectTransform blockerRect = blocker.rectTransform;
            blockerRect.anchorMin = Vector2.zero; blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero; blockerRect.offsetMax = Vector2.zero;
            blocker.color = new Color(0f, 0f, 0f, 0f);
            Button blockerButton = AddButton(blocker.gameObject, false);
            blocker.transform.SetSiblingIndex(sibling++);
            Image popover = CreateImage("ElementPopover", canvas.transform, slot, Image.Type.Sliced, true);
            popover.color = PopoverTint;
            Place(popover.rectTransform, BottomCenter, new Vector2(0.5f, 0f), new Vector2(0f, BarHeight + 4f), new Vector2(268f, 96f));
            popover.transform.SetSiblingIndex(sibling++);
            string[] names = { "일반", "불", "물", "풀" };
            Button[] options = new Button[4];
            Image[] rings = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2 pos = new Vector2(-97.5f + i * 65f, 12f);
                rings[i] = CreateImage("Selected_" + i, popover.transform, circle, Image.Type.Simple, false);
                Place(rings[i].rectTransform, Center, Center, pos, new Vector2(54f, 58f));
                Image option = CreateImage("Option_" + i, popover.transform, circle, Image.Type.Simple, true);
                option.color = ElementRules.GetBrickTint((ElementType)i);
                Place(option.rectTransform, Center, Center, pos, new Vector2(44f, 47f));
                options[i] = AddButton(option.gameObject, true);
                Image optionIcon = CreateImage("Icon", option.transform, icons[i], Image.Type.Simple, false);
                Place(optionIcon.rectTransform, Center, Center, new Vector2(0f, 1.5f), new Vector2(28f, 28f));
                Text("Name", popover.transform, names[i], 13f, Color.white, true, Center, Center, pos + new Vector2(0f, -35f), new Vector2(60f, 18f), TextAlignmentOptions.Midline);
            }

            // 동작 스크립트는 항상 켜져 있는 아래 띠에 붙여. 숨겨지는 버튼에 붙이면 다시 켤 수 없어.
            ItemSlotButton slotScript = bar.gameObject.AddComponent<ItemSlotButton>();
            ShopPanel shop = bar.gameObject.AddComponent<ShopPanel>();
            ElementSelectButton select = bar.gameObject.AddComponent<ElementSelectButton>();

            SerializedObject so = new SerializedObject(slotScript);
            Set(so, "board", board); Set(so, "launcher", launcher); Set(so, "button", slotButton);
            Set(so, "emptyFrame", empty); Set(so, "readyGlow", glow); Set(so, "itemBody", itemBody);
            Set(so, "label", slotLabel); Set(so, "iceIcon", ice);
            so.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(select);
            Set(so, "board", board); Set(so, "launcher", launcher); Set(so, "shop", shop);
            Set(so, "buttonGroup", elementGroup); Set(so, "button", faceButton); Set(so, "face", face); Set(so, "icon", faceIcon);
            Set(so, "priceLabel", facePrice); Set(so, "popover", popover.gameObject); Set(so, "closeBlocker", blockerButton);
            for (int i = 0; i < 4; i++)
            {
                so.FindProperty("options").GetArrayElementAtIndex(i).objectReferenceValue = options[i];
                so.FindProperty("selectedRings").GetArrayElementAtIndex(i).objectReferenceValue = rings[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(shop);
            Set(so, "board", board); Set(so, "launcher", launcher); Set(so, "elementSelect", select);
            Set(so, "shopButton", shopButton); Set(so, "shopFace", shopImage); Set(so, "shopLabel", shopLabel);
            Set(so, "list", list.gameObject); Set(so, "dimOverlay", dimButton);
            SetCard(so, "bombCard", bombCard); SetCard(so, "iceCard", iceCard); SetCard(so, "brickChangeCard", brickCard);
            so.ApplyModifiedPropertiesWithoutUndo();

            dim.gameObject.SetActive(false);
            list.gameObject.SetActive(false);
            blocker.gameObject.SetActive(false);
            popover.gameObject.SetActive(false);
            glow.enabled = false;
            itemBody.enabled = false;

            Undo.RegisterCreatedObjectUndo(dim.gameObject, "Create Bottom Bar & Shop");
            Undo.RegisterCreatedObjectUndo(list.gameObject, "Create Bottom Bar & Shop");
            Undo.RegisterCreatedObjectUndo(bar.gameObject, "Create Bottom Bar & Shop");
            Undo.RegisterCreatedObjectUndo(blocker.gameObject, "Create Bottom Bar & Shop");
            Undo.RegisterCreatedObjectUndo(popover.gameObject, "Create Bottom Bar & Shop");
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = bar.gameObject;
            Debug.Log("아래 띠와 상점을 만들었어. 확인하고 Ctrl+S로 씬을 저장해. 되돌리려면 Ctrl+Z.");
            return true;
        }

        // 판 안의 세로 구분선이야. x는 판 폭 대비 위치야.
        private static void Divider(RectTransform panel, float x)
        {
            Image line = CreateImage("Divider", panel, null, Image.Type.Simple, false);
            line.color = DividerColor;
            Place(line.rectTransform, new Vector2(x, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(1f, 88f));
        }

        private static ShopPanel.Card CreateCard(string name, Transform list, float y, string title, Sprite panel, Sprite circle, Sprite pill, Sprite coin)
        {
            Image card = CreateImage(name, list, panel, Image.Type.Sliced, true);
            card.color = CardTint;
            Place(card.rectTransform, BottomLeft, BottomLeft, new Vector2(0f, y), new Vector2(290f, 66f));
            ShopPanel.Card result = new ShopPanel.Card { group = card.gameObject.AddComponent<CanvasGroup>() };
            Image back = CreateImage("IconBack", card.transform, circle, Image.Type.Simple, false);
            back.color = IconBackTint;
            Place(back.rectTransform, BottomLeft, Center, new Vector2(36f, 31f), new Vector2(50f, 53f));
            Text("Name", card.transform, title, 19f, Color.white, true, BottomLeft, new Vector2(0f, 0.5f), new Vector2(70f, 44f), new Vector2(150f, 26f), TextAlignmentOptions.MidlineLeft);
            result.description = Text("Description", card.transform, "", 12.5f, Muted, false, BottomLeft, new Vector2(0f, 0.5f), new Vector2(70f, 21f), new Vector2(160f, 20f), TextAlignmentOptions.MidlineLeft);
            Image price = CreateImage("Price", card.transform, pill, Image.Type.Sliced, true);
            price.color = PriceTint;
            price.pixelsPerUnitMultiplier = 50f / 32f;
            Place(price.rectTransform, BottomLeft, Center, new Vector2(254f, 33f), new Vector2(54f, 32f));
            result.buyButton = AddButton(price.gameObject, true);
            Image coinImage = CreateImage("Coin", price.transform, coin, Image.Type.Simple, false);
            Place(coinImage.rectTransform, Center, Center, new Vector2(-11f, 1.5f), new Vector2(20f, 20f));
            result.priceLabel = Text("Amount", price.transform, "1", 18f, Color.white, true, Center, Center, new Vector2(10f, 1.5f), new Vector2(26f, 24f), TextAlignmentOptions.Midline);
            return result;
        }

        private static TMP_Text PricePill(Transform parent, Vector2 position, Vector2 size, Sprite pill, Sprite coin, float fontSize)
        {
            Image price = CreateImage("Price", parent, pill, Image.Type.Sliced, false);
            price.color = PriceTint;
            price.pixelsPerUnitMultiplier = 50f / size.y;
            Place(price.rectTransform, Center, Center, position, size);
            Image coinImage = CreateImage("Coin", price.transform, coin, Image.Type.Simple, false);
            Place(coinImage.rectTransform, Center, Center, new Vector2(-9f, 1f), new Vector2(15f, 15f));
            return Text("Amount", price.transform, "1", fontSize, Color.white, true, Center, Center, new Vector2(7f, 1f), new Vector2(22f, 20f), TextAlignmentOptions.Midline);
        }

        private static void SetCard(SerializedObject so, string field, ShopPanel.Card card)
        {
            Set(so, field + ".group", card.group);
            Set(so, field + ".buyButton", card.buyButton);
            Set(so, field + ".description", card.description);
            Set(so, field + ".priceLabel", card.priceLabel);
            Set(so, field + ".iconBody", card.iconBody);
            Set(so, field + ".iconOverlay", card.iconOverlay);
            Set(so, field + ".iconBadge", card.iconBadge);
        }

        private static void Set(SerializedObject so, string path, Object value)
        {
            SerializedProperty property = so.FindProperty(path);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void Notify(string message) => EditorUtility.DisplayDialog("Bottom Bar & Shop", message, "확인");

        private static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");

        private static void EnsureImported(string path)
        {
            // 가져오기 설정 스크립트보다 이미지가 먼저 들어왔으면 9-slice 테두리가 빠져 있어서 한 번 다시 가져와.
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

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Image.Type type, bool raycast)
        {
            Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? type : Image.Type.Simple;
            image.preserveAspect = sprite != null && type == Image.Type.Simple;
            image.raycastTarget = raycast;
            return image;
        }

        // 버튼 색 변화는 스크립트가 직접 정해. colorTint는 공 속성 선택지처럼 흐리게만 보이면 될 때 써.
        private static Button AddButton(GameObject target, bool colorTint)
        {
            Button button = target.AddComponent<Button>();
            button.transition = colorTint ? Selectable.Transition.ColorTint : Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            return button;
        }

        private static TMP_Text Text(string name, Transform parent, string text, float size, Color color, bool outlined,
            Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 rectSize, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI label = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSharedMaterial = outlined ? outline : font.material;
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
