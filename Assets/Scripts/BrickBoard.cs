using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ElementalBreaker
{
    // 칸 배치, 벽돌 풀, 턴 사이 하강과 게임 오버를 맡아.
    public sealed class BrickBoard : MonoBehaviour
    {
        [SerializeField] private BallLauncher launcher;
        [Tooltip("같은 오브젝트에 Brick, SpriteRenderer, BoxCollider2D가 있는 프리팹 또는 씬 견본")]
        [SerializeField] private Brick brickTemplate;
        [Tooltip("공 +1 아이템이야. 새 줄마다 무작위 한 칸에 꼭 하나 나와.")]
        [SerializeField] private BoardPickup pickupTemplate;
        [SerializeField, Range(0.1f, 0.9f)] private float pickupSizeRatio = 0.55f;
        [Tooltip("월드 좌표의 보드 중심")]
        [SerializeField] private Vector2 boardCenter = new Vector2(0f, 0.7f);
        [SerializeField] private Vector2 boardSize = new Vector2(4.6f, 7f);
        [SerializeField, Min(1)] private int columns = 7;
        [SerializeField, Min(2)] private int rows = 10;
        [Tooltip("0부터 세는 위험 행. 기본 9는 열 번째 행이야.")]
        [SerializeField, Min(1)] private int dangerRow = 9;
        [SerializeField] private Vector2 brickGap = new Vector2(0.08f, 0.08f);
        [Header("새 줄 벽돌")]
        [Tooltip("한 줄 최대 벽돌 수야. 공 +1 아이템 자리 한 칸은 늘 비워 둬서 실제로는 칸 수 - 1까지만 나와.")]
        [SerializeField, Range(0, 6)] private int maxBricksPerRow = 6;
        [Tooltip("한 줄에 적어도 이만큼은 벽돌이 나와.")]
        [SerializeField, Min(0)] private int minBricksPerRow = 2;
        [Tooltip("칸마다 벽돌이 나올 확률이야. 1~lateSpawnTurn턴은 early, 그다음 finalSpawnTurn턴까지 late, 그 뒤로는 final 확률을 써.")]
        [SerializeField, Range(0f, 1f)] private float earlySpawnProbability = 0.6f;
        [SerializeField, Range(0f, 1f)] private float lateSpawnProbability = 0.7f;
        [SerializeField, Range(0f, 1f)] private float finalSpawnProbability = 0.8f;
        [SerializeField, Min(1)] private int lateSpawnTurn = 20;
        [SerializeField, Min(1)] private int finalSpawnTurn = 29;
        [Tooltip("새 줄 벽돌 체력 = 턴 × 이 값이야. 1이면 1턴 1, 10턴 10.")]
        [SerializeField, Min(1)] private int healthPerTurn = 1;
        [Header("시작")]
        [Tooltip("게임을 시작할 때 가진 골드야.")]
        [SerializeField, Min(0)] private int startingGold = 200;
        [Tooltip("벽돌을 깨면 받는 골드(턴 ÷ 10, 최소 1)에 이 턴마다 1골드씩 더 얹어. 40이면 40턴 5골드, 80턴 10골드, 120턴 15골드야. 0이면 없어.")]
        [SerializeField, Min(0)] private int breakGoldBonusInterval = 40;
        [SerializeField, Min(0f)] private float descentDuration = 0.25f;
        [Header("아이템: 속성 폭탄")]
        [Tooltip("맨 앞 공 칸에서 몇 칸까지 맞는지야. 1이면 3x3.")]
        [SerializeField, Min(0)] private int bombRadius = 1;
        [Tooltip("기본 데미지 = 턴 ÷ 이 값(올림). 유리한 상성이면 2배야.")]
        [SerializeField, Min(1)] private int bombDamageDivisor = 2;
        [Tooltip("폭발 중심과 같은 줄, 같은 칸(가운데 십자)에 있는 벽돌이 받는 배율. 대각선은 1배야. 유리한 상성 2배와 같이 곱해져.")]
        [SerializeField, Min(1)] private int bombCrossMultiplier = 2;
        [Tooltip("누르는 순간 게임이 멈춘 듯 느려지며 하얀 섬광 장면(임팩트 프레임)을 보여 주는 시간(초, 실제 시간). 끝나는 순간 폭발이 터져.")]
        [SerializeField, Min(0f)] private float bombHitStopTime = 0.1f;
        [Tooltip("느려지는 동안의 게임 속도. 0이면 완전히 멈춰.")]
        [SerializeField, Range(0f, 1f)] private float bombHitStopScale = 0.05f;
        [Header("턴 이벤트: 해금, 골드 보상, 디버프")]
        [SerializeField] private TurnEventSettings eventSettings = new TurnEventSettings();

        private Brick[,] cells;
        private Stack<Brick> pool;
        private BoardPickup[,] pickups;
        private Stack<BoardPickup> pickupPool;
        private Transform poolRoot;
        private int[] columnOrder;
        private Vector2 cellSize;
        private Vector2 brickSize;
        private float descentElapsed;
        // 두 칸 하강 디버프 턴에 한 번 더 내려와야 하는 횟수야.
        private int extraDescents;
        // 두 칸 하강 턴에는 한 칸씩 천천히 내려오고, 두 칸 사이에 잠깐 쉬어. 남은 쉬는 시간이 0 이상이면 쉬는 중이야.
        private const float DoubleDescentDuration = 0.5f;
        private const float DoubleDescentPause = 0.3f;
        private bool slowDescent;
        private float descentPause = -1f;
        private int lastProcessedTurn;
        private bool initialized;

        public bool IsProcessing { get; private set; }
        public bool IsGameOver { get; private set; }
        public int TurnNumber { get; private set; } = 1;
        public int DescentCount { get; private set; }
        public BrickEffects Effects { get; private set; }
        public IceEffects Ice { get; private set; }
        // 턴 보상과 디버프 일정, 이번 턴 규칙이야. 배너가 Awake에서 연출을 붙일 수 있게 처음 찾을 때 만들어.
        public TurnEvents TurnEvents => turnEvents ??= new TurnEvents(this, launcher, eventSettings);
        // 아이템 금지 디버프 턴이야. 아이템 칸, 공 속성, 상점을 모두 쓸 수 없어.
        public bool ItemsBanned => TurnEvents.ItemsBanned;
        // 벽돌 속성 변경 물결이야.
        public ElementWave ElementWave { get; private set; }
        // 이번 턴에 얼음을 썼어. 턴이 끝나면 벽돌이 내려오지 않고 새 줄도 생기지 않아. 턴 수는 그대로 1 올라가.
        public bool IsFrozen { get; private set; }
        // 이번 판에 얼음을 쓴 횟수야. 상점의 얼음 가격이 쓸 때마다 2배가 돼.
        public int IceUseCount { get; private set; }
        public GoldWallet Gold { get; } = new GoldWallet();
        // 아이템 칸이야. 상점에서 사면 여기에 들어오고, 공이 날아가는 동안 꺼내 써.
        public ItemSlot Items { get; } = new ItemSlot();
        public event Action GameOver;

        private BombItem bomb;
        private TurnEvents turnEvents;
        private BoundaryCollapse collapse;
        // 이어하기로 받은 판이야. Start에서 놓고 비워.
        private SaveData resume;
        // 상점의 벽돌 속성 변경을 마지막으로 산 턴이야. 한 턴에 한 번만 살 수 있어.
        private int brickChangeTurn = -1;

        public bool BrickChangeUsedThisTurn => brickChangeTurn == TurnNumber;
        public void MarkBrickChangeUsed() => brickChangeTurn = TurnNumber;

        // 벽돌 하나를 깨면 받는 골드가 이 턴마다 1씩 올라. 1~19턴 1골드, 20~29턴 2골드, 30~39턴 3골드야.
        private const int BreakGoldTurnStep = 10;

        // 벽돌 하나를 깨면 (턴 ÷ 10, 최소 1)골드에 40턴마다 1골드씩 더 줘.
        // 39턴 3골드, 40턴 5골드, 80턴 10골드, 120턴 15골드야. 유리한 상성으로 깨면 그 2배야.
        public void AwardBreak(bool advantage)
        {
            int gold = Mathf.Max(1, TurnNumber / BreakGoldTurnStep);
            if (breakGoldBonusInterval > 0)
                gold += TurnNumber / breakGoldBonusInterval;
            Gold.Add(advantage ? gold * 2 : gold);
        }

        // 상점 가격처럼 10턴마다 오르는 값이 지금 몇 번 올랐는지야. 1~9턴 0, 10~19턴 1, 20~29턴 2야.
        public int PriceTier => TurnNumber / 10;

        private void Awake()
        {
            if (launcher != null)
                launcher.SetInputBlocked(true);
            TakePendingSave();
        }

        // 로비에서 이어하기를 눌렀으면 저장된 판을 받아. 이 게임판에 그대로 놓을 수 있을 때만 쓰고, 아니면 버리고 새 게임으로 시작해.
        // 발사기는 Start에서 공을 만들어서, 그 전(Awake)에 공 수와 속성을 넘겨.
        private void TakePendingSave()
        {
            SaveData data = GameSave.Pending;
            GameSave.Pending = null;
            if (data == null || launcher == null)
                return;
            ClampSettings();
            if (!GameSave.FitsBoard(data, rows, columns, dangerRow))
            {
                Debug.LogWarning("저장된 판이 지금 게임판과 맞지 않아서 새 게임으로 시작해.", this);
                GameSave.Delete();
                return;
            }
            Rect rect = BoardRect;
            data.launchX = Mathf.Clamp(data.launchX, rect.xMin, rect.xMax);
            launcher.Restore(data.balls, (ElementType)data.turnElement,
                data.reservedElement >= 0 ? (ElementType?)data.reservedElement : null, data.launchX);
            resume = data;
        }

        private void OnEnable()
        {
            if (launcher != null)
            {
                launcher.Launched += OnLaunched;
                launcher.TurnEnded += OnTurnEnded;
                launcher.SetInputBlocked(!initialized || IsProcessing || IsGameOver);
                if (initialized)
                    OnTurnEnded();
            }
        }

        private void Start()
        {
            // 벽돌 이미지는 생성할 때 코드에서 넣어서, 견본에는 SpriteRenderer만 있으면 돼.
            if (launcher == null || brickTemplate == null || !brickTemplate.TryGetComponent(out SpriteRenderer visual))
            {
                Debug.LogError("BrickBoard: Launcher와 SpriteRenderer가 있는 Brick Template을 연결해.", this);
                enabled = false;
                return;
            }

            ClampSettings();
            cellSize = new Vector2(boardSize.x / columns, boardSize.y / rows);
            brickSize = cellSize - brickGap;
            cells = new Brick[rows, columns];
            pickups = new BoardPickup[rows, columns];
            columnOrder = new int[columns];
            int capacity = columns * rows;
            pool = new Stack<Brick>(capacity);
            pickupPool = new Stack<BoardPickup>(capacity);
            // 부모 스케일과 회전에 영향받지 않는 풀을 한 번 만들어.
            poolRoot = new GameObject("Brick Pool").transform;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(poolRoot.gameObject, gameObject.scene);
            // 벽돌 이펙트도 같은 이유로 씬에 바로 두고, 실행 중에 한 번만 만들어.
            // 벽돌 한 칸은 벽돌(+0), 얼음 서리막(+1), 아이콘(+2), 숫자(+3)를 써서, 이펙트는 그 위(+1부터 센 순서)에 그려.
            int effectOrder = visual.sortingOrder + 1;
            Effects = new GameObject("Brick Effects").AddComponent<BrickEffects>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(Effects.gameObject, gameObject.scene);
            Effects.Initialize(visual.sharedMaterial, visual.sortingLayerID, effectOrder);
            HitStop hitStop = Effects.gameObject.AddComponent<HitStop>();
            BombEffects bombEffects = new GameObject("Bomb Effects").AddComponent<BombEffects>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(bombEffects.gameObject, gameObject.scene);
            bombEffects.Initialize(this, visual.sharedMaterial, brickTemplate.GetComponentInChildren<TMP_Text>(true),
                visual.sortingLayerID, effectOrder);
            bomb = new BombItem(this, launcher, hitStop, bombEffects);
            // 폭탄이 터질 맨 앞 공의 조준 고리야. 벽돌 숫자(+3) 위에 그려서 벽돌 사이에서도 보여.
            BombTargetMarker bombTarget = new GameObject("Bomb Target").AddComponent<BombTargetMarker>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(bombTarget.gameObject, gameObject.scene);
            bombTarget.Initialize(this, launcher, visual.sharedMaterial, visual.sortingLayerID, visual.sortingOrder + 4);
            // 게임 오버 때 위쪽 경계선과 회수선이 무너지는 연출이야. 선은 BoardPresentation이 그리고 있어서 그걸 조각내.
            BoardPresentation presentation = FindFirstObjectByType<BoardPresentation>();
            if (presentation != null)
            {
                collapse = new GameObject("Boundary Collapse").AddComponent<BoundaryCollapse>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(collapse.gameObject, gameObject.scene);
                collapse.Initialize(presentation);
            }
            ElementWave = new GameObject("Element Wave").AddComponent<ElementWave>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ElementWave.gameObject, gameObject.scene);
            ElementWave.Initialize(this, visual.sharedMaterial, visual.sortingLayerID, visual.sortingOrder);
            Ice = new GameObject("Ice Effects").AddComponent<IceEffects>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(Ice.gameObject, gameObject.scene);
            Ice.Initialize(this, launcher, visual.sharedMaterial, visual.sortingLayerID, visual.sortingOrder);
            if (brickTemplate.gameObject.scene.IsValid())
                brickTemplate.gameObject.SetActive(false);
            if (pickupTemplate != null && pickupTemplate.gameObject.scene.IsValid())
                pickupTemplate.gameObject.SetActive(false);
            for (int i = 0; i < capacity; i++)
            {
                Brick brick = Instantiate(brickTemplate, poolRoot);
                brick.gameObject.SetActive(false);
                pool.Push(brick);
                if (pickupTemplate != null)
                {
                    BoardPickup pickup = Instantiate(pickupTemplate, poolRoot);
                    pickup.gameObject.SetActive(false);
                    pickupPool.Push(pickup);
                }
            }
            if (resume != null)
            {
                SaveData data = resume;
                resume = null;
                Resume(data);
                return;
            }
            TurnNumber = TurnEvents.StartTurn;
            TurnEvents.Seed = UnityEngine.Random.Range(1, int.MaxValue);
            Gold.Add(startingGold);
            TurnEvents.SkipTo(TurnNumber);
            initialized = true;
            lastProcessedTurn = launcher.CompletedTurnCount;
            StartTurn(true);
        }

        public Brick GetBrick(int row, int column) => cells[row, column];

        // 월드 위치가 들어 있는 칸이야. 보드 밖이면 가장 가까운 칸으로 맞춰.
        public void WorldToCell(Vector2 position, out int row, out int column)
        {
            float left = boardCenter.x - boardSize.x * 0.5f;
            float top = boardCenter.y + boardSize.y * 0.5f;
            column = Mathf.Clamp(Mathf.FloorToInt((position.x - left) / cellSize.x), 0, columns - 1);
            row = Mathf.Clamp(Mathf.FloorToInt((top - position.y) / cellSize.y), 0, rows - 1);
        }

        // 폭탄 범위의 가운데 칸이야. 첫째 줄 위의 맨 위 빈 공간은 줄 번호가 없어서 -1번 줄로 쳐.
        // 그래서 맨 위에서 터지면 3x3 중 벽돌이 있는 바로 밑 첫째 줄만 맞아.
        public void WorldToBombCell(Vector2 position, out int row, out int column)
        {
            WorldToCell(position, out row, out column);
            if (position.y > boardCenter.y + boardSize.y * 0.5f)
                row = -1;
        }

        // (row, column) 주변 radius칸 안의 벽돌을 results에 담아. 보드 밖 칸(맨 위 -1번 줄 등)은 건너뛰어.
        public void CollectBricksInRange(int row, int column, int radius, List<Brick> results)
        {
            results.Clear();
            int rowMin = Mathf.Max(0, row - radius), rowMax = Mathf.Min(rows - 1, row + radius);
            int columnMin = Mathf.Max(0, column - radius), columnMax = Mathf.Min(columns - 1, column + radius);
            for (int r = rowMin; r <= rowMax; r++)
                for (int c = columnMin; c <= columnMax; c++)
                    if (cells[r, c] != null)
                        results.Add(cells[r, c]);
        }

        // 이 자리에서 폭탄이 터지면 맞는 벽돌이 하나라도 있는지야. 맨 앞 공 조준 표시와 폭탄 취소에 써.
        public bool HasBricksInBombRange(Vector2 position)
        {
            WorldToBombCell(position, out int row, out int column);
            int rowMin = Mathf.Max(0, row - bombRadius), rowMax = Mathf.Min(rows - 1, row + bombRadius);
            int columnMin = Mathf.Max(0, column - bombRadius), columnMax = Mathf.Min(columns - 1, column + bombRadius);
            for (int r = rowMin; r <= rowMax; r++)
                for (int c = columnMin; c <= columnMax; c++)
                    if (cells[r, c] != null)
                        return true;
            return false;
        }

        public int BombRadius => bombRadius;
        // 칸 하나의 월드 크기야. 폭발 크기를 범위 칸 수에 맞출 때 써.
        public Vector2 CellSize => cellSize;
        public int Rows => rows;
        public int Columns => columns;
        // 벽돌 칸 전체의 월드 사각형이야.
        public Rect BoardRect => new Rect(boardCenter - boardSize * 0.5f, boardSize);

        // 상점과 9턴 해금 보상의 "벽돌 속성 변경"이야. 보드에 있는 모든 벽돌을 target 속성으로 바꿔.
        // 발사 자리에서 바뀔 속성 색 물결이 퍼지고, 물결이 지나가는 벽돌부터 바뀌어(ElementWave.Duration 동안).
        public void ChangeAllBrickElements(ElementType target)
        {
            GameAudio.Play(SoundCue.BoardChange);
            if (ElementWave != null)
            {
                ElementWave.Play(launcher.LaunchPosition, target);
                return;
            }
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                    if (cells[r, c] != null)
                        cells[r, c].SetElement(target);
        }

        // 폭탄 기본 데미지 = 턴 ÷ divisor(올림). 최소 1이야.
        public int BombDamage(int divisor) => Mathf.Max(1, Mathf.CeilToInt(TurnNumber / (float)Mathf.Max(1, divisor)));
        // 이번 턴 폭탄 데미지야. 대각선은 기본, 가운데 십자는 배율만큼 세. 유리한 상성 2배는 맞는 벽돌마다 달라서 빼.
        public int BombBaseDamage => BombDamage(bombDamageDivisor);
        public int BombCrossDamage => BombBaseDamage * bombCrossMultiplier;

        // 지금 아이템 칸의 아이템을 쓸 수 있는지야.
        // 폭탄은 공이 날아가는 중에만, 얼음은 조준 중(발사 전)이나 공이 날아가는 중에 한 턴에 한 번 쓸 수 있어.
        // 아이템 금지 디버프 턴에는 둘 다 못 써.
        public bool CanUseHeldItem
        {
            get
            {
                if (!initialized || IsGameOver || IsProcessing || ItemsBanned)
                    return false;
                switch (Items.Held)
                {
                    case ItemType.Bomb:
                        return launcher.IsTurnActive && launcher.FlyingBallCount > 0;
                    case ItemType.Ice:
                        return !IsFrozen && (launcher.CanPrepare || launcher.IsTurnActive);
                    default:
                        return false;
                }
            }
        }

        public bool UseHeldItem()
        {
            if (!CanUseHeldItem)
                return false;
            bool used = false;
            switch (Items.Held)
            {
                case ItemType.Bomb:
                    used = bomb.Detonate(bombRadius, bombDamageDivisor, bombCrossMultiplier, bombHitStopTime, bombHitStopScale);
                    break;
                case ItemType.Ice:
                    IsFrozen = true;
                    IceUseCount++;
                    Ice.PlayFreeze(launcher.LaunchPosition);
                    used = true;
                    break;
            }
            if (used)
            {
                Items.Take();
                // 써 보고 나갔다 들어와서 되돌리지 못하게 바로 저장해.
                Commit(launcher.IsTurnActive ? SavePhase.ShotFired : SavePhase.Aiming);
            }
            return used;
        }

        // 조준 중에 상점에서 사거나 공 속성을 바꾼 뒤 불러. 되돌릴 수 없게 바로 저장해.
        public void SaveProgress()
        {
            if (launcher != null && launcher.CanPrepare)
                Commit(SavePhase.Aiming);
        }

        // 손을 떼서 쏜 순간이야. 이어하면 이 샷은 아무것도 못 맞힌 걸로 쳐서, 잘못 쏘고 나가도 다시 쏠 수 없어.
        private void OnLaunched() => Commit(SavePhase.ShotFired);

        private void Commit(SavePhase phase)
        {
            if (!initialized || IsGameOver)
                return;
            GameSave.Write(Capture(phase));
        }

        // 지금 판을 저장 형식으로 옮겨. 속성 물결이 지나가는 중이면 물결이 끝난 뒤의 속성으로 적어.
        private SaveData Capture(SavePhase phase)
        {
            bool waving = ElementWave != null && ElementWave.IsPlaying;
            List<SavedBrick> savedBricks = new List<SavedBrick>();
            List<SavedPickup> savedPickups = new List<SavedPickup>();
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                Brick brick = cells[row, column];
                if (brick != null)
                    savedBricks.Add(new SavedBrick
                    {
                        row = row,
                        column = column,
                        element = (int)(waving ? ElementWave.Target : brick.Element),
                        health = brick.Health,
                        boosted = brick.IsBoosted
                    });
                BoardPickup pickup = pickups[row, column];
                if (pickup != null)
                    savedPickups.Add(new SavedPickup { row = row, column = column, element = (int)pickup.Element });
            }
            ElementType? reserved = launcher.ReservedElement;
            return new SaveData
            {
                phase = (int)phase,
                turn = TurnNumber,
                gold = Gold.Amount,
                balls = launcher.BallCount,
                turnElement = (int)launcher.TurnElement,
                reservedElement = reserved.HasValue ? (int)reserved.Value : -1,
                launchX = launcher.LaunchPosition.x,
                heldItem = (int)Items.Held,
                iceUseCount = IceUseCount,
                frozen = IsFrozen,
                pendingHealthBoost = TurnEvents.PendingHealthBoost,
                debuffSeed = TurnEvents.Seed,
                brickChangeUsed = BrickChangeUsedThisTurn,
                bricks = savedBricks.ToArray(),
                pickups = savedPickups.ToArray()
            };
        }

        // 이어하기: 저장된 판을 그대로 놓고, 저장한 순간에 따라 이어 가.
        private void Resume(SaveData data)
        {
            TurnNumber = data.turn;
            Gold.Add(data.gold);
            IceUseCount = data.iceUseCount;
            if (data.brickChangeUsed)
                brickChangeTurn = data.turn;
            Items.TryGive((ItemType)data.heldItem);
            foreach (SavedBrick saved in data.bricks ?? Array.Empty<SavedBrick>())
            {
                Brick brick = pool.Pop();
                cells[saved.row, saved.column] = brick;
                brick.Spawn(this, saved.row, saved.column, saved.health, brickSize, (ElementType)saved.element);
                if (saved.boosted)
                    brick.SetBoosted(true);
            }
            foreach (SavedPickup saved in data.pickups ?? Array.Empty<SavedPickup>())
                SpawnPickup(saved.row, saved.column, (ElementType)saved.element);
            SavePhase phase = (SavePhase)data.phase;
            IsFrozen = data.frozen;
            TurnEvents.Seed = data.debuffSeed;
            TurnEvents.Restore(TurnNumber, phase != SavePhase.TurnStart, data.pendingHealthBoost);
            initialized = true;
            lastProcessedTurn = launcher.CompletedTurnCount;
            CheckResumed(data, phase);
            switch (phase)
            {
                case SavePhase.TurnStart:
                    // 이번 턴 이벤트를 아직 못 봤어. 새 줄은 이미 있으니 이벤트만 보여 주고 조작을 열어.
                    TurnEvents.Play(OpenTurn);
                    break;
                case SavePhase.Aiming:
                    ShowResumedState();
                    OpenTurn();
                    break;
                default:
                    // 쏜 샷은 아무것도 못 맞힌 걸로 치고 바로 턴을 끝내. 나가는 게 끝까지 보는 것보다 늘 손해야.
                    ShowResumedState();
                    EndTurn();
                    break;
            }
        }

        // 저장 → 불러오기 → 다시 저장이 같은지 확인해. 다르면 저장에 빠진 항목이 있다는 뜻이라 콘솔에 남겨.
        private void CheckResumed(SaveData data, SavePhase phase)
        {
            SaveData restored = Capture(phase);
            restored.version = data.version;
            // 발사기가 보드보다 먼저 시작했으면 다음 턴 속성을 이번 턴 속성으로 벌써 옮겼을 수 있어. 결과가 같아서 맞춰 비교해.
            if (data.reservedElement >= 0 && restored.reservedElement < 0 && restored.turnElement == data.reservedElement)
            {
                restored.reservedElement = data.reservedElement;
                restored.turnElement = data.turnElement;
            }
            string expected = JsonUtility.ToJson(data), actual = JsonUtility.ToJson(restored);
            if (expected != actual)
                Debug.LogWarning("이어하기 검사: 저장한 판과 불러온 판이 달라.\n저장: " + expected + "\n불러옴: " + actual, this);
        }

        // 조준 중이나 쏜 뒤에 이어하면 이벤트 배너는 다시 안 보여 줘서, 얼음과 디버프 표시만 바로 다시 띄워.
        private void ShowResumedState()
        {
            if (IsFrozen && Ice != null)
                Ice.PlayFreeze(launcher.LaunchPosition);
            TurnEventBanner banner = FindFirstObjectByType<TurnEventBanner>();
            if (banner != null)
                banner.ShowActiveDebuff();
        }

        // 에디터 메뉴 "저장 검사"가 불러. 지금 판을 저장 형식으로 바꿨다가 다시 읽어도 같은지, 이 판에 다시 놓을 수 있는지 봐.
        public bool VerifySave(out string json)
        {
            json = null;
            if (!initialized)
                return false;
            SaveData data = Capture(launcher.IsTurnActive ? SavePhase.ShotFired : SavePhase.Aiming);
            data.version = GameSave.Version;
            return GameSave.RoundTrips(data, out json) && GameSave.FitsBoard(data, rows, columns, dangerRow);
        }

        public Vector3 GetCellCenter(int row, int column)
        {
            return new Vector3(boardCenter.x - boardSize.x * 0.5f + (column + 0.5f) * cellSize.x,
                boardCenter.y + boardSize.y * 0.5f - (row + 0.5f) * cellSize.y, transform.position.z);
        }

        private void OnTurnEnded()
        {
            if (!initialized || IsProcessing || IsGameOver || launcher.IsTurnActive ||
                launcher.PendingBallCount != 0 || launcher.FlyingBallCount != 0 ||
                launcher.CompletedTurnCount <= lastProcessedTurn)
                return;

            lastProcessedTurn = launcher.CompletedTurnCount;
            // 공이 다 돌아와서 이 샷 결과가 확정됐어. 벽돌이 내려오는 동안 나가도 결과가 남게 바로 저장해.
            Commit(SavePhase.ShotFired);
            EndTurn();
        }

        // 이번 턴을 끝내. 공이 다 돌아왔을 때와, 쏜 뒤에 나갔다가 이어할 때 불러.
        private void EndTurn()
        {
            launcher.SetInputBlocked(true);
            IsProcessing = true;
            // 얼음을 쓴 턴이면 내려오지 않아. 서리가 빛나고 가루로 풀리는 연출이 끝나면 다음 턴으로 넘어가.
            // 두 칸 하강 디버프 턴이어도 얼음이 이겨서 안 내려와.
            if (IsFrozen)
            {
                Ice.PlayRelease(FinishFrozenTurn);
                return;
            }
            // 두 칸 하강 디버프 턴이면 한 칸 내려온 뒤 새 줄을 하나 만들고 한 번 더 내려와. 턴 수는 1만 올라가.
            extraDescents = TurnEvents.ActiveDebuff == Debuff.DoubleDescent ? 1 : 0;
            slowDescent = extraDescents > 0;
            StartDescent();
        }

        // 모든 벽돌과 아이템을 한 칸 아래 칸으로 옮기고, 화면 위치는 Update에서 descentDuration 동안 따라 내려가.
        private void StartDescent()
        {
            IsProcessing = true;
            descentElapsed = 0f;
            DescentCount++;
            // 아래부터 옮겨서 같은 벽돌이 두 번 이동하지 않게 해.
            for (int row = rows - 2; row >= 0; row--)
            for (int column = 0; column < columns; column++)
            {
                Brick brick = cells[row, column];
                cells[row + 1, column] = brick;
                cells[row, column] = null;
                if (brick != null)
                    brick.SetRow(row + 1);
                BoardPickup pickup = pickups[row, column];
                pickups[row + 1, column] = pickup;
                pickups[row, column] = null;
                if (pickup != null)
                    pickup.SetRow(row + 1);
            }
            descentPause = -1f;
            if (!slowDescent && descentDuration <= 0f)
                AdvanceDescent(0f);
        }

        private void Update()
        {
            if (!IsProcessing || IsFrozen)
                return;
            // 두 칸 하강: 한 칸 내려온 뒤 잠깐 쉬었다가 두 번째 칸을 내려와.
            if (descentPause >= 0f)
            {
                descentPause -= Time.deltaTime;
                if (descentPause < 0f)
                    StartDescent();
                return;
            }
            AdvanceDescent(Time.deltaTime);
        }

        // 얼음 턴의 끝이야. 벽돌은 그 자리에 있고 새 줄도 없이, 턴 수만 올리고 다음 턴을 열어.
        private void FinishFrozenTurn()
        {
            if (!IsProcessing || IsGameOver)
                return;
            IsFrozen = false;
            IsProcessing = false;
            TurnNumber++;
            StartTurn(false);
        }

        // 새 턴이 시작될 때야. 이번 턴 규칙(디버프)을 먼저 켜고 새 줄을 만든 뒤, 턴 이벤트를 보여 주고 나서 조작을 열어.
        // 이벤트를 보여 주는 동안은 조작이 막혀 있어서 상점과 공 속성도 못 눌러.
        private void StartTurn(bool spawnRow)
        {
            TurnEvents.Prepare(TurnNumber);
            if (spawnRow)
                SpawnTopRow(true);
            // 새 줄을 보고 나갔다 들어와도 같은 줄이 나오게, 이벤트를 보여 주기 전에 바로 저장해.
            Commit(SavePhase.TurnStart);
            TurnEvents.Play(OpenTurn);
        }

        private void OpenTurn()
        {
            if (IsGameOver)
                return;
            launcher.SetInputBlocked(false);
            // 해금 아이템과 골드 보상을 받고, 먹은 공 속성이 이번 턴 속성이 된 조준 상태야.
            Commit(SavePhase.Aiming);
        }

        private void AdvanceDescent(float deltaTime)
        {
            descentElapsed += deltaTime;
            // 두 칸 하강 턴에는 한 칸씩 천천히 내려오고 내려앉을 때 살짝 느려져. 평소에는 일정한 속도야.
            float duration = slowDescent ? DoubleDescentDuration : descentDuration;
            float progress = duration <= 0f ? 1f : Mathf.Clamp01(descentElapsed / duration);
            if (slowDescent)
                progress = EffectMath.EaseOut(progress);
            for (int row = 1; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                Brick brick = cells[row, column];
                if (brick != null)
                    brick.SetCellPosition(Vector3.Lerp(GetCellCenter(row - 1, column),
                        GetCellCenter(row, column), progress));
                BoardPickup pickup = pickups[row, column];
                if (pickup != null)
                    pickup.SetCellPosition(Vector3.Lerp(GetCellCenter(row - 1, column),
                        GetCellCenter(row, column), progress));
            }
            if (progress < 1f)
                return;

            IsProcessing = false;
            // 바닥(위험 행)까지 내려온 공 아이템은 못 맞혔어도 공 +1을 받아. 다음 턴 속성 예약은 바꾸지 않아.
            bool collected = false;
            for (int row = dangerRow; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                if (pickups[row, column] == null)
                    continue;
                ReturnPickup(pickups[row, column]);
                launcher.AddBall();
                collected = true;
            }
            if (collected)
                GameAudio.Play(SoundCue.Pickup);
            for (int row = dangerRow; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                if (cells[row, column] == null)
                    continue;
                // 벽돌이 기준선에 닿는 순간 둔탁한 "쿵"과 함께 위아래 경계선이 무너지고,
                // 다 무너진 뒤(BoundaryCollapse.Duration) GameOverUI가 다시 하기 창을 띄워.
                IsGameOver = true;
                // 무너지는 연출 중에 나가도 진 판이 이어하기로 살아나지 않게, 지는 순간 저장을 지워.
                GameSave.Delete();
                GameAudio.Play(SoundCue.GameOverThud);
                if (collapse != null)
                    collapse.Play();
                Debug.Log("게임 오버: 벽돌이 위험 행에 도달했어.", this);
                GameOver?.Invoke();
                return;
            }
            if (extraDescents > 0)
            {
                // 두 칸 하강 턴에 한 칸 내려온 뒤 생기는 줄이야. 공 +1 아이템은 한 턴에 하나라 이 줄에는 안 놓아.
                extraDescents--;
                SpawnTopRow(false);
                IsProcessing = true;
                descentPause = DoubleDescentPause;
                return;
            }
            slowDescent = false;
            TurnNumber++;
            StartTurn(true);
        }

        // 맨 위에 새 줄을 만들어. withPickup이면 무작위 한 칸을 공 +1 아이템 자리로 먼저 비워 두고,
        // 나머지 칸마다 확률(1~20턴 60%, 21~29턴 70%, 30턴부터 80%)로 벽돌을 놓아. 그래도 적으면 빈 칸을 채워 최소 개수를 맞춰.
        private void SpawnTopRow(bool withPickup)
        {
            // 열 순서를 섞어서 왼쪽에만 몰리지 않게 해. 맨 앞 칸이 공 아이템 자리야.
            for (int i = 0; i < columns; i++)
                columnOrder[i] = i;
            for (int i = 0; i < columns; i++)
            {
                int randomIndex = UnityEngine.Random.Range(i, columns);
                (columnOrder[i], columnOrder[randomIndex]) = (columnOrder[randomIndex], columnOrder[i]);
            }
            int pickupColumn = withPickup && pickupTemplate != null ? columnOrder[0] : -1;
            int first = pickupColumn >= 0 ? 1 : 0;
            int limit = Mathf.Min(maxBricksPerRow, columns - first);
            int minimum = Mathf.Min(minBricksPerRow, limit);
            float probability = TurnNumber > finalSpawnTurn ? finalSpawnProbability
                : TurnNumber > lateSpawnTurn ? lateSpawnProbability : earlySpawnProbability;
            int spawned = 0;
            long baseRowHealth = Math.Max(1L, (long)TurnNumber * healthPerTurn);
            // 체력 디버프(기본 2배, 소수점은 올림)는 그 뒤 처음 생기는 줄에 한 번 들어가. 그 벽돌들은 깨질 때까지 붉은 테두리가 남아.
            bool boosted = TurnEvents.TakeHealthBoost();
            if (boosted)
                baseRowHealth = (long)Math.Ceiling(baseRowHealth * (double)TurnEvents.HealthBoostMultiplier);
            int health = (int)Math.Min(int.MaxValue, baseRowHealth);
            for (int i = first; i < columns && spawned < limit; i++)
                if (UnityEngine.Random.value < probability)
                    SpawnBrick(columnOrder[i], health, boosted, ref spawned);
            for (int i = first; i < columns && spawned < minimum; i++)
                if (cells[0, columnOrder[i]] == null)
                    SpawnBrick(columnOrder[i], health, boosted, ref spawned);
            if (pickupColumn >= 0)
                SpawnPickup(0, pickupColumn, ElementRules.RandomElement());
            // 체력 디버프 턴에는 경고 배너가 사라질 때 "×2"를 보여 줘. 얼음 때문에 다음 턴으로 미뤄진 줄은 생기자마자 보여 줘.
            if (boosted && TurnEvents.ActiveDebuff != Debuff.HealthBoost)
                RevealBoostedRow();
        }

        // 맨 윗줄의 체력 디버프 벽돌이 번쩍이고 "×2"가 떠.
        public void RevealBoostedRow()
        {
            for (int column = 0; column < columns; column++)
            {
                Brick brick = cells[0, column];
                if (brick != null && brick.IsBoosted)
                    brick.PlayBoostPop();
            }
        }

        private void SpawnBrick(int column, int health, bool boosted, ref int spawned)
        {
            Brick brick = pool.Pop();
            cells[0, column] = brick;
            brick.Spawn(this, 0, column, health, brickSize, ElementRules.RandomElement());
            if (boosted)
                brick.SetBoosted(true);
            spawned++;
        }

        private void SpawnPickup(int row, int column, ElementType element)
        {
            if (cells[row, column] != null || pickups[row, column] != null || pickupPool.Count == 0)
                return;
            BoardPickup pickup = pickupPool.Pop();
            pickups[row, column] = pickup;
            pickup.Spawn(this, row, column, Mathf.Min(cellSize.x, cellSize.y) * pickupSizeRatio, element);
        }

        public void Collect(BoardPickup pickup)
        {
            if (IsProcessing || IsGameOver || !launcher.IsTurnActive || pickup == null ||
                pickup.Row < 0 || pickup.Row >= rows || pickup.Column < 0 || pickup.Column >= columns ||
                pickups[pickup.Row, pickup.Column] != pickup)
                return;
            ElementType element = pickup.Element;
            // 칸을 먼저 비워서 동시에 닿아도 보상은 한 번만 줘.
            ReturnPickup(pickup);
            launcher.AddBall();
            launcher.ReserveNextElement(element);
            GameAudio.Play(SoundCue.Pickup);
        }

        private void ReturnPickup(BoardPickup pickup)
        {
            pickups[pickup.Row, pickup.Column] = null;
            pickup.Despawn();
            pickupPool.Push(pickup);
        }

        public void Release(Brick brick)
        {
            int row = brick.Row;
            int column = brick.Column;
            if (row < 0 || row >= rows || column < 0 || column >= columns || cells[row, column] != brick)
                return;
            cells[row, column] = null;
            brick.Despawn();
            pool.Push(brick);
        }

        private void ClampSettings()
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(2, rows);
            dangerRow = Mathf.Clamp(dangerRow, 1, rows - 1);
            boardSize = new Vector2(Mathf.Max(0.1f, boardSize.x), Mathf.Max(0.1f, boardSize.y));
            brickGap = new Vector2(Mathf.Clamp(brickGap.x, 0f, boardSize.x / columns * 0.95f),
                Mathf.Clamp(brickGap.y, 0f, boardSize.y / rows * 0.95f));
            maxBricksPerRow = Mathf.Clamp(maxBricksPerRow, 0, Mathf.Min(6, columns));
            minBricksPerRow = Mathf.Clamp(minBricksPerRow, 0, maxBricksPerRow);
            earlySpawnProbability = Mathf.Clamp01(earlySpawnProbability);
            lateSpawnProbability = Mathf.Clamp01(lateSpawnProbability);
            finalSpawnProbability = Mathf.Clamp01(finalSpawnProbability);
            lateSpawnTurn = Mathf.Max(1, lateSpawnTurn);
            finalSpawnTurn = Mathf.Max(lateSpawnTurn, finalSpawnTurn);
            pickupSizeRatio = Mathf.Clamp(pickupSizeRatio, 0.1f, 0.9f);
            healthPerTurn = Mathf.Max(1, healthPerTurn);
            descentDuration = Mathf.Max(0f, descentDuration);
            bombRadius = Mathf.Max(0, bombRadius);
            bombDamageDivisor = Mathf.Max(1, bombDamageDivisor);
            bombCrossMultiplier = Mathf.Max(1, bombCrossMultiplier);
            bombHitStopTime = Mathf.Max(0f, bombHitStopTime);
            bombHitStopScale = Mathf.Clamp01(bombHitStopScale);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                ClampSettings();
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = new Vector3(boardCenter.x, boardCenter.y, transform.position.z);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center, boardSize);
            float width = boardSize.x / Mathf.Max(1, columns);
            float height = boardSize.y / Mathf.Max(2, rows);
            for (int column = 1; column < columns; column++)
            {
                float x = center.x - boardSize.x * 0.5f + column * width;
                Gizmos.DrawLine(new Vector3(x, center.y - boardSize.y * 0.5f, center.z),
                    new Vector3(x, center.y + boardSize.y * 0.5f, center.z));
            }
            for (int row = 1; row < rows; row++)
            {
                Gizmos.color = row == dangerRow ? Color.red : Color.cyan;
                float y = center.y + boardSize.y * 0.5f - row * height;
                Gizmos.DrawLine(new Vector3(center.x - boardSize.x * 0.5f, y, center.z),
                    new Vector3(center.x + boardSize.x * 0.5f, y, center.z));
            }
        }

        private void OnDisable()
        {
            if (launcher == null)
                return;
            launcher.Launched -= OnLaunched;
            launcher.TurnEnded -= OnTurnEnded;
            launcher.SetInputBlocked(true);
        }

        private void OnDestroy()
        {
            if (poolRoot != null)
                Destroy(poolRoot.gameObject);
        }
    }
}
