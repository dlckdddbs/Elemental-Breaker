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
        public GoldWallet Gold { get; } = new GoldWallet();
        public event Action GameOver;

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
            Effects = new GameObject("Brick Effects").AddComponent<BrickEffects>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(Effects.gameObject, gameObject.scene);
            Effects.Initialize(visual.sharedMaterial, visual.sortingLayerID, visual.sortingOrder);
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
            if (IsProcessing)
                AdvanceDescent(Time.deltaTime);
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
