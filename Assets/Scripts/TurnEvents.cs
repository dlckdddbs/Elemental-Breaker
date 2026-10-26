using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElementalBreaker
{
    // 상점에서 파는 것들이야. 이 순서대로 열려.
    public enum ShopItem { Bomb, Ice, BrickChange }

    public enum Debuff { None, DoubleHealth, NoItems, DoubleDescent, HalfBalls }

    public enum TurnEventKind { Unlock, Gold, Debuff }

    [Serializable]
    public sealed class TurnEventSettings
    {
        [Tooltip("상점 아이템이 이 턴 간격으로 하나씩 열려(폭탄 → 얼음 → 벽돌 속성 변경). 열릴 때 하나를 바로 받아.")]
        [Min(1)] public int unlockInterval = 3;
        [Tooltip("이 턴마다 골드를 줘(10이면 10, 20, 30턴...). 0이면 안 줘.")]
        [Min(0)] public int goldInterval = 10;
        [Tooltip("골드 보상 = 턴 × 이 값. 10이면 10턴에 100, 20턴에 200이야.")]
        [Min(0)] public int goldPerTurn = 10;
        [Tooltip("첫 디버프 턴이야. 여기서부터 디버프 간격마다 체력 2배 → 아이템 금지 → 두 칸 하강 → 공 절반 순서로 돌아.")]
        [Min(1)] public int debuffStartTurn = 4;
        [Tooltip("디버프 간격이야. 0이면 디버프가 없어.")]
        [Min(0)] public int debuffInterval = 10;
        [Tooltip("테스트용 시작 턴이야. 1이면 처음부터 해. 그 전에 열렸어야 할 아이템은 받지 않고 열린 채로 시작해.")]
        [Min(1)] public int startTurn = 1;
    }

    // 턴이 시작될 때 생기는 일 하나야. 해금 아이템을 받는 방법은 목록을 만들 때 미리 정해서 배너가 보여 줄 수 있어.
    public sealed class TurnEvent
    {
        public TurnEventKind Kind { get; private set; }
        public int Turn { get; private set; }
        public ShopItem Item { get; private set; }
        public Debuff Debuff { get; private set; }
        // 골드 보상 액수, 또는 해금 아이템을 칸이 차 있어서 대신 받은 골드야.
        public int Gold { get; internal set; }
        // 해금: 보관형은 아이템 칸에 들어왔고, 벽돌 속성 변경은 바로 발동했어. false면 Gold로 대신 받았어.
        public bool Granted { get; internal set; }
        // 해금(벽돌 속성 변경): 바뀐 벽돌 속성이야.
        public ElementType BrickElement { get; internal set; }
        public bool IsApplied { get; internal set; }

        public static TurnEvent Unlock(int turn, ShopItem item) => new TurnEvent { Kind = TurnEventKind.Unlock, Turn = turn, Item = item };
        public static TurnEvent Reward(int turn, int gold) => new TurnEvent { Kind = TurnEventKind.Gold, Turn = turn, Gold = gold };
        public static TurnEvent Warning(int turn, Debuff debuff) => new TurnEvent { Kind = TurnEventKind.Debuff, Turn = turn, Debuff = debuff };
    }

    // 턴 보상(해금, 골드)과 디버프 일정, 그리고 이번 턴 규칙을 맡아.
    // 새 턴이 시작되면 보드가 Prepare(규칙을 먼저 켜고 이벤트 목록을 만듦) → 새 줄 생성 → Play(차례로 보여 주며 적용) 순서로 불러.
    // 디버프는 그 턴이 시작될 때 켜져서 다음 턴이 시작될 때 꺼져. 두 칸 하강은 그 턴이 끝날 때 보드가 확인해.
    public sealed class TurnEvents
    {
        private static readonly ShopItem[] UnlockOrder = { ShopItem.Bomb, ShopItem.Ice, ShopItem.BrickChange };
        private static readonly Debuff[] DebuffOrder = { Debuff.DoubleHealth, Debuff.NoItems, Debuff.DoubleDescent, Debuff.HalfBalls };

        private readonly BrickBoard board;
        private readonly BallLauncher launcher;
        private readonly TurnEventSettings settings;
        private readonly bool[] unlocked = new bool[UnlockOrder.Length];
        private readonly List<TurnEvent> current = new List<TurnEvent>(3);
        private ShopPanel shop;
        private int playIndex;
        private Action onFinished;
        // 체력 2배는 "이번에 나오는 벽돌"이라서, 얼음 턴처럼 새 줄이 안 생기면 다음에 실제로 생기는 줄까지 기다려.
        private bool pendingDoubleHealth;

        public TurnEvents(BrickBoard board, BallLauncher launcher, TurnEventSettings settings)
        {
            this.board = board;
            this.launcher = launcher;
            this.settings = settings ?? new TurnEventSettings();
        }

        public int StartTurn => Mathf.Max(1, settings.startTurn);
        public Debuff ActiveDebuff { get; private set; }
        public bool ItemsBanned => ActiveDebuff == Debuff.NoItems;
        // 이번 턴 이벤트 목록이야. 배너가 읽어.
        public IReadOnlyList<TurnEvent> Current => current;

        // 이벤트를 화면에 보여 주는 쪽(배너)이야. (이벤트, 적용, 끝)을 받아서 알맞은 순간에 적용을 부르고, 다 보여 주면 끝을 불러.
        // 아직 없으면 바로 적용하고 넘어가.
        public Action<TurnEvent, Action, Action> Presenter { get; set; }

        public bool IsUnlocked(ShopItem item) => unlocked[(int)item];
        public int UnlockTurn(ShopItem item) => (Array.IndexOf(UnlockOrder, item) + 1) * Mathf.Max(1, settings.unlockInterval);

        // 테스트용 시작 턴에서 시작할 때야. 그 전에 열렸어야 할 아이템은 받지 않고 열린 채로 둬.
        public void SkipTo(int turn)
        {
            for (int i = 0; i < UnlockOrder.Length; i++)
                unlocked[(int)UnlockOrder[i]] = UnlockTurn(UnlockOrder[i]) < turn;
        }

        // 새 턴의 규칙을 켜고 이벤트 목록을 만들어. 새 줄을 만들기 전에 불러야 체력 2배가 이번 줄에 들어가.
        public void Prepare(int turn)
        {
            current.Clear();
            ActiveDebuff = DebuffAt(turn);
            if (ActiveDebuff == Debuff.DoubleHealth)
                pendingDoubleHealth = true;
            launcher.SetLaunchHalf(ActiveDebuff == Debuff.HalfBalls);

            // 해금 아이템을 어떻게 받을지는 여기서 미리 정해. 배너가 보여 주기 전에 알아야 하고,
            // 이벤트가 끝날 때까지 조작이 막혀 있어서 그사이 아이템 칸이 바뀌지 않아.
            bool slotFree = board.Items.IsEmpty;
            for (int i = 0; i < UnlockOrder.Length; i++)
            {
                if (IsUnlocked(UnlockOrder[i]) || UnlockTurn(UnlockOrder[i]) != turn)
                    continue;
                TurnEvent unlock = TurnEvent.Unlock(turn, UnlockOrder[i]);
                PlanUnlock(unlock, ref slotFree);
                current.Add(unlock);
            }
            if (settings.goldInterval > 0 && turn % settings.goldInterval == 0)
                current.Add(TurnEvent.Reward(turn, turn * settings.goldPerTurn));
            if (ActiveDebuff != Debuff.None)
                current.Add(TurnEvent.Warning(turn, ActiveDebuff));
        }

        // 새 줄이 체력 2배인지야. 한 번 쓰면 꺼져.
        public bool TakeDoubleHealth()
        {
            bool doubled = pendingDoubleHealth;
            pendingDoubleHealth = false;
            return doubled;
        }

        // 이번 턴 이벤트를 차례로 보여 주고 적용해. 다 끝나면 finished를 불러.
        public void Play(Action finished)
        {
            onFinished = finished;
            playIndex = 0;
            PlayNext();
        }

        private void PlayNext()
        {
            if (playIndex >= current.Count)
            {
                Action finished = onFinished;
                onFinished = null;
                finished?.Invoke();
                return;
            }
            TurnEvent turnEvent = current[playIndex++];
            if (Presenter == null)
            {
                Apply(turnEvent);
                PlayNext();
                return;
            }
            // 보여 주는 쪽이 적용을 빼먹어도 넘어가기 전에는 꼭 적용돼.
            Presenter(turnEvent, () => Apply(turnEvent), () =>
            {
                Apply(turnEvent);
                PlayNext();
            });
        }

        private void Apply(TurnEvent turnEvent)
        {
            if (turnEvent.IsApplied)
                return;
            turnEvent.IsApplied = true;
            switch (turnEvent.Kind)
            {
                case TurnEventKind.Unlock:
                    unlocked[(int)turnEvent.Item] = true;
                    GiveUnlocked(turnEvent);
                    break;
                case TurnEventKind.Gold:
                    board.Gold.Add(turnEvent.Gold);
                    break;
            }
            // 디버프 규칙은 Prepare에서 이미 켜져 있어서 여기서는 알리기만 해.
            Debug.Log(Describe(turnEvent));
        }

        // 열린 아이템 하나를 어떻게 받을지 정해. 보관형은 아이템 칸이 비어 있으면 칸에, 차 있으면 그 아이템 가격만큼 골드로 받아.
        // 벽돌 속성 변경은 그 자리에서 발동해. 다음 턴 공이 일반이면 약점이 없어서 벽돌을 무작위 한 속성으로 통일해.
        private void PlanUnlock(TurnEvent turnEvent, ref bool slotFree)
        {
            if (turnEvent.Item == ShopItem.BrickChange)
            {
                ElementType next = launcher.ReservedElement ?? launcher.TurnElement;
                turnEvent.BrickElement = next == ElementType.Normal
                    ? (ElementType)UnityEngine.Random.Range((int)ElementType.Fire, (int)ElementType.Grass + 1)
                    : ElementRules.WeakTo(next);
                turnEvent.Granted = true;
                return;
            }
            turnEvent.Granted = slotFree;
            if (slotFree)
                slotFree = false;
            else
                turnEvent.Gold = PriceOf(turnEvent.Item);
        }

        private void GiveUnlocked(TurnEvent turnEvent)
        {
            if (turnEvent.Item == ShopItem.BrickChange)
            {
                board.ChangeAllBrickElements(turnEvent.BrickElement);
                return;
            }
            if (turnEvent.Granted && board.Items.TryGive(turnEvent.Item == ShopItem.Bomb ? ItemType.Bomb : ItemType.Ice))
                return;
            // 혹시 칸이 차 있으면 골드로 대신 줘.
            if (turnEvent.Granted)
            {
                turnEvent.Granted = false;
                turnEvent.Gold = PriceOf(turnEvent.Item);
            }
            board.Gold.Add(turnEvent.Gold);
        }

        private int PriceOf(ShopItem item)
        {
            if (shop == null)
                shop = UnityEngine.Object.FindFirstObjectByType<ShopPanel>();
            return shop != null ? shop.PriceOf(item) : 0;
        }

        private Debuff DebuffAt(int turn)
        {
            int interval = settings.debuffInterval, start = Mathf.Max(1, settings.debuffStartTurn);
            if (interval <= 0 || turn < start || (turn - start) % interval != 0)
                return Debuff.None;
            return DebuffOrder[(turn - start) / interval % DebuffOrder.Length];
        }

        // 배너가 생기기 전까지 콘솔에서 확인하는 글이야.
        private static string Describe(TurnEvent turnEvent)
        {
            switch (turnEvent.Kind)
            {
                case TurnEventKind.Unlock:
                    return turnEvent.Turn + "턴 해금: " + turnEvent.Item +
                        (turnEvent.Granted ? " (받음)" : " (칸이 차 있어 " + turnEvent.Gold + "골드로 받음)");
                case TurnEventKind.Gold:
                    return turnEvent.Turn + "턴 보상: " + turnEvent.Gold + "골드";
                default:
                    return turnEvent.Turn + "턴 디버프: " + turnEvent.Debuff;
            }
        }
    }
}
