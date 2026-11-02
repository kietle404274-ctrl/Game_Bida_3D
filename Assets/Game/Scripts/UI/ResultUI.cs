using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;
using Game.Billiards.Match;

namespace Game.Billiards.UI
{
    public class ResultUI : MonoBehaviour
    {
        [Header("UI Roots")]
        [SerializeField] private GameObject resultModalRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text winnerNameText;
        [SerializeField] private Text scoreSummaryText;
        [SerializeField] private Text detailsText;

        [Header("Buttons")]
        [SerializeField] private Button nextRackButton;
        [SerializeField] private Button rematchButton;
        [SerializeField] private Button returnToLobbyButton;

        private MatchStateManager matchStateManager;
        private BallManager ballManager;
        private Action onReturnToLobbyAction;

        private void Awake()
        {
            if (nextRackButton != null) nextRackButton.onClick.AddListener(OnNextRackClicked);
            if (rematchButton != null) rematchButton.onClick.AddListener(OnRematchClicked);
            if (returnToLobbyButton != null) returnToLobbyButton.onClick.AddListener(OnReturnToLobbyClicked);

            // Modal đã được builder để tắt sẵn. KHÔNG SetActive(false) ở đây: Awake chạy ngay khi modal được bật lần đầu và sẽ tự tắt modal.
        }

        public void Initialize(MatchStateManager stateMgr, BallManager bManager, Action returnToLobby = null)
        {
            matchStateManager = stateMgr;
            ballManager = bManager;
            onReturnToLobbyAction = returnToLobby;
        }

        public void ShowRackResult(int winnerIndex, int playerAScore, int playerBScore, RaceTarget raceTarget)
        {
            HudModalState.ForceOpen("result"); if (resultModalRoot != null) resultModalRoot.SetActive(true);

            string winnerName = (winnerIndex == 0) ? LocalizationManager.Get("hud_default_p1") : LocalizationManager.Get("hud_default_p2");
            if (matchStateManager != null && matchStateManager.State != null)
            {
                winnerName = matchStateManager.State.players[winnerIndex].displayName;
            }

            if (titleText != null) titleText.text = LocalizationManager.Get("result_rack_end_title");
            if (winnerNameText != null) winnerNameText.text = LocalizationManager.Format("result_rack_win_text", winnerName);
            if (scoreSummaryText != null) scoreSummaryText.text = LocalizationManager.Format("result_current_score", playerAScore, playerBScore, (int)raceTarget);
            if (detailsText != null) detailsText.text = LocalizationManager.Get("result_next_rack_desc");

            var net = FindObjectOfType<Network.NetworkMatchManager>();
            bool isOnline = (net != null && net.IsOnlineMatch);
            bool isClient = isOnline && !net.IsMaster;

            if (nextRackButton != null)
            {
                nextRackButton.gameObject.SetActive(true);
                Text t = nextRackButton.GetComponentInChildren<Text>();
                if (t != null)
                {
                    t.text = isClient ? LocalizationManager.Get("result_waiting_host") : LocalizationManager.Get("hud_next_rack");
                }
                nextRackButton.interactable = !isClient;
            }
            if (rematchButton != null) rematchButton.gameObject.SetActive(false);
            if (returnToLobbyButton != null)
            {
                returnToLobbyButton.gameObject.SetActive(true);
                Text t = returnToLobbyButton.GetComponentInChildren<Text>();
                if (t != null) t.text = LocalizationManager.Get("hud_return_lobby");
            }
        }

        public void ShowMatchResult(int winnerIndex, int finalScoreA, int finalScoreB)
        {
            HudModalState.ForceOpen("result"); if (resultModalRoot != null) resultModalRoot.SetActive(true);

            string winnerName = (winnerIndex == 0) ? LocalizationManager.Get("hud_default_p1") : LocalizationManager.Get("hud_default_p2");
            if (matchStateManager != null && matchStateManager.State != null)
            {
                winnerName = matchStateManager.State.players[winnerIndex].displayName;
            }

            if (titleText != null) titleText.text = LocalizationManager.Get("result_match_end_title");
            if (winnerNameText != null) winnerNameText.text = LocalizationManager.Format("result_match_winner", winnerName);
            if (scoreSummaryText != null) scoreSummaryText.text = LocalizationManager.Format("result_final_score", finalScoreA, finalScoreB);
            if (detailsText != null) detailsText.text = LocalizationManager.Get("result_congrats");

            var net = FindObjectOfType<Network.NetworkMatchManager>();
            bool isOnline = (net != null && net.IsOnlineMatch);
            bool isClient = isOnline && !net.IsMaster;

            if (nextRackButton != null) nextRackButton.gameObject.SetActive(false);
            if (rematchButton != null)
            {
                rematchButton.gameObject.SetActive(true);
                Text t = rematchButton.GetComponentInChildren<Text>();
                if (t != null)
                {
                    t.text = isClient ? LocalizationManager.Get("result_waiting_host") : LocalizationManager.Get("hud_rematch");
                }
                rematchButton.interactable = !isClient;
            }
            if (returnToLobbyButton != null)
            {
                returnToLobbyButton.gameObject.SetActive(true);
                Text t = returnToLobbyButton.GetComponentInChildren<Text>();
                if (t != null) t.text = LocalizationManager.Get("hud_return_lobby");
            }
        }

