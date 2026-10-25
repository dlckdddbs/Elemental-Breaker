using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElementalBreaker
{
    // 칸 배치, 벽돌 풀, 턴 사이 하강과 게임 오버를 맡아.
    public sealed class BrickBoard : MonoBehaviour
    {
        [SerializeField] private BallLauncher launcher;
        [Tooltip("같은 오브젝트에 Brick, SpriteRenderer, BoxCollider2D가 있는 프리팹 또는 씬 견본")]
        [SerializeField] private Brick brickTemplate;
        [SerializeField] private BoardPickup pickupTemplate;
        [SerializeField, Range(0f, 1f)] private float pickupProbability = 0.3f;
        [SerializeField, Min(0)] private int maxPickupsPerRow = 2;
        [SerializeField, Range(0.1f, 0.9f)] private float pickupSizeRatio = 0.55f;
        [Tooltip("월드 좌표의 보드 중심")]
        [SerializeField] private Vector2 boardCenter = new Vector2(0f, 0.7f);
        [SerializeField] private Vector2 boardSize = new Vector2(4.6f, 7f);
        [SerializeField, Min(1)] private int columns = 7;
        [SerializeField, Min(2)] private int rows = 10;
        [Tooltip("0부터 세는 위험 행. 기본 9는 열 번째 행이야.")]
        [SerializeField, Min(1)] private int dangerRow = 9;
        [SerializeField] private Vector2 brickGap = new Vector2(0.08f, 0.08f);
        [SerializeField, Range(0, 6)] private int maxBricksPerRow = 6;
        [SerializeField, Range(0f, 1f)] private float spawnProbability = 0.7f;
        [SerializeField, Min(1)] private int baseHealth = 3;
        [SerializeField, Min(0)] private int healthPerTurn = 1;
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

        private Brick[,] cells;
        private Stack<Brick> pool;
        private BoardPickup[,] pickups;
        private Stack<BoardPickup> pickupPool;
        private Transform poolRoot;
        private int[] columnOrder;
        private Vector2 cellSize;
        private Vector2 brickSize;
        private float descentElapsed;
        private int lastProcessedTurn;
        private bool initialized;

        public bool IsProcessing { get; private set; }
        public bool IsGameOver { get; private set; }
        public int TurnNumber { get; private set; } = 1;
        public int DescentCount { get; private set; }
        public BrickEffects Effects { get; private set; }
        public IceEffects Ice { get; private set; }
        // 이번 턴에 얼음을 썼어. 턴이 끝나면 벽돌이 내려오지 않고 새 줄도 생기지 않아. 턴 수는 그대로 1 올라가.
        public bool IsFrozen { get; private set; }
        public GoldWallet Gold { get; } = new GoldWallet();
        // 아이템 칸이야. 상점에서 사면 여기에 들어오고, 공이 날아가는 동안 꺼내 써.
        public ItemSlot Items { get; } = new ItemSlot();
        public event Action GameOver;

        private BombItem bomb;

        private const int BreakGold = 1;
        private const int AdvantageBreakGold = 2;

        // 벽돌 하나를 깨면 1골드, 유리한 상성으로 깨면 2골드야.
        public void AwardBreak(bool advantage) => Gold.Add(advantage ? AdvantageBreakGold : BreakGold);

        private void Awake()
        {
            if (launcher != null)
                launcher.SetInputBlocked(true);
        }

        private void OnEnable()
        {
            if (launcher != null)
            {
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
            bombEffects.Initialize(this, visual.sharedMaterial, visual.sortingLayerID, effectOrder);
            bomb = new BombItem(this, launcher, hitStop, bombEffects);
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
            initialized = true;
            lastProcessedTurn = launcher.CompletedTurnCount;
            SpawnTopRow();
            launcher.SetInputBlocked(false);
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

        public int BombRadius => bombRadius;
        // 칸 하나의 월드 크기야. 폭발 크기를 범위 칸 수에 맞출 때 써.
        public Vector2 CellSize => cellSize;
        public int Rows => rows;
        public int Columns => columns;
        // 벽돌 칸 전체의 월드 사각형이야.
        public Rect BoardRect => new Rect(boardCenter - boardSize * 0.5f, boardSize);

        // 상점의 "벽돌 속성 변경"이야. 보드에 있는 모든 벽돌을 target 속성으로 바꿔.
        public void ChangeAllBrickElements(ElementType target)
        {
            GameAudio.Play(SoundCue.BoardChange);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                    if (cells[r, c] != null)
                        cells[r, c].SetElement(target);
        }

        // 폭탄 기본 데미지 = 턴 ÷ divisor(올림). 최소 1이야.
        public int BombDamage(int divisor) => Mathf.Max(1, Mathf.CeilToInt(TurnNumber / (float)Mathf.Max(1, divisor)));

        // 지금 아이템 칸의 아이템을 쓸 수 있는지야.
        // 폭탄은 공이 날아가는 중에만, 얼음은 조준 중(발사 전)이나 공이 날아가는 중에 한 턴에 한 번 쓸 수 있어.
        public bool CanUseHeldItem
        {
            get
            {
                if (!initialized || IsGameOver || IsProcessing)
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
                    Ice.PlayFreeze(launcher.LaunchPosition);
                    used = true;
                    break;
            }
            if (used)
                Items.Take();
            return used;
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
            launcher.SetInputBlocked(true);
            IsProcessing = true;
            // 얼음을 쓴 턴이면 내려오지 않아. 서리가 빛나고 가루로 풀리는 연출이 끝나면 다음 턴으로 넘어가.
            if (IsFrozen)
            {
                Ice.PlayRelease(FinishFrozenTurn);
                return;
            }
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
            if (descentDuration <= 0f)
                AdvanceDescent(0f);
        }

        private void Update()
        {
            if (IsProcessing && !IsFrozen)
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
            launcher.SetInputBlocked(false);
        }

        private void AdvanceDescent(float deltaTime)
        {
            descentElapsed += deltaTime;
            float progress = descentDuration <= 0f ? 1f : Mathf.Clamp01(descentElapsed / descentDuration);
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
            // 놓친 아이템은 위험 행에서 보상 없이 치워.
            for (int row = dangerRow; row < rows; row++)
            for (int column = 0; column < columns; column++)
                if (pickups[row, column] != null)
                    ReturnPickup(pickups[row, column]);
            for (int row = dangerRow; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                if (cells[row, column] == null)
                    continue;
                IsGameOver = true;
                GameAudio.Play(SoundCue.GameOver);
                Debug.Log("게임 오버: 벽돌이 위험 행에 도달했어.", this);
                GameOver?.Invoke();
                return;
            }
            TurnNumber++;
            SpawnTopRow();
            launcher.SetInputBlocked(false);
        }

        private void SpawnTopRow()
        {
            for (int i = 0; i < columns; i++)
                columnOrder[i] = i;
            int spawned = 0;
            int health = (int)Math.Min(int.MaxValue, (long)baseHealth + (long)(TurnNumber - 1) * healthPerTurn);
            // 열 순서를 섞어서 왼쪽에만 몰리지 않게 해.
            for (int i = 0; i < columns && spawned < maxBricksPerRow; i++)
            {
                int randomIndex = UnityEngine.Random.Range(i, columns);
                int column = columnOrder[randomIndex];
                columnOrder[randomIndex] = columnOrder[i];
                columnOrder[i] = column;
                if (spawnProbability <= 0f || (spawnProbability < 1f && UnityEngine.Random.value >= spawnProbability))
                    continue;
                Brick brick = pool.Pop();
                cells[0, column] = brick;
                brick.Spawn(this, 0, column, health, brickSize, ElementRules.RandomElement());
                spawned++;
            }
            SpawnPickups();
        }

        private void SpawnPickups()
        {
            if (pickupTemplate == null)
                return;
            int spawned = 0;
            for (int i = 0; i < columns && spawned < maxPickupsPerRow; i++)
            {
                int randomIndex = UnityEngine.Random.Range(i, columns);
                int column = columnOrder[randomIndex];
                columnOrder[randomIndex] = columnOrder[i];
                columnOrder[i] = column;
                if (cells[0, column] != null || pickups[0, column] != null || pickupProbability <= 0f ||
                    (pickupProbability < 1f && UnityEngine.Random.value >= pickupProbability))
                    continue;
                BoardPickup pickup = pickupPool.Pop();
                pickups[0, column] = pickup;
                pickup.Spawn(this, 0, column, Mathf.Min(cellSize.x, cellSize.y) * pickupSizeRatio,
                    ElementRules.RandomElement());
                spawned++;
            }
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
            spawnProbability = Mathf.Clamp01(spawnProbability);
            pickupProbability = Mathf.Clamp01(pickupProbability);
            maxPickupsPerRow = Mathf.Clamp(maxPickupsPerRow, 0, columns);
            pickupSizeRatio = Mathf.Clamp(pickupSizeRatio, 0.1f, 0.9f);
            baseHealth = Mathf.Max(1, baseHealth);
            healthPerTurn = Mathf.Max(0, healthPerTurn);
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
