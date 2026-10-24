using System;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Game.Billiards.Core;

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

        public bool IsConnected => PhotonNetwork.IsConnectedAndReady;
        public bool IsInRoom => PhotonNetwork.InRoom;
        public bool IsMaster => PhotonNetwork.IsMasterClient;
        public string CurrentRoomName => PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name : string.Empty;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
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

        public void Connect(string playerName = "Cơ Thủ")
        {
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.NickName = playerName;
                OnConnectedToMasterServer?.Invoke();
                return;
            }

            PhotonNetwork.NickName = string.IsNullOrEmpty(playerName) ? "Cơ Thủ" : playerName;
            PhotonNetwork.GameVersion = NetworkConstants.GAME_VERSION;
            Debug.Log($"[NetworkManager] Đang kết nối tới máy chủ Photon (Version: {NetworkConstants.GAME_VERSION}, Player: {PhotonNetwork.NickName})...");
            PhotonNetwork.ConnectUsingSettings();
        }

        public void CreateRoom(string customRoomId, MatchSettings settings)
        {
            if (!PhotonNetwork.IsConnectedAndReady)
            {
                OnRoomCreateFailed?.Invoke("Chưa kết nối tới máy chủ Photon!");
                return;
            }

            string roomId = string.IsNullOrEmpty(customRoomId) ? NetworkConstants.GenerateRoomId() : customRoomId.Trim().ToUpper();

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

            Debug.Log($"[NetworkManager] Tạo phòng mới ID: {roomId} (Race to: {settings.raceTarget}, Timer: {settings.shotTimer})");
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
            if (!PhotonNetwork.IsConnectedAndReady)
            {
                OnRoomJoinFailed?.Invoke("Chưa kết nối tới máy chủ Photon!");
                return;
            }

            if (string.IsNullOrEmpty(roomId))
            {
                OnRoomJoinFailed?.Invoke("Mã phòng không được để trống!");
                return;
            }

            string cleanId = roomId.Trim().ToUpper();
            Debug.Log($"[NetworkManager] Tham gia phòng ID: {cleanId}...");
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

            // Đóng phòng và đổi trạng thái
            var props = new ExitGames.Client.Photon.Hashtable
            {
                { NetworkConstants.PROP_ROOM_STATE, NetworkConstants.STATE_IN_MATCH }
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
            if (!PhotonNetwork.InRoom) return "Đang đợi...";
            if (PhotonNetwork.CurrentRoom.PlayerCount < 2) return "Đang đợi đối thủ...";

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
            Debug.Log("[NetworkManager] Kết nối thành công tới Photon Master Server!");
            OnConnectedToMasterServer?.Invoke();
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
            
            // Xóa slot Player 2 nếu rời phòng ở Lobby (Rule 126)
            if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom)
            {
                var updatedProps = new ExitGames.Client.Photon.Hashtable
                {
                    { NetworkConstants.PROP_P2_NAME, "" },
                    { NetworkConstants.PROP_P2_READY, false }
                };
                PhotonNetwork.CurrentRoom.SetCustomProperties(updatedProps);
            }

            OnRemotePlayerLeft?.Invoke(otherPlayer);
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
        }

        #endregion
    }
}
