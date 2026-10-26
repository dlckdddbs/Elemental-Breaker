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
        // 하단 띠(회수선 ~ 화면 아래)와 상단 띠 높이야(기준 해상도 480x854 픽셀). 실행 중에는 PlayAreaBand가 선에 맞춰 바꿔.
        public const float BarHeight = 148f;
        private const float HudHeight = TopHudBuilder.HudHeight;
        // 버튼 가운데는 화면 바닥에서 91, 라벨은 37. 버튼 아래 테두리의 알약(가격, 폭탄 데미지)과 라벨 사이를 띄워.
        private const float ButtonSize = 62f;
        private const float ButtonY = 91f;
        private const float LabelY = 37f;
        private static readonly Color32 DamageColor = new Color32(0xFF, 0xC2, 0x66, 0xFF);
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
        private static readonly Color Muted = new Color(1f, 1f, 1f, 0.67f);
        // 폭탄 취소 알림: 어두운 알약에 빨간 테두리와 "!" 표시. 아이템 칸 가운데에서 왼쪽으로 64만큼 나와서 좁은 폰에서도 화면 안에 들어와.
        private static readonly Vector2 ToastSize = new Vector2(252f, 36f);
        private const float ToastY = 136f;
        private const float ToastTailX = 64f;
        private static readonly Color32 ToastColor = new Color32(0x10, 0x16, 0x30, 0xF2);
        private static readonly Color32 ToastEdgeColor = new Color32(0xFF, 0x78, 0x78, 0xB3);
        private static readonly Color32 AlertColor = new Color32(0xFF, 0x6B, 0x6B, 0xFF);
        // 새로 열린 아이템의 NEW 표시와 잠긴 카드의 자물쇠 바탕이야.
        private static readonly Color32 NewTint = new Color32(0xFF, 0x4D, 0x5E, 0xFF);
        private static readonly Color32 LockBackTint = new Color32(0x14, 0x1B, 0x3A, 0xFF);
        // 아이템 금지 턴의 버튼 덮개(회색 판에 곱해져 어두운 남색 반투명)와 자물쇠 색이야.
        private static readonly Color32 BanCoverTint = new Color32(0x0A, 0x0E, 0x22, 0x9C);
        private static readonly Color32 BanLockTint = new Color32(0xFF, 0x8A, 0x80, 0xFF);

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
            Sprite coin = Load("fx_coin"), lockIcon = Load("ui_lock");
            Sprite ring = AssetDatabase.LoadAssetAtPath<Sprite>(EffectFolder + "fx_ring.png");
            Sprite[] icons = { Load("ui_icon_normal"), Load("ui_icon_fire"), Load("ui_icon_water"), Load("ui_icon_grass") };
            if (slot == null || dash == null || circle == null || pill == null || panelSprite == null || bombIcon == null || ice == null ||
                bag == null || chevron == null || cycle == null || brick == null || coin == null || ring == null || icons[0] == null ||
                lockIcon == null)
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
            // 두 띠 사이(게임판)만 덮게 실행 중에 선 위치로 맞춰.
            dim.gameObject.AddComponent<PlayAreaBand>().Configure(PlayAreaBand.Mode.Between, 0f);
            Button dimButton = AddButton(dim.gameObject, false);
            dim.transform.SetSiblingIndex(sibling++);

            // 상점 목록: 버튼 위로 펼쳐져. 위 구역은 보관형, 아래 구역은 바로 적용.
            RectTransform list = CreateRect("ShopList", canvas.transform);
            Place(list, BottomRight, new Vector2(1f, 0f), new Vector2(-14f, BarHeight + 8f), new Vector2(290f, 300f));
            list.gameObject.AddComponent<PlayAreaBand>().Configure(PlayAreaBand.Mode.AboveBottom, 8f);
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
            // NEW와 자물쇠는 아이콘보다 위에 오게 아이콘을 다 만든 뒤에 붙여.
            AddCardMarks(bombCard, pill, circle, lockIcon);
            AddCardMarks(iceCard, pill, circle, lockIcon);
            AddCardMarks(brickCard, pill, circle, lockIcon);

            // 아래 띠. 컨테이너 자체는 입력을 막지 않아서 빈 곳에서는 조준이 그대로 돼.
            RectTransform bar = CreateRect("BottomBar", canvas.transform);
            bar.anchorMin = BottomLeft; bar.anchorMax = BottomRight; bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, BarHeight); bar.anchoredPosition = Vector2.zero;
            bar.SetSiblingIndex(sibling++);
            // 높이는 실행 중에 회수선 위치로 맞춰. 바탕은 따로 칠하지 않아서 게임 배경이 그대로 이어져.
            // 컨테이너는 터치를 받지 않아서 버튼 밖 빈 곳에서도 조준과 2배속이 그대로 돼.
            bar.gameObject.AddComponent<PlayAreaBand>().Configure(PlayAreaBand.Mode.Bottom, 0f);

            // 칸 가운데: 화면 폭의 1/6, 1/2, 5/6. 라벨 세 개는 같은 줄이야.
            // 공 속성과 상점은 화면 폭 1/3씩인 칸 안의 가운데에 놓여.
            Vector2 leftColumn = new Vector2(1f / 6f, 0f);
            Vector2 buttonSize = new Vector2(ButtonSize, ButtonSize), labelRect = new Vector2(120f, 20f);

            // 왼쪽: 아이템 칸. 비어 있으면 점선과 "+", 쓸 수 있으면 금색 빛
            Image slotImage = CreateImage("ItemSlot", bar, slot, Image.Type.Sliced, true);
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
            // 폭탄 데미지: 공 속성 가격처럼 버튼 아래 테두리에 겹친 알약 [✸ 6]. 폭탄을 들고 있을 때만 보여.
            Image damage = CreateImage("Damage", slotImage.transform, pill, Image.Type.Sliced, false);
            damage.color = PriceTint;
            damage.pixelsPerUnitMultiplier = 50f / 19f;
            Place(damage.rectTransform, Center, Center, new Vector2(0f, -ButtonSize * 0.5f + 1f), new Vector2(42f, 19f));
            Image burst = CreateImage("Burst", damage.transform, Load("fx_bomb_star"), Image.Type.Simple, false);
            Place(burst.rectTransform, Center, Center, new Vector2(-9f, 0.5f), new Vector2(14f, 14f));
            TMP_Text damageLabel = Text("Amount", damage.transform, "1", 12.5f, DamageColor, true, Center, Center, new Vector2(7f, 1f), new Vector2(24f, 18f), TextAlignmentOptions.Midline);
            damage.gameObject.SetActive(false);
            TMP_Text slotLabel = Text("ItemLabel", bar, "아이템", LabelSize, Color.white, false, leftColumn, Center, new Vector2(0f, LabelY), labelRect, TextAlignmentOptions.Midline);
            CanvasGroup cancelToast = CreateCancelToast(bar, leftColumn, pill, circle);

            // 가운데: 공 속성. 버튼과 라벨을 한 칸으로 묶어서 발사 중에는 칸째 흐리게 해.
            RectTransform elementCell = CreateRect("ElementSelect", bar);
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

            // 오른쪽: 상점 (다른 버튼과 같은 파란 판, 가방 아이콘만 노란색). 공 속성처럼 버튼과 라벨을 한 칸으로 묶어서 발사 중에는 칸째 흐리게 해.
            RectTransform shopCell = CreateRect("Shop", bar);
            shopCell.anchorMin = new Vector2(2f / 3f, 0f);
            shopCell.anchorMax = new Vector2(1f, 1f);
            shopCell.offsetMin = shopCell.offsetMax = Vector2.zero;
            CanvasGroup shopGroup = shopCell.gameObject.AddComponent<CanvasGroup>();
            Image shopImage = CreateImage("ShopButton", shopCell, slot, Image.Type.Sliced, true);
            shopImage.color = ButtonTint;
            Place(shopImage.rectTransform, BottomCenter, Center, new Vector2(0f, ButtonY), buttonSize);
            Button shopButton = AddButton(shopImage.gameObject, false);
            Image bagIcon = CreateImage("Bag", shopImage.transform, bag, Image.Type.Simple, false);
            Place(bagIcon.rectTransform, Center, Center, new Vector2(0f, 3f), new Vector2(40f, 40f));
            TMP_Text shopLabel = Text("ShopLabel", shopCell, "상점", LabelSize, Color.white, false, BottomCenter, Center, new Vector2(0f, LabelY), labelRect, TextAlignmentOptions.Midline);
            // 새로 열린 아이템이 있으면 버튼 오른쪽 위 모서리에 NEW가 붙어.
            GameObject shopNew = NewTag(shopImage.transform, pill, new Vector2(27f, 28f));

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
            popover.gameObject.AddComponent<PlayAreaBand>().Configure(PlayAreaBand.Mode.AboveBottom, 4f);
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

            // 아이템 금지 턴: 세 버튼 위 자물쇠와, 누른 버튼을 가리키는 말풍선. 말풍선은 칸들보다 위에 그려.
            Button[] banLocks =
            {
                BanLock(slotImage.transform, slot, circle, lockIcon),
                BanLock(faceButtonImage.transform, slot, circle, lockIcon),
                BanLock(shopImage.transform, slot, circle, lockIcon)
            };
            CanvasGroup banToast = CreateToast("BanToast", bar, BottomLeft, new Vector2(16f, ToastY), "이번 턴은 사용할 수 없어요",
                pill, circle, out RectTransform banTail);

            // 동작 스크립트는 항상 켜져 있는 아래 띠에 붙여. 숨겨지는 버튼에 붙이면 다시 켤 수 없어.
            ItemSlotButton slotScript = bar.gameObject.AddComponent<ItemSlotButton>();
            ShopPanel shop = bar.gameObject.AddComponent<ShopPanel>();
            ElementSelectButton select = bar.gameObject.AddComponent<ElementSelectButton>();
            ItemBanLocks banScript = bar.gameObject.AddComponent<ItemBanLocks>();

            SerializedObject so = new SerializedObject(slotScript);
            Set(so, "board", board); Set(so, "launcher", launcher); Set(so, "button", slotButton);
            Set(so, "emptyFrame", empty); Set(so, "readyGlow", glow); Set(so, "itemBody", itemBody);
            Set(so, "label", slotLabel); Set(so, "iceIcon", ice);
            Set(so, "damageBadge", damage.rectTransform); Set(so, "damageLabel", damageLabel);
            Set(so, "cancelToast", cancelToast);
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
            Set(so, "shopGroup", shopGroup); Set(so, "shopButton", shopButton); Set(so, "shopLabel", shopLabel);
            Set(so, "shopNewBadge", shopNew);
            Set(so, "list", list.gameObject); Set(so, "dimOverlay", dimButton);
            SetCard(so, "bombCard", bombCard); SetCard(so, "iceCard", iceCard); SetCard(so, "brickChangeCard", brickCard);
            so.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(banScript);
            Set(so, "board", board); Set(so, "toast", banToast); Set(so, "toastTail", banTail);
            for (int i = 0; i < banLocks.Length; i++)
                so.FindProperty("locks").GetArrayElementAtIndex(i).objectReferenceValue = banLocks[i];
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
            // 카드가 흐려져도 이유("6턴에 열려요", "골드가 부족해요")는 또렷하게 읽혀.
            result.description.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
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

        // 폭탄 취소 알림 말풍선이야. 아이템 칸 위에 떠서 꼬리가 칸 가운데를 가리켜. 평소에는 꺼 두고 ItemSlotButton이 잠깐 켜.
        private static CanvasGroup CreateCancelToast(RectTransform bar, Vector2 column, Sprite pill, Sprite circle) =>
            CreateToast("CancelToast", bar, column, new Vector2(-ToastTailX, ToastY), "근처에 벽돌이 없어 취소했어요", pill, circle, out _);

        // 말풍선 하나를 만들어. anchor·position은 아래 띠 기준이고 말풍선 왼쪽 아래가 그 자리야. 꼬리는 왼쪽에서 ToastTailX에 있어.
        private static CanvasGroup CreateToast(string name, RectTransform bar, Vector2 anchor, Vector2 position, string message,
            Sprite pill, Sprite circle, out RectTransform tailRect)
        {
            RectTransform toast = CreateRect(name, bar);
            Place(toast, anchor, BottomLeft, position, ToastSize);
            CanvasGroup group = toast.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            // 빨간 테두리는 한 겹 큰 알약을 뒤에 깔아서 만들어. 꼬리가 아래 테두리를 덮어서 말풍선처럼 이어져.
            Image edge = CreateImage("Edge", toast, pill, Image.Type.Sliced, false);
            edge.color = ToastEdgeColor;
            edge.pixelsPerUnitMultiplier = 50f / (ToastSize.y + 3f);
            Place(edge.rectTransform, Center, Center, Vector2.zero, ToastSize + new Vector2(3f, 3f));
            Image tail = CreateImage("Tail", toast, null, Image.Type.Simple, false);
            tail.color = ToastColor;
            Place(tail.rectTransform, BottomLeft, Center, new Vector2(ToastTailX, 0f), new Vector2(11.3f, 11.3f));
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tailRect = tail.rectTransform;
            Image fill = CreateImage("Fill", toast, pill, Image.Type.Sliced, false);
            fill.color = ToastColor;
            fill.pixelsPerUnitMultiplier = 50f / ToastSize.y;
            Place(fill.rectTransform, Center, Center, Vector2.zero, ToastSize);
            Image alert = CreateImage("Alert", toast, circle, Image.Type.Simple, false);
            alert.color = AlertColor;
            Place(alert.rectTransform, new Vector2(0f, 0.5f), Center, new Vector2(22f, 0f), new Vector2(20f, 21.4f));
            Text("Mark", alert.transform, "!", 15f, Color.white, false, Center, Center, new Vector2(0f, 1f), new Vector2(20f, 20f), TextAlignmentOptions.Midline);
            Text("Message", toast, message, 15f, Color.white, false, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(38f, 1f), new Vector2(ToastSize.x - 44f, 24f), TextAlignmentOptions.MidlineLeft);
            toast.gameObject.SetActive(false);
            return group;
        }

        // 아이템 금지 턴에 버튼 위를 덮는 자물쇠야. 버튼과 같은 모양의 어두운 판에 자물쇠를 올리고, 누르면 금지 알림이 떠.
        // 공 속성과 상점 칸은 흐려진 상태라 그 흐림을 따르지 않고 또렷하게 보여.
        private static Button BanLock(Transform button, Sprite slot, Sprite circle, Sprite lockIcon)
        {
            Image cover = CreateImage("BanLock", button, slot, Image.Type.Sliced, true);
            cover.color = BanCoverTint;
            Place(cover.rectTransform, Center, Center, Vector2.zero, new Vector2(ButtonSize, ButtonSize));
            cover.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            Image back = CreateImage("Back", cover.transform, circle, Image.Type.Simple, false);
            back.color = LockBackTint;
            Place(back.rectTransform, Center, Center, new Vector2(0f, 2f), new Vector2(34f, 36f));
            Image glyph = CreateImage("Icon", back.transform, lockIcon, Image.Type.Simple, false);
            glyph.color = BanLockTint;
            Place(glyph.rectTransform, Center, Center, new Vector2(0f, 1.5f), new Vector2(20f, 20f));
            Button lockButton = AddButton(cover.gameObject, false);
            cover.gameObject.SetActive(false);
            return lockButton;
        }

        // 카드 오른쪽 위 NEW와 아이콘 위 자물쇠야. 둘 다 카드가 흐려져도 또렷하게 보이고, 평소에는 꺼 둬.
        private static void AddCardMarks(ShopPanel.Card card, Sprite pill, Sprite circle, Sprite lockIcon)
        {
            Transform cardTransform = card.group.transform;
            card.newTag = NewTag(cardTransform, pill, Vector2.zero);
            Place((RectTransform)card.newTag.transform, BottomLeft, Center, new Vector2(264f, 64f), new Vector2(38f, 18f));
            card.newTag.AddComponent<CanvasGroup>().ignoreParentGroups = true;

            Image lockBack = CreateImage("Lock", cardTransform, circle, Image.Type.Simple, false);
            lockBack.color = LockBackTint;
            Place(lockBack.rectTransform, BottomLeft, Center, new Vector2(54f, 16f), new Vector2(26f, 28f));
            lockBack.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            Image lockGlyph = CreateImage("Icon", lockBack.transform, lockIcon, Image.Type.Simple, false);
            lockGlyph.color = GlowColor;
            Place(lockGlyph.rectTransform, Center, Center, new Vector2(0f, 1.5f), new Vector2(15f, 15f));
            lockBack.gameObject.SetActive(false);
            card.lockBadge = lockBack.gameObject;
        }

        // [NEW] 빨간 알약이야. position은 부모 가운데 기준이고 꺼 둔 채로 만들어.
        private static GameObject NewTag(Transform parent, Sprite pill, Vector2 position)
        {
            Image tag = CreateImage("New", parent, pill, Image.Type.Sliced, false);
            tag.color = NewTint;
            tag.pixelsPerUnitMultiplier = 50f / 18f;
            Place(tag.rectTransform, Center, Center, position, new Vector2(38f, 18f));
            Text("Label", tag.transform, "NEW", 12f, Color.white, true, Center, Center, new Vector2(0f, 0.5f), new Vector2(38f, 18f), TextAlignmentOptions.Midline);
            tag.gameObject.SetActive(false);
            return tag.gameObject;
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
            Set(so, field + ".newTag", card.newTag);
            Set(so, field + ".lockBadge", card.lockBadge);
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
