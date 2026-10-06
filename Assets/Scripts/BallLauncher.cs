using UnityEngine;
using UnityEngine.InputSystem;

namespace ElementalBreaker
{
    // 아래로 당겨 조준하고 손을 떼면 공 하나를 발사해.
    public sealed class BallLauncher : MonoBehaviour
    {
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Ball ball;
        [SerializeField] private Transform launchPoint;
        [SerializeField] private LineRenderer aimLine;
        [SerializeField, Min(1f)] private float minimumDragPixels = 20f;
        [SerializeField, Range(1f, 80f)] private float minimumLaunchAngle = 10f;
        [SerializeField, Min(0.1f)] private float aimLineLength = 3f;
        [SerializeField, Min(0.1f)] private float aimStartHeight = 2f;

        private InputAction pointerPosition;
        private InputAction pointerPress;
        private Vector2 dragStart;
        private bool dragging;
        private bool ready;

        private void Awake()
        {
            // 프로젝트의 새 Input System을 써. 별도 PlayerInput은 필요 없어.
            pointerPosition = new InputAction("Aim Position", InputActionType.PassThrough, "<Pointer>/position");
            pointerPress = new InputAction("Aim Press", InputActionType.Button, "<Pointer>/press");
            if (aimCamera == null)
                aimCamera = Camera.main;
            if (aimLine != null)
            {
                aimLine.useWorldSpace = true;
                aimLine.positionCount = 2;
                aimLine.enabled = false;
            }
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
            ball.Prepare(launchPoint.position);
            ready = true;
        }

        private void Update()
        {
            if (!ready || ball.IsFlying)
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
                    ball.Launch(direction);
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
            pointerPosition?.Dispose();
            pointerPress?.Dispose();
        }
    }
}
