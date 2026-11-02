using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Billiards.AI;

namespace Game.Billiards.Core
{
    public enum GameMode
    {
        OfflineVsAI = 0,
        OnlinePvP = 1,
        LocalPractice = 2
    }

    public enum TableTheme
    {
        Blue = 0,
        Purple = 1,
        Green = 2,
        Red = 3,
        Grey = 4
    }

    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        [Header("Match Setup")]
        [SerializeField] private GameMode currentGameMode = GameMode.OfflineVsAI;
        [SerializeField] private AIDifficulty selectedAIDifficulty = AIDifficulty.Normal;
        [SerializeField] private MatchSettings currentSettings = MatchSettings.Default;

        [Header("Players")]
        [SerializeField] private string playerAName = "Cơ Thủ (Player 1)";
        [SerializeField] private string playerBName = "AI Đối Thủ (Normal)";
        [SerializeField] private bool isPlayerBAi = true;
        [SerializeField] private bool isOnlineMaster = true;

        public GameMode CurrentGameMode => currentGameMode;
        public AIDifficulty SelectedAIDifficulty => selectedAIDifficulty;
        public MatchSettings CurrentSettings => currentSettings;
        public string PlayerAName => playerAName;
        public string PlayerBName => playerBName;
        public bool IsPlayerBAi => isPlayerBAi;
        public bool IsOnlineMaster => isOnlineMaster;

        private const string AimGuidePrefKey = "AimGuide_Enabled";

