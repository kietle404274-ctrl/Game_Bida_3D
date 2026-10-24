using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Match;

namespace Game.Billiards.UI
{
    public class SettingsUI : MonoBehaviour
    {
        [Header("Modal Root")]
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button resumeButton;

        [Header("Volume Sliders")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;

        [Header("Match Actions")]
        [SerializeField] private Button surrenderButton;
        [SerializeField] private Button quitMatchButton;
        [SerializeField] private GameObject confirmDialogRoot;
        [SerializeField] private Text confirmMessageText;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;

        private MatchStateManager matchStateManager;
        private Action onQuitAction;
        private Action pendingConfirmAction;

        private void Awake()
        {
            LoadSavedSettings();

            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (resumeButton != null) resumeButton.onClick.AddListener(Hide);

            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

            if (surrenderButton != null) surrenderButton.onClick.AddListener(OnSurrenderClicked);
            if (quitMatchButton != null) quitMatchButton.onClick.AddListener(OnQuitClicked);

            if (confirmYesButton != null) confirmYesButton.onClick.AddListener(ExecutePendingConfirm);
            if (confirmNoButton != null) confirmNoButton.onClick.AddListener(CancelConfirmDialog);

            if (confirmDialogRoot != null) confirmDialogRoot.SetActive(false);
        }

        public void Initialize(MatchStateManager stateMgr, Action quitAction = null)
        {
            matchStateManager = stateMgr;
            onQuitAction = quitAction;
        }

        public void Show()
        {
            if (!HudModalState.TryOpen("settings")) return;
            if (confirmDialogRoot != null) confirmDialogRoot.SetActive(false);
            if (modalRoot != null) modalRoot.SetActive(true);

            // Tạm dừng hoàn toàn mô phỏng khi ở chế độ Offline theo đặc tả Mục 5
            bool isOnline = (Game.Billiards.Core.GameSession.Instance != null && 
                             Game.Billiards.Core.GameSession.Instance.CurrentGameMode == Game.Billiards.Core.GameMode.OnlinePvP);
            if (!isOnline)
            {
                Time.timeScale = 0f;
            }
        }

        public void Hide()
        {
            HudModalState.Release("settings");
            if (confirmDialogRoot != null) confirmDialogRoot.SetActive(false);
            if (modalRoot != null) modalRoot.SetActive(false);

            Time.timeScale = 1f;
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            HudModalState.Release("settings");
            Time.timeScale = 1f;
        }

        public void Toggle()
        {
            if (modalRoot != null)
            {
                if (modalRoot.activeSelf) Hide();
                else Show();
            }
        }

        private void LoadSavedSettings()
        {
            float master = PlayerPrefs.GetFloat("Volume_Master", 1.0f);
            float music = PlayerPrefs.GetFloat("Volume_Music", 0.8f);
            float sfx = PlayerPrefs.GetFloat("Volume_SFX", 1.0f);

            if (masterVolumeSlider != null) masterVolumeSlider.value = master;
            if (musicVolumeSlider != null) musicVolumeSlider.value = music;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfx;
        }

        private void OnMasterVolumeChanged(float val)
        {
            if (Game.Billiards.Audio.AudioManager.Instance != null)
            {
                Game.Billiards.Audio.AudioManager.Instance.SetMasterVolume(val);
            }
            else
            {
                PlayerPrefs.SetFloat("Volume_Master", val);
                AudioListener.volume = val;
            }
        }

        private void OnMusicVolumeChanged(float val)
        {
            if (Game.Billiards.Audio.AudioManager.Instance != null)
            {
                Game.Billiards.Audio.AudioManager.Instance.SetMusicVolume(val);
            }
            else
            {
                PlayerPrefs.SetFloat("Volume_Music", val);
            }
        }

        private void OnSfxVolumeChanged(float val)
        {
            if (Game.Billiards.Audio.AudioManager.Instance != null)
            {
                Game.Billiards.Audio.AudioManager.Instance.SetSFXVolume(val);
            }
            else
            {
                PlayerPrefs.SetFloat("Volume_SFX", val);
            }
        }

        private void OnSurrenderClicked()
        {
            ShowConfirmDialog("Bạn có chắc chắn muốn ĐẦU HÀNG ván đấu này không?", () =>
            {
                var net = FindObjectOfType<Game.Billiards.Network.NetworkMatchManager>();
                if (net != null && net.IsOnlineMatch)
                {
                    // Online: gửi sự kiện đầu hàng cho đối thủ và ghi điểm đồng bộ trên cả hai máy
                    net.Surrender();
                }
                else if (matchStateManager != null)
                {
                    // Người đầu hàng là người chơi cục bộ (không phụ thuộc lượt hiện tại); đối thủ nhận điểm ván
                    int surrenderingIndex = GetLocalPlayerIndex();
                    int opponentIndex = (surrenderingIndex == 0) ? 1 : 0;
                    matchStateManager.RecordRackWin(opponentIndex);
                }
                Hide();
            });
        }

        /// <summary>Xác định chỉ số người chơi đang dùng máy này: online = Master/Client, offline = người chơi không phải AI.</summary>
        private int GetLocalPlayerIndex()
        {
            bool isOnline = (Game.Billiards.Core.GameSession.Instance != null &&
                             Game.Billiards.Core.GameSession.Instance.CurrentGameMode == Game.Billiards.Core.GameMode.OnlinePvP);
            if (isOnline)
            {
                var net = FindObjectOfType<Game.Billiards.Network.NetworkMatchManager>();
                if (net != null) return net.LocalPlayerIndex;
            }

            var players = matchStateManager.State.players;
            if (players != null)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    if (!players[i].isAI) return i;
                }
            }
            return matchStateManager.CurrentPlayerIndex;
        }

        private void OnQuitClicked()
        {
            ShowConfirmDialog("Bạn có chắc chắn muốn THOÁT trận đấu về màn hình chính không?", () =>
            {
                Hide();
                if (onQuitAction != null)
                {
                    onQuitAction.Invoke();
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
                }
            });
        }

        private void ShowConfirmDialog(string message, Action onConfirm)
        {
            pendingConfirmAction = onConfirm;
            if (confirmMessageText != null) confirmMessageText.text = message;
            if (confirmDialogRoot != null) confirmDialogRoot.SetActive(true);
        }

        private void ExecutePendingConfirm()
        {
            if (confirmDialogRoot != null) confirmDialogRoot.SetActive(false);
            pendingConfirmAction?.Invoke();
            pendingConfirmAction = null;
        }

        private void CancelConfirmDialog()
        {
            if (confirmDialogRoot != null) confirmDialogRoot.SetActive(false);
            pendingConfirmAction = null;
        }
    }
}
