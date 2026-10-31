using System;
using UnityEngine;
using Game.Billiards.Core;
using Game.Billiards.Physics;
using Game.Billiards.Rules;

namespace Game.Billiards.Match
{
    public class TurnManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchStateManager stateManager;
        [SerializeField] private Wpa8BallRuleEngine ruleEngine;
        [SerializeField] private ShotEventCollector eventCollector;
        [SerializeField] private ShotExecutor shotExecutor;
        [SerializeField] private BallMovementMonitor movementMonitor;
        [SerializeField] private BallManager ballManager;
        [SerializeField] private BallInHandController ballInHandController;

        private ShotCommand lastExecutedCommand;
        public ShotResult LastShotResult { get; private set; }

        private float remainingShotTime = 0f;
        private float totalShotTime = 0f;
        private bool isTimerRunning = false;

        public float RemainingShotTime => remainingShotTime;
        public float TotalShotTime => totalShotTime;
        public bool IsTimerRunning => isTimerRunning;

        public event Action<ShotResult> OnShotEvaluated;
        public event Action<int> OnTurnChanged;
        public event Action<float, float> OnShotTimerTick; // (remaining, total)
        public event Action OnShotClockExpired;

        [SerializeField] private Network.NetworkMatchManager networkMatchManager;

        public void Initialize(MatchStateManager sManager, Wpa8BallRuleEngine rEngine, ShotEventCollector eCollector, ShotExecutor executor, BallMovementMonitor monitor, BallManager bManager, BallInHandController bihCtrl)
        {
            stateManager = sManager;
            ruleEngine = rEngine;
            eventCollector = eCollector;
            shotExecutor = executor;
            movementMonitor = monitor;
            ballManager = bManager;
            ballInHandController = bihCtrl;

            SubscribeEvents();
        }

        public void SetNetworkMatchManager(Network.NetworkMatchManager netMgr)
        {
            networkMatchManager = netMgr;
        }

        public void RaiseTurnChanged(int newPlayerIndex)
        {
            OnTurnChanged?.Invoke(newPlayerIndex);
        }

        private void SubscribeEvents()
        {
            if (stateManager != null)
            {
                stateManager.OnPhaseChanged -= HandlePhaseChanged;
                stateManager.OnPhaseChanged += HandlePhaseChanged;

                stateManager.OnRackEnded -= HandleRackOrMatchEnded;
                stateManager.OnRackEnded += HandleRackOrMatchEnded;
                stateManager.OnMatchWon -= HandleMatchWonCleanup;
                stateManager.OnMatchWon += HandleMatchWonCleanup;
            }

            if (shotExecutor != null)
            {
                shotExecutor.OnShotExecuted -= HandleShotExecuted;
                shotExecutor.OnShotExecuted += HandleShotExecuted;
            }

            if (movementMonitor != null)
            {
                movementMonitor.OnAllBallsSettled -= HandleAllBallsSettled;
                movementMonitor.OnAllBallsSettled += HandleAllBallsSettled;
            }

            if (ballInHandController != null)
            {
                ballInHandController.OnBallInHandConfirmed -= HandleBallInHandConfirmed;
                ballInHandController.OnBallInHandConfirmed += HandleBallInHandConfirmed;
            }
        }

        private void OnDestroy()
        {
            if (stateManager != null)
            {
                stateManager.OnPhaseChanged -= HandlePhaseChanged;
                stateManager.OnRackEnded -= HandleRackOrMatchEnded;
                stateManager.OnMatchWon -= HandleMatchWonCleanup;
            }

            if (shotExecutor != null)
                shotExecutor.OnShotExecuted -= HandleShotExecuted;

            if (movementMonitor != null)
                movementMonitor.OnAllBallsSettled -= HandleAllBallsSettled;

            if (ballInHandController != null)
                ballInHandController.OnBallInHandConfirmed -= HandleBallInHandConfirmed;
        }

        private void HandleMatchWonCleanup(int winnerIndex)
        {
            HandleRackOrMatchEnded(RackOutcome.Continue);
        }