        /// <summary>Bật/tắt thước ngắm (lưu qua PlayerPrefs, mặc định bật).</summary>
        public static bool AimGuideEnabled
        {
            get => PlayerPrefs.GetInt(AimGuidePrefKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(AimGuidePrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private const string RackIntroPrefKey = "RackIntro_Enabled";

        /// <summary>Bật/tắt đoạn intro xếp bi đầu trận (lưu qua PlayerPrefs, mặc định bật).</summary>
        public static bool RackIntroEnabled
        {
            get => PlayerPrefs.GetInt(RackIntroPrefKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(RackIntroPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private const string FirstBreakMethodPrefKey = "Online_FirstBreakMethod";

        /// <summary>Cơ chế chọn lượt phá đầu trong phòng Online (lưu qua PlayerPrefs, mặc định Vòng quay ngẫu nhiên).</summary>
        public static FirstBreakMethod SavedFirstBreakMethod
        {
            get
            {
                int val = PlayerPrefs.GetInt(FirstBreakMethodPrefKey, (int)FirstBreakMethod.RandomWheel);
                return val == (int)FirstBreakMethod.LagShot ? FirstBreakMethod.LagShot : FirstBreakMethod.RandomWheel;
            }
            set
            {
                PlayerPrefs.SetInt(FirstBreakMethodPrefKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        private const string TableThemePrefKey = "SelectedTableTheme";

        /// <summary>Màu/chủ đề mặt bàn đang chọn (lưu qua PlayerPrefs, mặc định Bàn Xanh Dương).</summary>
        public static TableTheme SelectedTableTheme
        {
            get
            {
                int val = PlayerPrefs.GetInt(TableThemePrefKey, (int)TableTheme.Blue);
                if (val < 0 || val > (int)TableTheme.Grey) val = (int)TableTheme.Blue;
                return (TableTheme)val;
            }
            set
            {
                PlayerPrefs.SetInt(TableThemePrefKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static string GetTableThemeName(TableTheme theme)
        {
            switch (theme)
            {
                case TableTheme.Purple: return "Bàn Tím";
                case TableTheme.Green:  return "Bàn Xanh Lá";
                case TableTheme.Red:    return "Bàn Đỏ";
                case TableTheme.Grey:   return "Bàn Xám";
                case TableTheme.Blue:
                default:                return "Bàn Xanh Dương";
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Cấu hình target frame rate theo yêu cầu đồ án (60 FPS) & luôn chạy nền cho chế độ Online / Voice Chat
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
        }

        public static GameSession GetOrCreateInstance()
        {
            if (Instance == null)
            {
                var go = new GameObject("GameSession");
                Instance = go.AddComponent<GameSession>();
                DontDestroyOnLoad(go);
            }
            return Instance;
        }

        public void StartOfflineMatch(AIDifficulty difficulty, RaceTarget raceTarget, ShotTimerOption timerOption, string playerName = "Cơ Thủ")
        {
            currentGameMode = GameMode.OfflineVsAI;
            selectedAIDifficulty = difficulty;
            isPlayerBAi = true;

            currentSettings = new MatchSettings
            {
                raceTarget = raceTarget,
                shotTimer = timerOption,
                useWpaRules = true
            };

            playerAName = string.IsNullOrEmpty(playerName) ? LocalizationManager.Get("default_player_name") : playerName;
            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    playerBName = LocalizationManager.Get("ai_easy_name");
                    break;
                case AIDifficulty.Hard:
                    playerBName = LocalizationManager.Get("ai_hard_name");
                    break;
                case AIDifficulty.Normal:
                default:
                    playerBName = LocalizationManager.Get("ai_normal_name");
                    break;
            }

            Debug.Log($"[GameSession] Khởi động trận Offline vs AI: Cấp độ {difficulty}, Race to {(int)raceTarget}, Shot Timer: {timerOption}");
            LoadGameplayScene();
        }

        public void StartOnlineMatch(MatchSettings settings, string playerA, string playerB, bool amIMaster)
        {
            SyncOnlineMatchState(settings, playerA, playerB, amIMaster);
            Debug.Log($"[GameSession] Khởi động trận Online 1v1: {playerA} vs {playerB} (amIMaster={amIMaster})");
            LoadGameplayScene();
        }

        /// <summary>
        /// Đồng bộ trạng thái phiên Online PvP (vai trò Chủ phòng/Khách, cài đặt, tắt AI) mà KHÔNG gọi LoadGameplayScene lần nữa.
        /// </summary>
        public void SyncOnlineMatchState(MatchSettings settings, string playerA, string playerB, bool amIMaster)
        {
            currentGameMode = GameMode.OnlinePvP;
            selectedAIDifficulty = AIDifficulty.Normal;
            isPlayerBAi = false;
            isOnlineMaster = amIMaster;
            currentSettings = settings;
            if (!string.IsNullOrWhiteSpace(playerA)) playerAName = playerA;
            if (!string.IsNullOrWhiteSpace(playerB)) playerBName = playerB;
        }

        public void LoadLobbyScene()
        {
            if (Application.isPlaying)
            {
                if (Game.Billiards.Audio.AudioManager.Instance != null)
                {
                    Game.Billiards.Audio.AudioManager.Instance.StopMatchResultAudio();
                }

                // Online: rời phòng Photon để đối thủ nhận OnPlayerLeftRoom (thoát giữa trận = thua trận, Rule 124)
                if (currentGameMode == GameMode.OnlinePvP && Photon.Pun.PhotonNetwork.InRoom)
                {
                    Photon.Pun.PhotonNetwork.LeaveRoom();
                }

                // Màn hình tải bất đồng bộ khi thoát về sảnh
                Game.Billiards.UI.LoadingScreen.ShowForLobby();
            }
        }

        public void LoadGameplayScene()
        {
            if (!Application.isPlaying) return;

            if (currentGameMode == GameMode.OnlinePvP)
            {
                // Online: scene Gameplay do Photon tải đồng bộ (PhotonNetwork.LoadLevel). Màn hình tải theo dõi tiến độ và chờ cả hai máy.
                Game.Billiards.UI.LoadingScreen.ShowForOnlineMatch();
            }
            else
            {
                Game.Billiards.UI.LoadingScreen.ShowForOfflineMatch();
            }
        }

        public void QuitGame()
        {
            Debug.Log("[GameSession] Thoát trò chơi về Desktop.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
