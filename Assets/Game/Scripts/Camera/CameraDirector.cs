using UnityEngine;

namespace Game.Billiards.Camera
{
    public enum CameraState
    {
        Aim,
        ShotWide,
        BallInHand,
        Cinematic
    }

    public class CameraDirector : MonoBehaviour
    {
        [Header("Target & References")]
        [Tooltip("Transform của bi cái để camera ngắm bám theo")]
        [SerializeField] private Transform cueBallTarget;

        [Tooltip("Camera chính")]
        [SerializeField] private UnityEngine.Camera mainCamera;

        [Header("Aim Camera Settings")]
        [Tooltip("Khoảng cách từ camera đến bi cái khi ngắm")]
        [SerializeField] private float aimDistance = 1.15f;

        [Tooltip("Độ cao bù thêm so với tâm bi")]
        [SerializeField] private float aimHeightOffset = 0.08f;

        [Tooltip("Góc Pitch tối thiểu (độ) để không xuyên mặt bàn")]
        [SerializeField] private float minPitch = 8f;

        [Tooltip("Góc Pitch tối đa (độ)")]
        [SerializeField] private float maxPitch = 55f;

        [Tooltip("Độ nhạy xoay ngắm của chuột")]
        [SerializeField] private float mouseSensitivity = 2.5f;

        [Tooltip("Tốc độ mượt mà khi bám vị trí")]
        [SerializeField] private float smoothSpeed = 12f;

        [Header("Wide Shot Camera Settings")]
        [Tooltip("Vị trí camera góc rộng theo dõi toàn bàn khi bi đang chạy")]
        [SerializeField] private Vector3 wideCameraPosition = new Vector3(0f, 3.2f, -1.5f);

        [Tooltip("Điểm nhìn của camera góc rộng (thường là tâm bàn bida)")]
        [SerializeField] private Vector3 tableCenter = new Vector3(0f, 0.835f, 0f);

        [Tooltip("Tốc độ chuyển đổi giữa các góc camera")]
        [SerializeField] private float transitionSpeed = 4.0f;

        private CameraState currentState = CameraState.Aim;
        private float currentYaw = 0f;
        private float currentPitch = 18f;

        // Camera Micro-Shake (M10)
        private float shakeTimer = 0f;
        private float totalShakeDuration = 0f;
        private float currentShakeIntensity = 0f;

        public CameraState CurrentState => currentState;
        public UnityEngine.Camera MainCamera => mainCamera;

        /// <summary>
        /// Hướng ngắm cơ trên mặt phẳng nằm ngang XZ (hướng từ camera tới bi cái)
        /// </summary>
        public Vector3 AimDirection
        {
            get
            {
                Quaternion rot = Quaternion.Euler(0f, currentYaw, 0f);
                return (rot * Vector3.forward).normalized;
            }
        }

        private void Awake()
        {
            if (mainCamera == null)
            {
                mainCamera = UnityEngine.Camera.main;
            }
        }

        public void SetCueBallTarget(Transform target)
        {
            cueBallTarget = target;
            if (target != null)
            {
                // Hướng ngắm mặc định ban đầu hướng về tâm bàn
                Vector3 toCenter = (tableCenter - target.position);
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude > 0.01f)
                {
                    currentYaw = Quaternion.LookRotation(toCenter).eulerAngles.y;
                }
            }
        }

        public void RotateAim(float mouseDeltaX, float mouseDeltaY)
        {
            if (currentState != CameraState.Aim) return;

            currentYaw += mouseDeltaX * mouseSensitivity;
            currentPitch -= mouseDeltaY * mouseSensitivity;
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
        }

        public void SwitchToShotCamera()
        {
            currentState = CameraState.ShotWide;
        }

        public void SwitchToAimCamera()
        {
            currentState = CameraState.Aim;
        }

        public void TriggerCameraShake(float duration = 0.22f, float intensity = 0.04f)
        {
            shakeTimer = duration;
            totalShakeDuration = duration;
            currentShakeIntensity = intensity;
        }

        private void LateUpdate()
        {
            if (mainCamera == null) return;

            switch (currentState)
            {
                case CameraState.Aim:
                    UpdateAimCamera();
                    break;

                case CameraState.ShotWide:
                    UpdateWideShotCamera();
                    break;
            }

            // Áp dụng Camera Micro-Shake nếu đang kích hoạt (M10)
            if (shakeTimer > 0f && totalShakeDuration > 0f)
            {
                shakeTimer -= Time.deltaTime;
                float progress = Mathf.Clamp01(shakeTimer / totalShakeDuration);
                float currentIntensity = currentShakeIntensity * progress;

                Vector3 shakeOffset = new Vector3(
                    Mathf.Sin(Time.time * 55f) * currentIntensity,
                    Mathf.Cos(Time.time * 45f) * currentIntensity,
                    0f
                );
                mainCamera.transform.position += shakeOffset;
            }
        }

        private void UpdateAimCamera()
        {
            if (cueBallTarget == null) return;

            Vector3 focusPoint = cueBallTarget.position + (Vector3.up * aimHeightOffset);
            Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 desiredPosition = focusPoint - (rotation * Vector3.forward * aimDistance);

            // Giữ camera luôn ở trên mặt nỉ ít nhất 0.15m để không bao giờ cắt qua bàn
            if (desiredPosition.y < tableCenter.y + 0.15f)
            {
                desiredPosition.y = tableCenter.y + 0.15f;
            }

            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, desiredPosition, Time.deltaTime * smoothSpeed);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, Quaternion.LookRotation(focusPoint - mainCamera.transform.position), Time.deltaTime * smoothSpeed);
        }

        private void UpdateWideShotCamera()
        {
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, wideCameraPosition, Time.deltaTime * transitionSpeed);
            Quaternion lookRot = Quaternion.LookRotation(tableCenter - mainCamera.transform.position);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, lookRot, Time.deltaTime * transitionSpeed);
        }
    }
}
