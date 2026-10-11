using System;
using System.Collections.Generic;
using UnityEngine;
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

        private InputAction pointerPosition;
        private InputAction pointerPress;
        private Vector2 dragStart;
        private bool dragging;
        private bool ready;
        private Material generatedAimMaterial;
        private readonly List<Ball> balls = new List<Ball>();
        private readonly HashSet<Ball> flyingBalls = new HashSet<Ball>();
        private readonly List<Collider2D> ballColliders = new List<Collider2D>();
        private Vector2 turnLaunchPosition;
        private Vector2 turnDirection;
        private int nextBallIndex;
        private float launchCountdown;
        private bool hasReturnPosition;

        public bool IsTurnActive { get; private set; }
        public int PendingBallCount { get; private set; }
        public int FlyingBallCount => flyingBalls.Count;
        public int BallCount => balls.Count;
        public int CompletedTurnCount { get; private set; }
        public bool IsInputBlocked { get; private set; }
        public event Action TurnEnded;

        public void SetInputBlocked(bool blocked)
        {
            IsInputBlocked = blocked;
            if (blocked)
                CancelAim();
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
            // 최초 한 번만 생성하고 이후 턴에는 같은 공들을 재사용해.
            ball.Prepare(launchPoint.position);
            for (int i = 0; i < Mathf.Max(1, testBallCount); i++)
            {
                Ball pooledBall = i == 0 ? ball : Instantiate(ball, ball.transform.parent);
                pooledBall.Prepare(launchPoint.position);
                pooledBall.Returned += OnBallReturned;
                balls.Add(pooledBall);
                ballColliders.Add(pooledBall.GetComponent<Collider2D>());
            }
            ready = true;
        }

        private void BeginTurn(Vector2 direction)
        {
            if (!ready || IsTurnActive || IsInputBlocked)
                return;

            IsTurnActive = true;
            hasReturnPosition = false;
            turnLaunchPosition = launchPoint.position;
            turnDirection = direction;
            nextBallIndex = 0;
            PendingBallCount = balls.Count;
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
            // 회수 시 물리 시뮬레이션을 끄므로 매 발사 때 충돌 제외를 다시 설정해.
            for (int i = 0; i < balls.Count; i++)
            {
                if (balls[i] != nextBall && balls[i].IsFlying)
                    Physics2D.IgnoreCollision(nextCollider, ballColliders[i]);
            }
            launchCountdown = Mathf.Max(0.01f, launchInterval);
        }

        private void OnBallReturned(Ball returnedBall)
        {
            if (!IsTurnActive || !flyingBalls.Remove(returnedBall))
                return;

            if (!hasReturnPosition)
            {
                hasReturnPosition = true;
                Vector3 nextPosition = launchPoint.position;
                nextPosition.x = returnedBall.transform.position.x;
                launchPoint.position = nextPosition;
            }
            returnedBall.Prepare(launchPoint.position);

            if (PendingBallCount != 0 || FlyingBallCount != 0)
                return;

            IsTurnActive = false;
            CompletedTurnCount++;
            CancelAim();
            TurnEnded?.Invoke();
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
            if (pointerPress.WasPressedThisFrame() && TryWorldPoint(screenPosition, out Vector2 start))
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
                if (pooledBall != ball)
                    Destroy(pooledBall.gameObject);
            }
            pointerPosition?.Dispose();
            pointerPress?.Dispose();
            if (generatedAimMaterial != null)
                Destroy(generatedAimMaterial);
        }
    }
}