        /// <summary>
        /// Dọn trạng thái dở dang của ván cũ khi ván/trận kết thúc bất kể lý do (thắng, đầu hàng, mất kết nối):
        /// dừng đồng hồ bắn, dừng theo dõi bi, hủy chế độ đặt bi (ball-in-hand) để không ẩn cây cơ ở ván sau.
        /// </summary>
        private void HandleRackOrMatchEnded(RackOutcome outcome)
        {
            StopShotTimer();

            if (movementMonitor != null) movementMonitor.ResetToResting();
            if (eventCollector != null) eventCollector.StopTracking();
            if (ballInHandController != null) ballInHandController.CancelBallInHand();
        }

        /// <summary>Cú đánh vừa được đánh giá có phải cú khai cuộc (tính trước khi turnSequence tăng) — Master gửi kèm cho Client.</summary>
        public bool LastShotWasBreak { get; private set; }

        private void Update()
        {
            if (isTimerRunning && stateManager != null && stateManager.CurrentPhase == MatchPhase.Aiming)
            {
                remainingShotTime -= Time.deltaTime;
                OnShotTimerTick?.Invoke(Mathf.Max(0f, remainingShotTime), totalShotTime);

                if (remainingShotTime <= 0f)
                {
                    HandleShotClockExpired();
                }
            }
        }

        private void HandlePhaseChanged(MatchPhase phase)
        {
            if (phase == MatchPhase.Aiming)
            {
                StartShotTimer();
            }
            else
            {
                StopShotTimer();
            }
        }

        public void StartShotTimer()
        {
            if (stateManager == null) return;

            int timerSec = (int)stateManager.State.settings.shotTimer;
            if (timerSec > 0)
            {
                totalShotTime = timerSec;
                remainingShotTime = timerSec;
                isTimerRunning = true;
                OnShotTimerTick?.Invoke(remainingShotTime, totalShotTime);
            }
            else
            {
                isTimerRunning = false;
                remainingShotTime = 0f;
                totalShotTime = 0f;
            }
        }

