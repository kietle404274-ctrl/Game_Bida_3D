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
        [SerializeField] private Match.BallInHandController ballInHandController;
        [SerializeField] private UI.CueBallContactSelector contactSelector;

        [Header("Input Tuning")]
        [Tooltip("Bước tăng giảm lực khi lăn con lăn chuột")]
        [SerializeField] private float powerScrollStep = 0.05f;

        [Tooltip("Lực bắn mặc định khi mới vào lượt (0..1)")]
        [SerializeField] private float defaultPower01 = 0.35f;

        [Tooltip("Lực bắn tối thiểu để có thể phát bóng")]
        [SerializeField] private float minPower01 = 0.05f;

        private float currentPower01 = 0.35f;
        private Vector2 cueContact01 = Vector2.zero;
        private int calledBallNumber = -1;
        private int calledPocketId = -1;
        private bool calledSafety = false;
        private bool isTurnActive = true;
        private bool isShotInProgress = false;

        public float CurrentPower01 => currentPower01;
        public Vector2 CueContact01 => cueContact01;
        public int CalledBallNumber => calledBallNumber;
        public int CalledPocketId => calledPocketId;
        public bool CalledSafety => calledSafety;
        public bool IsAimingAllowed => isTurnActive && !isShotInProgress && 
                                       (ballInHandController == null || !ballInHandController.IsActive) &&
                                       (movementMonitor == null || movementMonitor.AreAllBallsResting);

        public event Action<float> OnPowerChanged;
        public event Action<ShotCommand> OnShotInitiated;
        public event Action<int, int, bool> OnCallShotChanged; // (ball, pocket, isSafety)

        private void Start()
        {
            currentPower01 = defaultPower01;

            if (movementMonitor != null)
            {
                movementMonitor.OnAllBallsSettled += HandleAllBallsSettled;
            }

            if (contactSelector != null)
            {
                contactSelector.OnContactPointChanged += SetCueContact;
            }

            if (ballInHandController != null)
            {
                ballInHandController.OnBallInHandConfirmed += HandleBallInHandConfirmed;
            }
        }

        private void OnDestroy()
        {
            if (movementMonitor != null)
            {
                movementMonitor.OnAllBallsSettled -= HandleAllBallsSettled;
            }

            if (contactSelector != null)
            {
                contactSelector.OnContactPointChanged -= SetCueContact;
            }

            if (ballInHandController != null)
            {
                ballInHandController.OnBallInHandConfirmed -= HandleBallInHandConfirmed;
            }
        }

        public void Initialize(CameraDirector camDir, CueController cueCtrl, ShotExecutor executor, BallMovementMonitor monitor, Match.BallInHandController bihCtrl = null, UI.CueBallContactSelector selector = null)
        {
            cameraDirector = camDir;
            cueController = cueCtrl;
            shotExecutor = executor;
            movementMonitor = monitor;
            ballInHandController = bihCtrl;
            contactSelector = selector;

            if (movementMonitor != null)
            {
                movementMonitor.OnAllBallsSettled -= HandleAllBallsSettled;
                movementMonitor.OnAllBallsSettled += HandleAllBallsSettled;
            }

            if (contactSelector != null)
            {
                contactSelector.OnContactPointChanged -= SetCueContact;
                contactSelector.OnContactPointChanged += SetCueContact;
            }

            if (ballInHandController != null)
            {
                ballInHandController.OnBallInHandConfirmed -= HandleBallInHandConfirmed;
                ballInHandController.OnBallInHandConfirmed += HandleBallInHandConfirmed;
            }
        }

        private void HandleBallInHandConfirmed(Vector3 confirmedPos)
        {
            if (cueController != null)
            {
                cueController.SetAimingState(true);
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

        public void SetCallShot(int ballNumber, int pocketId)
        {
            calledBallNumber = ballNumber;
            calledPocketId = pocketId;
            calledSafety = false;
            OnCallShotChanged?.Invoke(calledBallNumber, calledPocketId, calledSafety);
        }

        public void SetCallSafety(bool isSafety)
        {
            calledSafety = isSafety;
            if (isSafety)
            {
                calledBallNumber = -1;
                calledPocketId = -1;
            }
            OnCallShotChanged?.Invoke(calledBallNumber, calledPocketId, calledSafety);
        }

        public void ClearCallShot()
        {
            calledBallNumber = -1;
            calledPocketId = -1;
            calledSafety = false;
            OnCallShotChanged?.Invoke(-1, -1, false);
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
                cueContact01: cueContact01,
                calledBallNumber: calledBallNumber,
                calledPocketId: calledPocketId,
                calledSafety: calledSafety
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
