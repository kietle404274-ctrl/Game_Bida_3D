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
            if (confirmDialogRoot != null) confirmDialogRoot.SetActive(false);
            if (modalRoot != null) modalRoot.SetActive(true);
        }

        public void Hide()
        {
            if (confirmDialogRoot != null) confirmDialogRoot.SetActive(false);
            if (modalRoot != null) modalRoot.SetActive(false);
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
            PlayerPrefs.SetFloat("Volume_Master", val);
            AudioListener.volume = val;
        }

        private void OnMusicVolumeChanged(float val)
        {
            PlayerPrefs.SetFloat("Volume_Music", val);
        }

        private void OnSfxVolumeChanged(float val)
        {
            PlayerPrefs.SetFloat("Volume_SFX", val);
        }

        private void OnSurrenderClicked()
        {
            ShowConfirmDialog("Bạn có chắc chắn muốn ĐẦU HÀNG ván đấu này không?", () =>
            {
                if (matchStateManager != null)
                {
                    // Người đầu hàng nhường điểm thắng ván cho đối thủ
                    int opponentIndex = (matchStateManager.CurrentPlayerIndex == 0) ? 1 : 0;
                    matchStateManager.RecordRackWin(opponentIndex);
                }
                Hide();
            });
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
