using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.AI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    public class LobbyUIController : MonoBehaviour
    {
        [Header("Main Menu Buttons")]
        [SerializeField] private Button playVsAIButton;
        [SerializeField] private Button playOnlineButton;
        [SerializeField] private Button rulesButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitGameButton;

        [Header("Offline Difficulty Modal")]
        [SerializeField] private GameObject difficultyModalRoot;
        [SerializeField] private Button diffEasyButton;
        [SerializeField] private Button diffNormalButton;
        [SerializeField] private Button diffHardButton;
        [SerializeField] private Image diffEasyHighlight;
        [SerializeField] private Image diffNormalHighlight;
        [SerializeField] private Image diffHardHighlight;
        [SerializeField] private Dropdown raceTargetDropdown;
        [SerializeField] private Dropdown shotTimerDropdown;
        [SerializeField] private InputField playerNameInput;
        [SerializeField] private Button startOfflineMatchButton;
        [SerializeField] private Button closeDifficultyModalButton;

        [Header("Online 1v1 Modal")]
        [SerializeField] private GameObject onlineModalRoot;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private InputField roomCodeInput;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button closeOnlineModalButton;
        [SerializeField] private Text onlineStatusText;

        [Header("Settings Modal")]
        [SerializeField] private GameObject settingsModalRoot;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Button closeSettingsModalButton;

        [Header("Rules Modal")]
        [SerializeField] private GameObject rulesModalRoot;
        [SerializeField] private Button closeRulesModalButton;

        [Header("Quit Confirm Modal")]
        [SerializeField] private GameObject quitConfirmModalRoot;
        [SerializeField] private Button quitConfirmYesButton;
        [SerializeField] private Button quitConfirmNoButton;

        private AIDifficulty selectedDifficulty = AIDifficulty.Normal;
        private readonly RaceTarget[] raceOptions = new[] { RaceTarget.RaceTo3, RaceTarget.RaceTo5, RaceTarget.RaceTo7, RaceTarget.RaceTo9 };
        private readonly ShotTimerOption[] timerOptions = new[] { ShotTimerOption.Unlimited, ShotTimerOption.Seconds30, ShotTimerOption.Seconds45, ShotTimerOption.Seconds60 };

        public AIDifficulty SelectedDifficulty => selectedDifficulty;

        private void Start()
        {
            SetupDropdowns();
            SetupButtonListeners();
            LoadAudioSettings();
            SelectDifficulty(AIDifficulty.Normal);
            HideAllModals();
        }

        private void SetupDropdowns()
        {
            if (raceTargetDropdown != null)
            {
                raceTargetDropdown.ClearOptions();
                List<string> options = new List<string> { "Race to 3 (Chạm 3)", "Race to 5 (Chạm 5)", "Race to 7 (Chạm 7)", "Race to 9 (Chạm 9)" };
                raceTargetDropdown.AddOptions(options);
                raceTargetDropdown.value = 1; // Mặc định Race to 5
            }

            if (shotTimerDropdown != null)
            {
                shotTimerDropdown.ClearOptions();
                List<string> options = new List<string> { "Không giới hạn", "30 Giây / Cú đánh", "45 Giây / Cú đánh", "60 Giây / Cú đánh" };
                shotTimerDropdown.AddOptions(options);
                shotTimerDropdown.value = 1; // Mặc định 30 giây
            }

            if (playerNameInput != null && string.IsNullOrEmpty(playerNameInput.text))
            {
                playerNameInput.text = PlayerPrefs.GetString("PlayerName", "Cơ Thủ");
            }
        }

        private void SetupButtonListeners()
        {
            // Main menu
            if (playVsAIButton != null) playVsAIButton.onClick.AddListener(ShowDifficultyModal);
            if (playOnlineButton != null) playOnlineButton.onClick.AddListener(ShowOnlineModal);
            if (settingsButton != null) settingsButton.onClick.AddListener(ShowSettingsModal);
            if (rulesButton != null) rulesButton.onClick.AddListener(ShowRulesModal);
            if (quitGameButton != null) quitGameButton.onClick.AddListener(ShowQuitModal);

            // Difficulty buttons
            if (diffEasyButton != null) diffEasyButton.onClick.AddListener(() => SelectDifficulty(AIDifficulty.Easy));
            if (diffNormalButton != null) diffNormalButton.onClick.AddListener(() => SelectDifficulty(AIDifficulty.Normal));
            if (diffHardButton != null) diffHardButton.onClick.AddListener(() => SelectDifficulty(AIDifficulty.Hard));
            if (startOfflineMatchButton != null) startOfflineMatchButton.onClick.AddListener(OnStartOfflineMatchClicked);
            if (closeDifficultyModalButton != null) closeDifficultyModalButton.onClick.AddListener(HideAllModals);

            // Online modal
            if (createRoomButton != null) createRoomButton.onClick.AddListener(OnCreateRoomClicked);
            if (joinRoomButton != null) joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
            if (closeOnlineModalButton != null) closeOnlineModalButton.onClick.AddListener(HideAllModals);

            // Settings modal
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            if (closeSettingsModalButton != null) closeSettingsModalButton.onClick.AddListener(HideAllModals);

            // Rules modal
            if (closeRulesModalButton != null) closeRulesModalButton.onClick.AddListener(HideAllModals);

            // Quit modal
            if (quitConfirmYesButton != null) quitConfirmYesButton.onClick.AddListener(OnConfirmQuitClicked);
            if (quitConfirmNoButton != null) quitConfirmNoButton.onClick.AddListener(HideAllModals);
        }

        public void SelectDifficulty(AIDifficulty diff)
        {
            selectedDifficulty = diff;

            Color activeColor = new Color(0.2f, 0.8f, 0.4f, 1f);
            Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

            if (diffEasyHighlight != null) diffEasyHighlight.color = (diff == AIDifficulty.Easy) ? activeColor : inactiveColor;
            if (diffNormalHighlight != null) diffNormalHighlight.color = (diff == AIDifficulty.Normal) ? activeColor : inactiveColor;
            if (diffHardHighlight != null) diffHardHighlight.color = (diff == AIDifficulty.Hard) ? activeColor : inactiveColor;
        }

        public void ShowDifficultyModal()
        {
            HideAllModals();
            if (difficultyModalRoot != null) difficultyModalRoot.SetActive(true);
        }

        public void ShowOnlineModal()
        {
            HideAllModals();
            if (onlineModalRoot != null) onlineModalRoot.SetActive(true);
            if (onlineStatusText != null) onlineStatusText.text = "Sẵn sàng tạo phòng hoặc tham gia bằng Room ID";
        }

        public void ShowSettingsModal()
        {
            HideAllModals();
            if (settingsModalRoot != null) settingsModalRoot.SetActive(true);
        }

        public void ShowRulesModal()
        {
            HideAllModals();
            if (rulesModalRoot != null) rulesModalRoot.SetActive(true);
        }

        public void ShowQuitModal()
        {
            HideAllModals();
            if (quitConfirmModalRoot != null) quitConfirmModalRoot.SetActive(true);
        }

        public void HideAllModals()
        {
            if (difficultyModalRoot != null) difficultyModalRoot.SetActive(false);
            if (onlineModalRoot != null) onlineModalRoot.SetActive(false);
            if (settingsModalRoot != null) settingsModalRoot.SetActive(false);
            if (rulesModalRoot != null) rulesModalRoot.SetActive(false);
            if (quitConfirmModalRoot != null) quitConfirmModalRoot.SetActive(false);
        }

        private void OnStartOfflineMatchClicked()
        {
            RaceTarget race = RaceTarget.RaceTo5;
            if (raceTargetDropdown != null && raceTargetDropdown.value < raceOptions.Length)
            {
                race = raceOptions[raceTargetDropdown.value];
            }

            ShotTimerOption timer = ShotTimerOption.Seconds30;
            if (shotTimerDropdown != null && shotTimerDropdown.value < timerOptions.Length)
            {
                timer = timerOptions[shotTimerDropdown.value];
            }

            string name = (playerNameInput != null && !string.IsNullOrEmpty(playerNameInput.text)) ? playerNameInput.text : "Cơ Thủ";
            PlayerPrefs.SetString("PlayerName", name);

            GameSession session = GameSession.GetOrCreateInstance();
            session.StartOfflineMatch(selectedDifficulty, race, timer, name);
        }

        private void OnCreateRoomClicked()
        {
            if (onlineStatusText != null)
            {
                onlineStatusText.text = "Đang kết nối tới máy chủ Photon PUN 2... (Milestone M8)";
            }
            Debug.Log("[LobbyUI] Tạo phòng Online (chuẩn bị kết nối ở Milestone M8)");
        }

        private void OnJoinRoomClicked()
        {
            string code = (roomCodeInput != null) ? roomCodeInput.text.Trim() : "";
            if (string.IsNullOrEmpty(code))
            {
                if (onlineStatusText != null)
                {
                    onlineStatusText.text = "<color=red>Vui lòng nhập ID phòng hợp lệ!</color>";
                }
                return;
            }

            if (onlineStatusText != null)
            {
                onlineStatusText.text = $"Đang tìm phòng {code}... (Milestone M8)";
            }
            Debug.Log($"[LobbyUI] Tham gia phòng {code} (chuẩn bị kết nối ở Milestone M8)");
        }

        private void LoadAudioSettings()
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

        private void OnConfirmQuitClicked()
        {
            GameSession session = GameSession.GetOrCreateInstance();
            session.QuitGame();
        }
    }
}
