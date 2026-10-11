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

        public event Action<ShotResult> OnShotEvaluated;
        public event Action<int> OnTurnChanged;

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

        private void SubscribeEvents()
        {
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
            if (shotExecutor != null)
                shotExecutor.OnShotExecuted -= HandleShotExecuted;

            if (movementMonitor != null)
                movementMonitor.OnAllBallsSettled -= HandleAllBallsSettled;

            if (ballInHandController != null)
                ballInHandController.OnBallInHandConfirmed -= HandleBallInHandConfirmed;
        }

        private void HandleShotExecuted(ShotCommand command)
        {
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

            eventCollector.StopTracking();
            stateManager.SetPhase(MatchPhase.ResolveShot);

            // 1. Đánh giá cú đánh qua Wpa8BallRuleEngine độc lập
            ShotResult rawResult = eventCollector.BuildShotResult();
            ShotResult finalResult = ruleEngine.EvaluateShot(lastExecutedCommand, rawResult, stateManager.State, ballManager);
            LastShotResult = finalResult;

            OnShotEvaluated?.Invoke(finalResult);

            // 2. Chuyển trạng thái trận đấu dựa trên kết quả luật
            ResolveShotOutcome(finalResult);
        }

        private void ResolveShotOutcome(ShotResult result)
        {
            bool isBreakShot = (stateManager.State.turnSequence == 0);

            // Trường hợp 1: Rack kết thúc (thắng hoặc thua qua bi 8)
            if (result.rackEnded)
            {
                int winnerIndex = (result.outcome == RackOutcome.PlayerAWins) ? 0 : 1;
                stateManager.RecordRackWin(winnerIndex);
                return;
            }

            // Trường hợp 2: Phạm lỗi (Foul)
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
                // Gọi trúng bi và vào đúng lỗ -> Giữ lượt, đánh tiếp
                Debug.Log("[TurnManager] Called shot thành công! Người chơi tiếp tục lượt.");
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
