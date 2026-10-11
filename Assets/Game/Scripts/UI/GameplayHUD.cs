using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;
using Game.Billiards.Input;
using Game.Billiards.Match;
using Game.Billiards.Physics;

namespace Game.Billiards.UI
{
    public class GameplayHUD : MonoBehaviour
    {
        [Header("Sub-Panel References")]
        [SerializeField] private PlayerPanelUI playerLeftPanel;
        [SerializeField] private PlayerPanelUI playerRightPanel;
        [SerializeField] private MatchHeaderUI matchHeader;
        [SerializeField] private PowerBarUI powerBar;
        [SerializeField] private CallShotUI callShotUI;
        [SerializeField] private EventToastUI eventToast;
        [SerializeField] private HelpUI helpUI;
        [SerializeField] private RulesUI rulesUI;
        [SerializeField] private SettingsUI settingsUI;
        [SerializeField] private ResultUI resultUI;
        [SerializeField] private CueBallContactSelector contactSelector;

        [Header("System References")]
        [SerializeField] private MatchStateManager matchStateManager;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private PlayerInputController inputController;
        [SerializeField] private BallManager ballManager;
        [SerializeField] private BallMovementMonitor movementMonitor;

        [Header("Action Buttons")]
        [SerializeField] private Button helpButton;
        [SerializeField] private Button rulesButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button cueContactButton;

        public PlayerPanelUI PlayerLeftPanel => playerLeftPanel;
        public PlayerPanelUI PlayerRightPanel => playerRightPanel;
        public MatchHeaderUI MatchHeader => matchHeader;
        public PowerBarUI PowerBar => powerBar;
        public CallShotUI CallShot => callShotUI;
        public EventToastUI EventToast => eventToast;
        public HelpUI Help => helpUI;
        public RulesUI Rules => rulesUI;
        public SettingsUI Settings => settingsUI;
        public ResultUI Result => resultUI;

        private void Awake()
        {
            FindSubPanelsIfMissing();
        }

        private void FindSubPanelsIfMissing()
        {
            if (playerLeftPanel == null || playerRightPanel == null)
            {
                var panels = GetComponentsInChildren<PlayerPanelUI>(true);
                if (panels.Length >= 2)
                {
                    if (playerLeftPanel == null) playerLeftPanel = panels[0];
                    if (playerRightPanel == null) playerRightPanel = panels[1];
                }
                else if (panels.Length == 1 && playerLeftPanel == null)
                {
                    playerLeftPanel = panels[0];
                }
            }

            if (matchHeader == null) matchHeader = GetComponentInChildren<MatchHeaderUI>(true);
            if (powerBar == null) powerBar = GetComponentInChildren<PowerBarUI>(true);
            if (callShotUI == null) callShotUI = GetComponentInChildren<CallShotUI>(true);
            if (eventToast == null) eventToast = GetComponentInChildren<EventToastUI>(true);
            if (helpUI == null) helpUI = GetComponentInChildren<HelpUI>(true);
            if (rulesUI == null) rulesUI = GetComponentInChildren<RulesUI>(true);
            if (settingsUI == null) settingsUI = GetComponentInChildren<SettingsUI>(true);
            if (resultUI == null) resultUI = GetComponentInChildren<ResultUI>(true);
            if (contactSelector == null) contactSelector = FindObjectOfType<CueBallContactSelector>();
        }