        public void Hide()
        {
            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.StopMatchResultAudio();
            }
            HudModalState.Release("result");
            if (resultModalRoot != null) resultModalRoot.SetActive(false);
        }

        private void OnNextRackClicked()
        {
            var net = FindObjectOfType<Network.NetworkMatchManager>();
            if (net != null && net.IsOnlineMatch)
            {
                if (!net.IsMaster) return; // Chỉ chủ phòng mới có quyền bấm chuyển ván
                net.BroadcastNextRack();
            }
            AdvanceToNextRack();
        }

        /// <summary>Chuyển sang ván mới: xếp lại bi đồng bộ cả Master &amp; Client trước hoạt cảnh Intro rồi bắt đầu ván.</summary>
        public void AdvanceToNextRack()
        {
            Hide();
            HudModalState.ForceOpen("result");
            var netMgr = FindObjectOfType<Network.NetworkMatchManager>();
            if (netMgr != null && netMgr.IsOnlineMatch)
            {
                netMgr.ResetSyncStateForNewRack();
                int nextRackNo = (matchStateManager != null && matchStateManager.State != null) ? (matchStateManager.State.rackNumber + 1) : 2;
                int rackSeed = netMgr.ActiveMatchSeed + nextRackNo * 9973;
                if (ballManager != null)
                {
                    ballManager.ResetRack(BallManager.GenerateWpaRackBallNumbersStatic(rackSeed));
                }
            }
            else if (ballManager != null)
            {
                ballManager.ResetRack();
            }
            PlayIntroThen(() =>
            {
                HudModalState.Release("result");
                if (matchStateManager != null)
                {
                    matchStateManager.StartNextRack();
                }
                if (netMgr != null && netMgr.IsOnlineMatch)
                {
                    netMgr.UpdateInputPermissions();
                }
            });
        }

        /// <summary>Client nhận lệnh chuyển ván từ Master: đóng popup, xếp dàn bi đồng bộ rồi phát hoạt cảnh intro.</summary>
        public void AdvanceToNextRackFromMaster()
        {
            AdvanceToNextRack();
        }

        private void OnRematchClicked()
        {
            int firstBreaker = UnityEngine.Random.Range(0, 2);
            int spinSeed = UnityEngine.Random.Range(1000, 999999);

            var net = FindObjectOfType<Network.NetworkMatchManager>();
            if (net != null && net.IsOnlineMatch)
            {
                if (!net.IsMaster) return; // Chỉ chủ phòng mới có quyền bấm đấu lại
                net.BroadcastRematch(firstBreaker, spinSeed);
            }
            Rematch(firstBreaker, spinSeed);
        }

        /// <summary>Chơi lại trận mới với cùng cài đặt và cùng người chơi (quay chọn người phá bi đầu tiên trước khi xếp bi).</summary>
        public void Rematch(int firstBreaker = -1, int spinSeed = -1)
        {
            Hide();
            if (firstBreaker < 0) firstBreaker = UnityEngine.Random.Range(0, 2);
            if (spinSeed < 0) spinSeed = UnityEngine.Random.Range(1000, 999999);

            StartSpinWheelAndStartMatch(firstBreaker, spinSeed, isOnlineClient: false);
        }

        /// <summary>Client nhận lệnh đấu lại từ Master.</summary>
        public void RematchFromMaster(int firstBreaker = 0, int spinSeed = 123456)
        {
            Hide();
            StartSpinWheelAndStartMatch(firstBreaker, spinSeed, isOnlineClient: true);
        }

