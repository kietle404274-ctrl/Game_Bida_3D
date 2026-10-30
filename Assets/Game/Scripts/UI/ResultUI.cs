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

        /// <summary>Chuyển sang ván mới: xếp lại bi, chạy cảnh mở đầu rồi bắt đầu ván (khóa thao tác trong lúc đó).</summary>
        public void AdvanceToNextRack()
        {
            Hide();
            HudModalState.ForceOpen("result");
            // Client online: dàn bi mới do Master đồng bộ (EVENT_RACK_SYNC); tự xếp ngẫu nhiên ở đây có thể ghi đè dàn bi đó
            var netMgr = FindObjectOfType<Network.NetworkMatchManager>();
            bool onlineClient = netMgr != null && netMgr.IsOnlineMatch && !netMgr.IsMaster;
            if (ballManager != null && !onlineClient)
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
            });
        }

        /// <summary>Client nhận lệnh chuyển ván từ Master: đóng popup, phát hoạt cảnh intro (dàn bi được Master đồng bộ qua EVENT_RACK_SYNC).</summary>
        public void AdvanceToNextRackFromMaster()
        {
            Hide();
            HudModalState.ForceOpen("result");
            PlayIntroThen(() =>
            {
                HudModalState.Release("result");
                if (matchStateManager != null)
                {
                    matchStateManager.StartNextRack();
                }
            });
        }

        private void OnRematchClicked()
        {
            var net = FindObjectOfType<Network.NetworkMatchManager>();
            if (net != null && net.IsOnlineMatch)
            {
                if (!net.IsMaster) return; // Chỉ chủ phòng mới có quyền bấm đấu lại
                net.BroadcastRematch();
            }
            Rematch();
        }

        /// <summary>Chơi lại trận mới với cùng cài đặt và cùng người chơi.</summary>
        public void Rematch()
        {
            Hide();
            if (ballManager != null)
            {
                ballManager.ResetRack();
            }
            PlayIntroThen(() =>
            {
                if (matchStateManager != null)
                {
                    var st = matchStateManager.State;
                    matchStateManager.Initialize(st.settings, st.players[0].displayName, st.players[1].displayName, st.players[1].isAI);
                    matchStateManager.RaiseRackStarted();
                }
            });
        }

        /// <summary>Client nhận lệnh đấu lại từ Master.</summary>
        public void RematchFromMaster()
        {
            Hide();
            PlayIntroThen(() =>
            {
                if (matchStateManager != null)
                {
                    var st = matchStateManager.State;
                    matchStateManager.Initialize(st.settings, st.players[0].displayName, st.players[1].displayName, st.players[1].isAI);
                    matchStateManager.RaiseRackStarted();
                }
            });
        }

        private void PlayIntroThen(Action onComplete)
        {
            var intro = FindObjectOfType<RackIntroSequence>();
            if (intro != null && ballManager != null)
            {
                intro.Play(onComplete);
            }
            else
            {
                onComplete?.Invoke();
            }
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
