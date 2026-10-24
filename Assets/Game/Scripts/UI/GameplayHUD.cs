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
        [Header("Match Start Intro Overlay")]
        [SerializeField] private GameObject matchStartIntroRoot;
        [SerializeField] private Text matchStartTitleText;
        [SerializeField] private Text matchStartVersusText;
        [SerializeField] private Text matchStartDetailsText;
        [SerializeField] private Button matchStartReadyButton;

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
        public Button RulesButton => rulesButton;

        /// <summary>Các nút HUD (Hướng dẫn / Luật / Menu) cho hiệu ứng trượt vào khi bắt đầu trận.</summary>
        public System.Collections.Generic.IEnumerable<Button> EntranceButtons()
        {
            yield return helpButton;
            yield return rulesButton;
            yield return settingsButton;
        }

        private StatsPanelUI statsPanel;

        /// <summary>Gắn màn hình thống kê (tạo lúc runtime) để phím Tab / Esc điều khiển.</summary>
        public void SetStatsPanel(StatsPanelUI panel)
        {
            statsPanel = panel;
        }

        private void Awake()
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas != null && canvas.transform.localScale == Vector3.zero)
            {
                canvas.transform.localScale = Vector3.one;
            }
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

            if (matchStartIntroRoot != null)
            {
                matchStartIntroRoot.SetActive(false);
            }

            var rootCanvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
            if (rootCanvas != null && rootCanvas.transform.localScale == Vector3.zero)
            {
                rootCanvas.transform.localScale = Vector3.one;
            }

            EnsureGameplayCanvasVisible();

            if (eventToast != null && matchStateManager != null && matchStateManager.State != null)
            {
                int cur = matchStateManager.State.currentPlayerIndex;
                string pName = matchStateManager.State.players[cur].displayName;
                eventToast.ShowToast("TRẬN ĐẤU BẮT ĐẦU!", $"LƯỢT BẮN: {pName} KHAI CUỘC (BREAK SHOT)!", new Color(0.06f, 0.73f, 0.51f, 0.95f), 3.0f);
            }
        }

        private void EnsureGameplayCanvasVisible()
        {
            var canvases = FindObjectsOfType<Canvas>(true);
            foreach (var c in canvases)
            {
                if (c == null || c.name != "GameplayCanvas") continue;

                c.gameObject.SetActive(true);
                c.enabled = true;
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 50;
                if (c.transform.localScale == Vector3.zero) c.transform.localScale = Vector3.one;

                var rt = (RectTransform)c.transform;
                Debug.Log($"[GameplayHUD] GameplayCanvas OK: mode={c.renderMode} rect={rt.rect.width}x{rt.rect.height} " +
                          $"scale={c.transform.localScale} children={rt.childCount} screen={Screen.width}x{Screen.height}");
                return;
            }

            Debug.LogWarning("[GameplayHUD] Không tìm thấy GameplayCanvas trong scene đang chạy!");
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

                matchStateManager.OnRackStarted -= HandleRackStarted;
                matchStateManager.OnRackStarted += HandleRackStarted;

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
                matchStateManager.OnRackStarted -= HandleRackStarted;
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
                // Esc đóng popup đang mở trước; nếu không có popup nào thì mở Menu
                switch (HudModalState.Current)
                {
                    case "help": if (helpUI != null) helpUI.Hide(); break;
                    case "rules": if (rulesUI != null) rulesUI.Hide(); break;
                    case "settings": if (settingsUI != null) settingsUI.Hide(); break;
                    case "spin": if (contactSelector != null) contactSelector.SetOpen(false); break;
                    case "stats": if (statsPanel != null) statsPanel.Hide(); break;
                    default: if (settingsUI != null) settingsUI.Toggle(); break;
                }
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.H) || UnityEngine.Input.GetKeyDown(KeyCode.F1))
            {
                if (helpUI != null) helpUI.Toggle();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
            {
                if (statsPanel != null) statsPanel.Toggle();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.R))
            {
                if (rulesUI != null) rulesUI.Toggle();
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
            if (phase != MatchPhase.Aiming)
            {
                if (playerLeftPanel != null) playerLeftPanel.ClearTimer();
                if (playerRightPanel != null) playerRightPanel.ClearTimer();
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

            // Banner thắng ván nhanh rồi tự chuyển sang ván mới (không cần bấm nút)
            FoulPopupUI.ShowWin($"{winnerName.ToUpper()}   {state.playerARackWins} - {state.playerBRackWins}", 2.0f);
            HudModalState.ForceOpen("result");
            if (autoNextRackRoutine != null) StopCoroutine(autoNextRackRoutine);
            autoNextRackRoutine = StartCoroutine(AutoNextRackRoutine());
        }

        private Coroutine autoNextRackRoutine;

        private System.Collections.IEnumerator AutoNextRackRoutine()
        {
            yield return new WaitForSeconds(2.6f);
            autoNextRackRoutine = null;
            if (resultUI != null)
            {
                resultUI.AdvanceToNextRack();
            }
            else
            {
                HudModalState.Release("result");
            }
        }

        private void HandleRackStarted()
        {
            RefreshAllUI();
            HudEntranceAnimator.Replay(this);
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

            // Màn hình kết thúc trận mới (thống kê + nút Chơi lại / Về sảnh); dùng popup cũ nếu chưa có màn hình thống kê
            if (statsPanel != null && resultUI != null)
            {
                statsPanel.ShowMatchResult(
                    winnerIndex,
                    () => { statsPanel.CloseResult(); resultUI.Rematch(); },
                    () => { statsPanel.CloseResult(); resultUI.ReturnToLobby(); });
            }
            else if (resultUI != null)
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
            if (result.HasFoul)
            {
                FoulPopupUI.Show(result.foul);
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
            int cur = matchStateManager != null ? matchStateManager.CurrentPlayerIndex : 0;
            PlayerPanelUI active = cur == 0 ? playerLeftPanel : playerRightPanel;
            PlayerPanelUI other = cur == 0 ? playerRightPanel : playerLeftPanel;
            if (active != null) active.SetTimer(remaining, total);
            if (other != null) other.ClearTimer();
        }

        private void HandleShotClockExpired()
        {
            FoulPopupUI.Show(FoulType.ShotClockExpired);
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

        public void ShowMatchStartIntro()
        {
            if (matchStartIntroRoot != null)
            {
                matchStartIntroRoot.SetActive(true);

                if (inputController != null)
                {
                    inputController.SetTurnActive(false);
                }

                if (matchStateManager != null && matchStateManager.State != null)
                {
                    var s = matchStateManager.State;
                    string p1 = s.players.Length > 0 ? s.players[0].displayName : "CƠ THỦ 1 (BẠN)";
                    string p2 = s.players.Length > 1 ? s.players[1].displayName : "CƠ THỦ 2 (AI)";
                    string breaker = s.currentPlayerIndex == 0 ? p1 : p2;

                    if (matchStartVersusText != null)
                    {
                        matchStartVersusText.text = $"{p1}  ⚔️  {p2}";
                    }
                    if (matchStartDetailsText != null)
                    {
                        matchStartDetailsText.text = $"THỂ THỨC: RACE TO {(int)s.settings.raceTarget} (CHẠM {(int)s.settings.raceTarget})\nLUẬT THI ĐẤU: WPA 8-BALL QUỐC TẾ\nQUYỀN KHAI CUỘC: <b>{breaker.ToUpper()}</b>";
                    }
                }
            }
        }

        public void DismissMatchStartIntro()
        {
            if (matchStartIntroRoot != null)
            {
                matchStartIntroRoot.SetActive(false);
            }

            if (inputController != null && matchStateManager != null && matchStateManager.State != null)
            {
                int cur = matchStateManager.State.currentPlayerIndex;
                bool isAI = matchStateManager.State.players[cur].isAI;
                inputController.SetTurnActive(!isAI);
            }
            else if (inputController != null)
            {
                inputController.SetTurnActive(true);
            }

            if (eventToast != null && matchStateManager != null && matchStateManager.State != null)
            {
                int cur = matchStateManager.State.currentPlayerIndex;
                string pName = matchStateManager.State.players[cur].displayName;
                eventToast.ShowToast("TRẬN ĐẤU BẮT ĐẦU!", $"LƯỢT BẮN: {pName} KHAI CUỘC (BREAK SHOT)!", new Color(0.06f, 0.73f, 0.51f, 0.95f), 3.0f);
            }
        }
    }
}