        private void StartSpinWheelAndStartMatch(int firstBreaker, int spinSeed, bool isOnlineClient)
        {
            var session = GameSession.GetOrCreateInstance();
            bool isOnline = (session != null && session.CurrentGameMode == GameMode.OnlinePvP)
                         || Photon.Pun.PhotonNetwork.InRoom;
            int localPlayerIdx = isOnline
                ? (Photon.Pun.PhotonNetwork.InRoom
                    ? (Photon.Pun.PhotonNetwork.IsMasterClient ? 0 : 1)
                    : ((session != null && session.IsOnlineMaster) ? 0 : 1))
                : 0;

            string pAName = "Player 1";
            string pBName = "Player 2";
            bool pBIsAi = false;
            FirstBreakMethod breakMethod = FirstBreakMethod.RandomWheel;
            var hud = FindObjectOfType<GameplayHUD>(true);
            var inputCtrl = FindObjectOfType<Input.PlayerInputController>(true);

            if (matchStateManager != null && matchStateManager.State != null)
            {
                var oldState = matchStateManager.State;
                var settings = oldState.settings;
                breakMethod = settings.firstBreakMethod;

                if (Photon.Pun.PhotonNetwork.InRoom && Photon.Pun.PhotonNetwork.CurrentRoom != null && Photon.Pun.PhotonNetwork.CurrentRoom.CustomProperties != null)
                {
                    var roomProps = Photon.Pun.PhotonNetwork.CurrentRoom.CustomProperties;
                    if (roomProps.TryGetValue(Network.NetworkConstants.PROP_BREAK_METHOD, out object bmObj))
                    {
                        breakMethod = (FirstBreakMethod)System.Convert.ToInt32(bmObj);
                        settings.firstBreakMethod = breakMethod;
                    }
                }

                if (oldState.players != null && oldState.players.Length >= 2)
                {
                    pAName = oldState.players[0].displayName;
                    pBName = oldState.players[1].displayName;
                    pBIsAi = oldState.players[1].isAI;
                }

                // Reset ngay tỷ số về 0 - 0, Ván 1, nhóm bi Open và chuyển Phase khỏi MatchResult
                // trước khi bước vào 20s Đua Bi / Vòng Quay của trận Đấu lại
                matchStateManager.Initialize(settings, pAName, pBName, pBIsAi, firstBreaker >= 0 ? firstBreaker : 0);
                matchStateManager.SetPhase(MatchPhase.Initializing);
            }

            if (hud != null)
            {
                hud.RefreshAllUI();
            }

            Action<int> finishAndStartRack = resolvedBreaker =>
            {
                HudModalState.ForceOpen("result");
                if (ballManager != null)
                {
                    int rackSeed = spinSeed > 0 ? (spinSeed + 9973) : UnityEngine.Random.Range(1000, 999999);
                    int[] syncedRack = BallManager.GenerateWpaRackBallNumbersStatic(rackSeed);
                    ballManager.ResetRack(syncedRack);
                }
                PlayIntroThen(() =>
                {
                    HudModalState.Release("result");
                    if (matchStateManager != null)
                    {
                        var st = matchStateManager.State;
                        matchStateManager.Initialize(st.settings, pAName, pBName, pBIsAi, resolvedBreaker);
                        matchStateManager.RaiseRackStarted();
                    }
                    var netMgr = FindObjectOfType<Network.NetworkMatchManager>();
                    if (netMgr != null && netMgr.IsOnlineMatch)
                    {
                        netMgr.UpdateInputPermissions();
                    }
                });
            };

            var camDir = (inputCtrl != null && inputCtrl.CameraDirector != null)
                ? inputCtrl.CameraDirector
                : FindObjectOfType<Camera.CameraDirector>(true);
            var cueCtrl = (inputCtrl != null && inputCtrl.CueController != null)
                ? inputCtrl.CueController
                : FindObjectOfType<Physics.CueController>(true);
            var monitor = (inputCtrl != null && inputCtrl.MovementMonitor != null)
                ? inputCtrl.MovementMonitor
                : FindObjectOfType<Physics.BallMovementMonitor>(true);

            if (isOnline && breakMethod == FirstBreakMethod.LagShot && ballManager != null && ballManager.TableWrapper != null)
            {
                if (hud != null)
                {
                    hud.PrepareForPreMatchLag(matchStateManager, inputCtrl, ballManager);
                }

                LagForBreakController.PlayLagShot(
                    this,
                    ballManager,
                    ballManager.TableWrapper,
                    camDir,
                    cueCtrl,
                    monitor,
                    hud,
                    localPlayerIdx,
                    pAName,
                    pBName,
                    spinSeed,
                    winnerIdx => finishAndStartRack(winnerIdx));
            }
            else
            {
                if (ballManager != null && ballManager.AllBalls != null)
                {
                    foreach (var b in ballManager.AllBalls)
                    {
                        if (b != null) b.gameObject.SetActive(false);
                    }
                }
                if (cueCtrl != null) cueCtrl.SetAimingState(false);

                BreakTossWheelUI.PlayWheel(this, firstBreaker, localPlayerIdx, pAName, pBName, spinSeed, () =>
                {
                    finishAndStartRack(firstBreaker);
                });
            }
        }

        private void PlayIntroThen(Action onComplete)
        {
            bool rackIntroEnabled = GameSession.RackIntroEnabled;
            if (Photon.Pun.PhotonNetwork.InRoom &&
                Photon.Pun.PhotonNetwork.CurrentRoom != null &&
                Photon.Pun.PhotonNetwork.CurrentRoom.CustomProperties != null &&
                Photon.Pun.PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(Network.NetworkConstants.PROP_RACK_INTRO, out object riObj))
            {
                rackIntroEnabled = System.Convert.ToInt32(riObj) != 0;
            }

            if (!rackIntroEnabled || ballManager == null)
            {
                onComplete?.Invoke();
                return;
            }

            var intro = FindObjectOfType<RackIntroSequence>();
            if (intro == null)
            {
                intro = gameObject.AddComponent<RackIntroSequence>();
            }
            intro.Play(onComplete);
        }

        private void OnReturnToLobbyClicked()
        {
            ReturnToLobby();
        }

        /// <summary>Về sảnh chính.</summary>
        public void ReturnToLobby()
        {
            Hide();
            if (onReturnToLobbyAction != null)
            {
                onReturnToLobbyAction.Invoke();
            }
            else
            {
                Game.Billiards.Core.GameSession.GetOrCreateInstance().LoadLobbyScene();
            }
        }
    }
}
