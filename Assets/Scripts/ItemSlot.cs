using System;

namespace ElementalBreaker
{
    public enum ItemType { None, Bomb, Ice }

    // 아이템 칸이야. 보관형 아이템을 한 번에 하나만 들고 있어.
    public sealed class ItemSlot
    {
        public ItemType Held { get; private set; }
        public bool IsEmpty => Held == ItemType.None;
        public event Action<ItemType> Changed;

        // 칸이 비어 있을 때만 넣어. 이미 들고 있으면 실패해.
        public bool TryGive(ItemType item)
        {
            if (item == ItemType.None || !IsEmpty)
                return false;
            Held = item;
            Changed?.Invoke(Held);
            return true;
        }

        public ItemType Take()
        {
            ItemType item = Held;
            if (item == ItemType.None)
                return item;
            Held = ItemType.None;
            Changed?.Invoke(Held);
            return item;
        }
    }
}
