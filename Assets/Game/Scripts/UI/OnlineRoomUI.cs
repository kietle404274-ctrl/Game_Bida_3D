using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using Game.Billiards.Core;
using Game.Billiards.Network;

namespace Game.Billiards.UI
{
    public class OnlineRoomUI : MonoBehaviour
    {
        [Header("Root Panels")]
        [SerializeField] private GameObject onlineRootPanel;
        [SerializeField] private GameObject lobbySubPanel;
        [SerializeField] private GameObject roomSubPanel;

        [Header("Lobby SubPanel Elements")]
        [SerializeField] private InputField playerNameInput;
        [SerializeField] private Text connectionStatusText;
        [SerializeField] private Button connectButton;
        [SerializeField] private Dropdown raceTargetDropdown;
        [SerializeField] private Dropdown shotTimerDropdown;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private InputField joinRoomCodeInput;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button backToMainMenuButton;

        [Header("Inside Room SubPanel Elements")]
        [SerializeField] private Text roomIdDisplayTitle;
        [SerializeField] private Button copyRoomIdButton;
        [SerializeField] private Text roomSettingsInfoText;
        [SerializeField] private Text player1NameText;
        [SerializeField] private Text player1ReadyBadgeText;
        [SerializeField] private Text player2NameText;
        [SerializeField] private Text player2ReadyBadgeText;
        [SerializeField] private Button toggleReadyButton;
        [SerializeField] private Text toggleReadyButtonText;
        [SerializeField] private Button startMatchButton;
        [SerializeField] private Button leaveRoomButton;
        [SerializeField] private Text roomStatusMessageText;

        private readonly RaceTarget[] raceOptions = new[] { RaceTarget.RaceTo3, RaceTarget.RaceTo5, RaceTarget.RaceTo7, RaceTarget.RaceTo9 };
        private readonly ShotTimerOption[] timerOptions = new[] { ShotTimerOption.Unlimited, ShotTimerOption.Seconds30, ShotTimerOption.Seconds45, ShotTimerOption.Seconds60 };

        private void Start()
        {
            SetupDropdowns();
            SetupListeners();
            SubscribeNetworkEvents();

            if (playerNameInput != null && string.IsNullOrEmpty(playerNameInput.text))
            {
                playerNameInput.text = PlayerPrefs.GetString("PlayerName", "Cơ Thủ");
            }
        }

        private void OnDestroy()
        {
            UnsubscribeNetworkEvents();
        }

        private void SetupDropdowns()
        {
            if (raceTargetDropdown != null)
            {
                raceTargetDropdown.ClearOptions();
                raceTargetDropdown.AddOptions(new List<string> { "Race to 3 (Chạm 3)", "Race to 5 (Chạm 5)", "Race to 7 (Chạm 7)", "Race to 9 (Chạm 9)" });
                raceTargetDropdown.value = 1;
            }

            if (shotTimerDropdown != null)
            {
                shotTimerDropdown.ClearOptions();
                shotTimerDropdown.AddOptions(new List<string> { "Không giới hạn", "30 Giây / Cú đánh", "45 Giây / Cú đánh", "60 Giây / Cú đánh" });
                shotTimerDropdown.value = 1;
            }
        }

        private void SetupListeners()
        {
            if (connectButton != null) connectButton.onClick.AddListener(OnConnectClicked);
            if (createRoomButton != null) createRoomButton.onClick.AddListener(OnCreateRoomClicked);
            if (joinRoomButton != null) joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
            if (copyRoomIdButton != null) copyRoomIdButton.onClick.AddListener(OnCopyRoomIdClicked);
            if (toggleReadyButton != null) toggleReadyButton.onClick.AddListener(OnToggleReadyClicked);
            if (startMatchButton != null) startMatchButton.onClick.AddListener(OnStartMatchClicked);
            if (leaveRoomButton != null) leaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);
            if (backToMainMenuButton != null) backToMainMenuButton.onClick.AddListener(CloseOnlinePanel);
        }

