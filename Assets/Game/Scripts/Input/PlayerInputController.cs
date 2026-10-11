using System;
using UnityEngine;
using Game.Billiards.Camera;
using Game.Billiards.Core;
using Game.Billiards.Physics;

namespace Game.Billiards.Input
{
    public class PlayerInputController : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private CameraDirector cameraDirector;
        [SerializeField] private CueController cueController;
        [SerializeField] private ShotExecutor shotExecutor;
        [SerializeField] private BallMovementMonitor movementMonitor;

        [Header("Input Tuning")]
        [Tooltip("Bước tăng giảm lực khi lăn con lăn chuột")]
        [SerializeField] private float powerScrollStep = 0.05f;

        [Tooltip("Lực bắn mặc định khi mới vào lượt (0..1)")]
        [SerializeField] private float defaultPower01 = 0.35f;

        [Tooltip("Lực bắn tối thiểu để có thể phát bóng")]
        [SerializeField] private float minPower01 = 0.05f;

        private float currentPower01 = 0.35f;
        private Vector2 cueContact01 = Vector2.zero;
        private bool isTurnActive = true;
        private bool isShotInProgress = false;

        public float CurrentPower01 => currentPower01;
        public Vector2 CueContact01 => cueContact01;
        public bool IsAimingAllowed => isTurnActive && !isShotInProgress && (movementMonitor == null || movementMonitor.AreAllBallsResting);

        public event Action<float> OnPowerChanged;
        public event Action<ShotCommand> OnShotInitiated;

        private void Start()
        {
            currentPower01 = defaultPower01;

            if (movementMonitor != null)
            {
                movementMonitor.OnAllBallsSettled += HandleAllBallsSettled;
            }
        }

        private void OnDestroy()
        {
            if (movementMonitor != null)
            {
                movementMonitor.OnAllBallsSettled -= HandleAllBallsSettled;
            }
        }

        public void Initialize(CameraDirector camDir, CueController cueCtrl, ShotExecutor executor, BallMovementMonitor monitor)
        {
            cameraDirector = camDir;
            cueController = cueCtrl;
            shotExecutor = executor;
            movementMonitor = monitor;

            if (movementMonitor != null)
            {
                movementMonitor.OnAllBallsSettled -= HandleAllBallsSettled;
                movementMonitor.OnAllBallsSettled += HandleAllBallsSettled;
            }
        }

        public void SetTurnActive(bool active)
        {
            isTurnActive = active;
            if (cueController != null)
            {
                cueController.SetAimingState(IsAimingAllowed);
            }
        }

        public void SetCueContact(Vector2 contact)
        {
            cueContact01 = Vector2.ClampMagnitude(contact, 1f);
        }

        private void Update()
        {
            if (!IsAimingAllowed) return;

            HandleAimRotation();
            HandlePowerAdjustment();
            HandleShootInput();
        }

        private void HandleAimRotation()
        {
            // Xoay hướng ngắm theo chuyển động chuột
            float mouseX = UnityEngine.Input.GetAxis("Mouse X");
            float mouseY = UnityEngine.Input.GetAxis("Mouse Y");

            if (Mathf.Abs(mouseX) > 0.001f || Mathf.Abs(mouseY) > 0.001f)
            {
                if (cameraDirector != null)
                {
                    cameraDirector.RotateAim(mouseX, mouseY);
                }
            }

            // Cập nhật vị trí gậy cơ bám theo hướng ngắm của camera
            if (cueController != null && cameraDirector != null)
            {
                cueController.UpdateAim(cameraDirector.AimDirection, currentPower01);
            }
        }

        private void HandlePowerAdjustment()
        {
            // Lăn con lăn chuột để tăng/giảm lực
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                currentPower01 = Mathf.Clamp(currentPower01 + (Mathf.Sign(scroll) * powerScrollStep), minPower01, 1.0f);
                OnPowerChanged?.Invoke(currentPower01);

                if (cueController != null && cameraDirector != null)
                {
                    cueController.UpdateAim(cameraDirector.AimDirection, currentPower01);
                }
            }
        }

        private void HandleShootInput()
        {
            // Click chuột trái để bắn
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                if (shotExecutor != null && shotExecutor.CanExecuteShot() && cameraDirector != null)
                {
                    ExecutePlayerShot();
                }
            }
        }

        private void ExecutePlayerShot()
        {
            isShotInProgress = true;

            ShotCommand command = new ShotCommand(
                shooterActorId: 0,
                direction: cameraDirector.AimDirection,
                power01: currentPower01,
                cueContact01: cueContact01
            );

            OnShotInitiated?.Invoke(command);

            // Kích hoạt animation cơ đẩy tới -> khi chạm bi thì tác dụng lực vật lý và chuyển camera rộng
            if (cueController != null)
            {
                cueController.TriggerStrikeAnimation(() =>
                {
                    if (shotExecutor != null)
                    {
                        shotExecutor.ExecuteShot(command);
                    }

                    if (cameraDirector != null)
                    {
                        cameraDirector.SwitchToShotCamera();
                    }
                });
            }
            else
            {
                shotExecutor.ExecuteShot(command);
                if (cameraDirector != null)
                {
                    cameraDirector.SwitchToShotCamera();
                }
            }
        }

        private void HandleAllBallsSettled()
        {
            isShotInProgress = false;

            // Khi bi dừng hẳn, camera và cơ trở lại trạng thái ngắm nếu người chơi tiếp tục có lượt
            if (cameraDirector != null)
            {
                cameraDirector.SwitchToAimCamera();
            }

            if (cueController != null && isTurnActive)
            {
                cueController.SetAimingState(true);
                if (cameraDirector != null)
                {
                    cueController.UpdateAim(cameraDirector.AimDirection, currentPower01);
                }
            }
        }
    }
}
