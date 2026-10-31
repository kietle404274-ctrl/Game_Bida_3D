using System;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Game.Billiards.Core;
using Game.Billiards.UI;

namespace Game.Billiards.Network
{
    public class NetworkManager : MonoBehaviourPunCallbacks
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Connection Settings")]
        [SerializeField] private bool autoConnectOnStart = false;

        public event Action OnConnectedToMasterServer;
        public event Action<string> OnRoomCreated;
        public event Action<string> OnRoomJoined;
        public event Action<string> OnRoomJoinFailed;
        public event Action<string> OnRoomCreateFailed;
        public event Action OnLeftCurrentRoom;
        public event Action<Player> OnRemotePlayerJoined;
        public event Action<Player> OnRemotePlayerLeft;
        public event Action OnRoomPropertiesChanged;
        public event Action<DisconnectCause> OnDisconnectedFromServer;

        public const string DEFAULT_REGION = "asia";

        private string pendingJoinRoomId;
        private bool hasPendingCreateRoom;
        private string pendingCreateRoomId;
        private MatchSettings pendingCreateSettings;

        /// <summary>
        /// Chỉ trả về true khi đã thực sự ở trên MasterServer (sẵn sàng tạo/vào phòng) hoặc đang ở trong phòng.
        /// Tránh trả về true sớm khi mới kết nối tới NameServer khiến lệnh JoinRoom/CreateRoom bị từ chối.
        /// </summary>
        public bool IsConnected => PhotonNetwork.IsConnectedAndReady
            && (PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer
                || PhotonNetwork.NetworkClientState == ClientState.JoinedLobby
                || PhotonNetwork.InRoom);
        public bool IsInRoom => PhotonNetwork.InRoom;
        public bool IsMaster => PhotonNetwork.IsMasterClient;
        public string CurrentRoomName => PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name : string.Empty;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Nếu gắn chung GameObject với các UI Controller khác (như LobbySystems),
                // chỉ huỷ component NetworkManager thừa này, giữ nguyên GameObject để các UI controller khác hoạt động.
                if (GetComponent<LobbyUIController>() != null || GetComponent<OnlineRoomUI>() != null)
                {
                    Destroy(this);
                }
                else
                {
                    Destroy(gameObject);
                }
                return;
            }

            // Nếu đang gắn chung GameObject với các component thuộc scene:
            // Tách riêng NetworkManager sang GameObject độc lập để DontDestroyOnLoad không kéo theo LobbySystems/LobbyUIController!
            if (GetComponent<LobbyUIController>() != null || GetComponent<OnlineRoomUI>() != null)
            {
                GameObject netGo = new GameObject("NetworkManager");
                NetworkManager newNet = netGo.AddComponent<NetworkManager>();
                newNet.autoConnectOnStart = this.autoConnectOnStart;
                Instance = newNet;
                DontDestroyOnLoad(netGo);
                PhotonNetwork.AutomaticallySyncScene = true;
                Destroy(this);
                return;
            }

            Instance = this;
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            PhotonNetwork.AutomaticallySyncScene = true;
        }

        private void Start()
        {
            if (autoConnectOnStart && !PhotonNetwork.IsConnected)
            {
                Connect();
            }
        }

        /// <summary>Khóa Custom Property của Player lưu chỉ số avatar.</summary>
        public const string PLAYER_PROP_AVATAR = "av";

        /// <summary>Áp tên + avatar từ PlayerProfile (đặt ở Cài đặt) lên người chơi cục bộ trên Photon.</summary>
        public void ApplyLocalProfile()
        {
            PhotonNetwork.NickName = PlayerProfile.Name;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
            {
                { PLAYER_PROP_AVATAR, AvatarLibrary.Wrap(PlayerProfile.AvatarIndex) }
            });
        }

        /// <summary>Avatar của người chơi 1 (chủ phòng) hoặc người chơi 2 (khách); -1 nếu chưa có.</summary>
        public int GetPlayerAvatar(bool player1)
        {
            if (!PhotonNetwork.InRoom) return -1;
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (p.IsMasterClient != player1) continue;
                if (p.CustomProperties.TryGetValue(PLAYER_PROP_AVATAR, out object v) && v is int idx) return AvatarLibrary.Wrap(idx);
                return 0;
            }
            return -1;
        }

        /// <summary>
        /// Chuẩn hóa mã phòng người chơi nhập vào:
        /// - Nếu chỉ nhập số (VD: "4842") hoặc thiếu dấu gạch ngang (VD: "POOL4842", "POOL 4842"),
        ///   tự động chuyển thành chuẩn "POOL-4842".
        /// </summary>
        public static string NormalizeRoomId(string rawRoomId)
        {
            if (string.IsNullOrWhiteSpace(rawRoomId)) return string.Empty;
            string s = rawRoomId.Trim().ToUpperInvariant();

            // Kiểm tra nếu toàn bộ là chữ số (VD: "4842") -> thêm tiền tố "POOL-"
            bool allDigits = true;
            for (int i = 0; i < s.Length; i++)
            {
                if (!char.IsDigit(s[i]))
                {
                    allDigits = false;
                    break;
                }
            }
            if (allDigits)
            {
                return $"POOL-{s}";
            }

            // Nếu bắt đầu bằng "POOL" nhưng dùng khoảng trắng, gạch dưới hoặc viết liền (VD: "POOL 4842", "POOL4842")
            if (s.StartsWith("POOL") && !s.StartsWith("POOL-"))
            {
                string suffix = s.Substring(4).Trim(' ', '-', '_', ':');
                if (!string.IsNullOrEmpty(suffix))
                {
                    return $"POOL-{suffix}";
                }
            }

            return s;
        }

        public void Connect(string playerName = "Cơ Thủ")
        {
            string resolvedName = string.IsNullOrEmpty(playerName) ? LocalizationManager.Get("default_player_name") : playerName;
            PhotonNetwork.NickName = resolvedName;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
            {
                { PLAYER_PROP_AVATAR, AvatarLibrary.Wrap(PlayerProfile.AvatarIndex) }
            });

            if (PhotonNetwork.IsConnected)
            {
                if (!string.IsNullOrEmpty(PhotonNetwork.CloudRegion) &&
                    !PhotonNetwork.CloudRegion.StartsWith(DEFAULT_REGION, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning($"[NetworkManager] Phát hiện lệch Region ({PhotonNetwork.CloudRegion} != {DEFAULT_REGION}), đang kết nối lại sang {DEFAULT_REGION}...");
                    PhotonNetwork.Disconnect();
                }
                else
                {
                    if (IsConnected)
                    {
                        OnConnectedToMasterServer?.Invoke();
                    }
                    return;
                }
            }

            // Cố định Region = "asia" và AppVersion = GAME_VERSION để mọi bản Build / Editor / máy khác nhau
            // luôn kết nối vào cùng 1 cụm Master Server của Photon (tránh lệch BestRegion gây lỗi 32758 Game does not exist).
            if (PhotonNetwork.PhotonServerSettings != null && PhotonNetwork.PhotonServerSettings.AppSettings != null)
            {
                PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = DEFAULT_REGION;
                PhotonNetwork.PhotonServerSettings.AppSettings.AppVersion = NetworkConstants.GAME_VERSION;
            }
            PhotonNetwork.GameVersion = NetworkConstants.GAME_VERSION;

            Debug.Log($"[NetworkManager] Đang kết nối tới máy chủ Photon (Region: {DEFAULT_REGION}, Version: {NetworkConstants.GAME_VERSION}, Player: {PhotonNetwork.NickName})...");
            PhotonNetwork.ConnectUsingSettings();
            PhotonNetwork.GameVersion = NetworkConstants.GAME_VERSION;
        }

        public void CreateRoom(string customRoomId, MatchSettings settings)
        {
            string roomId = string.IsNullOrEmpty(customRoomId) ? NetworkConstants.GenerateRoomId() : NormalizeRoomId(customRoomId);

            // Nếu Photon đang trong quá trình chuyển tiếp tới MasterServer (VD: đang ở NameServer hoặc vừa rời phòng cũ),
            // xếp hàng yêu cầu tạo phòng để tự động thực thi ngay khi OnConnectedToMaster hoàn tất!
            if (!IsConnected)
            {
                hasPendingCreateRoom = true;
                pendingCreateRoomId = roomId;
                pendingCreateSettings = settings;
                pendingJoinRoomId = null;

                if (!PhotonNetwork.IsConnected)
                {
                    Connect(PlayerProfile.Name);
                }
                Debug.Log($"[NetworkManager] Đang chờ kết nối MasterServer hoàn tất để tự động tạo phòng {roomId}...");
                return;
            }

            hasPendingCreateRoom = false;

            RoomOptions options = new RoomOptions
            {
                MaxPlayers = NetworkConstants.MAX_PLAYERS,
                IsOpen = true,
                IsVisible = true,
                PublishUserId = true,
                CleanupCacheOnLeave = true
            };

            var customProps = new ExitGames.Client.Photon.Hashtable
            {
                { NetworkConstants.PROP_GAME_VERSION, NetworkConstants.GAME_VERSION },
                { NetworkConstants.PROP_ROOM_STATE, NetworkConstants.STATE_WAITING },
                { NetworkConstants.PROP_RACE_TARGET, (int)settings.raceTarget },
                { NetworkConstants.PROP_SHOT_TIMER, (int)settings.shotTimer },
                { NetworkConstants.PROP_P1_NAME, PhotonNetwork.NickName },
                { NetworkConstants.PROP_P2_NAME, "" },
                { NetworkConstants.PROP_P1_READY, true }, // Host mặc định sẵn sàng
                { NetworkConstants.PROP_P2_READY, false }
            };

            options.CustomRoomProperties = customProps;
            options.CustomRoomPropertiesForLobby = new string[] { NetworkConstants.PROP_GAME_VERSION, NetworkConstants.PROP_ROOM_STATE };

            Debug.Log($"[NetworkManager] Tạo phòng mới ID: {roomId} (Region: {PhotonNetwork.CloudRegion}, Race to: {settings.raceTarget}, Timer: {settings.shotTimer})");
            PhotonNetwork.CreateRoom(roomId, options, TypedLobby.Default);
        }

        /// <summary>Chủ phòng đổi cấu hình trận (Race/Timer) khi phòng còn đang chờ; khách nhận qua OnRoomPropertiesChanged.</summary>
        public bool UpdateRoomSettings(MatchSettings settings)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return false;

            var room = PhotonNetwork.CurrentRoom;
            if (room.CustomProperties.TryGetValue(NetworkConstants.PROP_ROOM_STATE, out object state)
                && state.ToString() != NetworkConstants.STATE_WAITING)
            {
                return false;
            }

            var props = new ExitGames.Client.Photon.Hashtable
            {
                { NetworkConstants.PROP_RACE_TARGET, (int)settings.raceTarget },
                { NetworkConstants.PROP_SHOT_TIMER, (int)settings.shotTimer }
            };
            room.SetCustomProperties(props);
            return true;
        }

        public void JoinRoom(string roomId)
        {
            string cleanId = NormalizeRoomId(roomId);
            if (string.IsNullOrEmpty(cleanId))
            {
                OnRoomJoinFailed?.Invoke(LocalizationManager.Get("net_room_empty"));
                return;
            }

            // Nếu Photon đang kết nối dở (đang ở NameServer hoặc đang chuyển về MasterServer),
            // lưu mã phòng lại và tự động JoinRoom ngay khi OnConnectedToMaster gọi về!
            if (!IsConnected)
            {
                pendingJoinRoomId = cleanId;
                hasPendingCreateRoom = false;

                if (!PhotonNetwork.IsConnected)
                {
                    Connect(PlayerProfile.Name);
                }
                Debug.Log($"[NetworkManager] Đang chờ kết nối MasterServer hoàn tất để tự động vào phòng ID: {cleanId}...");
                return;
            }

            pendingJoinRoomId = null;
            Debug.Log($"[NetworkManager] Tham gia phòng ID: {cleanId} (Region: {PhotonNetwork.CloudRegion})...");
            PhotonNetwork.JoinRoom(cleanId);
        }

        public void LeaveRoom()
        {
            if (PhotonNetwork.InRoom)
            {
                Debug.Log("[NetworkManager] Rời phòng hiện tại...");
                PhotonNetwork.LeaveRoom();
            }
        }

        public void ToggleReady()
        {
            if (!PhotonNetwork.InRoom) return;

            bool isP1 = PhotonNetwork.IsMasterClient;
            string readyKey = isP1 ? NetworkConstants.PROP_P1_READY : NetworkConstants.PROP_P2_READY;

            bool currentReady = false;
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(readyKey, out object val))
            {
                currentReady = (bool)val;
            }

            bool newReady = !currentReady;
            var updatedProps = new ExitGames.Client.Photon.Hashtable
            {
                { readyKey, newReady }
            };

            PhotonNetwork.CurrentRoom.SetCustomProperties(updatedProps);
            Debug.Log($"[NetworkManager] {(isP1 ? "Player 1 (Host)" : "Player 2")} đổi trạng thái sẵn sàng: {newReady}");
        }

        public bool CanStartMatch()
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return false;
            if (PhotonNetwork.CurrentRoom.PlayerCount < 2) return false;

            var props = PhotonNetwork.CurrentRoom.CustomProperties;
            bool p1Ready = props.TryGetValue(NetworkConstants.PROP_P1_READY, out object val1) && (bool)val1;
            bool p2Ready = props.TryGetValue(NetworkConstants.PROP_P2_READY, out object val2) && (bool)val2;

            return p1Ready && p2Ready;
        }

        public void StartMatch()
        {
            if (!CanStartMatch())
            {
                Debug.LogWarning("[NetworkManager] Không thể bắt đầu trận: Chưa đủ 2 người chơi hoặc chưa sẵn sàng!");
                return;
            }

            // Đóng phòng, lưu dàn bi ban đầu và đổi trạng thái
            int[] initialRack = Game.Billiards.Match.BallManager.GenerateWpaRackBallNumbersStatic();
            var props = new ExitGames.Client.Photon.Hashtable
            {
                { NetworkConstants.PROP_ROOM_STATE, NetworkConstants.STATE_IN_MATCH },
                { NetworkConstants.PROP_RACK_BALLS, initialRack }
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            PhotonNetwork.CurrentRoom.IsOpen = false;

            // Đọc cấu hình match
            MatchSettings settings = ExtractSettingsFromRoomProperties();
            string p1 = GetPlayer1Name();
            string p2 = GetPlayer2Name();

            GameSession session = GameSession.GetOrCreateInstance();
            session.StartOnlineMatch(settings, p1, p2, amIMaster: true);

            Debug.Log($"[NetworkManager] Master Client bắt đầu trận đấu Online 1v1 ({p1} vs {p2})! Đang load scene Gameplay đồng bộ...");
            PhotonNetwork.LoadLevel("Gameplay");
        }

        public MatchSettings ExtractSettingsFromRoomProperties()
        {
            MatchSettings settings = MatchSettings.Default;
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties != null)
            {
                var props = PhotonNetwork.CurrentRoom.CustomProperties;
                if (props.TryGetValue(NetworkConstants.PROP_RACE_TARGET, out object raceVal))
                {
                    settings.raceTarget = (RaceTarget)Convert.ToInt32(raceVal);
                }
                if (props.TryGetValue(NetworkConstants.PROP_SHOT_TIMER, out object timerVal))
                {
                    settings.shotTimer = (ShotTimerOption)Convert.ToInt32(timerVal);
                }
            }
            return settings;
        }

        public string GetPlayer1Name()
        {
            if (!PhotonNetwork.InRoom) return "Player 1";
            var props = PhotonNetwork.CurrentRoom.CustomProperties;
            if (props.TryGetValue(NetworkConstants.PROP_P1_NAME, out object val))
            {
                return val.ToString();
            }
            return PhotonNetwork.MasterClient != null ? PhotonNetwork.MasterClient.NickName : "Player 1";
        }

        public string GetPlayer2Name()
        {
            if (!PhotonNetwork.InRoom) return LocalizationManager.Get("net_waiting");
            if (PhotonNetwork.CurrentRoom.PlayerCount < 2) return LocalizationManager.Get("net_waiting_opponent");

            var props = PhotonNetwork.CurrentRoom.CustomProperties;
            if (props.TryGetValue(NetworkConstants.PROP_P2_NAME, out object val) && !string.IsNullOrEmpty(val?.ToString()))
            {
                return val.ToString();
            }

            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (!p.IsMasterClient) return p.NickName;
            }
            return "Player 2";
        }

        public bool IsPlayer1Ready()
        {
            if (!PhotonNetwork.InRoom) return false;
            var props = PhotonNetwork.CurrentRoom.CustomProperties;
            return props.TryGetValue(NetworkConstants.PROP_P1_READY, out object val) && (bool)val;
        }

        public bool IsPlayer2Ready()
        {
            if (!PhotonNetwork.InRoom) return false;
            var props = PhotonNetwork.CurrentRoom.CustomProperties;
            return props.TryGetValue(NetworkConstants.PROP_P2_READY, out object val) && (bool)val;
        }

        #region Photon Callbacks

        public override void OnConnectedToMaster()
        {
            Debug.Log($"[NetworkManager] Kết nối thành công tới Photon Master Server (Region: {PhotonNetwork.CloudRegion}, Version: {PhotonNetwork.AppVersion})!");
            OnConnectedToMasterServer?.Invoke();

            if (!string.IsNullOrEmpty(pendingJoinRoomId))
            {
                string targetRoom = pendingJoinRoomId;
                pendingJoinRoomId = null;
                JoinRoom(targetRoom);
            }
            else if (hasPendingCreateRoom)
            {
                hasPendingCreateRoom = false;
                CreateRoom(pendingCreateRoomId, pendingCreateSettings);
            }
        }

        public override void OnCreatedRoom()
        {
            Debug.Log($"[NetworkManager] Tạo phòng thành công: {PhotonNetwork.CurrentRoom.Name}");
            OnRoomCreated?.Invoke(PhotonNetwork.CurrentRoom.Name);
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"[NetworkManager] Lỗi tạo phòng: {message} (Code: {returnCode})");
            OnRoomCreateFailed?.Invoke(message);
        }

        public override void OnJoinedRoom()
        {
            Debug.Log($"[NetworkManager] Tham gia phòng thành công: {PhotonNetwork.CurrentRoom.Name}");
            
            // Nếu là client thứ 2 vào phòng, cập nhật tên và trạng thái
            if (!PhotonNetwork.IsMasterClient)
            {
                var updatedProps = new ExitGames.Client.Photon.Hashtable
                {
                    { NetworkConstants.PROP_P2_NAME, PhotonNetwork.NickName },
                    { NetworkConstants.PROP_P2_READY, false }
                };
                PhotonNetwork.CurrentRoom.SetCustomProperties(updatedProps);
            }

            OnRoomJoined?.Invoke(PhotonNetwork.CurrentRoom.Name);
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"[NetworkManager] Không thể tham gia phòng: {message} (Code: {returnCode})");
            OnRoomJoinFailed?.Invoke(message);
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            Debug.Log($"[NetworkManager] Người chơi {newPlayer.NickName} đã vào phòng.");
            OnRemotePlayerJoined?.Invoke(newPlayer);
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            Debug.Log($"[NetworkManager] Người chơi {otherPlayer.NickName} đã rời phòng.");
            
            // Nếu đang ở phòng chờ (STATE_WAITING) và máy này là MasterClient (hoặc vừa được đôn lên làm MasterClient khi chủ phòng cũ thoát):
            // Đặt lại P1 = chính mình (Host mới, mặc định Ready) và xóa trống slot P2 (Rule 126)
            if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom)
            {
                bool isWaiting = true;
                if (PhotonNetwork.CurrentRoom.CustomProperties != null &&
                    PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetworkConstants.PROP_ROOM_STATE, out object stVal) &&
                    stVal != null && stVal.ToString() != NetworkConstants.STATE_WAITING)
                {
                    isWaiting = false;
                }

                if (isWaiting)
                {
                    var updatedProps = new ExitGames.Client.Photon.Hashtable
                    {
                        { NetworkConstants.PROP_P1_NAME, PhotonNetwork.NickName },
                        { NetworkConstants.PROP_P1_READY, true },
                        { NetworkConstants.PROP_P2_NAME, "" },
                        { NetworkConstants.PROP_P2_READY, false }
                    };
                    PhotonNetwork.CurrentRoom.SetCustomProperties(updatedProps);
                }
            }

            OnRemotePlayerLeft?.Invoke(otherPlayer);
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            Debug.Log($"[NetworkManager] Chủ phòng chuyển sang: {newMasterClient?.NickName}");
            if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom)
            {
                bool isWaiting = true;
                if (PhotonNetwork.CurrentRoom.CustomProperties != null &&
                    PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetworkConstants.PROP_ROOM_STATE, out object stVal) &&
                    stVal != null && stVal.ToString() != NetworkConstants.STATE_WAITING)
                {
                    isWaiting = false;
                }

                if (isWaiting)
                {
                    var updatedProps = new ExitGames.Client.Photon.Hashtable
                    {
                        { NetworkConstants.PROP_P1_NAME, PhotonNetwork.NickName },
                        { NetworkConstants.PROP_P1_READY, true },
                        { NetworkConstants.PROP_P2_NAME, "" },
                        { NetworkConstants.PROP_P2_READY, false }
                    };
                    PhotonNetwork.CurrentRoom.SetCustomProperties(updatedProps);
                }
            }
            OnRoomPropertiesChanged?.Invoke();
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {
            // Avatar của người chơi đổi/đến muộn -> làm mới giao diện phòng
            if (changedProps != null && changedProps.ContainsKey(PLAYER_PROP_AVATAR))
            {
                OnRoomPropertiesChanged?.Invoke();
            }
        }

        public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
        {
            // Kiểm tra nếu Master Client đã chuyển trạng thái sang InMatch
            if (propertiesThatChanged.TryGetValue(NetworkConstants.PROP_ROOM_STATE, out object stateVal))
            {
                if (stateVal.ToString() == NetworkConstants.STATE_IN_MATCH && !PhotonNetwork.IsMasterClient)
                {
                    Debug.Log("[NetworkManager] Nhận thông báo trận đấu bắt đầu từ Master Client! Đồng bộ thiết lập GameSession...");
                    MatchSettings settings = ExtractSettingsFromRoomProperties();
                    string p1 = GetPlayer1Name();
                    string p2 = GetPlayer2Name();
                    GameSession session = GameSession.GetOrCreateInstance();
                    session.StartOnlineMatch(settings, p1, p2, amIMaster: false);
                }
            }

            OnRoomPropertiesChanged?.Invoke();
        }

        public override void OnLeftRoom()
        {
            Debug.Log("[NetworkManager] Đã rời phòng.");
            OnLeftCurrentRoom?.Invoke();
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogWarning($"[NetworkManager] Mất kết nối tới Photon: {cause}");
            pendingJoinRoomId = null;
            hasPendingCreateRoom = false;
            OnLeftCurrentRoom?.Invoke();
            OnDisconnectedFromServer?.Invoke(cause);
        }

        #endregion
    }
}