        public void Initialize(MatchStateManager stateMgr, TurnManager tManager, PlayerInputController inputCtrl, BallManager bManager, BallMovementMonitor monitor, CueBallContactSelector cSelector = null)
        {
            matchStateManager = stateMgr;
            turnManager = tManager;
            inputController = inputCtrl;
            ballManager = bManager;
            movementMonitor = monitor;
            if (cSelector != null) contactSelector = cSelector;

            // Wire sub-components
            if (powerBar != null && inputController != null)
            {
                powerBar.Initialize(inputController);
            }

            if (callShotUI != null && inputController != null && ballManager != null && matchStateManager != null)
            {
                callShotUI.Initialize(inputController, ballManager, matchStateManager);
            }

            if (settingsUI != null && matchStateManager != null)
            {
                settingsUI.Initialize(matchStateManager);
            }

            if (resultUI != null && matchStateManager != null && ballManager != null)
            {
                resultUI.Initialize(matchStateManager, ballManager);
            }

            SetupButtons();
            SubscribeEvents();
            RefreshAllUI();
        }

        private void SetupButtons()
        {
            if (helpButton != null && helpUI != null)
            {
                helpButton.onClick.RemoveAllListeners();
                helpButton.onClick.AddListener(helpUI.Toggle);
            }

            if (rulesButton != null && rulesUI != null)
            {
                rulesButton.onClick.RemoveAllListeners();
                rulesButton.onClick.AddListener(rulesUI.Toggle);
            }

            if (settingsButton != null && settingsUI != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(settingsUI.Toggle);
            }

            if (cueContactButton != null && contactSelector != null)
            {
                cueContactButton.onClick.RemoveAllListeners();
                cueContactButton.onClick.AddListener(contactSelector.ToggleSelector);
            }
        }

        private void SubscribeEvents()
        {
            if (matchStateManager != null)
            {
                matchStateManager.OnPhaseChanged -= HandlePhaseChanged;
                matchStateManager.OnPhaseChanged += HandlePhaseChanged;

                matchStateManager.OnRackScoreChanged -= HandleRackScoreChanged;
                matchStateManager.OnRackScoreChanged += HandleRackScoreChanged;

                matchStateManager.OnRackEnded -= HandleRackEnded;
                matchStateManager.OnRackEnded += HandleRackEnded;

                matchStateManager.OnMatchWon -= HandleMatchWon;
                matchStateManager.OnMatchWon += HandleMatchWon;
            }

            if (turnManager != null)
            {
                turnManager.OnTurnChanged -= HandleTurnChanged;
                turnManager.OnTurnChanged += HandleTurnChanged;

                turnManager.OnShotEvaluated -= HandleShotEvaluated;
                turnManager.OnShotEvaluated += HandleShotEvaluated;

                turnManager.OnShotTimerTick -= HandleShotTimerTick;
                turnManager.OnShotTimerTick += HandleShotTimerTick;

                turnManager.OnShotClockExpired -= HandleShotClockExpired;
                turnManager.OnShotClockExpired += HandleShotClockExpired;
            }

            if (movementMonitor != null)
            {
                movementMonitor.OnAllBallsSettled -= HandleAllBallsSettled;
                movementMonitor.OnAllBallsSettled += HandleAllBallsSettled;
            }
        }

        private void OnDestroy()
        {
            if (matchStateManager != null)
            {
                matchStateManager.OnPhaseChanged -= HandlePhaseChanged;
                matchStateManager.OnRackScoreChanged -= HandleRackScoreChanged;
                matchStateManager.OnRackEnded -= HandleRackEnded;
                matchStateManager.OnMatchWon -= HandleMatchWon;
            }

            if (turnManager != null)
            {
                turnManager.OnTurnChanged -= HandleTurnChanged;
                turnManager.OnShotEvaluated -= HandleShotEvaluated;
                turnManager.OnShotTimerTick -= HandleShotTimerTick;
                turnManager.OnShotClockExpired -= HandleShotClockExpired;
            }

            if (movementMonitor != null)
            {
                movementMonitor.OnAllBallsSettled -= HandleAllBallsSettled;
            }
        }

        private void Update()
        {
            HandleKeyboardShortcuts();
        }

