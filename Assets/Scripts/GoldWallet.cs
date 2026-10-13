using System;

namespace ElementalBreaker
{
    // 보유 골드를 관리해. 벽돌을 깨면 늘고, 나중에 상점에서 쓰면 줄어.
    public sealed class GoldWallet
    {
        public int Amount { get; private set; }
        public event Action<int> Changed;

        public void Add(int amount)
        {
            if (amount <= 0)
                return;
            Amount += amount;
            Changed?.Invoke(Amount);
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0 || amount > Amount)
                return false;
            Amount -= amount;
            Changed?.Invoke(Amount);
            return true;
        }
    }
}
