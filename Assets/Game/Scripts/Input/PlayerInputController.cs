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
        [SerializeField] private Match.MatchStateManager matchStateManager;
        [SerializeField] private Match.TurnManager turnManager;

        [Header("Input Tuning")]
        [Tooltip("Bước tăng giảm lực khi lăn con lăn chuột")]
        [SerializeField] private float powerScrollStep = 0.05f;

        [Tooltip("Lực bắn mặc định khi mới vào lượt (0..1)")]
        [SerializeField] private float resetPower01 = 0.25f;

        [Tooltip("Lực bắn tối thiểu để có thể phát bóng")]
        [SerializeField] private float minPower01 = 0.05f;

        private float currentPower01 = 0.35f;
        private Vector2 cueContact01 = Vector2.zero;
        private int calledBallNumber = -1;
        private int calledPocketId = -1;
        private bool calledSafety = false;
        private bool isTurnActive = true;
        private bool isShotInProgress = false;

        private Network.NetworkMatchManager networkMatchManager;

        public float CurrentPower01 => currentPower01;
        public Vector2 CueContact01 => cueContact01;
        public int CalledBallNumber => calledBallNumber;
        public int CalledPocketId => calledPocketId;
        public bool CalledSafety => calledSafety;
        public bool IsAimingAllowed => isTurnActive && !isShotInProgress && !IsOpponentTurn() &&
                                       (ballInHandController == null || !ballInHandController.IsActive) &&
                                       (movementMonitor == null || movementMonitor.AreAllBallsResting);

        /// <summary>
        /// True khi hiện đang là lượt của đối thủ (AI hoặc người chơi khác trong trận online).
        /// Kiểm tra trực tiếp theo trạng thái trận đấu mỗi khung hình để khóa hoàn toàn thao tác của người chơi.
        /// </summary>
        public bool IsOpponentTurn()
        {
            if (matchStateManager == null || matchStateManager.State == null) return false;

            var players = matchStateManager.State.players;
            int cur = matchStateManager.CurrentPlayerIndex;
            if (players == null || cur < 0 || cur >= players.Length) return false;

            var session = GameSession.Instance;
            if (session != null && session.CurrentGameMode == GameMode.OnlinePvP)
            {
                if (networkMatchManager == null) networkMatchManager = FindObjectOfType<Network.NetworkMatchManager>();
                return networkMatchManager != null && networkMatchManager.IsOnlineMatch && cur != networkMatchManager.LocalPlayerIndex;
            }

            if (session != null && session.CurrentGameMode == GameMode.LocalPractice) return false;

            return players[cur].isAI;
        }

        public event Action<float> OnPowerChanged;
        public event Action<ShotCommand> OnShotInitiated;
        public event Action<int, int, bool> OnCallShotChanged; // (ball, pocket, isSafety)

        private void Start()
        {
            currentPower01 = resetPower01;
            isTurnActive = true;
            isShotInProgress = false;

            if (movementMonitor != null)
            {
                movementMonitor.OnAllBallsSettled += HandleAllBallsSettled;
            }

            if (contactSelector == null)
            {
                contactSelector = FindObjectOfType<UI.CueBallContactSelector>();
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
                ballInHandController.InputBlockedPredicate = IsOpponentTurn;
            }

            if (cueController != null && cameraDirector != null)
            {
                cueController.SetAimingState(true);
                cueController.UpdateAim(cameraDirector.AimDirection, currentPower01);
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

            if (turnManager != null)
            {
                turnManager.OnTurnChanged -= HandleTurnChanged;
            }

            if (matchStateManager != null)
            {
                matchStateManager.OnRackStarted -= HandleRackStarted;
            }
        }

        public void Initialize(CameraDirector camDir, CueController cueCtrl, ShotExecutor executor, BallMovementMonitor monitor, Match.BallInHandController bihCtrl = null, UI.CueBallContactSelector selector = null, Match.MatchStateManager stateMgr = null, Match.TurnManager turnMgr = null)
        {
            cameraDirector = camDir;
            cueController = cueCtrl;
            shotExecutor = executor;
            movementMonitor = monitor;
            ballInHandController = bihCtrl;
            contactSelector = selector;
            if (stateMgr != null) matchStateManager = stateMgr;
            if (turnMgr != null) turnManager = turnMgr;

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
                ballInHandController.InputBlockedPredicate = IsOpponentTurn;
            }

            if (turnManager != null)
            {
                turnManager.OnTurnChanged -= HandleTurnChanged;
                turnManager.OnTurnChanged += HandleTurnChanged;
            }

            if (matchStateManager != null)
            {
                matchStateManager.OnRackStarted -= HandleRackStarted;
                matchStateManager.OnRackStarted += HandleRackStarted;
            }

            // Đồng bộ trạng thái lượt ban đầu
            if (matchStateManager != null && matchStateManager.State != null)
            {
                HandleTurnChanged(matchStateManager.CurrentPlayerIndex);
            }
        }

        /// <summary>
        /// Ván mới bắt đầu: reset cờ trạng thái bắn, đồng bộ lượt, camera và đặt lại cây cơ sau bi cái để phá bi.
        /// </summary>
        private void HandleRackStarted()
        {
            isShotInProgress = false;
            currentPower01 = resetPower01;
            calledBallNumber = -1;
            calledPocketId = -1;
            calledSafety = false;

            HandleTurnChanged(matchStateManager != null ? matchStateManager.CurrentPlayerIndex : 0);

            // Cú phá bi: bất kể ai phá, camera đặt ở đầu bàn nhìn thẳng vào dàn bi và cây cơ luôn hiện sau bi cái
            if (cameraDirector != null)
            {
                var ballMgr = FindObjectOfType<Match.BallManager>();
                if (ballMgr != null && ballMgr.CueBall != null)
                {
                    cameraDirector.SetCueBallTarget(ballMgr.CueBall.transform);
                    if (cueController != null) cueController.SetCueBallTarget(ballMgr.CueBall.transform);
                }
                // Người chơi phá: camera đầu bàn nhìn vào dàn bi. Đối thủ (AI/online) phá: giữ camera góc nhìn đối thủ
                if (IsOpponentTurn()) cameraDirector.SwitchToOpponentCamera();
                else cameraDirector.SwitchToAimCamera();
            }

            if (cueController != null)
            {
                cueController.SetAimingState(true);
                if (cameraDirector != null)
                {
                    cueController.UpdateAim(cameraDirector.AimDirection, currentPower01);
                }
            }

            OnPowerChanged?.Invoke(currentPower01);
        }

        private void ResetPowerToDefault()
        {
            currentPower01 = resetPower01;
            OnPowerChanged?.Invoke(currentPower01);
        }

        public void ResetShotState()
        {
            isShotInProgress = false;
            ResetPowerToDefault();
            ResetCueContact();
        }

        private void HandleTurnChanged(int newPlayerIndex)
        {
            isShotInProgress = false;
            ResetPowerToDefault();
            ResetCueContact();
            if (matchStateManager == null || matchStateManager.State == null) return;

            bool opponentTurn = IsOpponentTurn();
            SetTurnActive(!opponentTurn);

            if (cameraDirector != null)
            {
                try
                {
                    if (opponentTurn)
                    {
                        // Đến lượt đối thủ: camera ra cuối bàn, thấy trọn mặt bàn
                        cameraDirector.SwitchToOpponentCamera();
                    }
                    else if (cameraDirector.CurrentState != CameraState.Aim &&
                             (ballInHandController == null || !ballInHandController.IsActive))
                    {
                        // Đến lượt mình (và không phải đang đặt bi top-down): trở về góc ngắm phía sau bi cái
                        cameraDirector.SwitchToAimCamera();
                    }
                }
                catch (Exception e)
                {
                    // Lỗi camera không được phép làm gián đoạn chuỗi sự kiện đổi lượt (AI cần nhận sự kiện này)
                    Debug.LogException(e);
                }
            }
        }

        private void HandleBallInHandConfirmed(Vector3 confirmedPos)
        {
            if (cueController != null && !IsOpponentTurn())
            {
                cueController.SetAimingState(true);
            }
        }

        public void SetTurnActive(bool active)
        {
            isTurnActive = active;
            if (active)
            {
                isShotInProgress = false;
            }
            if (cueController != null)
            {
                cueController.SetAimingState(IsAimingAllowed);
            }
        }

        public void SetCueContact(Vector2 contact)
        {
            cueContact01 = Vector2.ClampMagnitude(contact, 1f);
        }

        public void ResetCueContact()
        {
            cueContact01 = Vector2.zero;
            if (contactSelector == null)
            {
                contactSelector = FindObjectOfType<UI.CueBallContactSelector>();
            }
            if (contactSelector != null)
            {
                contactSelector.ResetToCenter();
                if (contactSelector.IsOpen) contactSelector.SetOpen(false);
            }
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

        private void LateUpdate()
        {
            // Chốt chặn: lượt đối thủ nhưng chưa tới pha ngắm (đang xử lý lỗi / đối thủ đặt bi)
            // thì cây cơ của người chơi phải ẩn hẳn. AI chỉ tự hiện cơ khi đã vào pha Aiming/BreakSetup.
            if (cueController == null || !cueController.IsVisible || cueController.IsStriking) return;
            if (matchStateManager == null || matchStateManager.State == null) return;

            var phase = matchStateManager.CurrentPhase;
            bool aimPhase = phase == MatchPhase.Aiming || phase == MatchPhase.BreakSetup;
            if (!aimPhase && IsOpponentTurn())
            {
                cueController.SetAimingState(false);
            }
        }

        private void Update()
        {
            if (!IsAimingAllowed) return;

            // Popup chọn điểm cơ đang mở (phím J): giữ nguyên cây cơ, chỉ chọn điểm rồi nhấn Enter
            if (contactSelector != null && contactSelector.IsOpen) return;

            // Popup Hướng dẫn / Luật / Menu đang mở: không ngắm, chỉnh lực hay bắn
            if (UI.HudModalState.IsModalOpen) return;

            // Phím V: chuyển camera ngắm gần / xa (trạng thái giữ trong phiên chơi)
            if (UnityEngine.Input.GetKeyDown(KeyCode.V) && cameraDirector != null)
            {
                cameraDirector.ToggleCloseAim();
            }

            HandleAimRotation();
            HandlePowerAdjustment();
            HandleShootInput();
        }

        private void HandleAimRotation()
        {
            // Khi người chơi chủ động bấm Alt để hiện con trỏ thao tác giao diện -> không xoay camera
            if (UI.CursorToggle.IsCursorToggledByPlayer) return;

            // Xoay hướng ngắm theo chuyển động chuột
            float mouseX = UnityEngine.Input.GetAxis("Mouse X");
            float mouseY = UnityEngine.Input.GetAxis("Mouse Y");

            if (!UI.CueBallSpinWidgetUI.IsHovered && (Mathf.Abs(mouseX) > 0.001f || Mathf.Abs(mouseY) > 0.001f))
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

        private static readonly System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> uiHits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();

        /// <summary>True nếu con trỏ đang nằm trên một control UI tương tác được (Button, Toggle, Dropdown...).</summary>
        private static bool IsPointerOverInteractableUI()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;

            var data = new UnityEngine.EventSystems.PointerEventData(es) { position = UnityEngine.Input.mousePosition };
            uiHits.Clear();
            es.RaycastAll(data, uiHits);
            for (int i = 0; i < uiHits.Count; i++)
            {
                var sel = uiHits[i].gameObject != null ? uiHits[i].gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() : null;
                if (sel != null && sel.IsInteractable()) return true;
            }
            return false;
        }

        private void HandleShootInput()
        {
            // Click chuột trái để bắn
            if (UnityEngine.Input.GetMouseButtonDown(0) && !UI.CueBallSpinWidgetUI.IsHovered)
            {
                // Click vào nút HUD (Hướng dẫn, Luật, Menu...) thì không bắn
                if (IsPointerOverInteractableUI()) return;

                // Nếu người chơi đang bật con trỏ bằng Alt mà click vào khoảng trống bàn -> thu con trỏ lại để tiếp tục chơi
                if (UI.CursorToggle.IsCursorToggledByPlayer)
                {
                    UI.CursorToggle.SetCursorToggled(false);
                    return;
                }

                if (shotExecutor != null && shotExecutor.CanExecuteShot() && cameraDirector != null)
                {
                    ExecutePlayerShot();
                }
            }
        }

        private void ExecutePlayerShot()
        {
            isShotInProgress = true;

            var elevationSelector = UI.CueElevationSelector.Instance;
            float elevationDeg = elevationSelector != null ? elevationSelector.CurrentAngleDeg : 0f;

            ShotCommand command = new ShotCommand(
                shooterActorId: 0,
                direction: cameraDirector.AimDirection,
                power01: currentPower01,
                cueContact01: cueContact01,
                calledBallNumber: calledBallNumber,
                calledPocketId: calledPocketId,
                calledSafety: calledSafety,
                cueElevationDeg: elevationDeg
            );

            // Góc nâng cơ chỉ có hiệu lực cho 1 cú đánh: reset về 0° cho lượt sau (sau khi hoạt ảnh đánh đã bắt đầu để cơ không bị hạ xuống giữa chừng)

            OnShotInitiated?.Invoke(command);

            var session = Core.GameSession.Instance;
            bool isOnlineClient = (session != null && session.CurrentGameMode == Core.GameMode.OnlinePvP && !Photon.Pun.PhotonNetwork.IsMasterClient);

            if (isOnlineClient)
            {
                // Client trong trận Online: không tự ý chạy vật lý (Rule 114). Đợi Master phê duyệt.
                if (elevationSelector != null) elevationSelector.ResetAngle();
                ResetCueContact();
                return;
            }

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
                        cameraDirector.SwitchToShotFollowCamera(command.direction);
                    }
                });
                if (elevationSelector != null) elevationSelector.ResetAngle();
                ResetCueContact();
            }
            else
            {
                shotExecutor.ExecuteShot(command);
                if (elevationSelector != null) elevationSelector.ResetAngle();
                ResetCueContact();
                if (cameraDirector != null)
                {
                    cameraDirector.SwitchToShotFollowCamera(command.direction);
                }
            }
        }

        public void HandleAllBallsSettled()
        {
            // Client online đang chờ kết quả chính thức từ Master (cú đánh dự đoán cục bộ): không mở khóa ngắm sớm
            var netMgr = FindObjectOfType<Network.NetworkMatchManager>();
            if (netMgr != null && netMgr.IsOnlineMatch && netMgr.AwaitingMasterResult) return;

            isShotInProgress = false;
            ResetPowerToDefault();
            ResetCueContact();

            // Đang đặt bi: giữ camera nhìn từ trên xuống (cho xoay chuột) và giữ cây cơ ẩn
            if (ballInHandController != null && ballInHandController.IsActive) return;

            // Khi bi dừng hẳn: lượt đối thủ -> camera cuối bàn; lượt mình -> camera ngắm sau bi cái
            if (cameraDirector != null)
            {
                if (IsOpponentTurn())
                {
                    cameraDirector.SwitchToOpponentCamera();
                }
                else
                {
                    cameraDirector.SwitchToAimCamera();
                }
            }

            if (cueController != null && isTurnActive && !IsOpponentTurn())
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
