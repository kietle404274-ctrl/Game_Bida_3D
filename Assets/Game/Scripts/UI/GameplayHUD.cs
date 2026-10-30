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
        [SerializeField] private TurnSlideBannerUI turnSlideBanner;

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
        public TurnSlideBannerUI TurnSlideBanner => turnSlideBanner;

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

            LocalizationManager.OnLanguageChanged -= UpdateActionButtonsOnly;
            LocalizationManager.OnLanguageChanged += UpdateActionButtonsOnly;
            UpdateActionButtonsOnly();
        }

        public void UpdateActionButtonsOnly()
        {
            SetHudButtonLabel(helpButton, LocalizationManager.Get("hud_help"), "H");
            SetHudButtonLabel(rulesButton, LocalizationManager.Get("hud_rules"), "R");
            SetHudButtonLabel(settingsButton, LocalizationManager.Get("hud_settings"), "ESC");
        }

        private static void SetHudButtonLabel(Button btn, string label, string keyHint = null)
        {
            if (btn == null) return;

            // Ưu tiên tìm đúng GameObject "Label" của Button
            Transform lbl = btn.transform.Find("Label");
            Text labelText = lbl != null ? lbl.GetComponent<Text>() : null;

            // Nếu không có child tên "Label", tìm Text con không phải là icon chữ "Q" (?)
            if (labelText == null)
            {
                foreach (var t in btn.GetComponentsInChildren<Text>(true))
                {
                    if (t != null && t.gameObject.name != "Q")
                    {
                        labelText = t;
                        break;
                    }
                }
            }

            if (labelText == null) labelText = btn.GetComponentInChildren<Text>(true);

            if (labelText != null)
            {
                if (!string.IsNullOrEmpty(keyHint))
                    labelText.text = label + "  <size=11><color=#FFC933>[" + keyHint + "]</color></size>";
                else
                    labelText.text = label;
            }

            // Đảm bảo icon dấu hỏi chấm '?' của nút Help không bị ghi đè nhầm
            Transform qTrans = btn.transform.Find("Icon/Circle/Q");
            if (qTrans != null)
            {
                Text qText = qTrans.GetComponent<Text>();
                if (qText != null && qText.text != "?") qText.text = "?";
            }
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
            if (turnSlideBanner == null) turnSlideBanner = GetComponentInChildren<TurnSlideBannerUI>(true);
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

            if (turnSlideBanner == null) turnSlideBanner = TurnSlideBannerUI.Ensure(this);
            if (turnSlideBanner != null && matchStateManager != null && matchStateManager.State != null)
            {
                int cur = matchStateManager.State.currentPlayerIndex;
                string pName = matchStateManager.State.players[cur].displayName;
                bool isLocalPlayer = (cur == GetLocalPlayerIndex());
                turnSlideBanner.ShowTurn(isLocalPlayer, pName);
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

            LocalizationManager.OnLanguageChanged -= RefreshAllUI;
            LocalizationManager.OnLanguageChanged += RefreshAllUI;
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= RefreshAllUI;
            LocalizationManager.OnLanguageChanged -= UpdateActionButtonsOnly;

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
                    case "elevation": if (CueElevationSelector.Instance != null) CueElevationSelector.Instance.SetOpen(false); break;
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

        /// <summary>Chỉ số người chơi hiển thị ở thẻ TRÁI: Online = người chơi tại máy này; Offline = Player A (0).</summary>
        private int LeftPlayerIndex
        {
            get
            {
                var session = GameSession.Instance;
                if (session != null && session.CurrentGameMode == GameMode.OnlinePvP)
                {
                    var net = FindObjectOfType<Network.NetworkMatchManager>();
                    if (net != null && net.IsOnlineMatch) return net.LocalPlayerIndex == 1 ? 1 : 0;
                }
                return 0;
            }
        }

        private int RightPlayerIndex => 1 - LeftPlayerIndex;

        private static int WinsOfPlayer(MatchState s, int idx) => idx == 0 ? s.playerARackWins : s.playerBRackWins;
        private static PlayerGroup GroupOfPlayer(MatchState s, int idx) => idx == 0 ? s.playerAGroup : s.playerBGroup;

        public void RefreshAllUI()
        {
            if (matchStateManager == null || matchStateManager.State == null) return;

            MatchState state = matchStateManager.State;
            int li = LeftPlayerIndex;
            int ri = RightPlayerIndex;

            // 1. Thẻ trái: người chơi tại máy này (Offline luôn là Player A)
            if (playerLeftPanel != null)
            {
                playerLeftPanel.SetPlayerInfo(state.players[li].displayName);
                playerLeftPanel.SetScore(WinsOfPlayer(state, li));
                playerLeftPanel.SetGroup(GroupOfPlayer(state, li));
                playerLeftPanel.SetTurnActive(state.currentPlayerIndex == li);
                if (playerLeftPanel.BallTracker != null)
                {
                    playerLeftPanel.BallTracker.UpdateBallsStatus(ballManager);
                }
            }

            // 2. Thẻ phải: đối thủ (Player B / AI / người chơi online còn lại)
            if (playerRightPanel != null)
            {
                playerRightPanel.SetPlayerInfo(state.players[ri].displayName);
                playerRightPanel.SetScore(WinsOfPlayer(state, ri));
                playerRightPanel.SetGroup(GroupOfPlayer(state, ri));
                playerRightPanel.SetTurnActive(state.currentPlayerIndex == ri);
                if (playerRightPanel.BallTracker != null)
                {
                    playerRightPanel.BallTracker.UpdateBallsStatus(ballManager);
                }
            }

            // 3. Cập nhật Header (tỉ số theo thứ tự thẻ trái - thẻ phải)
            if (matchHeader != null)
            {
                matchHeader.UpdateRackAndScore(state.rackNumber, WinsOfPlayer(state, li), WinsOfPlayer(state, ri), state.settings.raceTarget);

            }

            // 4. Cập nhật Call Shot
            if (callShotUI != null)
            {
                callShotUI.RefreshCallShotOptions();
            }

            // 5. Cập nhật nhãn nút HUD & Intro banner
            UpdateActionButtonsOnly();
            if (matchStartTitleText != null) matchStartTitleText.text = LocalizationManager.Get("match_start_title");
            if (matchStartReadyButton != null) SetHudButtonLabel(matchStartReadyButton, LocalizationManager.Get("match_start_btn"));
        }

        private void HandleTurnChanged(int newPlayerIndex)
        {
            if (matchStateManager == null || matchStateManager.State == null) return;

            MatchState state = matchStateManager.State;
            string currentPlayerName = state.players[newPlayerIndex].displayName;

            if (playerLeftPanel != null) playerLeftPanel.SetTurnActive(newPlayerIndex == LeftPlayerIndex);
            if (playerRightPanel != null) playerRightPanel.SetTurnActive(newPlayerIndex == RightPlayerIndex);

            if (callShotUI != null)
            {
                callShotUI.RefreshCallShotOptions();
            }

            // Banner trượt màu xanh lam từ góc trên bên trái dưới hub tên
            if (turnSlideBanner == null) turnSlideBanner = TurnSlideBannerUI.Ensure(this);
            if (turnSlideBanner != null)
            {
                bool isLocalPlayer = (newPlayerIndex == GetLocalPlayerIndex());
                turnSlideBanner.ShowTurn(isLocalPlayer, currentPlayerName);
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

            int leftScore = LeftPlayerIndex == 0 ? scoreA : scoreB;
            int rightScore = LeftPlayerIndex == 0 ? scoreB : scoreA;
            if (playerLeftPanel != null) playerLeftPanel.SetScore(leftScore);
            if (playerRightPanel != null) playerRightPanel.SetScore(rightScore);

            if (matchHeader != null)
            {
                matchHeader.UpdateRackAndScore(state.rackNumber, leftScore, rightScore, state.settings.raceTarget);
            }
        }

        private void HandleRackEnded(RackOutcome outcome)
        {
            if (matchStateManager == null) return;
            MatchState state = matchStateManager.State;

            int winner = (outcome == RackOutcome.PlayerAWins) ? 0 : 1;
            int localIdx = GetLocalPlayerIndex();
            bool isLocalWinner = (winner == localIdx);

            string scoreText = LocalizationManager.Format("score_subtitle", state.playerARackWins, state.playerBRackWins);

            // Banner thắng/thua ván: Thắng = Xanh lá ("BẠN THẮNG VÁN NÀY"), Thua = Đỏ ("BẠN THUA VÁN NÀY :((")
            if (isLocalWinner)
            {
                FoulPopupUI.ShowRackWin(LocalizationManager.Get("rack_you_won"), scoreText, 2.2f);
            }
            else
            {
                FoulPopupUI.ShowRackLose(LocalizationManager.Get("rack_you_lost"), scoreText, 2.2f);
            }

            HudModalState.ForceOpen("result");
            if (autoNextRackRoutine != null) StopCoroutine(autoNextRackRoutine);
            autoNextRackRoutine = StartCoroutine(AutoNextRackRoutine());
        }

        /// <summary>Xác định chỉ số của người chơi tại máy này (0 hoặc 1) hỗ trợ cả Offline và Online PvP.</summary>
        private int GetLocalPlayerIndex()
        {
            var session = GameSession.Instance;
            if (session != null && session.CurrentGameMode == GameMode.OnlinePvP)
            {
                var net = FindObjectOfType<Network.NetworkMatchManager>();
                if (net != null) return net.LocalPlayerIndex;
            }

            if (matchStateManager != null && matchStateManager.State != null)
            {
                var players = matchStateManager.State.players;
                if (players != null)
                {
                    for (int i = 0; i < players.Length; i++)
                    {
                        if (!players[i].isAI) return i;
                    }
                }
            }
            return 0;
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

            if (matchStateManager != null && matchStateManager.State != null)
            {
                int cur = matchStateManager.State.currentPlayerIndex;
                string pName = matchStateManager.State.players[cur].displayName;
                if (turnSlideBanner == null) turnSlideBanner = TurnSlideBannerUI.Ensure(this);
                if (turnSlideBanner != null)
                {
                    bool isLocalPlayer = (cur == GetLocalPlayerIndex());
                    turnSlideBanner.ShowTurn(isLocalPlayer, pName);
                }
            }
        }

        private void HandleMatchWon(int winnerIndex)
        {
            if (matchStateManager == null) return;
            MatchState state = matchStateManager.State;

            string winnerName = state.players[winnerIndex].displayName;

            if (eventToast != null)
            {
                eventToast.ShowWinToast(winnerName, LocalizationManager.Get("hud_match_champion"));
            }

            // Màn hình kết thúc trận mới (thống kê + nút Chơi lại / Về sảnh); dùng popup cũ nếu chưa có màn hình thống kê
            if (statsPanel != null && resultUI != null)
            {
                var net = FindObjectOfType<Network.NetworkMatchManager>();
                bool online = net != null && net.IsOnlineMatch;

                if (online && net.LocalDisconnected)
                {
                    // Máy này mất kết nối: thua cả trận, chỉ cho Về sảnh
                    statsPanel.ShowMatchResult(
                        winnerIndex,
                        null,
                        () => { statsPanel.CloseResult(); resultUI.ReturnToLobby(); },
                        lobbyOnly: true,
                        title: LocalizationManager.Get("net_lose_disconnect"));
                }
                else if (online && net.OpponentLeft)
                {
                    // Đối thủ đã rời trận: bảng thông số chỉ còn nút Về sảnh ở giữa, không có Chơi lại
                    statsPanel.ShowMatchResult(
                        winnerIndex,
                        null,
                        () => { statsPanel.CloseResult(); resultUI.ReturnToLobby(); },
                        lobbyOnly: true);
                }
                else if (online)
                {
                    // Online: chỉ chủ phòng bấm Chơi lại (phát EVENT_REMATCH cho Client); Client chờ chủ phòng
                    bool isMaster = net.IsMaster;
                    statsPanel.ShowMatchResult(
                        winnerIndex,
                        () =>
                        {
                            if (!isMaster || net.OpponentLeft) return;
                            net.BroadcastRematch();
                            statsPanel.CloseResult();
                            resultUI.Rematch();
                        },
                        () => { statsPanel.CloseResult(); resultUI.ReturnToLobby(); },
                        rematchEnabled: isMaster,
                        rematchLabel: isMaster ? null : LocalizationManager.Get("result_waiting_host"));
                }
                else
                {
                    statsPanel.ShowMatchResult(
                        winnerIndex,
                        () => { statsPanel.CloseResult(); resultUI.Rematch(); },
                        () => { statsPanel.CloseResult(); resultUI.ReturnToLobby(); });
                }
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

            // Cập nhật nhóm bi nếu vừa được gán (thẻ trái = người chơi tại máy này)
            if (playerLeftPanel != null) playerLeftPanel.SetGroup(GroupOfPlayer(state, LeftPlayerIndex));
            if (playerRightPanel != null) playerRightPanel.SetGroup(GroupOfPlayer(state, RightPlayerIndex));

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
            else
            {
                // Kiểm tra cú phá bi khai cuộc (turnSequence == 0 trước khi increment)
                bool isBreakShot = (state.turnSequence == 0);
                int pocketedObjectCount = 0;
                if (result.pocketedBalls != null)
                {
                    for (int i = 0; i < result.pocketedBalls.Count; i++)
                    {
                        int b = result.pocketedBalls[i];
                        if (b >= 1 && b <= 15)
                        {
                            pocketedObjectCount++;
                        }
                    }
                }

                // Hiển thị thông báo khi phá bi có bi vào lỗ cho CẢ người chơi và đối thủ
                if (isBreakShot && pocketedObjectCount > 0)
                {
                    bool isLocalShooter = (state.currentPlayerIndex == GetLocalPlayerIndex());
                    string nextTurnSubtitle = isLocalShooter
                        ? LocalizationManager.Get("turn_your_turn")
                        : LocalizationManager.Format("turn_banner_opponent_sub_simple", state.players[state.currentPlayerIndex].displayName);

                    FoulPopupUI.ShowBreakSuccess(
                        LocalizationManager.Get("break_ball_pocketed_title"),
                        nextTurnSubtitle,
                        2.4f
                    );
                }
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
            PlayerPanelUI active = cur == LeftPlayerIndex ? playerLeftPanel : playerRightPanel;
            PlayerPanelUI other = cur == LeftPlayerIndex ? playerRightPanel : playerLeftPanel;
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
                    return LocalizationManager.Get("foul_scratch");
                case FoulType.WrongBallFirst:
                    return LocalizationManager.Get("foul_wrong_ball");
                case FoulType.NoBallHit:
                    return LocalizationManager.Get("foul_no_ball_hit");
                case FoulType.NoRailAfterContact:
                    return LocalizationManager.Get("foul_no_rail");
                case FoulType.IllegalBreak:
                    return LocalizationManager.Get("foul_illegal_break");
                case FoulType.BallDrivenOffTable:
                    return LocalizationManager.Get("foul_off_table");
                case FoulType.ShotClockExpired:
                    return LocalizationManager.Get("foul_timeout");
                default:
                    return LocalizationManager.Get("foul_generic");
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
                    string p1 = s.players.Length > 0 ? s.players[0].displayName : LocalizationManager.Get("hud_default_p1");
                    string p2 = s.players.Length > 1 ? s.players[1].displayName : LocalizationManager.Get("hud_default_p2");
                    string breaker = s.currentPlayerIndex == 0 ? p1 : p2;

                    if (matchStartVersusText != null)
                    {
                        matchStartVersusText.text = $"{p1}  ⚔️  {p2}";
                    }
                    if (matchStartDetailsText != null)
                    {
                        matchStartDetailsText.text = $"{LocalizationManager.Format("match_start_format", (int)s.settings.raceTarget)}\n{LocalizationManager.Get("match_start_rules")}\n{LocalizationManager.Format("match_start_breaker", breaker.ToUpper())}";
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
                eventToast.ShowToast(LocalizationManager.Get("match_start_title"), $"{LocalizationManager.Get("turn_new_turn")}: {pName}!", new Color(0.06f, 0.73f, 0.51f, 0.95f), 3.0f);
            }
        }
    }
}
