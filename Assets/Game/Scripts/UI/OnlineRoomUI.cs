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

        private OnlineRoomView roomView;
        private int lastReadyButtonState = -1; // -1: chưa đặt, 0: chưa sẵn sàng (xanh lá), 1: đã sẵn sàng (đỏ)

        private static readonly Color ReadyBody = new Color(0.07f, 0.40f, 0.22f, 0.98f);
        private static readonly Color ReadyFrame = new Color(0.25f, 0.95f, 0.50f, 1f);
        private static readonly Color ReadyHover = new Color(0.13f, 0.60f, 0.33f, 1f);
        private static readonly Color ReadyHoverFrame = new Color(0.60f, 1f, 0.72f, 1f);
        private static readonly Color CancelBody = new Color(0.58f, 0.11f, 0.13f, 0.98f);
        private static readonly Color CancelFrame = new Color(1f, 0.45f, 0.42f, 1f);
        private static readonly Color CancelHover = new Color(0.86f, 0.20f, 0.20f, 1f);
        private static readonly Color CancelHoverFrame = new Color(1f, 0.68f, 0.62f, 1f);

        private void Start()
        {
            SetupDropdowns();
            SetupListeners();
            SubscribeNetworkEvents();

            LocalizationManager.OnLanguageChanged -= RefreshAllLocalizedTexts;
            LocalizationManager.OnLanguageChanged += RefreshAllLocalizedTexts;
            PlayerProfile.OnChanged -= RefreshProfileName;
            PlayerProfile.OnChanged += RefreshProfileName;

            RefreshProfileName();
        }

        /// <summary>Ô tên chỉ hiển thị (không cho sửa) - tên được đổi trong Cài đặt ở sảnh.</summary>
        private void RefreshProfileName()
        {
            if (playerNameInput == null) return;
            playerNameInput.text = PlayerProfile.Name;
            playerNameInput.interactable = false;
        }

        /// <summary>Dựng giao diện phòng mới (thẻ người chơi, thanh nút ngang). Gọi từ LobbyUIController sau khi tạo nút cấu hình.</summary>
        public void SetupRoomView(Button configButton)
        {
            if (roomSubPanel == null) return;
            roomView = OnlineRoomView.Build((RectTransform)roomSubPanel.transform, roomIdDisplayTitle, copyRoomIdButton, roomSettingsInfoText,
                configButton, toggleReadyButton, startMatchButton, leaveRoomButton, roomStatusMessageText);
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= RefreshAllLocalizedTexts;
            PlayerProfile.OnChanged -= RefreshProfileName;
            UnsubscribeNetworkEvents();
        }

        private void SetupDropdowns()
        {
            if (raceTargetDropdown != null)
            {
                int cur = raceTargetDropdown.value;
                raceTargetDropdown.ClearOptions();
                raceTargetDropdown.AddOptions(new List<string> {
                    LocalizationManager.Get("opt_race_3"),
                    LocalizationManager.Get("opt_race_5"),
                    LocalizationManager.Get("opt_race_7"),
                    LocalizationManager.Get("opt_race_9")
                });
                raceTargetDropdown.value = Mathf.Clamp(cur, 0, 3);
                raceTargetDropdown.RefreshShownValue();
            }

            if (shotTimerDropdown != null)
            {
                int cur = shotTimerDropdown.value;
                shotTimerDropdown.ClearOptions();
                shotTimerDropdown.AddOptions(new List<string> {
                    LocalizationManager.Get("opt_timer_unlimited"),
                    LocalizationManager.Get("opt_timer_30"),
                    LocalizationManager.Get("opt_timer_45"),
                    LocalizationManager.Get("opt_timer_60")
                });
                shotTimerDropdown.value = Mathf.Clamp(cur, 0, 3);
                shotTimerDropdown.RefreshShownValue();
            }
        }

        private void SetupListeners()
        {
            // Nút "Kết nối" không còn cần thiết: game tự kết nối khi mở phòng online (có màn hình chờ + tự thử lại)
            if (connectButton != null) connectButton.gameObject.SetActive(false);
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
                NetworkManager.Instance.OnDisconnectedFromServer += HandleDisconnectedFromServer;
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
                NetworkManager.Instance.OnDisconnectedFromServer -= HandleDisconnectedFromServer;
            }
        }

        public void OpenOnlinePanel()
        {
            if (onlineRootPanel != null) onlineRootPanel.SetActive(true);
            ShowLobbySubPanel();

            RefreshProfileName();
            string name = PlayerProfile.Name;

            BuildConnectOverlay();
            connectWaitTime = 0f;
            connectRetryTimer = 0f;
            if (connectCancelButton != null) connectCancelButton.SetActive(false);
            if (connectOverlay != null && NetworkManager.Instance != null && !NetworkManager.Instance.IsConnected)
            {
                connectOverlay.SetActive(true);
                connectOverlay.transform.SetAsLastSibling();
            }
            if (connectionStatusText != null) connectionStatusText.gameObject.SetActive(false);

            if (NetworkManager.Instance != null && !NetworkManager.Instance.IsConnected)
            {
                // (trạng thái kết nối được thể hiện bằng màn hình chờ, không hiển thị dòng chữ)
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
                    ? $"<color=#2ecc71>{LocalizationManager.Get("online_connected")}</color>"
                    : $"<color=#e74c3c>{LocalizationManager.Get("online_disconnected")}</color>";
            }

            if (createRoomButton != null) createRoomButton.interactable = connected;
            if (joinRoomButton != null) joinRoomButton.interactable = connected;
        }

        // ---- Màn hình chờ kết nối Photon (chặn mọi thao tác cho đến khi kết nối xong) ----

        private GameObject connectOverlay;
        private Text connectOverlayText;
        private Image[] connectDots;
        private GameObject connectCancelButton;
        private float connectWaitTime;
        private float connectRetryTimer;

        private void BuildConnectOverlay()
        {
            if (connectOverlay != null || onlineRootPanel == null) return;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            connectOverlay = new GameObject("ConnectOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            connectOverlay.transform.SetParent(onlineRootPanel.transform, false);
            RectTransform rt = connectOverlay.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(6000f, 4000f);
            connectOverlay.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.92f);

            GameObject ring = new GameObject("Spinner", typeof(RectTransform));
            ring.transform.SetParent(connectOverlay.transform, false);
            RectTransform rr = ring.GetComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f);
            rr.anchoredPosition = new Vector2(0f, 30f);
            rr.sizeDelta = new Vector2(10f, 10f);
            connectDots = new Image[10];
            for (int i = 0; i < connectDots.Length; i++)
            {
                GameObject d = new GameObject("Dot" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                d.transform.SetParent(ring.transform, false);
                RectTransform dr = d.GetComponent<RectTransform>();
                dr.sizeDelta = new Vector2(14f, 14f);
                float a = i / (float)connectDots.Length * Mathf.PI * 2f;
                dr.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 34f;
                Image im = d.GetComponent<Image>();
                im.sprite = AvatarLibrary.DotSprite;
                im.color = new Color(0.40f, 0.85f, 1f, 1f);
                im.raycastTarget = false;
                connectDots[i] = im;
            }

            GameObject tg = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            tg.transform.SetParent(connectOverlay.transform, false);
            RectTransform tr = tg.GetComponent<RectTransform>();
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.anchoredPosition = new Vector2(0f, -55f);
            tr.sizeDelta = new Vector2(600f, 40f);
            connectOverlayText = tg.GetComponent<Text>();
            connectOverlayText.font = font;
            connectOverlayText.fontSize = 22;
            connectOverlayText.fontStyle = FontStyle.Bold;
            connectOverlayText.alignment = TextAnchor.MiddleCenter;
            connectOverlayText.color = Color.white;
            connectOverlayText.raycastTarget = false;

            // Nút hủy chỉ hiện sau một lúc để tránh bị kẹt khi mất mạng
            connectCancelButton = new GameObject("BtnCancelConnect", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            connectCancelButton.transform.SetParent(connectOverlay.transform, false);
            RectTransform cr = connectCancelButton.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.anchoredPosition = new Vector2(0f, -120f);
            cr.sizeDelta = new Vector2(200f, 40f);
            connectCancelButton.GetComponent<Image>().color = new Color(0.20f, 0.28f, 0.40f, 0.95f);
            connectCancelButton.GetComponent<Button>().onClick.AddListener(() => { connectOverlay.SetActive(false); CloseOnlinePanel(); });
            GameObject cl = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            cl.transform.SetParent(connectCancelButton.transform, false);
            RectTransform clr = cl.GetComponent<RectTransform>();
            clr.anchorMin = Vector2.zero; clr.anchorMax = Vector2.one; clr.offsetMin = Vector2.zero; clr.offsetMax = Vector2.zero;
            Text clt = cl.GetComponent<Text>();
            clt.font = font; clt.fontSize = 16; clt.fontStyle = FontStyle.Bold;
            clt.alignment = TextAnchor.MiddleCenter; clt.color = Color.white; clt.raycastTarget = false;
            clt.text = LocalizationManager.Get("online_btn_back");
            connectCancelButton.SetActive(false);

            connectOverlay.SetActive(false);
        }

        private void Update()
        {
            if (connectOverlay == null || onlineRootPanel == null || !onlineRootPanel.activeInHierarchy) return;

            ClientState state = PhotonNetwork.NetworkClientState;
            bool isTransitioningToRoom = state == ClientState.Joining
                || state == ClientState.Authenticating
                || state == ClientState.ConnectingToGameServer
                || state == ClientState.DisconnectingFromMasterServer;

            bool connected = NetworkManager.Instance == null || NetworkManager.Instance.IsConnected || isTransitioningToRoom;
            if (connected)
            {
                if (connectOverlay.activeSelf) { connectOverlay.SetActive(false); UpdateConnectionStatusUI(); }
                connectWaitTime = 0f;
                connectRetryTimer = 0f;
                return;
            }

            if (!connectOverlay.activeSelf)
            {
                connectOverlay.SetActive(true);
                connectOverlay.transform.SetAsLastSibling();
            }

            connectWaitTime += Time.unscaledDeltaTime;
            connectRetryTimer += Time.unscaledDeltaTime;

            // Tự kết nối lại nếu đã bị ngắt
            if (connectRetryTimer > 3f && PhotonNetwork.NetworkClientState == ClientState.Disconnected)
            {
                connectRetryTimer = 0f;
                NetworkManager.Instance.Connect(PlayerProfile.Name);
            }

            int dots = 1 + (int)(Time.unscaledTime * 2.5f) % 3;
            connectOverlayText.text = LocalizationManager.Get("online_connecting").TrimEnd('.', '…', ' ') + new string('.', dots);

            float t = Time.unscaledTime * 9f;
            for (int i = 0; i < connectDots.Length; i++)
            {
                float f = Mathf.Repeat(i - t, connectDots.Length) / connectDots.Length;
                Color c = connectDots[i].color;
                c.a = Mathf.Lerp(0.12f, 1f, 1f - f);
                connectDots[i].color = c;
            }

            if (connectCancelButton != null && !connectCancelButton.activeSelf && connectWaitTime > 12f) connectCancelButton.SetActive(true);
        }

        private void OnConnectClicked()
        {
            string name = PlayerProfile.Name;
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.Connect(name);
            }
        }

        private void OnCreateRoomClicked()
        {
            if (NetworkManager.Instance != null) NetworkManager.Instance.ApplyLocalProfile();

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

            if (connectionStatusText != null)
            {
                connectionStatusText.gameObject.SetActive(true);
                connectionStatusText.text = LocalizationManager.Get("online_creating");
            }
            NetworkManager.Instance.CreateRoom("", settings);
        }

        private void OnJoinRoomClicked()
        {
            string rawCode = (joinRoomCodeInput != null) ? joinRoomCodeInput.text.Trim() : "";
            string code = NetworkManager.NormalizeRoomId(rawCode);
            if (string.IsNullOrEmpty(code))
            {
                if (connectionStatusText != null)
                {
                    connectionStatusText.gameObject.SetActive(true);
                    connectionStatusText.text = $"<color=red>{LocalizationManager.Get("online_id_invalid")}</color>";
                }
                return;
            }

            if (joinRoomCodeInput != null)
            {
                joinRoomCodeInput.SetTextWithoutNotify(code);
            }

            if (NetworkManager.Instance != null) NetworkManager.Instance.ApplyLocalProfile();

            if (connectionStatusText != null)
            {
                connectionStatusText.gameObject.SetActive(true);
                connectionStatusText.text = LocalizationManager.Format("online_joining", code);
            }
            NetworkManager.Instance.JoinRoom(code);
        }

        private void OnCopyRoomIdClicked()
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsInRoom)
            {
                GUIUtility.systemCopyBuffer = NetworkManager.Instance.CurrentRoomName;
                if (roomStatusMessageText != null) roomStatusMessageText.text = $"<color=#2ecc71>{LocalizationManager.Get("online_copied")}</color>";
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
            if (connectionStatusText != null) connectionStatusText.gameObject.SetActive(false);
            ShowRoomSubPanel();
        }

        private void HandleRoomJoinFailed(string msg)
        {
            if (connectionStatusText != null)
            {
                connectionStatusText.gameObject.SetActive(true);
                connectionStatusText.text = $"<color=red>{LocalizationManager.Format("online_join_failed", msg)}</color>";
            }
        }

        private void HandleRoomCreateFailed(string msg)
        {
            if (connectionStatusText != null)
            {
                connectionStatusText.gameObject.SetActive(true);
                connectionStatusText.text = $"<color=red>{LocalizationManager.Format("online_create_failed", msg)}</color>";
            }
        }

        private void HandleRoomLeft()
        {
            ShowLobbySubPanel();
        }

        private void HandleDisconnectedFromServer(DisconnectCause cause)
        {
            ShowLobbySubPanel();
            UpdateConnectionStatusUI();
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
                roomIdDisplayTitle.text = LocalizationManager.Format("online_room_title", roomId);
            }

            MatchSettings settings = NetworkManager.Instance.ExtractSettingsFromRoomProperties();
            if (roomSettingsInfoText != null)
            {
                string timerStr = settings.shotTimer == ShotTimerOption.Unlimited ? LocalizationManager.Get("opt_timer_unlimited") : $"{(int)settings.shotTimer}s";
                roomSettingsInfoText.text = LocalizationManager.Format("online_settings_info", (int)settings.raceTarget, timerStr);
            }

            // Player 1 (Host)
            string p1Name = NetworkManager.Instance.GetPlayer1Name();
            bool p1Ready = NetworkManager.Instance.IsPlayer1Ready();
            if (player1NameText != null) player1NameText.text = LocalizationManager.Format("online_host_name", p1Name);
            if (player1ReadyBadgeText != null)
            {
                player1ReadyBadgeText.text = p1Ready
                    ? $"<color=#2ecc71>{LocalizationManager.Get("online_ready")}</color>"
                    : $"<color=#e67e22>{LocalizationManager.Get("online_not_ready")}</color>";
            }

            // Player 2 (Khách)
            string p2Name = NetworkManager.Instance.GetPlayer2Name();
            bool p2Ready = NetworkManager.Instance.IsPlayer2Ready();
            if (player2NameText != null) player2NameText.text = LocalizationManager.Format("online_guest_name", p2Name);
            if (player2ReadyBadgeText != null)
            {
                player2ReadyBadgeText.text = p2Ready
                    ? $"<color=#2ecc71>{LocalizationManager.Get("online_ready")}</color>"
                    : $"<color=#95a5a6>{LocalizationManager.Get("online_not_ready")}</color>";
            }

            bool amIHost = NetworkManager.Instance.IsMaster;
            bool hasP2 = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.PlayerCount >= 2;

            // Thẻ người chơi giao diện mới
            if (roomView == null && roomSubPanel != null) roomView = roomSubPanel.GetComponent<OnlineRoomView>();
            if (roomView != null)
            {
                roomView.Apply(p1Name, p1Ready, NetworkManager.Instance.GetPlayerAvatar(true),
                    hasP2, p2Name, p2Ready, NetworkManager.Instance.GetPlayerAvatar(false), amIHost);
            }

            // Nút sẵn sàng: "SẴN SÀNG" (xanh lá) khi chưa sẵn sàng, "HỦY SẴN SÀNG" (đỏ) khi đã sẵn sàng
            bool amIReady = amIHost ? p1Ready : p2Ready;
            if (toggleReadyButtonText != null)
            {
                toggleReadyButtonText.text = amIReady ? LocalizationManager.Get("online_cancel_ready") : LocalizationManager.Get("online_btn_ready");
            }
            ApplyReadyButtonStyle(amIReady);

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
                        ? $"<color=#2ecc71>{LocalizationManager.Get("online_msg_both_ready_wait")}</color>"
                        : LocalizationManager.Get("online_msg_click_ready");
                }
                else
                {
                    roomStatusMessageText.text = canStart
                        ? $"<color=#2ecc71>{LocalizationManager.Get("online_msg_both_ready_start")}</color>"
                        : LocalizationManager.Get("online_msg_wait_opponent");
                }
            }
        }

        /// <summary>Nút sẵn sàng: xanh lá khi chưa sẵn sàng, đỏ khi đã sẵn sàng (bấm để hủy). Chỉ cập nhật khi trạng thái đổi.</summary>
        private void ApplyReadyButtonStyle(bool amIReady)
        {
            if (toggleReadyButton == null) return;
            int state = amIReady ? 1 : 0;
            if (state == lastReadyButtonState) return;
            lastReadyButtonState = state;

            LobbyModalStyler.TintHudButton(toggleReadyButton, amIReady ? CancelBody : ReadyBody, amIReady ? CancelFrame : ReadyFrame);
            UIHoverEffect fx = toggleReadyButton.GetComponent<UIHoverEffect>();
            if (fx == null) fx = UIHoverEffect.Attach(toggleReadyButton.gameObject, true, null, null, 0f, 1.04f);
            fx.Recolor(amIReady ? CancelBody : ReadyBody, amIReady ? CancelFrame : ReadyFrame,
                amIReady ? CancelHover : ReadyHover, amIReady ? CancelHoverFrame : ReadyHoverFrame);
            if (roomView != null) roomView.SetReadyIcon(amIReady);
        }

        public void RefreshAllLocalizedTexts()
        {
            SetupDropdowns();
            UpdateConnectionStatusUI();
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsInRoom)
            {
                RefreshRoomUI();
            }
            if (createRoomButton != null) SetBtnText(createRoomButton, LocalizationManager.Get("online_btn_create"));
            if (joinRoomButton != null) SetBtnText(joinRoomButton, LocalizationManager.Get("online_btn_join"));
            if (connectButton != null) SetBtnText(connectButton, LocalizationManager.Get("online_btn_connect"));
            if (startMatchButton != null) SetBtnText(startMatchButton, LocalizationManager.Get("online_btn_start"));
            if (leaveRoomButton != null) SetBtnText(leaveRoomButton, LocalizationManager.Get("online_btn_leave"));
            if (backToMainMenuButton != null) SetBtnText(backToMainMenuButton, LocalizationManager.Get("online_btn_back"));
        }

        private static void SetBtnText(Button b, string text)
        {
            if (b == null) return;
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }
    }
}
