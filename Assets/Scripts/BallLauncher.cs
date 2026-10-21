using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ElementalBreaker
{
    // 조준, 순차 발사, 공 재사용과 턴 종료를 관리해.
    public sealed class BallLauncher : MonoBehaviour
    {
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Ball ball;
        [SerializeField] private Transform launchPoint;
        [Tooltip("플레이 시작 시 생성할 공 개수. 변경 후 플레이를 다시 시작해 주세요.")]
        [SerializeField, Min(1)] private int testBallCount = 5;
        [Tooltip("공 발사 간격(초). 물리 프레임 단위로 적용됩니다.")]
        [SerializeField, Min(0.01f)] private float launchInterval = 0.1f;
        [SerializeField] private LineRenderer aimLine;
        [SerializeField, Min(1f)] private float minimumDragPixels = 20f;
        [SerializeField, Range(1f, 80f)] private float minimumLaunchAngle = 10f;
        [SerializeField, Min(0.1f)] private float aimLineLength = 3f;
        [SerializeField, Min(0.01f)] private float aimLineWidth = 0.04f;
        [SerializeField] private Color aimLineColor = new Color(0.4f, 0.9f, 1f, 1f);
        [SerializeField, Min(0.1f)] private float aimStartHeight = 2f;
        [Header("트레일")]
        [Tooltip("동시에 쓰는 트레일 최대 개수. 공이 이보다 많으면 간격을 두고 나눠 붙여.")]
        [SerializeField, Min(0)] private int maxTrails = 10;
        [Tooltip("공 간격(발사 간격 × 속도)보다 확실히 짧아야 공끼리 이어져 보이지 않아.")]
        [SerializeField, Min(0.01f)] private float trailTime = 0.045f;
        [SerializeField, Min(0.01f)] private float trailWidth = 0.18f;

        // 공이 적어도 두 개 중 하나에만 트레일을 붙여서 화면이 복잡해지지 않게 해.
        private const int MinTrailInterval = 2;

        private InputAction pointerPosition;
        private InputAction pointerPress;
        private Vector2 dragStart;
        private bool dragging;
        private bool ready;
        private Material generatedAimMaterial;
        private readonly List<Ball> balls = new List<Ball>();
        private readonly HashSet<Ball> flyingBalls = new HashSet<Ball>();
        private readonly List<Collider2D> ballColliders = new List<Collider2D>();
        // 공의 발사 순서를 바로 찾으려고 둬. 트레일을 넘길 다음 공을 고를 때 써.
        private readonly Dictionary<Ball, int> ballIndices = new Dictionary<Ball, int>();
        private BallTrailPool trailPool;
        private BallRetireEffects retireEffects;
        // 공끼리 부딪히지 않게 쓰는 레이어야. 프로젝트에 Ball 레이어가 없으면 -1이야.
        private int ballLayer = -1;
        private const string BallLayerName = "Ball";
        private Vector2 turnLaunchPosition;
        private Vector2 turnDirection;
        private int nextBallIndex;
        // 아직 날고 있는 공 중 가장 먼저 쏜 공의 순번이야. 앞으로만 넘어가서 찾는 비용이 거의 없어.
        private int frontBallIndex;
        private float launchCountdown;
        private bool hasReturnPosition;
        private int acquiredBallCount;
        private bool needsTurnPreparation = true;
        public ElementType TurnElement { get; private set; }
        public ElementType? ReservedElement { get; private set; }

        public void ReserveNextElement(ElementType element)
        {
            if (!IsTurnActive)
                return;
            // 나중에 먹은 속성으로 다음 턴 예약을 덮어써.
            ReservedElement = element;
        }

        public bool IsTurnActive { get; private set; }
        public int PendingBallCount { get; private set; }
        public int FlyingBallCount => flyingBalls.Count;
        public int BallCount => balls.Count + acquiredBallCount;
        // 이번 턴에 발사하는 공 수야. 이번 턴에 얻은 공은 다음 턴부터 합류해서 따로 세.
        public int ActiveBallCount => balls.Count;
        public int AcquiredBallCount => acquiredBallCount;

        // 맨 앞 공: 남아 있는 공 중 가장 앞에 있는 공. 앞의 공이 회수되거나 퇴장하면 다음 공이 맨 앞이 돼.
        public Ball FrontBall
        {
            get
            {
                if (!IsTurnActive)
                    return null;
                while (frontBallIndex < nextBallIndex && !balls[frontBallIndex].IsFlying)
                    frontBallIndex++;
                return frontBallIndex < nextBallIndex ? balls[frontBallIndex] : null;
            }
        }
        public int CompletedTurnCount { get; private set; }
        public bool IsInputBlocked { get; private set; }
        public event Action TurnEnded;

        // 발사 전이라 상점이나 공 속성 선택을 쓸 수 있는 상태야.
        public bool CanPrepare => ready && !IsTurnActive && !IsInputBlocked;

        // 발사 전에 이번 턴 공 속성을 바꿔. 대기 중인 공들의 색과 오라도 바로 바뀌어.
        public void SetTurnElement(ElementType element)
        {
            if (!CanPrepare || element == TurnElement)
                return;
            TurnElement = element;
            foreach (Ball pooledBall in balls)
                pooledBall.PrepareTurn(launchPoint.position, element);
        }

        // UI(버튼, 상점 목록) 위를 누르면 조준을 시작하지 않으려고 화면 위치로 UI를 검사해.
        private PointerEventData uiPointer;
        private EventSystem uiPointerOwner;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        private bool IsPointerOverUI(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;
            if (uiPointer == null || uiPointerOwner != eventSystem)
            {
                uiPointer = new PointerEventData(eventSystem);
                uiPointerOwner = eventSystem;
            }
            uiPointer.position = screenPosition;
            uiHits.Clear();
            eventSystem.RaycastAll(uiPointer, uiHits);
            return uiHits.Count > 0;
        }

        public void AddBall()
        {
            // 현재 턴 발사 목록은 유지하고 다음 턴에 합류시켜.
            acquiredBallCount++;
        }

        public void SetInputBlocked(bool blocked)
        {
            IsInputBlocked = blocked;
            if (blocked)
                CancelAim();
            else if (ready && !IsTurnActive)
                PrepareUpcomingTurn();
        }

        private void PrepareUpcomingTurn()
        {
            if (!needsTurnPreparation)
                return;
            needsTurnPreparation = false;
            // 속성 아이템을 안 먹었으면 직전 턴 속성을 유지해. 첫 턴은 기본값인 일반이야.
            TurnElement = ReservedElement ?? TurnElement;
            ReservedElement = null;
            foreach (Ball pooledBall in balls)
                pooledBall.PrepareTurn(launchPoint.position, TurnElement);
        }

        private void Awake()
        {
            // 프로젝트의 새 Input System을 써. 별도 PlayerInput은 필요 없어.
            pointerPosition = new InputAction("Aim Position", InputActionType.PassThrough, "<Pointer>/position");
            pointerPress = new InputAction("Aim Press", InputActionType.Button, "<Pointer>/press");
            if (aimCamera == null)
                aimCamera = Camera.main;
            PrepareAimLine();
        }

        private void PrepareAimLine()
        {
            // 연결된 선이 없으면 시작할 때 한 번만 만들어서 계속 써.
            if (aimLine == null)
            {
                GameObject lineObject = new GameObject("AimLine");
                lineObject.transform.SetParent(transform, false);
                aimLine = lineObject.AddComponent<LineRenderer>();
            }

            if (aimLine.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    Debug.LogWarning("조준선에 쓸 Unlit 재질을 Aim Line의 Materials에 연결해.", this);
                    aimLine.enabled = false;
                    aimLine = null;
                    return;
                }
                generatedAimMaterial = new Material(shader);
                aimLine.sharedMaterial = generatedAimMaterial;
            }

            aimLine.useWorldSpace = true;
            aimLine.positionCount = 2;
            aimLine.widthMultiplier = 1f;
            aimLine.startWidth = aimLineWidth;
            aimLine.endWidth = aimLineWidth;
            aimLine.startColor = aimLineColor;
            aimLine.endColor = aimLineColor;
            aimLine.sortingOrder = 10;
            aimLine.numCapVertices = 4;
            aimLine.enabled = false;
        }

        private void OnEnable()
        {
            pointerPosition.Enable();
            pointerPress.Enable();
        }

        private void Start()
        {
            if (aimCamera == null || ball == null || launchPoint == null)
            {
                Debug.LogError("BallLauncher: Aim Camera, Ball, Launch Point를 연결해.", this);
                enabled = false;
                return;
            }
            // 공끼리 충돌은 레이어 설정 한 번으로 꺼서, 발사할 때마다 공 수만큼 반복하지 않게 해.
            ballLayer = LayerMask.NameToLayer(BallLayerName);
            if (ballLayer >= 0)
                Physics2D.IgnoreLayerCollision(ballLayer, ballLayer, true);
            else
                Debug.LogWarning("Ball 레이어가 없어서 공끼리 충돌을 발사할 때마다 하나씩 꺼. Tags and Layers에 Ball 레이어를 추가해.", this);
            // 최초 한 번만 생성하고 이후 턴에는 같은 공들을 재사용해.
            bool hasSceneBall = ball.gameObject.scene.IsValid();
            for (int i = 0; i < Mathf.Max(1, testBallCount); i++)
            {
                // 프리팹 원본에는 접근하지 않고 생성된 공만 준비해.
                Ball pooledBall = i == 0 && hasSceneBall ? ball : CreateBall();
                pooledBall.Prepare(launchPoint.position);
                pooledBall.gameObject.SetActive(true);
                AddToPool(pooledBall);
            }
            SpriteRenderer ballVisual = ball.GetComponent<SpriteRenderer>();
            int ballOrder = ballVisual != null ? ballVisual.sortingOrder : 0;
            if (ballVisual != null)
            {
                // 퇴장 이펙트는 벽돌 바로 앞에서 터지니까 벽돌 숫자(+2)보다 위에 그려.
                retireEffects = new GameObject("Ball Retire Effects").AddComponent<BallRetireEffects>();
                retireEffects.transform.SetParent(transform, false);
                retireEffects.Initialize(ballVisual.sharedMaterial, ballVisual.sortingLayerID, ballOrder + 3,
                    ball.transform.lossyScale.x);
            }
            if (maxTrails > 0)
            {
                // 조준선 재질을 같이 써서 재질을 따로 만들지 않아.
                // 공 바로 뒤(ballOrder - 1)는 속성 오라 자리라 트레일은 그보다 뒤에 그려.
                trailPool = new BallTrailPool(transform, maxTrails, trailTime, trailWidth, ballOrder - 2,
                    aimLine != null ? aimLine.sharedMaterial : null);
            }
            ready = true;
            PrepareUpcomingTurn();
        }

        private Ball CreateBall()
        {
            Transform parent = ball.gameObject.scene.IsValid() ? ball.transform.parent : null;
            Ball created = Instantiate(ball, parent);
            created.Prepare(launchPoint.position);
            created.gameObject.SetActive(true);
            return created;
        }

        private void AddToPool(Ball pooledBall)
        {
            if (ballLayer >= 0)
                pooledBall.gameObject.layer = ballLayer;
            pooledBall.Returned += OnBallReturned;
            pooledBall.Retired += OnBallRetired;
            ballIndices.Add(pooledBall, balls.Count);
            balls.Add(pooledBall);
            ballColliders.Add(pooledBall.GetComponent<Collider2D>());
        }

        private void AssignTrails()
        {
            // 속성 공은 오라가 꼬리 역할을 해서 트레일은 일반 공에만 붙여.
            if (trailPool == null || TurnElement != ElementType.Normal)
                return;
            // 공이 많으면 간격을 두고 붙여서 공 줄 전체에 트레일이 고르게 보이게 해.
            int interval = Mathf.Max(MinTrailInterval, Mathf.CeilToInt(balls.Count / (float)maxTrails));
            for (int i = 0; i < balls.Count; i += interval)
            {
                TrailRenderer trail = trailPool.Rent();
                if (trail == null)
                    return;
                balls[i].AttachTrail(trail);
            }
        }

        private void PassTrail(Ball stoppedBall)
        {
            if (trailPool == null || !stoppedBall.HasTrail)
                return;
            TrailRenderer trail = stoppedBall.DetachTrail();
            // 바로 뒤에서 날고 있거나 아직 발사 전인 공에 넘겨서 맨 앞 공에 트레일이 남게 해.
            for (int i = ballIndices[stoppedBall] + 1; i < balls.Count; i++)
            {
                Ball candidate = balls[i];
                if (!candidate.HasTrail && (candidate.IsFlying || i >= nextBallIndex))
                {
                    candidate.AttachTrail(trail);
                    return;
                }
            }
            trailPool.Return(trail);
        }

        private void BeginTurn(Vector2 direction)
        {
            if (!ready || IsTurnActive || IsInputBlocked)
                return;

            // 획득한 만큼만 풀을 늘리고 이후에는 계속 재사용해.
            while (acquiredBallCount > 0)
            {
                Ball addedBall = CreateBall();
                addedBall.PrepareTurn(launchPoint.position, TurnElement);
                AddToPool(addedBall);
                acquiredBallCount--;
            }
            IsTurnActive = true;
            hasReturnPosition = false;
            turnLaunchPosition = launchPoint.position;
            turnDirection = direction;
            nextBallIndex = 0;
            frontBallIndex = 0;
            PendingBallCount = balls.Count;
            AssignTrails();
            LaunchNextBall();
        }

        private void LaunchNextBall()
        {
            Ball nextBall = balls[nextBallIndex];
            Collider2D nextCollider = ballColliders[nextBallIndex];
            nextBallIndex++;
            PendingBallCount--;
            nextBall.Prepare(turnLaunchPosition);
            flyingBalls.Add(nextBall);
            nextBall.Launch(turnDirection);
            // Ball 레이어가 있으면 공끼리 충돌은 레이어 설정으로 이미 꺼져 있어. 없을 때만 예전처럼 하나씩 꺼.
            if (ballLayer < 0)
            {
                // 회수 시 물리 시뮬레이션을 끄므로 매 발사 때 충돌 제외를 다시 설정해.
                for (int i = 0; i < balls.Count; i++)
                {
                    if (balls[i] != nextBall && balls[i].IsFlying)
                        Physics2D.IgnoreCollision(nextCollider, ballColliders[i]);
                }
            }
            launchCountdown = Mathf.Max(0.01f, launchInterval);
        }

        private void OnBallReturned(Ball returnedBall)
        {
            if (!IsTurnActive || !flyingBalls.Remove(returnedBall))
                return;
            PassTrail(returnedBall);

            if (!hasReturnPosition)
            {
                hasReturnPosition = true;
                Vector3 nextPosition = launchPoint.position;
                nextPosition.x = returnedBall.transform.position.x;
                launchPoint.position = nextPosition;
            }
            returnedBall.Prepare(launchPoint.position);

            TryEndTurn();
        }

        private void OnBallRetired(Ball retiredBall)
        {
            if (!IsTurnActive || !flyingBalls.Remove(retiredBall))
                return;
            if (retireEffects != null)
                retireEffects.Play(retiredBall.Element, retiredBall.RetirePosition, retiredBall.RetireDirection);
            PassTrail(retiredBall);
            TryEndTurn();
        }

        private void TryEndTurn()
        {
            if (PendingBallCount != 0 || FlyingBallCount != 0)
                return;

            IsTurnActive = false;
            needsTurnPreparation = true;
            CompletedTurnCount++;
            CancelAim();
            TurnEnded?.Invoke();
            if (!IsInputBlocked)
                PrepareUpcomingTurn();
        }

        private void FixedUpdate()
        {
            if (!IsTurnActive || PendingBallCount == 0)
                return;

            launchCountdown -= Time.fixedDeltaTime;
            if (launchCountdown <= 0f)
                LaunchNextBall();
        }

        private void Update()
        {
            if (!ready || IsTurnActive || IsInputBlocked)
            {
                CancelAim();
                return;
            }

            Vector2 screenPosition = pointerPosition.ReadValue<Vector2>();
            if (pointerPress.WasPressedThisFrame() && !IsPointerOverUI(screenPosition) && TryWorldPoint(screenPosition, out Vector2 start))
            {
                // 벽돌 쪽에서 시작한 드래그는 무시해.
                if (start.y <= launchPoint.position.y + aimStartHeight &&
                    aimCamera.pixelRect.Contains(screenPosition))
                {
                    dragStart = screenPosition;
                    dragging = true;
                }
            }

            if (!dragging)
                return;

            bool valid = TryAimDirection(screenPosition, out Vector2 direction);
            if (aimLine != null)
            {
                aimLine.enabled = valid;
                if (valid)
                {
                    aimLine.SetPosition(0, launchPoint.position);
                    aimLine.SetPosition(1, launchPoint.position + (Vector3)(direction * aimLineLength));
                }
            }

            if (pointerPress.WasReleasedThisFrame())
            {
                CancelAim();
                if (valid)
                    BeginTurn(direction);
            }
        }

        private bool TryAimDirection(Vector2 screenPosition, out Vector2 direction)
        {
            direction = Vector2.zero;
            if ((dragStart - screenPosition).sqrMagnitude < minimumDragPixels * minimumDragPixels ||
                !TryWorldPoint(dragStart, out Vector2 start) ||
                !TryWorldPoint(screenPosition, out Vector2 end))
                return false;

            Vector2 drag = start - end;
            if (drag.y <= 0f || drag.sqrMagnitude < 0.0001f)
                return false;

            // 너무 낮은 각도는 올려서 공이 옆으로만 날아가지 않게 해.
            float angle = Mathf.Atan2(drag.y, drag.x) * Mathf.Rad2Deg;
            angle = Mathf.Clamp(angle, minimumLaunchAngle, 180f - minimumLaunchAngle) * Mathf.Deg2Rad;
            direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            return true;
        }

        private bool TryWorldPoint(Vector2 screenPosition, out Vector2 point)
        {
            Plane plane = new Plane(Vector3.forward, launchPoint.position);
            Ray ray = aimCamera.ScreenPointToRay(screenPosition);
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }
            point = Vector2.zero;
            return false;
        }

        private void CancelAim()
        {
            dragging = false;
            if (aimLine != null)
                aimLine.enabled = false;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                CancelAim();
        }

        private void OnDisable()
        {
            CancelAim();
            pointerPosition?.Disable();
            pointerPress?.Disable();
        }

        private void OnDestroy()
        {
            foreach (Ball pooledBall in balls)
            {
                if (pooledBall == null)
                    continue;
                pooledBall.Returned -= OnBallReturned;
                pooledBall.Retired -= OnBallRetired;
                if (pooledBall != ball)
                    Destroy(pooledBall.gameObject);
            }
            pointerPosition?.Dispose();
            pointerPress?.Dispose();
            if (generatedAimMaterial != null)
                Destroy(generatedAimMaterial);
            trailPool?.Dispose();
        }
    }
}