        public void StopShotTimer()
        {
            isTimerRunning = false;
            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.StopTimerTick();
            }
        }

        /// <summary>Trừ thời gian trễ mạng khỏi đồng hồ đang chạy (không bao giờ xuống dưới 1s để tránh hết giờ ngay).</summary>
        public void CompensateShotTimer(float elapsedSeconds)
        {
            if (!isTimerRunning || elapsedSeconds <= 0f) return;
            remainingShotTime = Mathf.Max(1f, remainingShotTime - elapsedSeconds);
            OnShotTimerTick?.Invoke(remainingShotTime, totalShotTime);
        }

        private void HandleShotClockExpired()
        {
            // Trong trận Online: Client không tự ý quyết định hết giờ đánh để tránh lệch pha do ping/fps
            if (networkMatchManager != null && networkMatchManager.IsOnlineMatch && !networkMatchManager.IsMaster)
            {
                StopShotTimer();
                return;
            }

            StopShotTimer();
            Debug.LogWarning("[TurnManager] Hết giờ đánh (Shot Clock Expired)! Chuyển lượt và đối thủ nhận Ball-in-Hand.");

            OnShotClockExpired?.Invoke();

            ShotResult timeoutResult = new ShotResult
            {
                foul = FoulType.ShotClockExpired
            };
            LastShotResult = timeoutResult;
            LastShotWasBreak = (stateManager.State.turnSequence == 0);
            OnShotEvaluated?.Invoke(timeoutResult);

            stateManager.State.SwitchTurn();
            OnTurnChanged?.Invoke(stateManager.CurrentPlayerIndex);
            stateManager.SetPhase(MatchPhase.BallInHand);

            if (ballInHandController != null)
            {
                ballInHandController.EnterBallInHand(false);
            }

            // Master Client trong trận Online: Broadcast kết quả hết giờ và snapshot tọa độ bi cho Client
            if (networkMatchManager != null && networkMatchManager.IsOnlineMatch && networkMatchManager.IsMaster)
            {
                networkMatchManager.BroadcastTurnOutcome(timeoutResult);
            }
        }

        private void HandleShotExecuted(ShotCommand command)
        {
            StopShotTimer();
            lastExecutedCommand = command;

            if (eventCollector != null)
            {
                eventCollector.StartTracking();
            }

            if (stateManager != null)
            {
                stateManager.SetPhase(MatchPhase.ShotInProgress);
            }
        }

        private void HandleAllBallsSettled()
        {
            if (stateManager == null || ruleEngine == null || eventCollector == null)
                return;

            // Ván/trận đã kết thúc (ví dụ đầu hàng lúc bi đang lăn): bỏ qua kết quả cú đánh cũ
            if (stateManager.CurrentPhase == MatchPhase.RackResult || stateManager.CurrentPhase == MatchPhase.MatchResult)
                return;

            eventCollector.StopTracking();

            // Client trong trận Online: dừng tracking và đợi snapshot authoritative từ Master Client (Rule 119/120)
            if (networkMatchManager != null && networkMatchManager.IsOnlineMatch && !networkMatchManager.IsMaster)
            {
                Debug.Log("[TurnManager] [Client] Toàn bộ bi đã dừng. Đang đợi kết quả authoritative từ Master Client...");
                return;
            }

            stateManager.SetPhase(MatchPhase.ResolveShot);

            // 1. Đánh giá cú đánh qua Wpa8BallRuleEngine độc lập
            ShotResult rawResult = eventCollector.BuildShotResult();
            ShotResult finalResult = ruleEngine.EvaluateShot(lastExecutedCommand, rawResult, stateManager.State, ballManager);
            LastShotResult = finalResult;
            LastShotWasBreak = (stateManager.State.turnSequence == 0);

            OnShotEvaluated?.Invoke(finalResult);

            // 2. Chuyển trạng thái trận đấu dựa trên kết quả luật
            ResolveShotOutcome(finalResult);

            // 3. Nếu là Master Client trong trận Online: Broadcast kết quả và snapshot cho Client (Rule 120)
            if (networkMatchManager != null && networkMatchManager.IsOnlineMatch && networkMatchManager.IsMaster)
            {
                networkMatchManager.BroadcastTurnOutcome(finalResult);
            }
        }

        private void ResolveShotOutcome(ShotResult result)
        {
            bool isBreakShot = (stateManager.State.turnSequence == 0);

            // Trường hợp 1: Rack kết thúc (thắng hoặc thua qua bi 8)
            // Âm thanh thắng/thua ván hoặc thắng/thua cả trận sẽ do GameplayHUD (FoulPopupUI / StatsPanelUI) phát đúng theo phe của người chơi tại máy này
            if (result.rackEnded)
            {
                int winnerIndex = (result.outcome == RackOutcome.PlayerAWins) ? 0 : 1;
                stateManager.RecordRackWin(winnerIndex);
                return;
            }

            // Trường hợp 2: Phạm lỗi (Foul)
            // Âm thanh báo lỗi sẽ được phát thông qua FoulPopupUI khi hiển thị banner lỗi để tránh phát trùng 2 lần
            if (result.HasFoul)
            {
                Debug.Log($"[TurnManager] Phát hiện lỗi: {result.foul}! Đổi lượt và cấp Ball-in-Hand cho đối thủ.");

                stateManager.State.SwitchTurn();
                OnTurnChanged?.Invoke(stateManager.CurrentPlayerIndex);

                stateManager.SetPhase(MatchPhase.BallInHand);

                if (ballInHandController != null)
                {
                    bool headStringOnly = (result.foul == FoulType.IllegalBreak || (isBreakShot && result.foul == FoulType.CueBallScratch));
                    ballInHandController.EnterBallInHand(headStringOnly);
                }
                return;
            }

            // Trường hợp 3: Không có lỗi
            if (result.calledShotMade && !lastExecutedCommand.calledSafety)
            {
                // Gọi trúng bi và vào đúng lỗ (hoặc cú khai cuộc ăn bi hợp lệ) -> Giữ lượt, đánh tiếp
                Debug.Log("[TurnManager] Cú đánh thành công! Người chơi tiếp tục lượt.");
                stateManager.State.IncrementTurnSequence();
                stateManager.SetPhase(MatchPhase.Aiming);
            }
            else
            {
                // Không đưa được called ball vào lỗ hoặc gọi Safety -> Đổi lượt bình thường (không có foul)
                Debug.Log("[TurnManager] Hết lượt đánh. Đổi lượt cho đối thủ.");
                stateManager.State.SwitchTurn();
                OnTurnChanged?.Invoke(stateManager.CurrentPlayerIndex);
                stateManager.SetPhase(MatchPhase.Aiming);
            }
        }

        private void HandleBallInHandConfirmed(Vector3 pos)
        {
            if (stateManager != null)
            {
                stateManager.SetPhase(MatchPhase.Aiming);
            }
        }
    }
}
