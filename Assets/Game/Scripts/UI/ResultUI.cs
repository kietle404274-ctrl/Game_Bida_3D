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

            string winnerName = (winnerIndex == 0) ? "CƠ THỦ 1 (PLAYER A)" : "CƠ THỦ 2 (PLAYER B / AI)";
            if (matchStateManager != null && matchStateManager.State != null)
            {
                winnerName = matchStateManager.State.players[winnerIndex].displayName;
            }

            if (titleText != null) titleText.text = "KẾT THÚC VÁN ĐẤU (RACK END)";
            if (winnerNameText != null) winnerNameText.text = $"{winnerName} CHIẾN THẮNG VÁN!";
            if (scoreSummaryText != null) scoreSummaryText.text = $"TỈ SỐ HIỆN TẠI: {playerAScore} - {playerBScore} (Race to {(int)raceTarget})";
            if (detailsText != null) detailsText.text = "Nhấn Ván Tiếp Theo để tiếp tục xếp bi và thi đấu ván mới.";

            if (nextRackButton != null) nextRackButton.gameObject.SetActive(true);
            if (rematchButton != null) rematchButton.gameObject.SetActive(false);
            if (returnToLobbyButton != null) returnToLobbyButton.gameObject.SetActive(true);
        }

        public void ShowMatchResult(int winnerIndex, int finalScoreA, int finalScoreB)
        {
            HudModalState.ForceOpen("result"); if (resultModalRoot != null) resultModalRoot.SetActive(true);

            string winnerName = (winnerIndex == 0) ? "CƠ THỦ 1 (PLAYER A)" : "CƠ THỦ 2 (PLAYER B / AI)";
            if (matchStateManager != null && matchStateManager.State != null)
            {
                winnerName = matchStateManager.State.players[winnerIndex].displayName;
            }

            if (titleText != null) titleText.text = "TRẬN ĐẤU KẾT THÚC (MATCH OVER)";
            if (winnerNameText != null) winnerNameText.text = $"🏆 {winnerName} CHIẾN THẮNG CHUNG CUỘC! 🏆";
            if (scoreSummaryText != null) scoreSummaryText.text = $"TỈ SỐ CHUNG CUỘC: {finalScoreA} - {finalScoreB}";
            if (detailsText != null) detailsText.text = "Chúc mừng nhà vô địch đã đạt đủ số ván thắng theo thể thức Race-to!";

            if (nextRackButton != null) nextRackButton.gameObject.SetActive(false);
            if (rematchButton != null) rematchButton.gameObject.SetActive(true);
            if (returnToLobbyButton != null) returnToLobbyButton.gameObject.SetActive(true);
        }

        public void Hide()
        {
            HudModalState.Release("result");
            if (resultModalRoot != null) resultModalRoot.SetActive(false);
        }

        private void OnNextRackClicked()
        {
            AdvanceToNextRack();
        }

        /// <summary>Chuyển sang ván mới: xếp lại bi, chạy cảnh mở đầu rồi bắt đầu ván (khóa thao tác trong lúc đó).</summary>
        public void AdvanceToNextRack()
        {
            Hide();
            HudModalState.ForceOpen("result");
            if (ballManager != null)
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

        private void OnRematchClicked()
        {
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
                UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
            }
        }
    }
}
