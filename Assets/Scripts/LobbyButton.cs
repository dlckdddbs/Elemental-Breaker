using UnityEngine;
using UnityEngine.UI;

namespace ElementalBreaker
{
    public enum LobbyButtonStyle
    {
        // 지금 누르길 권하는 버튼이야. 파란 바탕에 테두리가 조금 더 밝아.
        Strong,
        // 어두운 남색 바탕에 옅은 테두리야.
        Calm,
        // 누를 수 없어. 남색 버튼을 흐리게 보여 줘.
        Disabled,
        // 확인 창에서 되돌릴 수 없는 쪽(기록 지우기 등)이야. 차분한 빨간색이야.
        Danger
    }

    // 로비 버튼 하나의 모양이야. 두 버튼은 둥글기·테두리·글자가 같고 바탕과 테두리 색만 바뀌어.
    public sealed class LobbyButton : MonoBehaviour
    {
        // 게임 안에서 누를 수 없는 버튼과 같은 흐림 정도야.
        private const float DisabledAlpha = 0.4f;

        [SerializeField] private Button button;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image fill;
        [SerializeField] private Image border;
        [Header("강조")]
        [SerializeField] private Color strongFill = new Color32(0x31, 0x56, 0xA6, 0xFF);
        [SerializeField] private Color strongBorder = new Color32(0xAA, 0xC8, 0xFF, 0x9E);
        [Header("차분")]
        [SerializeField] private Color calmFill = new Color32(0x12, 0x1C, 0x3A, 0xCC);
        [SerializeField] private Color calmBorder = new Color32(0xA0, 0xBE, 0xFF, 0x4D);
        [Header("위험")]
        [SerializeField] private Color dangerFill = new Color32(0xA8, 0x40, 0x4A, 0xFF);
        [SerializeField] private Color dangerBorder = new Color32(0xFF, 0xAF, 0xAF, 0x80);

        public Button Button => button;

        public void SetStyle(LobbyButtonStyle style)
        {
            bool strong = style == LobbyButtonStyle.Strong;
            bool danger = style == LobbyButtonStyle.Danger;
            bool usable = style != LobbyButtonStyle.Disabled;
            if (fill != null)
                fill.color = strong ? strongFill : danger ? dangerFill : calmFill;
            if (border != null)
                border.color = strong ? strongBorder : danger ? dangerBorder : calmBorder;
            if (group != null)
                group.alpha = usable ? 1f : DisabledAlpha;
            if (button != null)
                button.interactable = usable;
        }
    }
}