        private void HandleKeyboardShortcuts()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                if (settingsUI != null) settingsUI.Toggle();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.H) || UnityEngine.Input.GetKeyDown(KeyCode.F1))
            {
                if (helpUI != null) helpUI.Toggle();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.R))
            {
                if (rulesUI != null) rulesUI.Toggle();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.C))
            {
                if (contactSelector != null) contactSelector.ToggleSelector();
            }
        }

        public void RefreshAllUI()
        {
            if (matchStateManager == null || matchStateManager.State == null) return;

            MatchState state = matchStateManager.State;

            // 1. Cập nhật Player Left (Player A)
            if (playerLeftPanel != null)
            {
                playerLeftPanel.SetPlayerInfo(state.players[0].displayName);
                playerLeftPanel.SetScore(state.playerARackWins);
                playerLeftPanel.SetGroup(state.playerAGroup);
                playerLeftPanel.SetTurnActive(state.currentPlayerIndex == 0);
                if (playerLeftPanel.BallTracker != null)
                {
                    playerLeftPanel.BallTracker.UpdateBallsStatus(ballManager);
                }
            }

            // 2. Cập nhật Player Right (Player B / AI)
            if (playerRightPanel != null)
            {
                playerRightPanel.SetPlayerInfo(state.players[1].displayName);
                playerRightPanel.SetScore(state.playerBRackWins);
                playerRightPanel.SetGroup(state.playerBGroup);
                playerRightPanel.SetTurnActive(state.currentPlayerIndex == 1);
                if (playerRightPanel.BallTracker != null)
                {
                    playerRightPanel.BallTracker.UpdateBallsStatus(ballManager);
                }
            }

            // 3. Cập nhật Header
            if (matchHeader != null)
            {
                matchHeader.UpdateRackAndScore(state.rackNumber, state.playerARackWins, state.playerBRackWins, state.settings.raceTarget);
                bool timerActive = (state.settings.shotTimer != ShotTimerOption.Unlimited) && (state.phase == MatchPhase.Aiming);
                matchHeader.SetTimerVisible(timerActive);
            }

            // 4. Cập nhật Call Shot
            if (callShotUI != null)
            {
                callShotUI.RefreshCallShotOptions();
            }
        }

        private void HandleTurnChanged(int newPlayerIndex)
        {
            if (matchStateManager == null || matchStateManager.State == null) return;

            MatchState state = matchStateManager.State;
            string currentPlayerName = state.players[newPlayerIndex].displayName;

            if (playerLeftPanel != null) playerLeftPanel.SetTurnActive(newPlayerIndex == 0);
            if (playerRightPanel != null) playerRightPanel.SetTurnActive(newPlayerIndex == 1);

            if (callShotUI != null)
            {
                callShotUI.RefreshCallShotOptions();
            }

            if (eventToast != null)
            {
                eventToast.ShowTurnToast(currentPlayerName);
            }
        }

        private void HandlePhaseChanged(MatchPhase phase)
        {
            if (matchHeader != null && matchStateManager != null)
            {
                bool isAiming = (phase == MatchPhase.Aiming);
                bool timerEnabled = (matchStateManager.State.settings.shotTimer != ShotTimerOption.Unlimited);
                matchHeader.SetTimerVisible(isAiming && timerEnabled);
            }

            if (phase == MatchPhase.Aiming && callShotUI != null)
            {
                callShotUI.RefreshCallShotOptions();
            }
        }

        private void HandleRackScoreChanged(int scoreA, int scoreB)
        {
            if (matchStateManager == null) return;
            MatchState state = matchStateManager.State;

            if (playerLeftPanel != null) playerLeftPanel.SetScore(scoreA);
            if (playerRightPanel != null) playerRightPanel.SetScore(scoreB);

            if (matchHeader != null)
            {
                matchHeader.UpdateRackAndScore(state.rackNumber, scoreA, scoreB, state.settings.raceTarget);
            }
        }

        private void HandleRackEnded(RackOutcome outcome)
        {
            if (matchStateManager == null) return;
            MatchState state = matchStateManager.State;

            int winner = (outcome == RackOutcome.PlayerAWins) ? 0 : 1;
            string winnerName = state.players[winner].displayName;

            if (eventToast != null)
            {
                eventToast.ShowWinToast(winnerName, $"Chiến thắng ván đấu {state.rackNumber}!");
            }

            if (resultUI != null)
            {
                resultUI.ShowRackResult(winner, state.playerARackWins, state.playerBRackWins, state.settings.raceTarget);
            }
        }

        private void HandleMatchWon(int winnerIndex)
        {
            if (matchStateManager == null) return;
            MatchState state = matchStateManager.State;

            string winnerName = state.players[winnerIndex].displayName;

            if (eventToast != null)
            {
                eventToast.ShowWinToast(winnerName, "VÔ ĐỊCH TOÀN BỘ TRẬN ĐẤU!");
            }

            if (resultUI != null)
            {
                resultUI.ShowMatchResult(winnerIndex, state.playerARackWins, state.playerBRackWins);
            }
        }

        private void HandleShotEvaluated(ShotResult result)
        {
            if (matchStateManager == null) return;
            MatchState state = matchStateManager.State;

            // Cập nhật nhóm bi nếu vừa được gán
            if (playerLeftPanel != null) playerLeftPanel.SetGroup(state.playerAGroup);
            if (playerRightPanel != null) playerRightPanel.SetGroup(state.playerBGroup);

            // Cập nhật trạng thái bi còn lại trong Ball Tracker
            if (playerLeftPanel != null && playerLeftPanel.BallTracker != null)
            {
                playerLeftPanel.BallTracker.UpdateBallsStatus(ballManager);
            }
            if (playerRightPanel != null && playerRightPanel.BallTracker != null)
            {
                playerRightPanel.BallTracker.UpdateBallsStatus(ballManager);
            }

            // Thông báo lỗi nếu có
            if (result.HasFoul && eventToast != null)
            {
                string desc = GetFoulDescription(result.foul);
                eventToast.ShowFoulToast(result.foul.ToString(), desc);
            }
        }

        private void HandleAllBallsSettled()
        {
            // Cập nhật danh sách bi còn lại trên bàn
            if (playerLeftPanel != null && playerLeftPanel.BallTracker != null)
            {
                playerLeftPanel.BallTracker.UpdateBallsStatus(ballManager);
            }
            if (playerRightPanel != null && playerRightPanel.BallTracker != null)
            {
                playerRightPanel.BallTracker.UpdateBallsStatus(ballManager);
            }
        }

        private void HandleShotTimerTick(float remaining, float total)
        {
            if (matchHeader != null)
            {
                matchHeader.UpdateTimer(remaining, total);
            }
        }

        private void HandleShotClockExpired()
        {
            if (eventToast != null)
            {
                eventToast.ShowFoulToast("Hết Giờ (Shot Clock Expired)", "Người chơi không kịp ra cơ trong thời gian quy định.");
            }
        }

        private string GetFoulDescription(FoulType foul)
        {
            switch (foul)
            {
                case FoulType.CueBallScratch:
                    return "Bi cái đã rơi xuống lỗ (Scratch).";
                case FoulType.WrongBallFirst:
                    return "Bi cái chạm sai bi mục tiêu hoặc không chạm bi nào.";
                case FoulType.NoRailAfterContact:
                    return "Không có bi nào chạm băng hoặc vào lỗ sau tiếp xúc.";
                case FoulType.IllegalBreak:
                    return "Khai cuộc không đủ 4 bi chạm băng hoặc không có bi vào lỗ.";
                case FoulType.BallDrivenOffTable:
                    return "Có bi bị đánh văng ra khỏi bàn thi đấu.";
                case FoulType.ShotClockExpired:
                    return "Hết thời gian đánh cho phép.";
                default:
                    return "Phạm quy theo luật WPA 8-Ball.";
            }
        }
    }
}