        private void SubscribeNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectedToMasterServer += UpdateConnectionStatusUI;
                NetworkManager.Instance.OnRoomCreated += HandleRoomJoined;
                NetworkManager.Instance.OnRoomJoined += HandleRoomJoined;
                NetworkManager.Instance.OnRoomJoinFailed += HandleRoomJoinFailed;
                NetworkManager.Instance.OnRoomCreateFailed += HandleRoomCreateFailed;
                NetworkManager.Instance.OnLeftCurrentRoom += HandleRoomLeft;
                NetworkManager.Instance.OnRemotePlayerJoined += HandleRemotePlayerChanged;
                NetworkManager.Instance.OnRemotePlayerLeft += HandleRemotePlayerChanged;
                NetworkManager.Instance.OnRoomPropertiesChanged += RefreshRoomUI;
            }
        }

        private void UnsubscribeNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectedToMasterServer -= UpdateConnectionStatusUI;
                NetworkManager.Instance.OnRoomCreated -= HandleRoomJoined;
                NetworkManager.Instance.OnRoomJoined -= HandleRoomJoined;
                NetworkManager.Instance.OnRoomJoinFailed -= HandleRoomJoinFailed;
                NetworkManager.Instance.OnRoomCreateFailed -= HandleRoomCreateFailed;
                NetworkManager.Instance.OnLeftCurrentRoom -= HandleRoomLeft;
                NetworkManager.Instance.OnRemotePlayerJoined -= HandleRemotePlayerChanged;
                NetworkManager.Instance.OnRemotePlayerLeft -= HandleRemotePlayerChanged;
                NetworkManager.Instance.OnRoomPropertiesChanged -= RefreshRoomUI;
            }
        }

        public void OpenOnlinePanel()
        {
            if (onlineRootPanel != null) onlineRootPanel.SetActive(true);
            ShowLobbySubPanel();

            string name = (playerNameInput != null && !string.IsNullOrEmpty(playerNameInput.text))
                ? playerNameInput.text
                : PlayerPrefs.GetString("PlayerName", "Cơ Thủ");

            if (NetworkManager.Instance != null && !NetworkManager.Instance.IsConnected)
            {
                if (connectionStatusText != null) connectionStatusText.text = "<color=yellow>Đang kết nối tới máy chủ Photon PUN 2...</color>";
                NetworkManager.Instance.Connect(name);
            }
            else
            {
                UpdateConnectionStatusUI();
            }
        }

        public void CloseOnlinePanel()
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsInRoom)
            {
                NetworkManager.Instance.LeaveRoom();
            }
            if (onlineRootPanel != null) onlineRootPanel.SetActive(false);
        }

        private void ShowLobbySubPanel()
        {
            if (lobbySubPanel != null) lobbySubPanel.SetActive(true);
            if (roomSubPanel != null) roomSubPanel.SetActive(false);
            UpdateConnectionStatusUI();
        }

        private void ShowRoomSubPanel()
        {
            if (lobbySubPanel != null) lobbySubPanel.SetActive(false);
            if (roomSubPanel != null) roomSubPanel.SetActive(true);
            RefreshRoomUI();
        }

        private void UpdateConnectionStatusUI()
        {
            bool connected = NetworkManager.Instance != null && NetworkManager.Instance.IsConnected;
            if (connectionStatusText != null)
            {
                connectionStatusText.text = connected
                    ? "<color=#2ecc71>● Đã kết nối Photon Master Server</color>"
                    : "<color=#e74c3c>○ Chưa kết nối máy chủ Photon</color>";
            }

            if (createRoomButton != null) createRoomButton.interactable = connected;
            if (joinRoomButton != null) joinRoomButton.interactable = connected;
        }

        private void OnConnectClicked()
        {
            string name = (playerNameInput != null && !string.IsNullOrEmpty(playerNameInput.text)) ? playerNameInput.text : "Cơ Thủ";
            PlayerPrefs.SetString("PlayerName", name);
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.Connect(name);
            }
        }

        private void OnCreateRoomClicked()
        {
            string name = (playerNameInput != null && !string.IsNullOrEmpty(playerNameInput.text)) ? playerNameInput.text : "Cơ Thủ";
            PlayerPrefs.SetString("PlayerName", name);

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

            MatchSettings settings = new MatchSettings
            {
                raceTarget = race,
                shotTimer = timer,
                useWpaRules = true
            };

            if (connectionStatusText != null) connectionStatusText.text = "Đang tạo phòng mới...";
            NetworkManager.Instance.CreateRoom("", settings);
        }

        private void OnJoinRoomClicked()
        {
            string code = (joinRoomCodeInput != null) ? joinRoomCodeInput.text.Trim() : "";
            if (string.IsNullOrEmpty(code))
            {
                if (connectionStatusText != null) connectionStatusText.text = "<color=red>Vui lòng nhập ID phòng hợp lệ!</color>";
                return;
            }

            string name = (playerNameInput != null && !string.IsNullOrEmpty(playerNameInput.text)) ? playerNameInput.text : "Cơ Thủ";
            PlayerPrefs.SetString("PlayerName", name);

            if (connectionStatusText != null) connectionStatusText.text = $"Đang tham gia phòng {code}...";
            NetworkManager.Instance.JoinRoom(code);
        }

        private void OnCopyRoomIdClicked()
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsInRoom)
            {
                GUIUtility.systemCopyBuffer = NetworkManager.Instance.CurrentRoomName;
                if (roomStatusMessageText != null) roomStatusMessageText.text = "<color=#2ecc71>Đã sao chép mã phòng vào clipboard!</color>";
            }
        }

        private void OnToggleReadyClicked()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.ToggleReady();
            }
        }

        private void OnStartMatchClicked()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.StartMatch();
            }
        }

        private void OnLeaveRoomClicked()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.LeaveRoom();
            }
        }

        private void HandleRoomJoined(string roomName)
        {
            ShowRoomSubPanel();
        }

        private void HandleRoomJoinFailed(string msg)
        {
            if (connectionStatusText != null)
            {
                connectionStatusText.text = $"<color=red>Vào phòng thất bại: {msg}</color>";
            }
        }

        private void HandleRoomCreateFailed(string msg)
        {
            if (connectionStatusText != null)
            {
                connectionStatusText.text = $"<color=red>Tạo phòng thất bại: {msg}</color>";
            }
        }

        private void HandleRoomLeft()
        {
            ShowLobbySubPanel();
        }

        private void HandleRemotePlayerChanged(Player p)
        {
            RefreshRoomUI();
        }

        public void RefreshRoomUI()
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsInRoom) return;

            string roomId = NetworkManager.Instance.CurrentRoomName;
            if (roomIdDisplayTitle != null)
            {
                roomIdDisplayTitle.text = $"PHÒNG: <color=#f1c40f>{roomId}</color>";
            }

            MatchSettings settings = NetworkManager.Instance.ExtractSettingsFromRoomProperties();
            if (roomSettingsInfoText != null)
            {
                string timerStr = settings.shotTimer == ShotTimerOption.Unlimited ? "Không giới hạn" : $"{(int)settings.shotTimer}s/Cơ";
                roomSettingsInfoText.text = $"Luật WPA 8-Ball | Chạm {(int)settings.raceTarget} | {timerStr}";
            }

            // Player 1 (Host)
            string p1Name = NetworkManager.Instance.GetPlayer1Name();
            bool p1Ready = NetworkManager.Instance.IsPlayer1Ready();
            if (player1NameText != null) player1NameText.text = $"[Chủ phòng] {p1Name}";
            if (player1ReadyBadgeText != null)
            {
                player1ReadyBadgeText.text = p1Ready ? "<color=#2ecc71>ĐÃ SẴN SÀNG</color>" : "<color=#e67e22>CHƯA SẴN SÀNG</color>";
            }

            // Player 2 (Khách)
            string p2Name = NetworkManager.Instance.GetPlayer2Name();
            bool p2Ready = NetworkManager.Instance.IsPlayer2Ready();
            if (player2NameText != null) player2NameText.text = $"[Khách] {p2Name}";
            if (player2ReadyBadgeText != null)
            {
                player2ReadyBadgeText.text = p2Ready ? "<color=#2ecc71>ĐÃ SẴN SÀNG</color>" : "<color=#95a5a6>CHƯA SẴN SÀNG</color>";
            }

            // Ready toggle button label
            bool amIReady = NetworkManager.Instance.IsMaster ? p1Ready : p2Ready;
            if (toggleReadyButtonText != null)
            {
                toggleReadyButtonText.text = amIReady ? "HỦY SẴN SÀNG" : "SẴN SÀNG";
            }

            // Start match button interactability (chỉ hiển thị cho Host và khi cả 2 đã ready)
            bool isHost = NetworkManager.Instance.IsMaster;
            bool canStart = NetworkManager.Instance.CanStartMatch();
            if (startMatchButton != null)
            {
                startMatchButton.gameObject.SetActive(isHost);
                startMatchButton.interactable = canStart;
            }

            if (roomStatusMessageText != null)
            {
                if (!isHost)
                {
                    roomStatusMessageText.text = canStart
                        ? "<color=#2ecc71>Cả 2 đã sẵn sàng! Đang chờ chủ phòng bắt đầu...</color>"
                        : "Vui lòng bấm Sẵn sàng để chuẩn bị vào trận.";
                }
                else
                {
                    roomStatusMessageText.text = canStart
                        ? "<color=#2ecc71>Cả 2 đã sẵn sàng! Hãy bấm BẮT ĐẦU TRẬN.</color>"
                        : "Đang chờ đủ 2 người chơi và sẵn sàng...";
                }
            }
        }
    }
}
